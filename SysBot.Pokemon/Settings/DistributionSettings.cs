using PKHeX.Core;
using SysBot.Base;
using System.ComponentModel;

namespace SysBot.Pokemon;

public class DistributionSettings : ISynchronizationSetting
{
    private const string Distribute = "分发设置";
    private const string Synchronize = "同步设置";
    public override string ToString() => "分发交易设置";

    // 分发设置

    [Category(Distribute), Description("启用后，空闲的链接交易机器人将随机分发 DistributeFolder 中的 PKM 文件。")]
    public bool DistributeWhileIdle { get; set; } = true;

    [Category(Distribute), Description("启用后，DistributionFolder 将随机产生文件而不是按相同顺序。")]
    public bool Shuffled { get; set; }

    [Category(Distribute), Description("设置为 None 以外的值时，随机交易将需要此物种以及昵称匹配。")]
    public Species LedySpecies { get; set; } = Species.None;

    [Category(Distribute), Description("设置为 true 时，随机 Ledy 昵称交换交易将退出而不是从池中交易随机实体。")]
    public bool LedyQuitIfNoMatch { get; set; }

    [Category(Distribute), Description("分发交易链接代码。")]
    public int TradeCode { get; set; } = 7196;

    [Category(Distribute), Description("分发交易链接代码使用最小和最大范围而不是固定交易代码。")]
    public bool RandomCode { get; set; }

    [Category(Distribute), Description("对于 BDSP，分发机器人将进入特定房间并保持在那里直到机器人停止。")]
    public bool RemainInUnionRoomBDSP { get; set; } = true;

    // 同步设置

    [Category(Synchronize), Description("链接交易：使用多个分发机器人——所有机器人将同时确认其交易代码。当为 Local 时，所有机器人都到达屏障后将继续。当为 Remote 时，必须由其他东西发出信号让机器人继续。")]
    public BotSyncOption SynchronizeBots { get; set; } = BotSyncOption.LocalSync;

    [Category(Synchronize), Description("链接交易：使用多个分发机器人——一旦所有机器人都准备好确认交易代码，中心将在释放所有机器人之前等待 X 毫秒。")]
    public int SynchronizeDelayBarrier { get; set; }

    [Category(Synchronize), Description("链接交易：使用多个分发机器人——机器人在继续之前等待同步的最长时间（秒）。")]
    public double SynchronizeTimeout { get; set; } = 90;
}
