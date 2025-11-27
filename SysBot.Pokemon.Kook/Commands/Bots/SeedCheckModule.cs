using Kook;
using Kook.Commands;
using PKHeX.Core;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Kook;

[Summary("排队新的种子检查交易")]
public class SeedCheckModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
{
    private static TradeQueueInfo<T> Info => KookBot<T>.Runner.Hub.Queues.Info;

    [Command("seedCheck")]
    [Alias("checkMySeed", "checkSeed", "seed", "s", "sc")]
    [Summary("检查宝可梦的种子。")]
    [RequireQueueRole(nameof(KookManager.RolesSeed))]
    public Task SeedCheckAsync(int code)
    {
        var sig = Context.User.GetFavor();
        return QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, new T(), PokeRoutineType.SeedCheck, PokeTradeType.Seed);
    }

    [Command("seedCheck")]
    [Alias("checkMySeed", "checkSeed", "seed", "s", "sc")]
    [Summary("检查宝可梦的种子。")]
    [RequireQueueRole(nameof(KookManager.RolesSeed))]
    public Task SeedCheckAsync([Summary("交易码")][Remainder] string code)
    {
        int tradeCode = Util.ToInt32(code);
        var sig = Context.User.GetFavor();
        return QueueHelper<T>.AddToQueueAsync(Context, tradeCode == 0 ? Info.GetRandomTradeCode() : tradeCode, Context.User.Username, sig, new T(), PokeRoutineType.SeedCheck, PokeTradeType.Seed);
    }

    [Command("seedCheck")]
    [Alias("checkMySeed", "checkSeed", "seed", "s", "sc")]
    [Summary("检查宝可梦的种子。")]
    [RequireQueueRole(nameof(KookManager.RolesSeed))]
    public Task SeedCheckAsync()
    {
        var code = Info.GetRandomTradeCode();
        return SeedCheckAsync(code);
    }

    [Command("seedList")]
    [Alias("sl", "scq", "seedCheckQueue", "seedQueue", "seedList")]
    [Summary("显示种子检查队列中的用户列表。")]
    [RequireSudo]
    public async Task GetSeedListAsync()
    {
        string msg = Info.GetTradeList(PokeRoutineType.SeedCheck);

        var card = new CardBuilder()
            .AddModule(new SectionModuleBuilder().WithText("当前等待种子检查的用户列表:"))
            .AddModule(new SectionModuleBuilder().WithText("待处理交易"))
            .AddModule(new SectionModuleBuilder().WithText(msg))
            .Build();
        await ReplyCardAsync(card).ConfigureAwait(false);
    }

    [Command("findFrame")]
    [Alias("ff", "getFrameData")]
    [Summary("从提供的种子中打印下一个闪光帧数据。")]
    public async Task FindFrameAsync([Remainder] string seedString)
    {
        var me = KookBot<T>.Runner;
        var hub = me.Hub;

        seedString = seedString.ToLower();
        if (seedString.StartsWith("0x"))
            seedString = seedString[2..];

        var seed = Util.GetHexValue64(seedString);

        var r = new SeedSearchResult(Z3SearchResult.Success, seed, -1, hub.Config.SeedCheckSWSH.ResultDisplayMode);
        var msg = r.ToString();

        var card = new CardBuilder()
            .AddModule(new SectionModuleBuilder().WithText($"这是种子 `{seed:X16}` 的详细信息:"))
            .AddModule(new SectionModuleBuilder().WithText($"种子: {seed:X16}"))
            .AddModule(new SectionModuleBuilder().WithText(msg))
            .Build();
        await ReplyCardAsync(card).ConfigureAwait(false);
    }
}
