using System;
using System.ComponentModel;
using System.Linq;

namespace SysBot.Pokemon;

public class TwitchSettings
{
    private const string Startup = "启动设置";
    private const string Operation = "操作设置";
    private const string Messages = "消息设置";
    public override string ToString() => "Twitch 集成设置";

    // 启动设置

    [Category(Startup), Description("机器人登录令牌")]
    public string Token { get; set; } = string.Empty;

    [Category(Startup), Description("机器人用户名")]
    public string Username { get; set; } = string.Empty;

    [Category(Startup), Description("发送消息的频道")]
    public string Channel { get; set; } = string.Empty;

    [Category(Startup), Description("机器人命令前缀")]
    public char CommandPrefix { get; set; } = '$';

    [Category(Operation), Description("屏障释放时发送的消息。")]
    public string MessageStart { get; set; } = string.Empty;

    // 消息限制

    [Category(Operation), Description("如果在过去Y秒内已发送X条消息，则限制机器人发送消息。")]
    public int ThrottleMessages { get; set; } = 100;

    [Category(Operation), Description("如果在过去Y秒内已发送X条消息，则限制机器人发送消息。")]
    public double ThrottleSeconds { get; set; } = 30;

    [Category(Operation), Description("如果在过去Y秒内已发送X条消息，则限制机器人发送私信。")]
    public int ThrottleWhispers { get; set; } = 100;

    [Category(Operation), Description("如果在过去Y秒内已发送X条消息，则限制机器人发送私信。")]
    public double ThrottleWhispersSeconds { get; set; } = 60;

    // 操作设置

    [Category(Operation), Description("Sudo 用户名")]
    public string SudoList { get; set; } = string.Empty;

    [Category(Operation), Description("拥有这些用户名的用户不能使用机器人。")]
    public string UserBlacklist { get; set; } = string.Empty;

    [Category(Operation), Description("启用后，机器人将处理发送到频道的命令。")]
    public bool AllowCommandsViaChannel { get; set; } = true;

    [Category(Operation), Description("启用后，机器人将允许用户通过私信发送命令（绕过慢速模式）")]
    public bool AllowCommandsViaWhisper { get; set; }

    // 消息目的地

    [Category(Messages), Description("确定通用通知的发送位置。")]
    public TwitchMessageDestination NotifyDestination { get; set; }

    [Category(Messages), Description("确定交易开始通知的发送位置。")]
    public TwitchMessageDestination TradeStartDestination { get; set; } = TwitchMessageDestination.Channel;

    [Category(Messages), Description("确定交易搜索通知的发送位置。")]
    public TwitchMessageDestination TradeSearchDestination { get; set; }

    [Category(Messages), Description("确定交易完成通知的发送位置。")]
    public TwitchMessageDestination TradeFinishDestination { get; set; }

    [Category(Messages), Description("确定交易取消通知的发送位置。")]
    public TwitchMessageDestination TradeCanceledDestination { get; set; } = TwitchMessageDestination.Channel;

    [Category(Messages), Description("切换分发交易在开始前是否倒计时。")]
    public bool DistributionCountDown { get; set; } = true;

    public bool IsSudo(string username)
    {
        var sudos = SudoList.Split([",", ", ", " "], StringSplitOptions.RemoveEmptyEntries);
        return sudos.Contains(username);
    }
}

public enum TwitchMessageDestination
{
    Disabled,
    Channel,
    Whisper,
}
