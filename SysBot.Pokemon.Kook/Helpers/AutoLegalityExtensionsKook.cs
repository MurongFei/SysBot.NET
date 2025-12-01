using Kook;
using Kook.WebSocket;
using PKHeX.Core;
using SysBot.Base;
using System;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Kook;

public static class AutoLegalityExtensionsKook
{
    public static async Task ReplyWithLegalizedSetAsync(this ISocketMessageChannel channel, ITrainerInfo sav, ShowdownSet set)
    {
        if (set.Species == 0)
        {
            await channel.SendTextAsync("抱歉！我无法解析您的消息！如果您想要转换内容，请仔细检查您粘贴的内容！").ConfigureAwait(false);
            return;
        }

        try
        {
            var template = AutoLegalityWrapper.GetTemplate(set);
            var pkm = sav.GetLegal(template, out var result);
            var la = new LegalityAnalysis(pkm);
            var spec = GameInfo.Strings.Species[template.Species];
            if (!la.Valid)
            {
                var reason = result switch
                {
                    "Timeout" => $"生成 {spec} 配置集超时。",
                    "VersionMismatch" => "请求被拒绝: PKHeX 和 Auto-Legality Mod 版本不匹配。",
                    _ => $"我无法从该配置集创建 {spec}。",
                };
                var imsg = $"错误！{reason}";
                if (result == "Failed")
                    imsg += $"\n{AutoLegalityWrapper.GetLegalizationHint(template, sav, pkm)}";
                await channel.SendTextAsync(imsg).ConfigureAwait(false);
                return;
            }

            var msg = $"这是您 ({result}) 合法化的 {spec} ({la.EncounterOriginal.Name}) PKM 文件！";
            await channel.SendPKMAsync(pkm, msg + $"\n{ReusableActions.GetFormattedShowdownText(pkm)}").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogUtil.LogSafe(ex, nameof(AutoLegalityExtensionsKook));
            var msg = $"错误！处理此 Showdown 配置集时出现意外问题：\n```{string.Join("\n", set.GetSetLines())}```";
            await channel.SendTextAsync(msg).ConfigureAwait(false);
        }
    }

    public static Task ReplyWithLegalizedSetAsync(this ISocketMessageChannel channel, string content, byte gen)
    {
        content = ReusableActions.StripCodeBlock(content);
        var set = new ShowdownSet(content);
        var sav = AutoLegalityWrapper.GetTrainerInfo(gen);
        return channel.ReplyWithLegalizedSetAsync(sav, set);
    }

    public static Task ReplyWithLegalizedSetAsync<T>(this ISocketMessageChannel channel, string content) where T : PKM, new()
    {
        content = ReusableActions.StripCodeBlock(content);
        var set = new ShowdownSet(content);
        var sav = AutoLegalityWrapper.GetTrainerInfo<T>();
        return channel.ReplyWithLegalizedSetAsync(sav, set);
    }

    public static async Task ReplyWithLegalizedSetAsync(this ISocketMessageChannel channel, IAttachment att)
    {
        var download = await NetUtil.DownloadPKMAsync(att).ConfigureAwait(false);
        if (!download.Success)
        {
            await channel.SendTextAsync(download.ErrorMessage).ConfigureAwait(false);
            return;
        }

        var pkm = download.Data!;
        if (new LegalityAnalysis(pkm).Valid)
        {
            await channel.SendTextAsync($"{download.SanitizedFileName}: 已经是合法的。").ConfigureAwait(false);
            return;
        }

        var legal = pkm.LegalizePokemon();
        if (!new LegalityAnalysis(legal).Valid)
        {
            await channel.SendTextAsync($"{download.SanitizedFileName}: 无法合法化。").ConfigureAwait(false);
            return;
        }

        legal.RefreshChecksum();

        var msg = $"这是您合法化的 {download.SanitizedFileName} PKM 文件！\n{ReusableActions.GetFormattedShowdownText(legal)}";
        await channel.SendPKMAsync(legal, msg).ConfigureAwait(false);
    }
}
