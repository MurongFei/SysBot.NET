using System;
using System.ComponentModel;
using System.Linq;

namespace SysBot.Pokemon;

public class YouTubeSettings
{
    private const string Startup = "启动设置";
    private const string Operation = "操作设置";
    private const string Messages = "消息设置";
    public override string ToString() => "YouTube 集成设置";

    // 启动设置

    [Category(Startup), Description("机器人客户端ID")]
    public string ClientID { get; set; } = string.Empty;

    [Category(Startup), Description("机器人客户端密钥")]
    public string ClientSecret { get; set; } = string.Empty;

    [Category(Startup), Description("发送消息的频道ID")]
    public string ChannelID { get; set; } = string.Empty;

    [Category(Startup), Description("机器人命令前缀")]
    public char CommandPrefix { get; set; } = '$';

    [Category(Operation), Description("屏障释放时发送的消息。")]
    public string MessageStart { get; set; } = string.Empty;

    // 操作设置

    [Category(Operation), Description("Sudo 用户名")]
    public string SudoList { get; set; } = string.Empty;

    [Category(Operation), Description("拥有这些用户名的用户不能使用机器人。")]
    public string UserBlacklist { get; set; } = string.Empty;

    public bool IsSudo(string username)
    {
        var sudos = SudoList.Split([",", ", ", " "], StringSplitOptions.RemoveEmptyEntries);
        return sudos.Contains(username);
    }
}

public enum YouTubeMessageDestination
{
    Disabled,
    Channel,
}
