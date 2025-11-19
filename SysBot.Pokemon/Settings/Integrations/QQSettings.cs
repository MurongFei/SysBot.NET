using System;
using System.ComponentModel;
using System.Linq;

namespace SysBot.Pokemon;

public class QQSettings
{
    private const string Startup = "启动设置";
    private const string Operation = "操作设置";
    private const string Messages = "消息设置";
    public override string ToString() => "QQ 集成设置";

    // 启动设置

    [Category(Startup), Description("Mirai 机器人地址")]
    public string Address { get; set; } = string.Empty;

    [Category(Startup), Description("Mirai 机器人验证密钥")]
    public string VerifyKey { get; set; } = string.Empty;

    [Category(Startup), Description("您的机器人QQ号")]
    public string QQ { get; set; } = string.Empty;

    [Category(Startup), Description("发送消息的QQ群号")]
    public string GroupId { get; set; } = string.Empty;

    [Category(Startup), Description("测试机器人存活的消息")]
    public string AliveMsg { get; set; } = "hello";

    [Category(Operation), Description("屏障释放时发送的消息。")]
    public string MessageStart { get; set; } = string.Empty;
}
