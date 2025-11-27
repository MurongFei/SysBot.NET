using Kook;
using Kook.Commands;
using PKHeX.Core;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Kook;

[Summary("排队新的导出交易")]
public class DumpModule<T> : ModuleBase<SocketCommandContext> where T : PKM, new()
{
    private static TradeQueueInfo<T> Info => KookBot<T>.Runner.Hub.Queues.Info;

    [Command("dump")]
    [Alias("d")]
    [Summary("通过连接交易导出您展示的宝可梦数据。")]
    [RequireQueueRole(nameof(KookManager.RolesDump))]
    public Task DumpAsync(int code)
    {
        var sig = Context.User.GetFavor();
        return QueueHelper<T>.AddToQueueAsync(Context, code, Context.User.Username, sig, new T(), PokeRoutineType.Dump, PokeTradeType.Dump);
    }

    [Command("dump")]
    [Alias("d")]
    [Summary("通过连接交易导出您展示的宝可梦数据。")]
    [RequireQueueRole(nameof(KookManager.RolesDump))]
    public Task DumpAsync([Summary("交易码")][Remainder] string code)
    {
        int tradeCode = Util.ToInt32(code);
        var sig = Context.User.GetFavor();
        return QueueHelper<T>.AddToQueueAsync(Context, tradeCode == 0 ? Info.GetRandomTradeCode() : tradeCode, Context.User.Username, sig, new T(), PokeRoutineType.Dump, PokeTradeType.Dump);
    }

    [Command("dump")]
    [Alias("d")]
    [Summary("通过连接交易导出您展示的宝可梦数据。")]
    [RequireQueueRole(nameof(KookManager.RolesDump))]
    public Task DumpAsync()
    {
        var code = Info.GetRandomTradeCode();
        return DumpAsync(code);
    }

    [Command("dumpList")]
    [Alias("dl", "dq")]
    [Summary("显示导出队列中的用户列表。")]
    [RequireSudo]
    public async Task GetListAsync()
    {
        string msg = Info.GetTradeList(PokeRoutineType.Dump);

        var card = new CardBuilder()
            .AddModule(new SectionModuleBuilder().WithText("当前等待导出的用户列表:"))
            .AddModule(new SectionModuleBuilder().WithText("待处理交易"))
            .AddModule(new SectionModuleBuilder().WithText(msg))
            .Build();
        await ReplyCardAsync(card).ConfigureAwait(false);
    }
}
