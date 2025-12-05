using Kook;
using Kook.WebSocket;
using PKHeX.Core;
using SysBot.Base;
using SysBot.Pokemon.Helpers;
using System;

namespace SysBot.Pokemon.Kook;

public class KookTradeAdapter<T> : AbstractTrade<T> where T : PKM, new()
{
    private readonly KookBot<T> _bot;
    private readonly SocketUserMessage _message;
    private readonly SocketGuildUser _user;
    private readonly TradeQueueInfo<T> _queueInfo;

    public KookTradeAdapter(KookBot<T> bot, SocketUserMessage message, SocketGuildUser user)
    {
        _bot = bot;
        _message = message;
        _user = user;

        // 设置交易者信息
        var trainerInfo = new PokeTradeTrainerInfo(user.Username, (ulong)user.Id);
        SetPokeTradeTrainerInfo(trainerInfo);

        // 设置队列信息
        _queueInfo = KookBot<T>.Runner.Hub.Queues.Info;
        SetTradeQueueInfo(_queueInfo);
    }

    // 添加这个方法供外部访问
    public TradeQueueInfo<T> GetQueueInfo() => _queueInfo;

    public override void SendMessage(string message)
    {
        // 检查是否是队列位置消息
        if (IsQueuePositionMessage(message))
        {
            // 发送卡片消息而不是文本消息
            SendQueuePositionCard(message);
            return;
        }

        // 其他消息保持文本格式
        _ = _message.Channel.SendTextAsync(message).ConfigureAwait(false);
    }

    // 检查是否是队列位置消息
    private bool IsQueuePositionMessage(string message)
    {
        // 检查消息是否包含"你在第"和"位"
        return message.Contains("你在第") && message.Contains("位");
    }

    // 发送队列位置卡片
    private async void SendQueuePositionCard(string message)
    {
        try
        {
            // 从消息中提取位置信息
            var positionText = ExtractPositionFromMessage(message);

            var card = new CardBuilder()
                .WithTheme(CardTheme.Info)
                .AddModule<SectionModuleBuilder>(b => b.WithText(positionText))
                .Build();

            await _message.Channel.SendCardAsync(card).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // 如果卡片发送失败，回退到文本消息
            LogUtil.LogError($"发送队列位置卡片失败: {ex.Message}", "KookTradeAdapter");
            await _message.Channel.SendTextAsync(message).ConfigureAwait(false);
        }
    }

    // 从消息中提取位置信息
    private string ExtractPositionFromMessage(string message)
    {
        // 消息格式可能是："你在第1位" 或 "xxx，你在第1位"
        if (message.Contains("你在第"))
        {
            var startIndex = message.IndexOf("你在第");
            var positionText = message.Substring(startIndex);

            // 只取到"位"为止
            var endIndex = positionText.IndexOf("位");
            if (endIndex > 0)
            {
                return positionText.Substring(0, endIndex + 1);
            }
        }

        // 如果提取失败，返回原始消息
        return message;
    }

    public override IPokeTradeNotifier<T> GetPokeTradeNotifier(T pkm, int code)
    {
        // 创建与单个PKM交易相同的通知器
        var trainer = new PokeTradeTrainerInfo(_user.Username, (ulong)_user.Id);
        var channel = _message.Channel as SocketTextChannel;
        return new KookTradeNotifier<T>(pkm, trainer, code, _user, channel);
    }
}
