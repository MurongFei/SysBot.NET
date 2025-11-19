using System.ComponentModel;

namespace SysBot.Pokemon;

public class TimingSettings
{
    private const string OpenGame = "打开游戏";
    private const string CloseGame = "关闭游戏";
    private const string Raid = "团战设置";
    private const string Misc = "杂项设置";
    public override string ToString() => "额外时间设置";

    // 打开游戏
    [Category(OpenGame), Description("启动游戏时等待配置文件加载的额外时间（毫秒）。")]
    public int ExtraTimeLoadProfile { get; set; }

    [Category(OpenGame), Description("在标题屏幕点击A之前等待的额外时间（毫秒）。")]
    public int ExtraTimeLoadGame { get; set; } = 5000;

    [Category(OpenGame), Description("标题屏幕后等待主世界加载的额外时间（毫秒）。")]
    public int ExtraTimeLoadOverworld { get; set; } = 3000;

    // 关闭游戏
    [Category(CloseGame), Description("按下HOME键最小化游戏后等待的额外时间（毫秒）。")]
    public int ExtraTimeReturnHome { get; set; }

    [Category(CloseGame), Description("点击关闭游戏后等待的额外时间（毫秒）。")]
    public int ExtraTimeCloseGame { get; set; }

    // 团战特定时间
    [Category(Raid), Description("[RaidBot] 点击巢穴后等待团战加载的额外时间（毫秒）。")]
    public int ExtraTimeLoadRaid { get; set; }

    [Category(Raid), Description("[RaidBot] 点击\"邀请其他人\"后锁定宝可梦前等待的额外时间（毫秒）。")]
    public int ExtraTimeOpenRaid { get; set; }

    [Category(Raid), Description("[RaidBot] 关闭游戏重置团战前等待的额外时间（毫秒）。")]
    public int ExtraTimeEndRaid { get; set; }

    [Category(Raid), Description("[RaidBot] 接受好友后等待的额外时间（毫秒）。")]
    public int ExtraTimeAddFriend { get; set; }

    [Category(Raid), Description("[RaidBot] 删除好友后等待的额外时间（毫秒）。")]
    public int ExtraTimeDeleteFriend { get; set; }

    // 杂项设置
    [Category(Misc), Description("[SWSH/SV] 点击+连接Y-Comm (SWSH) 或 L 连接在线 (SV) 后等待的额外时间（毫秒）。")]
    public int ExtraTimeConnectOnline { get; set; }

    [Category(Misc), Description("连接丢失后尝试重新连接到套接字的次数。设置为-1表示无限尝试。")]
    public int ReconnectAttempts { get; set; } = 30;

    [Category(Misc), Description("重新连接尝试之间等待的额外时间（毫秒）。基本时间为30秒。")]
    public int ExtraReconnectDelay { get; set; }

    [Category(Misc), Description("[BDSP] 离开联合房间后等待主世界加载的额外时间（毫秒）。")]
    public int ExtraTimeLeaveUnionRoom { get; set; } = 1000;

    [Category(Misc), Description("[BDSP] 每个交易循环开始时等待Y菜单加载的额外时间（毫秒）。")]
    public int ExtraTimeOpenYMenu { get; set; } = 500;

    [Category(Misc), Description("[BDSP] 在尝试呼叫交易前等待联合房间加载的额外时间（毫秒）。")]
    public int ExtraTimeJoinUnionRoom { get; set; } = 500;

    [Category(Misc), Description("[SV] 等待宝可入口站加载的额外时间（毫秒）。")]
    public int ExtraTimeLoadPortal { get; set; } = 1000;

    [Category(Misc), Description("找到交易后等待盒子加载的额外时间（毫秒）。")]
    public int ExtraTimeOpenBox { get; set; } = 1000;

    [Category(Misc), Description("交易期间打开键盘输入代码后等待的时间。")]
    public int ExtraTimeOpenCodeEntry { get; set; } = 1000;

    [Category(Misc), Description("在Switch菜单中导航或输入链接代码时每次按键后等待的时间。")]
    public int KeypressTime { get; set; }

    [Category(Misc), Description("启用此选项以拒绝传入的系统更新。")]
    public bool AvoidSystemUpdate { get; set; }
}
