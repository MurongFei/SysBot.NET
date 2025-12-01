using SysBot.Base;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;

namespace SysBot.Pokemon;

public class EncounterSettings : IBotStateSettings, ICountSettings
{
    private const string Counts = "计数统计";
    private const string Encounter = "遭遇设置";
    private const string Settings = "其他设置";
    public override string ToString() => "遭遇机器人 SWSH 设置";

    [Category(Encounter), Description("Line 和 Reset 机器人用于遭遇宝可梦的方法。")]
    public EncounterMode EncounteringType { get; set; } = EncounterMode.VerticalLine;

    [Category(Settings)]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public FossilSettings Fossil { get; set; } = new();

    [Category(Encounter), Description("启用后，机器人在找到合适匹配后会继续运行。")]
    public ContinueAfterMatch ContinueAfterMatch { get; set; } = ContinueAfterMatch.StopExit;

    [Category(Encounter), Description("启用后，在正常机器人循环操作期间将关闭屏幕以节省电量。")]
    public bool ScreenOff { get; set; }

    private int _completedWild;
    private int _completedLegend;
    private int _completedEggs;
    private int _completedFossils;

    [Category(Counts), Description("遭遇的野生宝可梦")]
    public int CompletedEncounters
    {
        get => _completedWild;
        set => _completedWild = value;
    }

    [Category(Counts), Description("遭遇的传说宝可梦")]
    public int CompletedLegends
    {
        get => _completedLegend;
        set => _completedLegend = value;
    }

    [Category(Counts), Description("获取的蛋")]
    public int CompletedEggs
    {
        get => _completedEggs;
        set => _completedEggs = value;
    }

    [Category(Counts), Description("复活的化石宝可梦")]
    public int CompletedFossils
    {
        get => _completedFossils;
        set => _completedFossils = value;
    }

    [Category(Counts), Description("启用后，在请求状态检查时将发出计数。")]
    public bool EmitCountsOnStatusCheck { get; set; }

    public int AddCompletedEncounters() => Interlocked.Increment(ref _completedWild);
    public int AddCompletedLegends() => Interlocked.Increment(ref _completedLegend);
    public int AddCompletedEggs() => Interlocked.Increment(ref _completedEggs);
    public int AddCompletedFossils() => Interlocked.Increment(ref _completedFossils);

    public IEnumerable<string> GetNonZeroCounts()
    {
        if (!EmitCountsOnStatusCheck)
            yield break;
        if (CompletedEncounters != 0)
            yield return $"野生遭遇: {CompletedEncounters}";
        if (CompletedLegends != 0)
            yield return $"传说遭遇: {CompletedLegends}";
        if (CompletedEggs != 0)
            yield return $"获取的蛋: {CompletedEggs}";
        if (CompletedFossils != 0)
            yield return $"完成的化石: {CompletedFossils}";
    }
}
