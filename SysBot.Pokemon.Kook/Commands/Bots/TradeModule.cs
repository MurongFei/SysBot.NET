using Kook;
using Kook.Commands;
using Kook.WebSocket;
using PKHeX.Core;
using SysBot.Base;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Kook;

[Summary("管理新的连接代码交易队列")]
public class TradeModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
{
    private static TradeQueueInfo<T> Info => KookBot<T>.Runner.Hub.Queues.Info;

    [Command("tradeList")]
    [Alias("tl")]
    [Summary("显示交易队列中的用户列表")]
    [RequireSudo]
    public async Task GetTradeListAsync()
    {
        string msg = Info.GetTradeList(PokeRoutineType.LinkTrade);

        var card = new CardBuilder()
            .AddModule(new SectionModuleBuilder().WithText("以下是当前正在等待的用户："))
            .AddModule(new SectionModuleBuilder().WithText("待处理交易"))
            .AddModule(new SectionModuleBuilder().WithText(msg))
            .Build();
        await ReplyCardAsync(card).ConfigureAwait(false);
    }

    [Command("trade")]
    [Alias("t")]
    [Summary("让机器人交易您附加的文件")]
    [RequireQueueRole(nameof(KookManager.RolesTrade))]
    public Task TradeAsyncAttach()
    {
        var code = Info.GetRandomTradeCode();
        return TradeAsyncAttach(code, Context.User.GetFavor(), Context.User);
    }

    [Command("trade")]
    [Alias("t")]
    [Summary("让机器人交易您附加的文件")]
    [RequireQueueRole(nameof(KookManager.RolesTrade))]
    public Task TradeAsyncAttach([Summary("交易代码")] int code)
    {
        var sig = Context.User.GetFavor();
        return TradeAsyncAttach(code, sig, Context.User);
    }

    [Command("trade")]
    [Alias("t")]
    [Summary("让机器人交易从提供的Showdown Set转换的宝可梦")]
    [RequireQueueRole(nameof(KookManager.RolesTrade))]
    public async Task TradeAsync([Summary("交易代码")] int code, [Summary("Showdown Set")][Remainder] string content)
    {
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

            var msg = sb.ToString();
            await ReplyTextAsync(msg).ConfigureAwait(false);
            return;
        }

        try
        {
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
                await ReplyTextAsync(imsg).ConfigureAwait(false);
                return;
            }
            pk.ResetPartyStats();

            var sig = Context.User.GetFavor();
            await AddTradeToQueueAsync(code, Context.User.Username, pk, sig, Context.User).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogUtil.LogSafe(ex, nameof(TradeModule<T>));
            var msg = $"抱歉！处理此Showdown Set时出现意外问题：\n```{string.Join("\n", set.GetSetLines())}```";
            await ReplyTextAsync(msg).ConfigureAwait(false);
        }
    }

    [Command("trade")]
    [Alias("t")]
    [Summary("让机器人交易从提供的Showdown Set转换的宝可梦")]
    [RequireQueueRole(nameof(KookManager.RolesTrade))]
    public Task TradeAsync([Summary("Showdown Set")][Remainder] string content)
    {
        var code = Info.GetRandomTradeCode();
        return TradeAsync(code, content);
    }

    [Command("trade")]
    [Alias("t")]
    [Summary("让机器人交易您附加的文件")]
    [RequireQueueRole(nameof(KookManager.RolesTrade))]
    public Task TradeAsyncAttach(Uri file)
    {
        var code = Info.GetRandomTradeCode();
        return TradeAsyncAttach(code);
    }

    [Command("banTrade")]
    [Alias("bt")]
    [RequireSudo]
    public async Task BanTradeAsync([Summary("在线ID")] ulong nnid, string comment)
    {
        KookBotSettings.HubConfig.TradeAbuse.BannedIDs.AddIfNew([GetReference(nnid, comment)]);
        await ReplyTextAsync("已完成。").ConfigureAwait(false);
    }

    private RemoteControlAccess GetReference(ulong id, string comment) => new()
    {
        ID = id,
        Name = id.ToString(),
        Comment = $"由 {Context.User.Username} 于 {DateTime.Now:yyyy.MM.dd-hh:mm:ss} 添加（{comment}）",
    };

    [Command("tradeUser")]
    [Alias("tu", "tradeOther")]
    [Summary("让机器人交易被提及用户附加的文件")]
    [RequireSudo]
    public async Task TradeAsyncAttachUser([Summary("交易代码")] int code, [Remainder] string _)
    {
        if (Context.Message.MentionedUsers.Count > 1)
        {
            await ReplyTextAsync("提及的用户过多。请一次只队列一个用户。").ConfigureAwait(false);
            return;
        }

        if (Context.Message.MentionedUsers.Count == 0)
        {
            await ReplyTextAsync("必须提及一个用户才能执行此操作。").ConfigureAwait(false);
            return;
        }

        var usr = Context.Message.MentionedUsers.ElementAt(0);
        var sig = usr.GetFavor();
        await TradeAsyncAttach(code, sig, usr).ConfigureAwait(false);
    }

    [Command("tradeUser")]
    [Alias("tu", "tradeOther")]
    [Summary("让机器人交易被提及用户附加的文件")]
    [RequireSudo]
    public Task TradeAsyncAttachUser([Remainder] string _)
    {
        var code = Info.GetRandomTradeCode();
        return TradeAsyncAttachUser(code, _);
    }

    private async Task TradeAsyncAttach(int code, RequestSignificance sig, SocketUser usr)
    {
        var attachment = Context.Message.Attachments.FirstOrDefault();
        if (attachment == default)
        {
            await ReplyTextAsync("未提供附件！").ConfigureAwait(false);
            return;
        }

        var att = await NetUtil.DownloadPKMAsync(attachment).ConfigureAwait(false);
        var pk = GetRequest(att);
        if (pk == null)
        {
            await ReplyTextAsync("提供的附件与此模块不兼容！").ConfigureAwait(false);
            return;
        }

        await AddTradeToQueueAsync(code, usr.Username, pk, sig, usr).ConfigureAwait(false);
    }

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

    private async Task AddTradeToQueueAsync(int code, string trainerName, T pk, RequestSignificance sig, SocketUser usr)
    {
        if (!pk.CanBeTraded())
        {
            // 禁止交易游戏中无法交易的内容（例如融合宝可梦）
            await ReplyTextAsync("提供的宝可梦内容被禁止交易！").ConfigureAwait(false);
            return;
        }

        var cfg = Info.Hub.Config.Trade;
        var la = new LegalityAnalysis(pk);
        if (!la.Valid)
        {
            // 禁止交易非法的宝可梦
            await ReplyTextAsync($"{typeof(T).Name} 附件不合法，无法交易！").ConfigureAwait(false);
            return;
        }
        if (cfg.DisallowNonNatives && (la.EncounterOriginal.Context != pk.Context || pk.GO))
        {
            // 允许所有者阻止交易需要HOME追踪器的实体，即使文件已有追踪器
            await ReplyTextAsync($"{typeof(T).Name} 附件不是原生版本，无法交易！").ConfigureAwait(false);
            return;
        }
        if (cfg.DisallowTracked && pk is IHomeTrack { HasTracker: true })
        {
            // 允许所有者阻止交易已有HOME追踪器的实体
            await ReplyTextAsync($"{typeof(T).Name} 附件已被HOME追踪，无法交易！").ConfigureAwait(false);
            return;
        }

        await QueueHelper<T>.AddToQueueAsync(Context, code, trainerName, sig, pk, PokeRoutineType.LinkTrade, PokeTradeType.Specific, usr).ConfigureAwait(false);
    }
}
