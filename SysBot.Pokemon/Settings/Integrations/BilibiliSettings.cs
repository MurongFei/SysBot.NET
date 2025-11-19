using System;
using System.ComponentModel;

namespace SysBot.Pokemon;

public class BilibiliSettings
{
    private const string Startup = "启动设置";

    public override string ToString() => "Bilibili 集成设置";

    // 启动设置

    [Category(Startup), Description("B站弹幕姬日志目录")]
    public string LogUrl { get; set; } = string.Empty;

    [Category(Startup), Description("直播间ID")]
    public int RoomId { get; set; } = 0;
}
