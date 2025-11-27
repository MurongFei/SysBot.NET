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
            await context.Channel.SendTextAsync("交易码应在 00000000-99999999 范围内！").ConfigureAwait(false);
            return;
        }

        try
        {
            const string helper = "已将您添加到队列中！当您的交易开始时，我会在这里通知您。";
            var test = await trader.SendTextAsync(helper).ConfigureAwait(false);

            // 尝试添加到队列
            var result = AddToTradeQueue(context, trade, code, trainer, sig, routine, type, trader, out var msg);

            // 在频道中通知
            await context.Channel.SendTextAsync(msg).ConfigureAwait(false);
            // 在私信中通知，镜像频道中的内容
            await trader.SendTextAsync($"{msg}\n您的交易码将是 {Format.Bold($"{code:0000 0000}")}。").ConfigureAwait(false);

            // 清理工作
            if (result)
            {
                // 为保护隐私，删除用户的加入消息
                if (!context.IsPrivate)
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

    private static bool AddToTradeQueue(SocketCommandContext context, T pk, int code, string trainerName, RequestSignificance sig, PokeRoutineType type, PokeTradeType t, SocketUser trader, out string msg)
    {
        var user = trader;
        var userID = user.Id;
        var name = user.Username;

        var trainer = new PokeTradeTrainerInfo(trainerName, userID);
        var notifier = new KookTradeNotifier<T>(pk, trainer, code, user);
        var detail = new PokeTradeDetail<T>(pk, trainer, notifier, t, code, sig == RequestSignificance.Favored);
        var trade = new TradeEntry<T>(detail, userID, type, name);

        var hub = KookBot<T>.Runner.Hub;
        var Info = hub.Queues.Info;
        var added = Info.AddToTradeQueue(trade, userID, sig == RequestSignificance.Owner);

        if (added == QueueResultAdd.AlreadyInQueue)
        {
            msg = "抱歉，您已经在队列中了。";
            return false;
        }

        var position = Info.CheckPosition(userID, type);

        var ticketID = "";
        if (TradeStartModule<T>.IsStartChannel(context.Channel.Id))
            ticketID = $", 唯一ID: {detail.ID}";

        var pokeName = "";
        if (t == PokeTradeType.Specific && pk.Species != 0)
            pokeName = $" 接收: {GameInfo.GetStrings("zh").Species[pk.Species]}。";
        msg = $"{user.KMarkdownMention} - 已添加到 {type} 队列{ticketID}。当前位置: {position.Position}。{pokeName}";

        var botct = Info.Hub.Bots.Count;
        if (position.Position > botct)
        {
            var eta = Info.Hub.Config.Queues.EstimateDelay(position.Position, botct);
            msg += $" 预计等待: {eta:F1} 分钟。";
        }
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
                    var permissions = context.Guild.CurrentUser.GetPermissions(context.Channel as IGuildChannel);
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
                break;
            //case KookErrorCode.CannotSendMessageToUser:
            //{
            //    // 用户可能关闭了私信，或者Kook认为他们关闭了。
            //    message = context.User == trader ? "您必须启用私信才能加入队列！" : "被提及的用户必须启用私信才能加入队列！";
            //}
            //    break;
            default:
                {
                    // 发送通用错误消息
                    message = ex.KookCode != null ? $"Kook 错误 {(int)ex.KookCode}: {ex.Reason}" : $"HTTP 错误 {(int)ex.HttpCode}: {ex.Message}";
                }
                break;
        }
        await context.Channel.SendTextAsync(message).ConfigureAwait(false);
    }
}
