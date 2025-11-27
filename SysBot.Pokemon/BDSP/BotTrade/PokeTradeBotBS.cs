using PKHeX.Core;
using PKHeX.Core.Searching;
using SysBot.Base;
using SysBot.Pokemon.Helpers;
using System;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using static SysBot.Base.SwitchButton;
using static SysBot.Pokemon.BasePokeDataOffsetsBS;

namespace SysBot.Pokemon;

// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public class PokeTradeBotBS(PokeTradeHub<PB8> Hub, PokeBotState Config) : PokeRoutineExecutor8BS(Config), ICountBot
{
    private readonly TradeSettings TradeSettings = Hub.Config.Trade;
    private readonly TradeAbuseSettings AbuseSettings = Hub.Config.TradeAbuse;

    public ICountSettings Counts => TradeSettings;

    /// <summary>
    /// 用于存储接收到的交易数据的文件夹。
    /// </summary>
    /// <remarks>如果为null，将跳过存储。</remarks>
    private readonly FolderSettings DumpSetting = Hub.Config.Folder;

    /// <summary>
    /// 多个机器人的同步启动。
    /// </summary>
    public bool ShouldWaitAtBarrier { get; private set; }

    /// <summary>
    /// 跟踪失败的同步启动以尝试重新同步。
    /// </summary>
    public int FailedBarrier { get; private set; }

    // 每个会话中保持不变的缓存偏移量。
    private ulong BoxStartOffset;
    private ulong UnionGamingOffset;
    private ulong UnionTalkingOffset;
    private ulong SoftBanOffset;
    private ulong LinkTradePokemonOffset;

    // 跟踪上次提供给我们的宝可梦，因为它在交易之间持续存在。
    private byte[] lastOffered = new byte[8];

    public override async Task MainLoop(CancellationToken token)
    {
        try
        {
            await InitializeHardware(Hub.Config.Trade, token).ConfigureAwait(false);

            Log("正在识别主机控制台的训练家数据。");
            var sav = await IdentifyTrainer(token).ConfigureAwait(false);
            RecentTrainerCache.SetRecentTrainer(sav);

            await RestartGameIfCantLeaveUnionRoom(token).ConfigureAwait(false);
            await InitializeSessionOffsets(token).ConfigureAwait(false);

            Log($"开始主 {nameof(PokeTradeBotBS)} 循环。");
            await InnerLoop(sav, token).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Log(e.Message);
        }

        Log($"结束 {nameof(PokeTradeBotBS)} 循环。");
    }

    public override Task HardStop()
    {
        UpdateBarrier(false);
        return CleanExit(CancellationToken.None);
    }

    private async Task InnerLoop(SAV8BS sav, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            Config.IterateNextRoutine();
            var task = Config.CurrentRoutineType switch
            {
                PokeRoutineType.Idle => DoNothing(token),
                _ => DoTrades(sav, token),
            };
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (SocketException e)
            {
                if (e.StackTrace != null)
                    Connection.LogError(e.StackTrace);
                var attempts = Hub.Config.Timings.ReconnectAttempts;
                var delay = Hub.Config.Timings.ExtraReconnectDelay;
                var protocol = Config.Connection.Protocol;
                if (!await TryReconnect(attempts, delay, protocol, token).ConfigureAwait(false))
                    return;
            }
        }
    }

    private async Task DoNothing(CancellationToken token)
    {
        int waitCounter = 0;
        while (!token.IsCancellationRequested && Config.NextRoutineType == PokeRoutineType.Idle)
        {
            if (waitCounter == 0)
                Log("没有分配任务。等待新任务分配。");
            waitCounter++;
            if (waitCounter % 10 == 0 && Hub.Config.AntiIdle)
                await Click(B, 1_000, token).ConfigureAwait(false);
            else
                await Task.Delay(1_000, token).ConfigureAwait(false);
        }
    }

    private async Task DoTrades(SAV8BS sav, CancellationToken token)
    {
        var type = Config.CurrentRoutineType;
        int waitCounter = 0;
        while (!token.IsCancellationRequested && Config.NextRoutineType == type)
        {
            var (detail, priority) = GetTradeData(type);
            if (detail is null)
            {
                await WaitForQueueStep(waitCounter++, token).ConfigureAwait(false);
                continue;
            }
            waitCounter = 0;

            detail.IsProcessing = true;
            if (detail.Type != PokeTradeType.Random || !Hub.Config.Distribution.RemainInUnionRoomBDSP)
                await RestartGameIfCantLeaveUnionRoom(token).ConfigureAwait(false);
            string tradetype = $" ({detail.Type})";
            Log($"开始下一个 {type}{tradetype} 机器人交易。获取数据中...");
            await Task.Delay(500, token).ConfigureAwait(false);
            Hub.Config.Stream.StartTrade(this, detail, Hub);
            Hub.Queues.StartTrade(this, detail);

            await PerformTrade(sav, detail, type, priority, token).ConfigureAwait(false);
        }
    }

    private Task WaitForQueueStep(int waitCounter, CancellationToken token)
    {
        if (waitCounter == 0)
        {
            // 更新资源。
            Hub.Config.Stream.IdleAssets(this);
            Log("没有需要检查的内容，等待新用户中...");
        }

        const int interval = 10;
        if (waitCounter % interval == interval - 1 && Hub.Config.AntiIdle)
            return Click(B, 1_000, token);
        return Task.Delay(1_000, token);
    }

    protected virtual (PokeTradeDetail<PB8>? detail, uint priority) GetTradeData(PokeRoutineType type)
    {
        if (Hub.Queues.TryDequeue(type, out var detail, out var priority))
            return (detail, priority);
        if (Hub.Queues.TryDequeueLedy(out detail))
            return (detail, PokeTradePriorities.TierFree);
        return (null, PokeTradePriorities.TierFree);
    }

    private async Task PerformTrade(SAV8BS sav, PokeTradeDetail<PB8> detail, PokeRoutineType type, uint priority, CancellationToken token)
    {
        PokeTradeResult result;
        try
        {
            result = await PerformLinkCodeTrade(sav, detail, token).ConfigureAwait(false);
            if (result == PokeTradeResult.Success)
                return;
        }
        catch (SocketException socket)
        {
            Log(socket.Message);
            result = PokeTradeResult.ExceptionConnection;
            HandleAbortedTrade(detail, type, priority, result);
            throw; // 让这个中断交易循环。重新进入交易循环将重新检查连接。
        }
        catch (Exception e)
        {
            Log(e.Message);
            result = PokeTradeResult.ExceptionInternal;
        }

        HandleAbortedTrade(detail, type, priority, result);
    }

    private void HandleAbortedTrade(PokeTradeDetail<PB8> detail, PokeRoutineType type, uint priority, PokeTradeResult result)
    {
        detail.IsProcessing = false;
        if (result.ShouldAttemptRetry() && detail.Type != PokeTradeType.Random && !detail.IsRetry)
        {
            detail.IsRetry = true;
            Hub.Queues.Enqueue(type, detail, Math.Min(priority, PokeTradePriorities.Tier2));
            detail.SendNotification(this, "哎呀！出了点问题。我将为您重新排队进行另一次尝试。");
        }
        else
        {
            detail.SendNotification(this, $"哎呀！出了点问题。取消交易: {result}。");
            detail.TradeCanceled(this, result);
        }
    }

    private async Task<PokeTradeResult> PerformLinkCodeTrade(SAV8BS sav, PokeTradeDetail<PB8> poke, CancellationToken token)
    {
        // 更新屏障设置
        UpdateBarrier(poke.IsSynchronized);
        poke.TradeInitialize(this);
        Hub.Config.Stream.EndEnterCode(this);

        var distroRemainInRoom = poke.Type == PokeTradeType.Random && Hub.Config.Distribution.RemainInUnionRoomBDSP;

        // 如果我们不应该保持连接并且开始时在联盟房间，确保我们不在盒子中。
        if (!distroRemainInRoom && await IsUnionWork(UnionGamingOffset, token).ConfigureAwait(false))
        {
            if (!await ExitBoxToUnionRoom(token).ConfigureAwait(false))
                return PokeTradeResult.RecoverReturnOverworld;
        }

        if (await CheckIfSoftBanned(SoftBanOffset, token).ConfigureAwait(false))
            await UnSoftBan(token).ConfigureAwait(false);

        var toSend = poke.TradeData;
        if (toSend.Species != 0)
            await SetBoxPokemonAbsolute(BoxStartOffset, toSend, token, sav).ConfigureAwait(false);

        // 进入联盟房间。如果已经在里面，不应该做任何事情。
        if (!await EnterUnionRoomWithCode(poke.Type, poke.Code, token).ConfigureAwait(false))
        {
            // 我们不知道我们进行了多远，所以重新启动游戏以确保安全。
            await RestartGameBDSP(token).ConfigureAwait(false);
            return PokeTradeResult.RecoverEnterUnionRoom;
        }

        await RequestUnionRoomTrade(token).ConfigureAwait(false);
        poke.TradeSearching(this);
        var waitPartner = Hub.Config.Trade.TradeWaitTime;

        // 持续按下A直到检测到有人与我们交谈。
        while (!await IsUnionWork(UnionTalkingOffset, token).ConfigureAwait(false) && waitPartner > 0)
        {
            for (int i = 0; i < 2; ++i)
                await Click(A, 0_450, token).ConfigureAwait(false);

            if (--waitPartner <= 0)
                return PokeTradeResult.NoTrainerFound;
        }
        Log("发现一个用户正在与我们交谈！");

        // 持续按下A直到TargetTranerParam加载完成（当我们进入盒子时）。
        while (!await IsPartnerParamLoaded(token).ConfigureAwait(false) && waitPartner > 0)
        {
            for (int i = 0; i < 2; ++i)
                await Click(A, 0_450, token).ConfigureAwait(false);

            // 如果他们交谈后退出，可能为false。
            if (!await IsUnionWork(UnionTalkingOffset, token).ConfigureAwait(false))
                break;
            if (--waitPartner <= 0)
                return PokeTradeResult.TrainerTooSlow;
        }
        Log("进入盒子中...");

        // 仍在通过对话框和盒子打开过程。
        await Task.Delay(3_000, token).ConfigureAwait(false);

        // 如果他们退出与我们的交谈，可能发生。
        if (!await IsPartnerParamLoaded(token).ConfigureAwait(false))
            return PokeTradeResult.TrainerTooSlow;

        var tradePartner = await GetTradePartnerInfo(token).ConfigureAwait(false);
        var trainerNID = GetFakeNID(tradePartner.TrainerName, tradePartner.TrainerID);
        RecordUtil<PokeTradeBotBS>.Record($"发起\t{trainerNID:X16}\t{tradePartner.TrainerName}\t{poke.Trainer.TrainerName}\t{poke.Trainer.ID}\t{poke.ID}\t{toSend.EncryptionConstant:X8}");
        Log($"找到连接交换伙伴: {tradePartner.TrainerName}-{tradePartner.TID7} (ID: {trainerNID}");

        var partnerCheck = await CheckPartnerReputation(this, poke, trainerNID, tradePartner.TrainerName, AbuseSettings, token);
        if (partnerCheck != PokeTradeResult.Success)
        {
            // 尝试退出盒子。
            if (!await ExitBoxToUnionRoom(token).ConfigureAwait(false))
                return PokeTradeResult.RecoverReturnOverworld;

            // 如果我们选择不保持连接，离开联盟房间。
            if (!distroRemainInRoom)
            {
                Log("尝试离开联盟房间。");
                if (!await EnsureOutsideOfUnionRoom(token).ConfigureAwait(false))
                    return PokeTradeResult.RecoverReturnOverworld;
            }
            return PokeTradeResult.SuspiciousActivity;
        }

        await Task.Delay(2_000 + Hub.Config.Timings.ExtraTimeOpenBox, token).ConfigureAwait(false);

        if (Hub.Config.Legality.UseTradePartnerInfo && !AbstractTrade<PB8>.GetSkipAutoOTListFromPokeTradeDetail(poke).First())
            await ApplyAutoOT(toSend, sav, poke, tradePartner, token).ConfigureAwait(false);

        // 确认盒子1槽位1
        if (poke.Type == PokeTradeType.Specific)
        {
            for (int i = 0; i < 5; i++)
                await Click(A, 0_500, token).ConfigureAwait(false);
        }

        poke.SendNotification(this, $"找到连接交换伙伴: {tradePartner.TrainerName} TID: {tradePartner.TID7} SID: {tradePartner.SID7}。正在等待宝可梦...");

        // 需要至少一次交易才能使这个指针有意义，所以在这里缓存它。
        LinkTradePokemonOffset = await SwitchConnection.PointerAll(Offsets.LinkTradePartnerPokemonPointer, token).ConfigureAwait(false);

        if (poke.Type == PokeTradeType.Dump)
            return await ProcessDumpTradeAsync(poke, token).ConfigureAwait(false);

        // 等待用户输入...需要与之前提供的宝可梦不同。
        var tradeOffered = await ReadUntilChanged(LinkTradePokemonOffset, lastOffered, 25_000, 1_000, false, true, token).ConfigureAwait(false);
        if (!tradeOffered)
            return PokeTradeResult.TrainerTooSlow;

        // 如果我们检测到变化，他们提供了某些东西。
        var offered = await ReadPokemon(LinkTradePokemonOffset, BoxFormatSlotSize, token).ConfigureAwait(false);
        if (offered.Species == 0 || !offered.ChecksumValid)
            return PokeTradeResult.TrainerTooSlow;
        lastOffered = await SwitchConnection.ReadBytesAbsoluteAsync(LinkTradePokemonOffset, 8, token).ConfigureAwait(false);

        var trainer = new PartnerDataHolder(0, tradePartner.TrainerName, tradePartner.TID7);
        (toSend, PokeTradeResult update) = await GetEntityToSend(sav, poke, offered, toSend, trainer, token).ConfigureAwait(false);
        if (update != PokeTradeResult.Success)
            return update;

        if (Hub.Config.Trade.DisallowTradeEvolve && TradeEvolutions.WillTradeEvolve(offered.Species, offered.Form, offered.HeldItem, toSend.Species))
        {
            Log("交易取消，因为训练家提供了一个在交易时会进化的宝可梦。");
            return PokeTradeResult.TradeEvolveNotAllowed;
        }

        var tradeResult = await ConfirmAndStartTrading(poke, token).ConfigureAwait(false);
        if (tradeResult != PokeTradeResult.Success)
            return tradeResult;

        if (token.IsCancellationRequested)
            return PokeTradeResult.RoutineCancel;

        // 交易成功！
        var received = await ReadPokemon(BoxStartOffset, BoxFormatSlotSize, token).ConfigureAwait(false);
        // b1s1中的宝可梦与他们应该接收的相同（从未发送）。
        if (SearchUtil.HashByDetails(received) == SearchUtil.HashByDetails(toSend) && received.Checksum == toSend.Checksum)
        {
            Log("用户未完成交易。");
            return PokeTradeResult.TrainerTooSlow;
        }

        var batchTradeResult = await PerformRemainingLinkCodeTrades(sav, poke, tradePartner, token);
        if (batchTradeResult != PokeTradeResult.Success)
            return batchTradeResult;

        // 只要我们在b1s1中处理了我们的注入，就假设交易成功。
        Log("用户完成了交易。");
        poke.TradeFinished(this, received);

        // 只有在完成交易时才记录。
        UpdateCountsAndExport(poke, received, toSend);

        // 记录交易滥用跟踪。
        LogSuccessfulTrades(poke, trainerNID, tradePartner.TrainerName);

        // 尝试退出盒子。
        if (!await ExitBoxToUnionRoom(token).ConfigureAwait(false))
            return PokeTradeResult.RecoverReturnOverworld;

        // 如果我们选择不保持连接，离开联盟房间。
        if (!distroRemainInRoom)
        {
            Log("尝试离开联盟房间。");
            if (!await EnsureOutsideOfUnionRoom(token).ConfigureAwait(false))
                return PokeTradeResult.RecoverReturnOverworld;
        }

        // 有时他们提供了另一个宝可梦，所以在离开联盟房间后立即存储。
        lastOffered = await SwitchConnection.ReadBytesAbsoluteAsync(LinkTradePokemonOffset, 8, token).ConfigureAwait(false);

        return PokeTradeResult.Success;
    }

    private static ulong GetFakeNID(string trainerName, uint trainerID)
    {
        var nameHash = trainerName.GetHashCode();
        return ((ulong)trainerID << 32) | (uint)nameHash;
    }

    private void UpdateCountsAndExport(PokeTradeDetail<PB8> poke, PB8 received, PB8 toSend)
    {
        var counts = TradeSettings;
        if (poke.Type == PokeTradeType.Random)
            counts.AddCompletedDistribution();
        else
            counts.AddCompletedTrade();

        if (DumpSetting.Dump && !string.IsNullOrEmpty(DumpSetting.DumpFolder))
        {
            var subfolder = poke.Type.ToString().ToLower();
            DumpPokemon(DumpSetting.DumpFolder, subfolder, received); // 机器人接收的
            if (poke.Type is PokeTradeType.Specific)
                DumpPokemon(DumpSetting.DumpFolder, "traded", toSend); // 发送给伙伴的
        }
    }

    private async Task<PokeTradeResult> ConfirmAndStartTrading(PokeTradeDetail<PB8> detail, CancellationToken token)
    {
        // 我们将继续监视B1S1的变化以指示交易开始 -> 此时应该尝试退出。
        var oldEC = await SwitchConnection.ReadBytesAbsoluteAsync(BoxStartOffset, 8, token).ConfigureAwait(false);

        await Click(A, 3_000, token).ConfigureAwait(false);
        for (int i = 0; i < Hub.Config.Trade.MaxTradeConfirmTime; i++)
        {
            if (await IsUserBeingShifty(detail, token).ConfigureAwait(false))
                return PokeTradeResult.SuspiciousActivity;
            // 我们不再交谈，所以他们可能退出了。
            if (!await IsUnionWork(UnionTalkingOffset, token).ConfigureAwait(false))
                return PokeTradeResult.TrainerTooSlow;
            await Click(A, 1_000, token).ConfigureAwait(false);

            // 在动画开始时可以检测到EC。
            var newEC = await SwitchConnection.ReadBytesAbsoluteAsync(BoxStartOffset, 8, token).ConfigureAwait(false);
            if (!newEC.SequenceEqual(oldEC))
            {
                await Task.Delay(25_000, token).ConfigureAwait(false);
                return PokeTradeResult.Success;
            }
        }

        // 如果我们没有检测到B1S1变化，交易在那段时间内没有完成。
        return PokeTradeResult.TrainerTooSlow;
    }

    private async Task<bool> EnterUnionRoomWithCode(PokeTradeType tradeType, int tradeCode, CancellationToken token)
    {
        // 已经在联盟房间中。
        if (await IsUnionWork(UnionGamingOffset, token).ConfigureAwait(false))
            return true;

        // 打开y通信并选择全球房间
        await Click(Y, 1_000 + Hub.Config.Timings.ExtraTimeOpenYMenu, token).ConfigureAwait(false);
        await Click(DRIGHT, 0_400, token).ConfigureAwait(false);

        // 法语少一个菜单
        if (GameLang is not LanguageID.French)
        {
            await Click(A, 0_050, token).ConfigureAwait(false);
            await PressAndHold(A, 1_000, 0, token).ConfigureAwait(false);
        }

        await Click(A, 0_050, token).ConfigureAwait(false);
        await PressAndHold(A, 1_500, 0, token).ConfigureAwait(false);

        // 日语多一个菜单
        if (GameLang is LanguageID.Japanese)
        {
            await Click(A, 0_050, token).ConfigureAwait(false);
            await PressAndHold(A, 1_000, 0, token).ConfigureAwait(false);
        }

        await Click(A, 1_000, token).ConfigureAwait(false); // 您要进入吗？屏幕

        Log("选择连接密码房间。");
        // 连接密码选择索引
        await Click(DDOWN, 0_200, token).ConfigureAwait(false);
        await Click(DDOWN, 0_200, token).ConfigureAwait(false);

        Log("连接到互联网。");
        await Click(A, 0_050, token).ConfigureAwait(false);
        await PressAndHold(A, 2_000, 0, token).ConfigureAwait(false);

        // 额外菜单。
        if (GameLang is LanguageID.German or LanguageID.Italian or LanguageID.Korean)
        {
            await Click(A, 0_050, token).ConfigureAwait(false);
            await PressAndHold(A, 0_750, 0, token).ConfigureAwait(false);
        }

        await Click(A, 0_050, token).ConfigureAwait(false);
        await PressAndHold(A, 1_000, 0, token).ConfigureAwait(false);
        await Click(A, 0_050, token).ConfigureAwait(false);
        await PressAndHold(A, 1_500, 0, token).ConfigureAwait(false);
        await Click(A, 0_050, token).ConfigureAwait(false);
        await PressAndHold(A, 1_500, 0, token).ConfigureAwait(false);

        // 您要保存您的冒险进度吗？
        await Click(A, 0_500, token).ConfigureAwait(false);
        await Click(A, 0_500, token).ConfigureAwait(false);

        Log("保存游戏中。");
        // 同意并保存游戏。
        await Click(A, 0_050, token).ConfigureAwait(false);
        await PressAndHold(A, 6_500, 0, token).ConfigureAwait(false);

        if (tradeType != PokeTradeType.Random)
            Hub.Config.Stream.StartEnterCode(this);
        Log($"输入连接交换密码: {tradeCode:0000 0000}...");
        await EnterLinkCode(tradeCode, Hub.Config, token).ConfigureAwait(false);

        // 等待屏障同时触发所有机器人。
        WaitAtBarrierIfApplicable(token);
        await Click(PLUS, 0_600, token).ConfigureAwait(false);
        Hub.Config.Stream.EndEnterCode(this);
        Log("进入联盟房间。");

        // 等待直到我们通过通信消息。
        int tries = 100;
        while (!await IsUnionWork(UnionGamingOffset, token).ConfigureAwait(false))
        {
            await Click(A, 0_300, token).ConfigureAwait(false);

            if (--tries < 1)
                return false;
        }

        await Task.Delay(1_300 + Hub.Config.Timings.ExtraTimeJoinUnionRoom, token).ConfigureAwait(false);

        return true; // 我们已经进入房间并准备好请求。
    }

    private async Task RequestUnionRoomTrade(CancellationToken token)
    {
        // Y按钮交易总是将我们置于可以打开呼叫菜单而无需移动的位置。
        Log("尝试打开Y菜单。");
        await Click(Y, 1_000, token).ConfigureAwait(false);
        await Click(A, 0_400, token).ConfigureAwait(false);
        await Click(DDOWN, 0_400, token).ConfigureAwait(false);
        await Click(DDOWN, 0_400, token).ConfigureAwait(false);
        await Click(A, 0_100, token).ConfigureAwait(false);
    }

    // 这些每个会话不会改变，我们经常访问它们，所以每次启动时设置这些。
    private async Task InitializeSessionOffsets(CancellationToken token)
    {
        Log("缓存会话偏移量...");
        BoxStartOffset = await SwitchConnection.PointerAll(Offsets.BoxStartPokemonPointer, token).ConfigureAwait(false);
        UnionGamingOffset = await SwitchConnection.PointerAll(Offsets.UnionWorkIsGamingPointer, token).ConfigureAwait(false);
        UnionTalkingOffset = await SwitchConnection.PointerAll(Offsets.UnionWorkIsTalkingPointer, token).ConfigureAwait(false);
        SoftBanOffset = await SwitchConnection.PointerAll(Offsets.UnionWorkPenaltyPointer, token).ConfigureAwait(false);
    }

    // todo: 未来
    protected virtual async Task<bool> IsUserBeingShifty(PokeTradeDetail<PB8> detail, CancellationToken token)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        return false;
    }

    private async Task RestartGameIfCantLeaveUnionRoom(CancellationToken token)
    {
        if (!await EnsureOutsideOfUnionRoom(token).ConfigureAwait(false))
            await RestartGameBDSP(token).ConfigureAwait(false);
    }

    private async Task RestartGameBDSP(CancellationToken token)
    {
        await ReOpenGame(Hub.Config, token).ConfigureAwait(false);
        await InitializeSessionOffsets(token).ConfigureAwait(false);
    }

    private async Task<bool> EnsureOutsideOfUnionRoom(CancellationToken token)
    {
        if (!await IsUnionWork(UnionGamingOffset, token).ConfigureAwait(false))
            return true;

        if (!await ExitBoxToUnionRoom(token).ConfigureAwait(false))
            return false;
        if (!await ExitUnionRoomToOverworld(token).ConfigureAwait(false))
            return false;
        return true;
    }

    private async Task<bool> ExitBoxToUnionRoom(CancellationToken token)
    {
        if (await IsUnionWork(UnionTalkingOffset, token).ConfigureAwait(false))
        {
            Log("退出盒子...");
            int tries = 30;
            while (await IsUnionWork(UnionTalkingOffset, token).ConfigureAwait(false))
            {
                await Click(B, 0_500, token).ConfigureAwait(false);
                if (!await IsUnionWork(UnionTalkingOffset, token).ConfigureAwait(false))
                    break;
                await Click(DUP, 0_200, token).ConfigureAwait(false);
                await Click(A, 0_500, token).ConfigureAwait(false);
                // 使常规退出更快一些，只需要这个用于交易进化和招式。
                if (tries < 10)
                    await Click(B, 0_500, token).ConfigureAwait(false);
                await Click(B, 0_500, token).ConfigureAwait(false);
                tries--;
                if (tries < 0)
                    return false;
            }
        }
        await Task.Delay(2_000, token).ConfigureAwait(false);
        return true;
    }

    private async Task<bool> ExitUnionRoomToOverworld(CancellationToken token)
    {
        if (await IsUnionWork(UnionGamingOffset, token).ConfigureAwait(false))
        {
            Log("退出联盟房间...");
            for (int i = 0; i < 3; ++i)
                await Click(B, 0_200, token).ConfigureAwait(false);

            await Click(Y, 1_000, token).ConfigureAwait(false);
            await Click(DDOWN, 0_200, token).ConfigureAwait(false);
            for (int i = 0; i < 3; ++i)
                await Click(A, 0_400, token).ConfigureAwait(false);

            int tries = 10;
            while (await IsUnionWork(UnionGamingOffset, token).ConfigureAwait(false))
            {
                await Task.Delay(0_400, token).ConfigureAwait(false);
                tries--;
                if (tries < 0)
                    return false;
            }
            await Task.Delay(3_000 + Hub.Config.Timings.ExtraTimeLeaveUnionRoom, token).ConfigureAwait(false);
        }
        return true;
    }

    private async Task<PokeTradeResult> ProcessDumpTradeAsync(PokeTradeDetail<PB8> detail, CancellationToken token)
    {
        int ctr = 0;
        var time = TimeSpan.FromSeconds(Hub.Config.Trade.MaxDumpTradeTime);
        var start = DateTime.Now;

        var bctr = 0;
        while (ctr < Hub.Config.Trade.MaxDumpsPerTrade && DateTime.Now - start < time)
        {
            // 我们不再交谈，所以他们可能退出了。
            if (!await IsUnionWork(UnionTalkingOffset, token).ConfigureAwait(false))
                break;
            if (bctr++ % 3 == 0)
                await Click(B, 0_100, token).ConfigureAwait(false);

            // 等待用户输入...需要与之前提供的宝可梦不同。
            var tradeOffered = await ReadUntilChanged(LinkTradePokemonOffset, lastOffered, 3_000, 1_000, false, true, token).ConfigureAwait(false);
            if (!tradeOffered)
                continue;

            // 如果我们检测到变化，他们提供了某些东西。
            var pk = await ReadPokemon(LinkTradePokemonOffset, BoxFormatSlotSize, token).ConfigureAwait(false);
            var newECchk = await SwitchConnection.ReadBytesAbsoluteAsync(LinkTradePokemonOffset, 8, token).ConfigureAwait(false);
            if (pk.Species == 0 || !pk.ChecksumValid || lastOffered.SequenceEqual(newECchk))
                continue;
            lastOffered = newECchk;

            // 从单独的线程发送结果；机器人不需要等待计算完成。
            if (DumpSetting.Dump)
            {
                var subfolder = detail.Type.ToString().ToLower();
                DumpPokemon(DumpSetting.DumpFolder, subfolder, pk); // 接收的
            }

            var la = new LegalityAnalysis(pk);
            var verbose = $"```{la.Report(true)}```";
            Log($"显示的宝可梦是: {(la.Valid ? "合法" : "非法")}。");

            ctr++;
            var msg = Hub.Config.Trade.DumpTradeLegalityCheck ? verbose : $"文件 {ctr}";

            // 关于训练家数据的额外信息，供使用自己训练家数据请求的人使用。
            var ot = pk.OriginalTrainerName;
            var ot_gender = pk.OriginalTrainerGender == 0 ? "男性" : "女性";
            var tid = pk.GetDisplayTID().ToString(pk.GetTrainerIDFormat().GetTrainerIDFormatStringTID());
            var sid = pk.GetDisplaySID().ToString(pk.GetTrainerIDFormat().GetTrainerIDFormatStringSID());
            msg += $"\n**训练家数据**\n```OT: {ot}\nOT性别: {ot_gender}\nTID: {tid}\nSID: {sid}```";

            // 关于闪光蛋的额外信息，因为有人通过存储来跳过孵化。
            var eggstring = pk.IsEgg ? "蛋 " : string.Empty;
            msg += pk.IsShiny ? $"\n**这个宝可梦{eggstring}是闪光的！**" : string.Empty;
            detail.SendNotification(this, pk, msg);
        }

        Log($"在处理 {ctr} 个宝可梦后结束存储循环。");
        if (ctr == 0)
            return PokeTradeResult.TrainerTooSlow;

        TradeSettings.AddCompletedDumps();
        detail.Notifier.SendNotification(this, detail, $"存储了 {ctr} 个宝可梦。");
        detail.Notifier.TradeFinished(this, detail, detail.TradeData); // 空白pk8
        return PokeTradeResult.Success;
    }

    private async Task<TradePartnerBS> GetTradePartnerInfo(CancellationToken token)
    {
        var id = await SwitchConnection.PointerPeek(4, Offsets.LinkTradePartnerIDPointer, token).ConfigureAwait(false);
        var name = await SwitchConnection.PointerPeek(TradePartnerBS.MaxByteLengthStringObject, Offsets.LinkTradePartnerNamePointer, token).ConfigureAwait(false);
        return new TradePartnerBS(id, name);
    }

    protected virtual async Task<(PB8 toSend, PokeTradeResult check)> GetEntityToSend(SAV8BS sav, PokeTradeDetail<PB8> poke, PB8 offered, PB8 toSend, PartnerDataHolder partnerID, CancellationToken token)
    {
        return poke.Type switch
        {
            PokeTradeType.Random => await HandleRandomLedy(sav, poke, offered, toSend, partnerID, token).ConfigureAwait(false),
            _ => (toSend, PokeTradeResult.Success),
        };
    }

    private async Task<(PB8 toSend, PokeTradeResult check)> HandleRandomLedy(SAV8BS sav, PokeTradeDetail<PB8> poke, PB8 offered, PB8 toSend, PartnerDataHolder partner, CancellationToken token)
    {
        // 允许交易伙伴进行Ledy交换。
        var config = Hub.Config.Distribution;
        var trade = Hub.Ledy.GetLedyTrade(offered, partner.TrainerOnlineID, config.LedySpecies);
        if (trade != null)
        {
            if (trade.Type == LedyResponseType.AbuseDetected)
            {
                var msg = $"发现 {partner.TrainerName} 被检测到滥用Ledy交易。";
                EchoUtil.Echo(msg);

                return (toSend, PokeTradeResult.SuspiciousActivity);
            }

            toSend = trade.Receive;
            poke.TradeData = toSend;

            poke.SendNotification(this, "正在注入请求的宝可梦。");
            await Click(A, 0_800, token).ConfigureAwait(false);
            await SetBoxPokemonAbsolute(BoxStartOffset, toSend, token, sav).ConfigureAwait(false);
            await Task.Delay(2_500, token).ConfigureAwait(false);
        }
        else if (config.LedyQuitIfNoMatch)
        {
            var nickname = offered.IsNicknamed ? $" (昵称: \"{offered.Nickname}\")" : string.Empty;
            poke.SendNotification(this, $"未找到与提供的 {GetSpeciesName(offered.Species)}{nickname} 匹配的内容。");
            return (toSend, PokeTradeResult.TrainerRequestBad);
        }

        for (int i = 0; i < 5; i++)
        {
            await Click(A, 0_500, token).ConfigureAwait(false);
        }

        return (toSend, PokeTradeResult.Success);
    }

    private void WaitAtBarrierIfApplicable(CancellationToken token)
    {
        if (!ShouldWaitAtBarrier)
            return;
        var opt = Hub.Config.Distribution.SynchronizeBots;
        if (opt == BotSyncOption.NoSync)
            return;

        var timeoutAfter = Hub.Config.Distribution.SynchronizeTimeout;
        if (FailedBarrier == 1) // 上次迭代失败
            timeoutAfter *= 2; // 如果事情太慢，尝试重新同步。

        var result = Hub.BotSync.Barrier.SignalAndWait(TimeSpan.FromSeconds(timeoutAfter), token);

        if (result)
        {
            FailedBarrier = 0;
            return;
        }

        FailedBarrier++;
        Log($"屏障同步在 {timeoutAfter} 秒后超时。继续。");
    }

    /// <summary>
    /// 检查是否需要更新屏障以考虑此机器人。
    /// 如果应该考虑，如果尚未添加，则将其添加到屏障中。
    /// 如果不应该考虑，如果尚未移除，则将其从屏障中移除。
    /// </summary>
    private void UpdateBarrier(bool shouldWait)
    {
        if (ShouldWaitAtBarrier == shouldWait)
            return; // 不需要更改

        ShouldWaitAtBarrier = shouldWait;
        if (shouldWait)
        {
            Hub.BotSync.Barrier.AddParticipant();
            Log($"加入了屏障。计数: {Hub.BotSync.Barrier.ParticipantCount}");
        }
        else
        {
            Hub.BotSync.Barrier.RemoveParticipant();
            Log($"离开了屏障。计数: {Hub.BotSync.Barrier.ParticipantCount}");
        }
    }

    // 基于 https://github.dev/bdawg1989/SysBot
    private async Task<bool> ApplyAutoOT(PB8 toSend, SAV8BS sav, PokeTradeDetail<PB8> poke, TradePartnerBS tradePartner, CancellationToken token)
    {
        if (token.IsCancellationRequested) return false;

        if (toSend is IHomeTrack pk && pk.HasTracker)
        {
            Log("检测到Home追踪器。无法应用AutoOT。");
            return false;
        }
        // 当前处理者不能是过去世代的OT
        if (toSend.Generation != toSend.Format)
        {
            Log("无法应用伙伴详情：当前处理者不能是不同世代的OT。");
            return false;
        }
        var cln = toSend.Clone();
        cln.TrainerTID7 = uint.Parse(tradePartner.TID7);
        cln.TrainerSID7 = uint.Parse(tradePartner.SID7);
        cln.OriginalTrainerName = tradePartner.TrainerName;
        if (!toSend.IsNicknamed)
            cln.ClearNickname();
        if (toSend.IsShiny)
            cln.PID = (uint)((cln.TID16 ^ cln.SID16 ^ (cln.PID & 0xFFFF) ^ toSend.ShinyXor) << 16) | (cln.PID & 0xFFFF);
        if (!toSend.ChecksumValid)
            cln.RefreshChecksum();
        var tradeBS = new LegalityAnalysis(cln);
        if (tradeBS.Valid)
        {
            Log("应用交易伙伴信息后宝可梦合法。交换详情。");
            await SetBoxPokemonAbsolute(BoxStartOffset, cln, token, sav).ConfigureAwait(false);
        }
        else
        {
            Log("使用交易伙伴信息后宝可梦不合法。");
        }
        return tradeBS.Valid;
    }

    private async Task<PokeTradeResult> PerformRemainingLinkCodeTrades(SAV8BS sav, PokeTradeDetail<PB8> poke, TradePartnerBS tradePartner, CancellationToken token)
    {
        var pkms = AbstractTrade<PB8>.GetPKMsFromPokeTradeDetail(poke).Skip(1);
        var skipAutoOTList = AbstractTrade<PB8>.GetSkipAutoOTListFromPokeTradeDetail(poke).Skip(1).ToList();
        foreach (var (toSend, index) in pkms.Select((value, i) => (value, i)))
        {
            Log($"处理批量交易中剩余的宝可梦 {index + 1} / {pkms.Count()}。");
            // 等待用户输入...需要与之前提供的宝可梦不同。
            var tradeOffered = await ReadUntilChanged(LinkTradePokemonOffset, lastOffered, 25_000, 1_000, false, true, token).ConfigureAwait(false);
            if (!tradeOffered)
                return PokeTradeResult.TrainerTooSlow;

            // 如果我们检测到变化，他们提供了某些东西。
            var offered = await ReadPokemon(LinkTradePokemonOffset, BoxFormatSlotSize, token).ConfigureAwait(false);
            if (offered.Species == 0 || !offered.ChecksumValid)
                return PokeTradeResult.TrainerTooSlow;
            lastOffered = await SwitchConnection.ReadBytesAbsoluteAsync(LinkTradePokemonOffset, 8, token).ConfigureAwait(false);

            var trainer = new PartnerDataHolder(0, tradePartner.TrainerName, tradePartner.TID7);

            if (Hub.Config.Legality.UseTradePartnerInfo && !skipAutoOTList[index])
                await ApplyAutoOT(toSend, sav, poke, tradePartner, token);
            else
                await SetBoxPokemonAbsolute(BoxStartOffset, toSend, token, sav).ConfigureAwait(false);

            var tradeResult = await ConfirmAndStartTrading(poke, token).ConfigureAwait(false);
            if (tradeResult != PokeTradeResult.Success)
                return tradeResult;

            if (token.IsCancellationRequested)
                return PokeTradeResult.RoutineCancel;

            // 交易成功！
            var received = await ReadPokemon(BoxStartOffset, BoxFormatSlotSize, token).ConfigureAwait(false);
            // b1s1中的宝可梦与他们应该接收的相同（从未发送）。
            if (SearchUtil.HashByDetails(received) == SearchUtil.HashByDetails(toSend) && received.Checksum == toSend.Checksum)
            {
                Log("用户未完成交易。");
                return PokeTradeResult.TrainerTooSlow;
            }
        }
        return PokeTradeResult.Success;
    }
}
