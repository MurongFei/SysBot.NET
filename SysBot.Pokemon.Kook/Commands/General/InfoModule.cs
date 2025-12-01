using Kook;
using Kook.Commands;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Kook;

// src: https://github.com/foxbot/patek/blob/master/src/Patek/Modules/InfoModule.cs
// ISC License (ISC)
// Copyright 2017, Christopher F. <foxbot@protonmail.com>
public class InfoModule : ModuleBase<SocketCommandContext>
{
    private const string detail = "我是一个基于 PKHeX.Core 和其他开源软件驱动的开源 Kook 机器人。";
    private const string repo = "https://github.com/kwsch/SysBot.NET";

    [Command("info")]
    [Alias("about", "whoami", "owner")]
    public async Task InfoAsync()
    {
        //var app = await Context.Client.GetApplicationInfoAsync().ConfigureAwait(false);

        var builder = new CardBuilder()
            .AddModule(new SectionModuleBuilder().WithText("以下是一些关于我的信息！"))
            .AddModule(new SectionModuleBuilder().WithText(detail));

        builder.AddModule(new SectionModuleBuilder().WithText(new KMarkdownElementBuilder("**基本信息**")))
            .AddModule(new SectionModuleBuilder().WithText(
            $"- [源代码]({repo})\n" +
            //$"- {Format.Bold("所有者")}: {app.Owner} ({app.Owner.Id})\n" +
            $"- {Format.Bold("库版本")}: Kook.Net ({KookConfig.Version})\n" +
            $"- {Format.Bold("运行时间")}: {GetUptime()}\n" +
            $"- {Format.Bold("运行时")}: {RuntimeInformation.FrameworkDescription} {RuntimeInformation.ProcessArchitecture} " +
            $"({RuntimeInformation.OSDescription} {RuntimeInformation.OSArchitecture})\n" +
            $"- {Format.Bold("构建时间")}: {GetVersionInfo("SysBot.Base", false)}\n" +
            $"- {Format.Bold("核心版本")}: {GetVersionInfo("PKHeX.Core")}\n" +
            $"- {Format.Bold("自动合法化版本")}: {GetVersionInfo("PKHeX.Core.AutoMod")}\n"
        ));

        builder.AddModule(new SectionModuleBuilder().WithText(new KMarkdownElementBuilder("**统计信息**")))
            .AddModule(new SectionModuleBuilder().WithText(
            $"- {Format.Bold("堆大小")}: {GetHeapSize()}MiB\n" +
            $"- {Format.Bold("服务器数量")}: {Context.Client.Guilds.Count}\n" +
            $"- {Format.Bold("频道数量")}: {Context.Client.Guilds.Sum(g => g.Channels.Count)}\n" +
            $"- {Format.Bold("用户数量")}: {Context.Client.Guilds.Sum(g => g.MemberCount)}\n"
        ));

        await ReplyCardAsync(builder.Build()).ConfigureAwait(false);
    }

    private static string GetUptime() => (DateTime.Now - Process.GetCurrentProcess().StartTime).ToString(@"dd\.hh\:mm\:ss");
    private static string GetHeapSize() => Math.Round(GC.GetTotalMemory(true) / (1024.0 * 1024.0), 2).ToString(CultureInfo.CurrentCulture);

    private static string GetVersionInfo(string assemblyName, bool inclVersion = true)
    {
        const string _default = "未知";
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        var assembly = Array.Find(assemblies, x => x.GetName().Name == assemblyName);

        var attribute = assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (attribute is null)
            return _default;

        var info = attribute.InformationalVersion;
        var split = info.Split('+');
        if (split.Length < 2)
            return _default;

        var version = split[0];
        var revision = split[1];
        if (DateTime.TryParseExact(revision, "yyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var buildTime))
            return (inclVersion ? $"{version} " : "") + $@"{buildTime:yy-MM-dd\.hh\:mm}";
        return !inclVersion ? _default : version;
    }
}
