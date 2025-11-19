using PKHeX.Core;
using SysBot.Base;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;

namespace SysBot.Pokemon;

public class RaidSettings : IBotStateSettings, ICountSettings
{
    private const string Hosting = "托管设置";
    private const string Counts = "计数统计";
    private const string FeatureToggle = "功能开关";
    public override string ToString() => "团战机器人设置";

    [Category(Hosting), Description("尝试开始团战前等待的秒数。范围从0到180秒。")]
    public int TimeToWait { get; set; } = 90;

    [Category(Hosting), Description("托管团战的最小链接代码。设置为-1表示不使用代码托管。")]
    public int MinRaidCode { get; set; } = 8180;

    [Category(Hosting), Description("托管团战的最大链接代码。设置为-1表示不使用代码托管。")]
    public int MaxRaidCode { get; set; } = 8199;

    [Category(FeatureToggle), Description("机器人托管的团战的可选描述。如果留空则使用自动宝可梦检测。")]
    public string RaidDescription { get; set; } = string.Empty;

    [Category(FeatureToggle), Description("当每个队伍成员锁定宝可梦时回显。")]
    public bool EchoPartyReady { get; set; }

    [Category(FeatureToggle), Description("如果设置了，允许机器人回显您的好友代码。")]
    public string FriendCode { get; set; } = string.Empty;

    [Category(Hosting), Description("每次接受的好友请求数量。")]
    public int NumberFriendsToAdd { get; set; }

    [Category(Hosting), Description("每次删除的好友数量。")]
    public int NumberFriendsToDelete { get; set; }

    [Category(Hosting), Description("在尝试添加/删除好友之前托管的团战数量。设置值为1将告诉机器人托管一个团战，然后开始添加/删除好友。")]
    public int InitialRaidsToHost { get; set; }

    [Category(Hosting), Description("在尝试添加好友之间托管的团战数量。")]
    public int RaidsBetweenAddFriends { get; set; }

    [Category(Hosting), Description("在尝试删除好友之间托管的团战数量。")]
    public int RaidsBetweenDeleteFriends { get; set; }

    [Category(Hosting), Description("开始尝试添加好友的行号。")]
    public int RowStartAddingFriends { get; set; } = 1;

    [Category(Hosting), Description("开始尝试删除好友的行号。")]
    public int RowStartDeletingFriends { get; set; } = 1;

    [Category(Hosting), Description("您用于管理好友的Nintendo Switch配置文件。例如，如果您使用第二个配置文件，请将其设置为2。")]
    public int ProfileNumber { get; set; } = 1;

    [Category(FeatureToggle), Description("启用后，在正常机器人循环操作期间将关闭屏幕以节省电量。")]
    public bool ScreenOff { get; set; }

    /// <summary>
    /// 根据范围设置获取随机团战代码。
    /// </summary>
    public int GetRandomRaidCode() => Util.Rand.Next(MinRaidCode, MaxRaidCode + 1);

    private int _completedRaids;

    [Category(Counts), Description("开始的团战")]
    public int CompletedRaids
    {
        get => _completedRaids;
        set => _completedRaids = value;
    }

    [Category(Counts), Description("启用后，在请求状态检查时将发出计数。")]
    public bool EmitCountsOnStatusCheck { get; set; }

    public int AddCompletedRaids() => Interlocked.Increment(ref _completedRaids);

    public IEnumerable<string> GetNonZeroCounts()
    {
        if (!EmitCountsOnStatusCheck)
            yield break;
        if (CompletedRaids != 0)
            yield return $"开始的团战: {CompletedRaids}";
    }
}
