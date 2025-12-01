using Kook;
using Kook.Commands;
using PKHeX.Core;
using System.Text;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Kook;

// ReSharper disable once UnusedType.Global
public class BotModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
{
    [Command("botStatus")]
    [Summary("获取机器人的状态信息")]
    [RequireSudo]
    public async Task GetStatusAsync()
    {
        var me = KookBot<T>.Runner;
        var sb = new StringBuilder();
        foreach (var bot in me.Bots)
        {
            if (bot.Bot is not PokeRoutineExecutorBase b)
                continue;
            sb.AppendLine(GetDetailedSummary(b));
        }
        if (sb.Length == 0)
        {
            await ReplyTextAsync("未配置任何机器人。").ConfigureAwait(false);
            return;
        }
        await ReplyTextAsync(Format.Code(sb.ToString())).ConfigureAwait(false);
    }

    private static string GetDetailedSummary<TBot>(TBot z) where TBot : PokeRoutineExecutorBase
    {
        return $"- {z.Connection.Name} | {z.Connection.Label} - {z.Config.CurrentRoutineType} ~ {z.LastTime:hh:mm:ss} | {z.LastLogged}";
    }

    [Command("botStart")]
    [Summary("通过IP地址/端口启动机器人")]
    [RequireSudo]
    public async Task StartBotAsync(string ip)
    {
        var bot = KookBot<T>.Runner.GetBot(ip);
        if (bot == null)
        {
            await ReplyTextAsync($"没有机器人使用该IP地址 ({ip})。").ConfigureAwait(false);
            return;
        }

        bot.Start();
        await Context.Channel.EchoAndReply($"已命令IP地址为 {ip} 的机器人 ({bot.Bot.Connection.Label}) 启动。").ConfigureAwait(false);
    }

    [Command("botStop")]
    [Summary("通过IP地址/端口停止机器人")]
    [RequireSudo]
    public async Task StopBotAsync(string ip)
    {
        var bot = KookBot<T>.Runner.GetBot(ip);
        if (bot == null)
        {
            await ReplyTextAsync($"没有机器人使用该IP地址 ({ip})。").ConfigureAwait(false);
            return;
        }

        bot.Stop();
        await Context.Channel.EchoAndReply($"已命令IP地址为 {ip} 的机器人 ({bot.Bot.Connection.Label}) 停止。").ConfigureAwait(false);
    }

    [Command("botIdle")]
    [Alias("botPause")]
    [Summary("通过IP地址/端口命令机器人进入空闲状态")]
    [RequireSudo]
    public async Task IdleBotAsync(string ip)
    {
        var bot = KookBot<T>.Runner.GetBot(ip);
        if (bot == null)
        {
            await ReplyTextAsync($"没有机器人使用该IP地址 ({ip})。").ConfigureAwait(false);
            return;
        }

        bot.Pause();
        await Context.Channel.EchoAndReply($"已命令IP地址为 {ip} 的机器人 ({bot.Bot.Connection.Label}) 进入空闲状态。").ConfigureAwait(false);
    }

    [Command("botChange")]
    [Summary("更改机器人的工作流程类型")]
    [RequireSudo]
    public async Task ChangeTaskAsync(string ip, [Summary("工作流程枚举名称")] PokeRoutineType task)
    {
        var bot = KookBot<T>.Runner.GetBot(ip);
        if (bot == null)
        {
            await ReplyTextAsync($"没有机器人使用该IP地址 ({ip})。").ConfigureAwait(false);
            return;
        }

        bot.Bot.Config.Initialize(task);
        await Context.Channel.EchoAndReply($"已命令IP地址为 {ip} 的机器人 ({bot.Bot.Connection.Label}) 的下一个任务为 {task}。").ConfigureAwait(false);
    }

    [Command("botRestart")]
    [Summary("通过IP地址重启机器人（多个IP地址用逗号分隔）")]
    [RequireSudo]
    public async Task RestartBotAsync(string ipAddressesCommaSeparated)
    {
        var ips = ipAddressesCommaSeparated.Split(',');
        foreach (var ip in ips)
        {
            var bot = KookBot<T>.Runner.GetBot(ip);
            if (bot == null)
            {
                await ReplyTextAsync($"没有机器人使用该IP地址 ({ip})。").ConfigureAwait(false);
                return;
            }

            var c = bot.Bot.Connection;
            c.Reset();
            bot.Start();
            await Context.Channel.EchoAndReply($"已命令IP地址为 {ip} 的机器人 ({c.Label}) 重启。").ConfigureAwait(false);
        }
    }
}
