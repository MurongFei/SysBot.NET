using System;
using System.ComponentModel;
// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global

namespace SysBot.Pokemon;

public class QueueSettings
{
    private const string FeatureToggle = "功能开关";
    private const string UserBias = "用户偏向";
    private const string TimeBias = "时间偏向";
    private const string QueueToggle = "队列开关";
    public override string ToString() => "队列加入设置";

    // 常规设置

    [Category(FeatureToggle), Description("切换用户是否可以加入队列。")]
    public bool CanQueue { get; set; } = true;

    [Category(FeatureToggle), Description("如果队列中已有这么多用户，则阻止添加新用户。")]
    public int MaxQueueCount { get; set; } = 999;

    [Category(FeatureToggle), Description("允许用户在正在交易时退出队列。")]
    public bool CanDequeueIfProcessing { get; set; }

    [Category(FeatureToggle), Description("确定 Flex 模式将如何处理队列。")]
    public FlexYieldMode FlexMode { get; set; } = FlexYieldMode.Weighted;

    [Category(FeatureToggle), Description("确定队列何时打开和关闭。")]
    public QueueOpening QueueToggleMode { get; set; } = QueueOpening.Threshold;

    // 队列开关

    [Category(QueueToggle), Description("阈值模式：导致队列打开的用户数量。")]
    public int ThresholdUnlock { get; set; }

    [Category(QueueToggle), Description("阈值模式：导致队列关闭的用户数量。")]
    public int ThresholdLock { get; set; } = 30;

    [Category(QueueToggle), Description("计划模式：队列锁定前保持打开的秒数。")]
    public int IntervalOpenFor { get; set; } = 5 * 60;

    [Category(QueueToggle), Description("计划模式：队列解锁前保持关闭的秒数。")]
    public int IntervalCloseFor { get; set; } = 15 * 60;

    // Flex 用户

    [Category(UserBias), Description("根据队列中的用户数量偏置交易队列的权重。")]
    public int YieldMultCountTrade { get; set; } = 100;

    [Category(UserBias), Description("根据队列中的用户数量偏置种子检查队列的权重。")]
    public int YieldMultCountSeedCheck { get; set; } = 100;

    [Category(UserBias), Description("根据队列中的用户数量偏置克隆队列的权重。")]
    public int YieldMultCountClone { get; set; } = 100;

    [Category(UserBias), Description("根据队列中的用户数量偏置导出队列的权重。")]
    public int YieldMultCountDump { get; set; } = 100;

    // Flex 时间

    [Category(TimeBias), Description("确定权重应该是加到总权重还是乘以总权重。")]
    public FlexBiasMode YieldMultWait { get; set; } = FlexBiasMode.Multiply;

    [Category(TimeBias), Description("检查用户加入交易队列后经过的时间，并相应增加队列的权重。")]
    public int YieldMultWaitTrade { get; set; } = 1;

    [Category(TimeBias), Description("检查用户加入种子检查队列后经过的时间，并相应增加队列的权重。")]
    public int YieldMultWaitSeedCheck { get; set; } = 1;

    [Category(TimeBias), Description("检查用户加入克隆队列后经过的时间，并相应增加队列的权重。")]
    public int YieldMultWaitClone { get; set; } = 1;

    [Category(TimeBias), Description("检查用户加入导出队列后经过的时间，并相应增加队列的权重。")]
    public int YieldMultWaitDump { get; set; } = 1;

    [Category(TimeBias), Description("乘以队列中的用户数量，以估算用户被处理前需要等待的时间。")]
    public float EstimatedDelayFactor { get; set; } = 1.1f;

    private int GetCountBias(PokeTradeType type) => type switch
    {
        PokeTradeType.Seed => YieldMultCountSeedCheck,
        PokeTradeType.Clone => YieldMultCountClone,
        PokeTradeType.Dump => YieldMultCountDump,
        _ => YieldMultCountTrade,
    };

    private int GetTimeBias(PokeTradeType type) => type switch
    {
        PokeTradeType.Seed => YieldMultWaitSeedCheck,
        PokeTradeType.Clone => YieldMultWaitClone,
        PokeTradeType.Dump => YieldMultWaitDump,
        _ => YieldMultWaitTrade,
    };

    /// <summary>
    /// 根据队列中的用户数量和用户等待的时间获取 <see cref="PokeTradeType"/> 的权重。
    /// </summary>
    /// <param name="count"><see cref="type"/> 的用户数量</param>
    /// <param name="time">下一个要处理的用户加入队列的时间</param>
    /// <param name="type">队列类型</param>
    /// <returns>交易类型的有效权重。</returns>
    public long GetWeight(int count, DateTime time, PokeTradeType type)
    {
        var now = DateTime.Now;
        var seconds = (now - time).Seconds;

        var cb = GetCountBias(type) * count;
        var tb = GetTimeBias(type) * seconds;

        return YieldMultWait switch
        {
            FlexBiasMode.Multiply => cb * tb,
            _ => cb + tb,
        };
    }

    /// <summary>
    /// 估算用户被处理前需要的时间（分钟）。
    /// </summary>
    /// <param name="position">在队列中的位置</param>
    /// <param name="botct">处理请求的机器人数量</param>
    /// <returns>估算的时间（分钟）</returns>
    public float EstimateDelay(int position, int botct) => (EstimatedDelayFactor * position) / botct;
}

public enum FlexBiasMode
{
    Add,
    Multiply,
}

public enum FlexYieldMode
{
    LessCheatyFirst,
    Weighted,
}

public enum QueueOpening
{
    Manual,
    Threshold,
    Interval,
}
