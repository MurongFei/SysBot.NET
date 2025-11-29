using Kook;
using Kook.Commands;
using Kook.Net;
using Kook.WebSocket;
using PKHeX.Core;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Kook;

public static class QueueHelper<T> where T : PKM, new()
{
    private const uint MaxTradeCode = 9999_9999;

    public static async Task AddToQueueAsync(SocketCommandContext context, int code, string trainer, RequestSignificance sig, T trade, PokeRoutineType routine, PokeTradeType type, SocketUser trader)
    {
        if ((uint)code > MaxTradeCode)
        {
            if (context.Channel != null)
                await context.Channel.SendTextAsync("交易码应在 00000000-99999999 范围内！").ConfigureAwait(false);
            return;
        }

        try
        {
            const string helper = "已将您添加到队列中！当您的交易开始时，我会在这里通知您。";
            var test = await trader.SendTextAsync(helper).ConfigureAwait(false);

            // 尝试添加到队列
            var result = AddToTradeQueue(context, trade, code, trainer, sig, routine, type, trader, out var msg, out var position, out var routineName, out var pokeName, out var waitTime);

            // 在频道中通知 - 使用卡片消息
            if (context.Channel != null)
            {
                // 发送队列添加卡片 - 传递所有参数
                var queueCard = CardHelper.CreateQueueAddedCard(trader.Username, routineName, position, pokeName ?? "", waitTime ?? "");
                await context.Channel.SendCardAsync(queueCard.Build()).ConfigureAwait(false);
            }

            // 在私信中通知，镜像频道中的内容（但去掉等待时间，保持原样）
            await trader.SendTextAsync($"{msg}\n您的交易码将是 {Format.Bold($"{code:0000 0000}")}.").ConfigureAwait(false);

            // 清理工作
            if (result)
            {
                // 为保护隐私，只删除传统的 $trade 命令消息
                // 其他所有方式（直接文件上传、@机器人+Showdown）都保留消息
                if (!context.IsPrivate && IsTraditionalTradeCommand(context.Message) && context.Channel != null)
                    await context.Message.DeleteAsync(RequestOptions.Default).ConfigureAwait(false);
            }
            else
            {
                // 删除我们的"正在添加您！"消息，并发送与通用频道相同的消息
                // KOOK.Net 中，无法删除私信消息，直接忽略此步骤
                // await test.DeleteAsync().ConfigureAwait(false);
            }
        }
        catch (HttpException ex)
        {
            await HandleKookExceptionAsync(context, trader, ex).ConfigureAwait(false);
        }
    }

    public static Task AddToQueueAsync(SocketCommandContext context, int code, string trainer, RequestSignificance sig, T trade, PokeRoutineType routine, PokeTradeType type)
    {
        return AddToQueueAsync(context, code, trainer, sig, trade, routine, type, context.User);
    }

    // 新增辅助方法：判断是否为传统交易命令
    private static bool IsTraditionalTradeCommand(SocketUserMessage msg)
    {
        // 只有以 $trade 开头的消息会被删除
        // 其他所有方式（直接文件上传、@机器人+Showdown）都保留消息
        return msg.Content.StartsWith("$trade");
    }

    private static bool AddToTradeQueue(SocketCommandContext context, T pk, int code, string trainerName, RequestSignificance sig, PokeRoutineType type, PokeTradeType t, SocketUser trader, out string msg, out int position, out string routineName, out string pokeName, out string waitTime)
    {
        var user = trader;
        var userID = user.Id;
        var name = user.Username;

        var trainer = new PokeTradeTrainerInfo(trainerName, userID);

        // 修改：传递频道信息给 KookTradeNotifier
        var notifier = new KookTradeNotifier<T>(pk, trainer, code, user, context.Channel as SocketTextChannel);

        var detail = new PokeTradeDetail<T>(pk, trainer, notifier, t, code, sig == RequestSignificance.Favored);
        var trade = new TradeEntry<T>(detail, userID, type, name);

        var hub = KookBot<T>.Runner.Hub;
        var Info = hub.Queues.Info;
        var added = Info.AddToTradeQueue(trade, userID, sig == RequestSignificance.Owner);

        // 初始化输出参数
        position = 0;
        routineName = "";
        pokeName = "";
        waitTime = "";

        if (added == QueueResultAdd.AlreadyInQueue)
        {
            msg = "抱歉，您已经在队列中了。";
            return false;
        }

        var positionInfo = Info.CheckPosition(userID, type);
        position = positionInfo.Position;

        // 主要队列消息 - 使用中文例行程序名称
        routineName = type.ToString() switch
        {
            "LinkTrade" => "连接交易",
            "Clone" => "克隆",
            "Dump" => "导出",
            "SeedCheck" => "种子检查",
            _ => type.ToString()
        };

        // 宝可梦名称
        if (t == PokeTradeType.Specific && pk.Species != 0)
            pokeName = $"接收: {ShowdownTranslator<T>.GameStringsZh.Species[pk.Species]}";

        msg = $"{user.Username} - 已添加到 {routineName} 队列。当前位置: {position}。";

        var botct = Info.Hub.Bots.Count;

        // 计算等待时间
        if (position > 1)
        {
            var eta = Info.Hub.Config.Queues.EstimateDelay(position, botct);
            waitTime = $"预计等待: {eta:F1} 分钟";
            msg += $" {waitTime}。";
        }
        else
        {
            // 第一位用户，立即开始交易
            waitTime = "预计等待: 0 分钟";
            // 注意：不在文本消息中添加等待时间，只在卡片中添加
        }

        if (!string.IsNullOrEmpty(pokeName))
            msg += $" {pokeName}.";

        return true;
    }

    private static async Task HandleKookExceptionAsync(SocketCommandContext context, SocketUser trader, HttpException ex)
    {
        string message = string.Empty;
        switch (ex.KookCode)
        {
            case KookErrorCode.MissingPermissions:
                {
                    // 检查异常是否由于缺少"发送消息"或"管理消息"权限引起。如果是，则提醒机器人所有者。
                    if (context.Channel is IGuildChannel guildChannel && context.Guild != null)
                    {
                        var permissions = context.Guild.CurrentUser.GetPermissions(guildChannel);
                        if (!permissions.SendMessages)
                        {
                            // 在日志中提醒所有者
                            message = "您必须授予我\"发送消息\"权限！";
                            Base.LogUtil.LogError(message, "QueueHelper");
                            return;
                        }
                        if (!permissions.ManageMessages)
                        {
                            var owner = KookBotSettings.Manager.Owner;
                            message = $"{MentionUtils.KMarkdownMentionUser(owner)} 您必须授予我\"管理消息\"权限！";
                        }
                    }
                }
                break;
            default:
                {
                    // 发送通用错误消息
                    message = ex.KookCode != null ? $"Kook 错误 {(int)ex.KookCode}: {ex.Reason}" : $"HTTP 错误 {(int)ex.HttpCode}: {ex.Message}";
                }
                break;
        }
        if (context.Channel != null)
            await context.Channel.SendTextAsync(message).ConfigureAwait(false);
    }
}
