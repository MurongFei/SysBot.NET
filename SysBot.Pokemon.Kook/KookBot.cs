using Kook;
using Kook.Commands;
using Kook.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using PKHeX.Core;
using SysBot.Base;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace SysBot.Pokemon.Kook;

public static class KookBotSettings
{
    public static KookManager Manager { get; internal set; } = default!;
    public static KookSettings Settings => Manager.Config;
    public static PokeTradeHubConfig HubConfig { get; internal set; } = default!;
}

public sealed class KookBot<T> where T : PKM, new()
{
    public static PokeBotRunner<T> Runner { get; private set; } = default!;
    private readonly KookSocketClient _client;
    private readonly KookManager Manager;
    public readonly PokeTradeHub<T> Hub;

    private readonly CommandService _commands;
    private readonly IServiceProvider _services;
    private bool MessageChannelsLoaded { get; set; }

    // 新增：添加静态属性访问
    private static TradeQueueInfo<T> Info => KookBot<T>.Runner.Hub.Queues.Info;

    public KookBot(PokeBotRunner<T> runner)
    {
        Runner = runner;
        Hub = runner.Hub;
        Manager = new KookManager(Hub.Config.Kook);

        KookBotSettings.Manager = Manager;
        KookBotSettings.HubConfig = Hub.Config;

        _client = new KookSocketClient(new KookSocketConfig
        {
            LogLevel = LogSeverity.Info,
            MessageCacheSize = 1000, // 根据需要调整
            AlwaysDownloadUsers = true, // 为命令下载用户数据
            DefaultRetryMode = RetryMode.AlwaysRetry,
        });

        _commands = new CommandService(new CommandServiceConfig
        {
            DefaultRunMode = Hub.Config.Kook.AsyncCommands ? RunMode.Async : RunMode.Sync,
            LogLevel = LogSeverity.Info,
            CaseSensitiveCommands = false,
        });

        _client.Log += Log;
        _commands.Log += Log;

        _services = ConfigServices();
    }

    private static ServiceProvider ConfigServices()
    {
        var map = new ServiceCollection();
        return map.BuildServiceProvider();
    }

    private static Task Log(LogMessage msg)
    {
        var text = $"[{msg.Severity,8}] {msg.Source}: {msg.Message} {msg.Exception}";
        Console.ForegroundColor = GetTextColor(msg.Severity);
        Console.WriteLine($"{DateTime.Now,-19} {text}");
        Console.ResetColor();

        LogUtil.LogText($"KookBot: {text}");

        return Task.CompletedTask;
    }

    private static ConsoleColor GetTextColor(LogSeverity sv) => sv switch
    {
        LogSeverity.Critical => ConsoleColor.Red,
        LogSeverity.Error => ConsoleColor.Red,

        LogSeverity.Warning => ConsoleColor.Yellow,
        LogSeverity.Info => ConsoleColor.White,

        LogSeverity.Verbose => ConsoleColor.DarkGray,
        LogSeverity.Debug => ConsoleColor.DarkGray,
        _ => Console.ForegroundColor,
    };

    public async Task MainAsync(string apiToken, CancellationToken token)
    {
        await InitCommands().ConfigureAwait(false);

        await _client.LoginAsync(TokenType.Bot, apiToken).ConfigureAwait(false);
        await _client.StartAsync().ConfigureAwait(false);
        LogUtil.LogInfo("Kook 机器人启动成功。", "KookBot");

        var guilds = await _client.Rest.GetGuildsAsync().ConfigureAwait(false);
        if (guilds.Count != 0)
        {
            var guild = guilds.First();
            var owner = await guild.GetOwnerAsync();
            Manager.Owner = owner.Id;
            LogUtil.LogInfo($"从服务器设置所有者 {owner.Id}: {guild.Name} (ID: {guild.Id})", "KookBot");
        }
        else
        {
            LogUtil.LogError("未找到任何服务器。请确保机器人已添加到至少一个服务器中。", "KookBot");
        }
        // 无限等待，确保机器人保持连接状态
        await MonitorLogIntervalAsync(token).ConfigureAwait(false);
    }

    public async Task InitCommands()
    {
        var assembly = Assembly.GetExecutingAssembly();

        await _commands.AddModulesAsync(assembly, _services).ConfigureAwait(false);
        var genericTypes = assembly.DefinedTypes.Where(z => z.IsSubclassOf(typeof(ModuleBase<SocketCommandContext>)) && z.IsGenericType);
        foreach (var t in genericTypes)
        {
            var genModule = t.MakeGenericType(typeof(T));
            try
            {
                await _commands.AddModuleAsync(genModule, _services).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogUtil.LogError($"添加模块失败 {genModule.Name}: {ex.Message}", "KookBot");
                // 可选：记录异常或根据需要处理
            }
        }
        var modules = _commands.Modules.ToList();

        var blacklist = Hub.Config.Kook.ModuleBlacklist
            .Replace("Module", "").Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(z => z.Trim()).ToList();

        foreach (var module in modules)
        {
            var name = module.Name;
            name = name.Replace("Module", "");
            var gen = name.IndexOf('`');
            if (gen != -1)
                name = name[..gen];
            if (blacklist.Any(z => z.Equals(name, StringComparison.OrdinalIgnoreCase)))
                await _commands.RemoveModuleAsync(module).ConfigureAwait(false);
        }

        // 订阅处理程序以检查消息是否调用命令
        _client.Ready += LoadLoggingAndEcho;
        _client.MessageReceived += HandleMessageAsync;
    }

    private async Task HandleMessageAsync(SocketMessage arg, SocketGuildUser user, SocketTextChannel channel)
    {
        // 如果是系统消息则退出
        if (arg is not SocketUserMessage msg)
            return;

        // 我们不希望机器人响应自己或其他机器人
        if (msg.Author.Id == _client.CurrentUser?.Id || (msg.Author.IsBot ?? false))
            return;

        // 创建一个数字来跟踪前缀结束和命令开始的位置
        int pos = 0;
        if (msg.HasStringPrefix(Hub.Config.Kook.CommandPrefix, ref pos))
        {
            bool handled = await TryHandleCommandAsync(msg, pos).ConfigureAwait(false);
            if (handled)
                return;
        }
        await TryHandleMessageAsync(msg).ConfigureAwait(false);
    }

    private async Task TryHandleMessageAsync(SocketUserMessage msg)
    {
        // 这应该是一个服务吗？
        if (msg.Attachments.Count > 0)
        {
            var mgr = Manager;
            var cfg = mgr.Config;

            // 新增：检查是否应该直接处理PKM文件交易
            if (await TryHandleDirectPkmTradeAsync(msg).ConfigureAwait(false))
            {
                return; // 如果已经处理了交易，就返回
            }

            if (cfg.ConvertPKMToShowdownSet && (cfg.ConvertPKMReplyAnyChannel || mgr.CanUseCommandChannel(msg.Channel.Id)))
            {
                foreach (var att in msg.Attachments)
                    await msg.Channel.RepostPKMAsShowdownAsync(att).ConfigureAwait(false);
            }
        }

        // 新增：检查是否@了机器人并且包含Showdown代码
        if (msg.MentionedUserIds.Where(id => id == _client.CurrentUser?.Id).Any())
        {
            // 先检查是否有附件，如果有附件则优先处理附件
            if (msg.Attachments.Count == 0)
            {
                // 如果没有附件，尝试处理Showdown代码
                if (await TryHandleMentionWithShowdownAsync(msg).ConfigureAwait(false))
                {
                    return; // 如果处理了Showdown代码交易，就返回
                }
            }

            // 如果没有处理交易，则发送帮助信息
            string commandPrefix = Manager.Config.CommandPrefix;
            await msg.Channel.SendTextAsync($"请使用 {commandPrefix}help 获取帮助信息").ConfigureAwait(false);
        }
    }

    // 新增方法：处理@机器人+Showdown代码
    private async Task<bool> TryHandleMentionWithShowdownAsync(SocketUserMessage msg)
    {
        var mgr = Manager;

        // 检查权限
        if (!mgr.CanUseCommandUser(msg.Author.Id) || !mgr.CanUseCommandChannel(msg.Channel.Id))
            return false;

        // 检查用户是否拥有交易角色权限
        if (msg.Author is SocketGuildUser guildUser)
        {
            var roles = guildUser.Roles.Select(r => r.Name);
            if (!mgr.GetHasRoleAccess(nameof(KookManager.RolesTrade), roles))
                return false;
        }
        else
        {
            // 如果不是服务器用户，则不允许交易
            return false;
        }

        // 获取消息内容，移除@机器人的部分
        var content = msg.Content;
        var mentionPattern = $"<@{_client.CurrentUser?.Id}>";
        content = content.Replace(mentionPattern, "").Trim();

        // 检查内容是否为空或过短
        if (string.IsNullOrWhiteSpace(content) || content.Length < 10)
            return false;

        try
        {
            // 尝试解析Showdown代码
            if (ShowdownTranslator<T>.GameStringsZh.Species.Skip(1).Any(s => content.Contains(s)))
            {
                // 如果内容包含中文Showdown Set，将其翻译为英文
                content = ShowdownTranslator<T>.Chinese2Showdown(content);
            }
            else
            {
                content = ReusableActions.StripCodeBlock(content);
            }

            var set = new ShowdownSet(content);
            var template = AutoLegalityWrapper.GetTemplate(set);
            if (set.InvalidLines.Count != 0 || set.Species is 0)
            {
                var sb = new StringBuilder(128);
                sb.AppendLine("无法解析Showdown Set。");
                var invalidlines = set.InvalidLines;
                if (invalidlines.Count != 0)
                {
                    var localization = BattleTemplateParseErrorLocalization.Get();
                    sb.AppendLine("检测到无效行：\n```");
                    foreach (var line in invalidlines)
                    {
                        var error = line.Humanize(localization);
                        sb.AppendLine(error);
                    }
                    sb.AppendLine("```");
                }
                if (set.Species is 0)
                    sb.AppendLine("无法识别宝可梦种类，请检查拼写。");

                var errorMsg = sb.ToString();
                await msg.Channel.SendTextAsync(errorMsg).ConfigureAwait(false);
                return false;
            }

            var sav = AutoLegalityWrapper.GetTrainerInfo<T>();
            var pkm = sav.GetLegal(template, out var result);
            var la = new LegalityAnalysis(pkm);
            var spec = GameInfo.Strings.Species[template.Species];
            pkm = EntityConverter.ConvertToType(pkm, typeof(T), out _) ?? pkm;
            if (pkm is not T pk || !la.Valid)
            {
                var reason = result switch
                {
                    "Timeout" => $"生成 {spec} 设置超时。",
                    "VersionMismatch" => "请求被拒绝：PKHeX和自动合法化模组版本不匹配。",
                    _ => $"无法从该设置创建 {spec}。",
                };
                var imsg = $"抱歉！{reason}";
                if (result == "Failed")
                    imsg += $"\n{AutoLegalityWrapper.GetLegalizationHint(template, sav, pkm)}";
                await msg.Channel.SendTextAsync(imsg).ConfigureAwait(false);
                return false;
            }
            pk.ResetPartyStats();

            // 生成随机交易代码
            var code = Info.GetRandomTradeCode();
            var sig = msg.Author.GetFavor();

            // 调用交易队列添加逻辑
            await AddTradeToQueueAsync(msg, code, msg.Author.Username, pk, sig, msg.Author).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            LogUtil.LogSafe(ex, nameof(KookBot<T>));
            await msg.Channel.SendTextAsync("处理Showdown代码时出现错误，请检查格式是否正确。").ConfigureAwait(false);
            return false;
        }
    }

    // 新增方法：直接处理PKM文件交易
    private async Task<bool> TryHandleDirectPkmTradeAsync(SocketUserMessage msg)
    {
        var mgr = Manager;

        // 检查权限
        if (!mgr.CanUseCommandUser(msg.Author.Id) || !mgr.CanUseCommandChannel(msg.Channel.Id))
            return false;

        // 检查用户是否拥有交易角色权限
        if (msg.Author is SocketGuildUser guildUser)
        {
            var roles = guildUser.Roles.Select(r => r.Name);
            if (!mgr.GetHasRoleAccess(nameof(KookManager.RolesTrade), roles))
                return false;
        }
        else
        {
            // 如果不是服务器用户，则不允许交易
            return false;
        }

        // 只处理第一个附件
        var attachment = msg.Attachments.FirstOrDefault();
        if (attachment == default)
            return false;

        try
        {
            // 下载并解析PKM文件
            var att = await NetUtil.DownloadPKMAsync(attachment).ConfigureAwait(false);
            var pk = GetRequest(att);
            if (pk == null)
            {
                await msg.Channel.SendTextAsync("提供的附件无法识别为有效的宝可梦文件！").ConfigureAwait(false);
                return false;
            }

            // 生成随机交易代码
            var code = Info.GetRandomTradeCode();
            var sig = msg.Author.GetFavor();

            // 直接调用交易队列添加逻辑
            await AddTradeToQueueAsync(msg, code, msg.Author.Username, pk, sig, msg.Author).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            LogUtil.LogSafe(ex, nameof(KookBot<T>));
            await msg.Channel.SendTextAsync("处理宝可梦文件时出现错误，请稍后重试。").ConfigureAwait(false);
            return false;
        }
    }

    // 从 TradeModule 复制的辅助方法
    private static T? GetRequest(Download<PKM> dl)
    {
        if (!dl.Success)
            return null;
        return dl.Data switch
        {
            null => null,
            T pk => pk,
            _ => EntityConverter.ConvertToType(dl.Data, typeof(T), out _) as T,
        };
    }

    // 从 TradeModule 复制的交易队列添加方法（适配修改）
    private async Task AddTradeToQueueAsync(SocketUserMessage msg, int code, string trainerName, T pk, RequestSignificance sig, SocketUser usr)
    {
        var la = new LegalityAnalysis(pk);
        var enc = la.EncounterOriginal;
        if (!pk.CanBeTraded(enc))
        {
            await msg.Channel.SendTextAsync("提供的宝可梦内容被禁止交易！").ConfigureAwait(false);
            return;
        }

        var cfg = Info.Hub.Config.Trade;
        if (!la.Valid)
        {
            await msg.Channel.SendTextAsync($"{typeof(T).Name} 附件不合法，无法交易！").ConfigureAwait(false);
            return;
        }
        if (cfg.DisallowNonNatives && (enc.Context != pk.Context || pk.GO))
        {
            await msg.Channel.SendTextAsync($"{typeof(T).Name} 附件不是原生版本，无法交易！").ConfigureAwait(false);
            return;
        }
        if (cfg.DisallowTracked && pk is IHomeTrack { HasTracker: true })
        {
            await msg.Channel.SendTextAsync($"{typeof(T).Name} 附件已被HOME追踪，无法交易！").ConfigureAwait(false);
            return;
        }

        // 创建命令上下文用于 QueueHelper
        var context = new SocketCommandContext(_client, msg);
        await QueueHelper<T>.AddToQueueAsync(context, code, trainerName, sig, pk, PokeRoutineType.LinkTrade, PokeTradeType.Specific, usr).ConfigureAwait(false);
    }

    private async Task<bool> TryHandleCommandAsync(SocketUserMessage msg, int pos)
    {
        // 创建命令上下文
        var context = new SocketCommandContext(_client, msg);

        // 检查权限
        var mgr = Manager;
        if (!mgr.CanUseCommandUser(msg.Author.Id))
        {
            await msg.Channel.SendTextAsync("您没有权限使用此命令。").ConfigureAwait(false);
            return true;
        }
        if (!mgr.CanUseCommandChannel(msg.Channel.Id) && msg.Author.Id != mgr.Owner)
        {
            if (Hub.Config.Kook.ReplyCannotUseCommandInChannel)
                await msg.Channel.SendTextAsync("您不能在此处使用该命令。").ConfigureAwait(false);
            return true;
        }

        // 执行命令（结果不表示返回值，而是表示命令是否成功执行的对象）
        var guild = msg.Channel is SocketGuildChannel g ? g.Guild.Name : "未知服务器";
        await Log(new LogMessage(LogSeverity.Info, "Command", $"执行来自 {guild}#{msg.Channel.Name}:@{msg.Author.Username} 的命令。内容: {msg}。ID: {msg.Author.IdentifyNumber}")).ConfigureAwait(false);
        var result = await _commands.ExecuteAsync(context, pos, _services).ConfigureAwait(false);

        if (result.Error == CommandError.UnknownCommand)
            return false;

        // 如果希望机器人在失败时发送消息，请取消注释以下行
        // 这不捕获带有 'RunMode.Async' 的命令错误，
        // 订阅 '_commands.CommandExecuted' 的处理程序以查看这些错误
        if (!result.IsSuccess)
            await msg.Channel.SendTextAsync(result.ErrorReason ?? "无错误原因").ConfigureAwait(false);
        return true;
    }

    private async Task MonitorLogIntervalAsync(CancellationToken token)
    {
        const int Interval = 20; // 秒
        // 检查日期时间以进行更新
        while (!token.IsCancellationRequested)
        {
            var time = DateTime.Now;
            var lastLogged = LogUtil.LastLogged;

            var delta = time - lastLogged;
            var gap = TimeSpan.FromSeconds(Interval) - delta;

            if (gap <= TimeSpan.Zero)
            {
                await Task.Delay(2_000, token).ConfigureAwait(false);
                continue;
            }
            await Task.Delay(gap, token).ConfigureAwait(false);
        }
    }
    private async Task LoadLoggingAndEcho()
    {
        if (MessageChannelsLoaded)
            return;

        // 恢复回显
        EchoModule.RestoreChannels(_client, Hub.Config.Kook);

        // 恢复日志记录
        LogModule.RestoreLogging(_client, Hub.Config.Kook);
        TradeStartModule<T>.RestoreTradeStarting(_client);

        // 在 Kook 出现问题时不要让它加载多次
        await Log(new LogMessage(LogSeverity.Info, "LoadLoggingAndEcho()", "日志和回显频道已加载！")).ConfigureAwait(false);
        MessageChannelsLoaded = true;
    }
}
