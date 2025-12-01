using Kook;
using Kook.WebSocket;
using PKHeX.Core;
using SysBot.Pokemon.Kook;
using System;
using System.Linq;

namespace SysBot.Pokemon.Kook;

public class KookTradeNotifier<T>(T Data, PokeTradeTrainerInfo Info, int Code, SocketUser Trader, SocketTextChannel? channel)
    : IPokeTradeNotifier<T>
    where T : PKM, new()
{
    private T Data { get; } = Data;
    private PokeTradeTrainerInfo Info { get; } = Info;
    private int Code { get; } = Code;
    private SocketUser Trader { get; } = Trader;
    private SocketTextChannel? Channel { get; } = channel;
    public Action<PokeRoutineExecutor<T>>? OnFinish { private get; set; }
    public readonly PokeTradeHub<T> Hub = KookBot<T>.Runner.Hub;

    public void TradeInitialize(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info)
    {
        var pokemonName = Data.Species == 0 ? "神秘宝可梦" : ShowdownTranslator<T>.GameStringsZh.Species[Data.Species];

        // 使用卡片消息替换原来的文本消息
        var card = CardHelper.CreateTradeInitCard(Trader.Username, pokemonName);
        if (Channel != null)
            Channel.SendCardAsync(card.Build()).ConfigureAwait(false);

        // 原有的私信通知保持不变
        var receive = Data.Species == 0 ? string.Empty : $" ({pokemonName})";
        Trader.SendTextAsync($"正在初始化交易{receive}。请做好准备。您的交易码是 {Format.Bold($"{Code: 0000 0000}")}。").ConfigureAwait(false);
    }

    public void TradeSearching(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info)
    {
        var name = Info.TrainerName;
        var trainer = string.IsNullOrEmpty(name) ? string.Empty : $", {name}";

        var pokemonName = Data.Species == 0 ? "神秘宝可梦" : ShowdownTranslator<T>.GameStringsZh.Species[Data.Species];
        var statusMessage = Data.Species == 0 ? "正在等待您" : $"正在派送 {pokemonName}";

        // 使用卡片消息替换原来的文本消息
        var card = CardHelper.CreateTradeSearchingCard(
            Trader.Username,
            statusMessage,
            trainer,
            routine.InGameName
        );
        if (Channel != null)
            Channel.SendCardAsync(card.Build()).ConfigureAwait(false);

        // 原有的私信通知保持不变
        Trader.SendTextAsync($"{statusMessage}{trainer}！您的交易码是 {Format.Bold($"{Code:0000 0000}")}。我的游戏内名称是 {Format.Bold($"{routine.InGameName}")}。").ConfigureAwait(false);
    }

    public void TradeCanceled(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, PokeTradeResult msg)
    {
        OnFinish?.Invoke(routine);
        var chineseMsg = msg.ToString() switch
        {
            "NoTrainerFound" => "未找到训练家",
            "TrainerTooSlow" => "训练家响应超时",
            "TrainerLeft" => "训练家已离开",
            "TrainerCanceled" => "训练家取消了交易",
            "IllegalTrade" => "非法交易请求",
            "SuspiciousActivity" => "检测到可疑活动",
            _ => msg.ToString()
        };

        // 使用卡片消息替换原来的文本消息
        var card = CardHelper.CreateTradeCanceledCard(Trader.Username, chineseMsg);
        if (Channel != null)
            Channel.SendCardAsync(card.Build()).ConfigureAwait(false);

        // 原有的私信通知保持不变
        Trader.SendTextAsync($"交易已取消: {chineseMsg}").ConfigureAwait(false);
    }

    public void TradeFinished(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, T result)
    {
        OnFinish?.Invoke(routine);
        var tradedToUser = Data.Species;
        var pokemonName = tradedToUser != 0 ? ShowdownTranslator<T>.GameStringsZh.Species[tradedToUser] : "宝可梦";
        var message = tradedToUser != 0 ? $"交易完成。祝您使用 {pokemonName} 愉快！" : "交易完成！";

        // 使用卡片消息替换原来的文本消息
        var card = CardHelper.CreateTradeFinishedCard(Trader.Username, message);
        if (Channel != null)
            Channel.SendCardAsync(card.Build()).ConfigureAwait(false);

        // 原有的私信通知保持不变
        Trader.SendTextAsync(message).ConfigureAwait(false);
        if (result.Species != 0 && Hub.Config.Kook.ReturnPKMs)
            Trader.SendPKMAsync(result, "这是您交易给我的宝可梦！").ConfigureAwait(false);
    }

    public void SendNotification(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, string message)
    {
        // 修复：直接过滤掉有问题的英文消息
        if (message.Contains("取消交易: NoTrainerFound") ||
            message.Contains("Canceling trade: NoTrainerFound") ||
            message.Contains("Oops! Something happened."))
        {
            // 直接忽略这条消息，因为 TradeCanceled 方法已经处理了
            return;
        }

        // 只发送其他正常的消息到私信
        Trader.SendTextAsync(message).ConfigureAwait(false);
    }

    public void SendNotification(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, PokeTradeSummary message)
    {
        if (message.ExtraInfo is SeedSearchResult r)
        {
            SendNotificationZ3(r);
            return;
        }

        var msg = message.Summary;
        if (message.Details.Count > 0)
            msg += ", " + string.Join(", ", message.Details.Select(z => $"{z.Heading}: {z.Detail}"));
        Trader.SendTextAsync(msg).ConfigureAwait(false);
    }

    public void SendNotification(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, T result, string message)
    {
        if (result.Species != 0 && (Hub.Config.Kook.ReturnPKMs || info.Type == PokeTradeType.Dump))
            Trader.SendPKMAsync(result, message).ConfigureAwait(false);
    }

    private void SendNotificationZ3(SeedSearchResult r)
    {
        var lines = r.ToString();

        var card = new CardBuilder()
            .AddModule(new SectionModuleBuilder().WithText($"这是种子 {r.Seed:X16} 的详细信息:"))
            .AddModule(new SectionModuleBuilder().WithText($"种子: {r.Seed:X16}"))
            .AddModule(new SectionModuleBuilder().WithText(lines))
            .Build();
        Trader.SendCardAsync(card).ConfigureAwait(false);
    }
}
