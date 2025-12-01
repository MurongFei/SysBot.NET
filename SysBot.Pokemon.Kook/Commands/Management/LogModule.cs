using Kook;
using Kook.Commands;
using Kook.WebSocket;
using SysBot.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Kook;

public class LogModule : ModuleBase<SocketCommandContext>
{
    private static readonly Dictionary<ulong, ChannelLogger> Channels = [];

    public static void RestoreLogging(KookSocketClient Kook, KookSettings settings)
    {
        foreach (var ch in settings.LoggingChannels)
        {
            if (Kook.GetChannel(ch.ID) is ISocketMessageChannel c)
                AddLogChannel(c, ch.ID);
        }

        LogUtil.LogInfo("已在机器人启动时添加日志记录到Kook频道。", "Kook");
    }

    [Command("logHere")]
    [Summary("让机器人在此频道记录日志。")]
    [RequireSudo]
    public async Task AddLogAsync()
    {
        var c = Context.Channel;
        var cid = c.Id;
        if (Channels.TryGetValue(cid, out _))
        {
            await ReplyTextAsync("已在此频道记录日志。").ConfigureAwait(false);
            return;
        }

        AddLogChannel(c, cid);

        // 添加到Kook全局记录器（在程序关闭时保存）
        KookBotSettings.Settings.LoggingChannels.AddIfNew([GetReference(Context.Channel)]);
        await ReplyTextAsync("已添加日志记录输出到此频道！").ConfigureAwait(false);
    }

    private static void AddLogChannel(ISocketMessageChannel c, ulong cid)
    {
        var logger = new ChannelLogger(cid, c);
        LogUtil.Forwarders.Add(logger);
        Channels.Add(cid, logger);
    }

    [Command("logInfo")]
    [Summary("显示日志记录设置信息。")]
    [RequireSudo]
    public async Task DumpLogInfoAsync()
    {
        foreach (var c in Channels)
            await ReplyTextAsync($"{c.Key} - {c.Value}").ConfigureAwait(false);
    }

    [Command("logClear")]
    [Summary("清除指定频道的日志记录设置。")]
    [RequireSudo]
    public async Task ClearLogsAsync()
    {
        var id = Context.Channel.Id;
        if (!Channels.TryGetValue(id, out var log))
        {
            await ReplyTextAsync("未在此频道启用日志记录功能。").ConfigureAwait(false);
            return;
        }
        LogUtil.Forwarders.Remove(log);
        Channels.Remove(Context.Channel.Id);
        KookBotSettings.Settings.LoggingChannels.RemoveAll(z => z.ID == id);
        await ReplyTextAsync($"已从频道清除日志记录: {Context.Channel.Name}").ConfigureAwait(false);
    }

    [Command("logClearAll")]
    [Summary("清除所有日志记录设置。")]
    [RequireSudo]
    public async Task ClearLogsAllAsync()
    {
        foreach (var l in Channels)
        {
            var entry = l.Value;
            await ReplyTextAsync($"已从 {entry.ChannelName} ({entry.ChannelID}) 清除日志记录！").ConfigureAwait(false);
            LogUtil.Forwarders.Remove(entry);
        }

        LogUtil.Forwarders.RemoveAll(y => Channels.Select(z => z.Value).Contains(y));
        Channels.Clear();
        KookBotSettings.Settings.LoggingChannels.Clear();
        await ReplyTextAsync("已从所有频道清除日志记录！").ConfigureAwait(false);
    }

    private RemoteControlAccess GetReference(IChannel channel) => new()
    {
        ID = channel.Id,
        Name = channel.Name,
        Comment = $"由 {Context.User.Username} 于 {DateTime.Now:yyyy.MM.dd-hh:mm:ss} 添加",
    };
}
