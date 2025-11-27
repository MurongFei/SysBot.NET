using Kook.Commands;
using PKHeX.Core;
using SysBot.Base;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Kook;

[Summary("远程控制机器人。")]
public class RemoteControlModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
{
    [Command("click")]
    [Summary("点击指定的按钮。")]
    [RequireRoleAccess(nameof(KookManager.RolesRemoteControl))]
    public async Task ClickAsync(SwitchButton b)
    {
        var bot = KookBot<T>.Runner.Bots.Find(z => IsRemoteControlBot(z.Bot));
        if (bot == null)
        {
            await ReplyTextAsync($"没有可用的机器人来执行您的命令: {b}").ConfigureAwait(false);
            return;
        }

        await ClickAsyncImpl(b, bot).ConfigureAwait(false);
    }

    [Command("click")]
    [Summary("点击指定的按钮。")]
    [RequireSudo]
    public async Task ClickAsync(string ip, SwitchButton b)
    {
        var bot = KookBot<T>.Runner.GetBot(ip);
        if (bot == null)
        {
            await ReplyTextAsync($"没有可用的机器人来执行您的命令: {b}").ConfigureAwait(false);
            return;
        }

        await ClickAsyncImpl(b, bot).ConfigureAwait(false);
    }

    [Command("setStick")]
    [Summary("设置摇杆到指定位置。")]
    [RequireRoleAccess(nameof(KookManager.RolesRemoteControl))]
    public async Task SetStickAsync(SwitchStick s, short x, short y, ushort ms = 1_000)
    {
        var bot = KookBot<T>.Runner.Bots.Find(z => IsRemoteControlBot(z.Bot));
        if (bot == null)
        {
            await ReplyTextAsync($"没有可用的机器人来执行您的命令: {s}").ConfigureAwait(false);
            return;
        }

        await SetStickAsyncImpl(s, x, y, ms, bot).ConfigureAwait(false);
    }

    [Command("setStick")]
    [Summary("设置摇杆到指定位置。")]
    [RequireSudo]
    public async Task SetStickAsync(string ip, SwitchStick s, short x, short y, ushort ms = 1_000)
    {
        var bot = KookBot<T>.Runner.GetBot(ip);
        if (bot == null)
        {
            await ReplyTextAsync($"没有机器人使用该IP地址 ({ip})。").ConfigureAwait(false);
            return;
        }

        await SetStickAsyncImpl(s, x, y, ms, bot).ConfigureAwait(false);
    }

    [Command("setScreenOn")]
    [Alias("screenOn", "scrOn")]
    [Summary("开启屏幕")]
    [RequireSudo]
    public Task SetScreenOnAsync([Remainder] string ip)
    {
        return SetScreen(true, ip);
    }

    [Command("setScreenOff")]
    [Alias("screenOff", "scrOff")]
    [Summary("关闭屏幕")]
    [RequireSudo]
    public Task SetScreenOffAsync([Remainder] string ip)
    {
        return SetScreen(false, ip);
    }

    private async Task SetScreen(bool on, string ip)
    {
        var bot = GetBot(ip);
        if (bot == null)
        {
            await ReplyTextAsync($"没有机器人使用该IP地址 ({ip})。").ConfigureAwait(false);
            return;
        }

        var b = bot.Bot;
        var crlf = b is SwitchRoutineExecutor<PokeBotState> { UseCRLF: true };
        await b.Connection.SendAsync(SwitchCommand.SetScreen(on ? ScreenState.On : ScreenState.Off, crlf), CancellationToken.None).ConfigureAwait(false);
        await ReplyTextAsync("屏幕状态已设置为: " + (on ? "开启" : "关闭")).ConfigureAwait(false);
    }

    private static BotSource<PokeBotState>? GetBot(string ip)
    {
        var r = KookBot<T>.Runner;
        return r.GetBot(ip) ?? r.Bots.Find(x => x.IsRunning); // 对于错误输入IP地址的单机器人实例的安全回退
    }

    private async Task ClickAsyncImpl(SwitchButton button, BotSource<PokeBotState> bot)
    {
        if (!Enum.IsDefined(button))
        {
            await ReplyTextAsync($"未知的按钮值: {button}").ConfigureAwait(false);
            return;
        }

        var b = bot.Bot;
        var crlf = b is SwitchRoutineExecutor<PokeBotState> { UseCRLF: true };
        await b.Connection.SendAsync(SwitchCommand.Click(button, crlf), CancellationToken.None).ConfigureAwait(false);
        await ReplyTextAsync($"{b.Connection.Name} 已执行: {button}").ConfigureAwait(false);
    }

    private async Task SetStickAsyncImpl(SwitchStick s, short x, short y, ushort ms, BotSource<PokeBotState> bot)
    {
        if (!Enum.IsDefined(s))
        {
            await ReplyTextAsync($"未知的摇杆: {s}").ConfigureAwait(false);
            return;
        }

        var b = bot.Bot;
        var crlf = b is SwitchRoutineExecutor<PokeBotState> { UseCRLF: true };
        await b.Connection.SendAsync(SwitchCommand.SetStick(s, x, y, crlf), CancellationToken.None).ConfigureAwait(false);
        await ReplyTextAsync($"{b.Connection.Name} 已执行: {s}").ConfigureAwait(false);
        await Task.Delay(ms).ConfigureAwait(false);
        await b.Connection.SendAsync(SwitchCommand.ResetStick(s, crlf), CancellationToken.None).ConfigureAwait(false);
        await ReplyTextAsync($"{b.Connection.Name} 已重置摇杆位置。").ConfigureAwait(false);
    }

    private static bool IsRemoteControlBot(RoutineExecutor<PokeBotState> botstate)
        => botstate is RemoteControlBotSWSH or RemoteControlBotBS or RemoteControlBotLA or RemoteControlBotSV;
}
