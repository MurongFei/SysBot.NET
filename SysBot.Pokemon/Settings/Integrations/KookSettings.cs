using System.ComponentModel;

namespace SysBot.Pokemon;

public class KookSettings
{
    private const string Startup = "启动设置";
    private const string Operation = "操作设置";
    private const string Channels = "频道设置";
    private const string Roles = "角色权限";
    private const string Users = "用户设置";
    public override string ToString() => "Kook 集成设置";

    // 启动设置

    [Category(Startup), Description("机器人登录令牌。")]
    public string Token { get; set; } = string.Empty;

    [Category(Startup), Description("机器人命令前缀。")]
    public string CommandPrefix { get; set; } = "$";

    [Category(Startup), Description("机器人启动时不会加载的模块列表（逗号分隔）。")]
    public string ModuleBlacklist { get; set; } = string.Empty;

    [Category(Startup), Description("切换以异步或同步方式处理命令。")]
    public bool AsyncCommands { get; set; }

    [Category(Operation), Description("当用户向机器人打招呼时，机器人将回复的自定义消息。使用字符串格式化在回复中提及用户。")]
    public string HelloResponse { get; set; } = "Hi {0}!";

    // 角色权限

    [Category(Roles), Description("拥有此角色的用户被允许进入交易队列。")]
    public RemoteControlAccessList RoleCanTrade { get; set; } = new() { AllowIfEmpty = false };

    [Category(Roles), Description("拥有此角色的用户被允许进入种子检查队列。")]
    public RemoteControlAccessList RoleCanSeedCheck { get; set; } = new() { AllowIfEmpty = false };

    [Category(Roles), Description("拥有此角色的用户被允许进入克隆队列。")]
    public RemoteControlAccessList RoleCanClone { get; set; } = new() { AllowIfEmpty = false };

    [Category(Roles), Description("拥有此角色的用户被允许进入导出队列。")]
    public RemoteControlAccessList RoleCanDump { get; set; } = new() { AllowIfEmpty = false };

    [Category(Roles), Description("拥有此角色的用户被允许远程控制控制台（如果作为远程控制机器人运行）。")]
    public RemoteControlAccessList RoleRemoteControl { get; set; } = new() { AllowIfEmpty = false };

    [Category(Roles), Description("拥有此角色的用户被允许绕过命令限制。")]
    public RemoteControlAccessList RoleSudo { get; set; } = new() { AllowIfEmpty = false };

    [Category(Roles), Description("拥有此角色的用户被允许以更好的位置加入队列。")]
    public RemoteControlAccessList RoleFavored { get; set; } = new() { AllowIfEmpty = false };

    // 新增：批量交易权限
    [Category(Roles), Description("拥有此角色的用户被允许使用批量交易功能。")]
    public RemoteControlAccessList RoleCanBatchTrade { get; set; } = new() { AllowIfEmpty = false };

    // 用户设置

    [Category(Users), Description("拥有这些用户ID的用户不能使用机器人。")]
    public RemoteControlAccessList UserBlacklist { get; set; } = new();

    [Category(Users), Description("逗号分隔的 Kook 用户ID，这些用户将拥有对机器人中心的 sudo 访问权限。")]
    public RemoteControlAccessList GlobalSudoList { get; set; } = new();

    [Category(Users), Description("禁用此选项将移除全局 sudo 支持。")]
    public bool AllowGlobalSudo { get; set; } = true;

    // 频道设置

    [Category(Channels), Description("具有这些ID的频道是机器人确认命令的唯一频道。")]
    public RemoteControlAccessList ChannelWhitelist { get; set; } = new();

    [Category(Channels), Description("将回显日志机器人数据的频道ID。")]
    public RemoteControlAccessList LoggingChannels { get; set; } = new();

    [Category(Channels), Description("将记录交易开始消息的日志频道。")]
    public RemoteControlAccessList TradeStartingChannels { get; set; } = new();

    [Category(Channels), Description("将记录特殊消息的回显频道。")]
    public RemoteControlAccessList EchoChannels { get; set; } = new();

    // 操作设置

    [Category(Operation), Description("将交易中显示的宝可梦的PKM文件返回给用户。")]
    public bool ReturnPKMs { get; set; } = true;

    [Category(Operation), Description("如果用户不允许在频道中使用给定命令，则回复用户。如果为 false，机器人将静默忽略它们。")]
    public bool ReplyCannotUseCommandInChannel { get; set; } = true;

    [Category(Operation), Description("机器人监听频道消息，以便在附加PKM文件时（不使用命令）回复 ShowdownSet。")]
    public bool ConvertPKMToShowdownSet { get; set; } = true;

    [Category(Operation), Description("机器人可以在任何机器人可以看到的频道中回复 ShowdownSet，而不仅限于机器人已被列入白名单运行的频道。仅当您希望机器人在非机器人频道中提供更多实用功能时，才将其设置为 true。")]
    public bool ConvertPKMReplyAnyChannel { get; set; }
}
