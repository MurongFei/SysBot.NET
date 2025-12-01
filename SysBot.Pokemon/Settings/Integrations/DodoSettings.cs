using System.ComponentModel;

namespace SysBot.Pokemon;

public class DodoSettings
{
    private const string Startup = "启动设置";

    public override string ToString() => "Dodo 集成设置";

    // 启动设置

    [Category(Startup), Description("接口地址")]
    public string BaseApi { get; set; } = "https://botopen.imdodo.com";

    [Category(Startup), Description("机器人唯一标识")]
    public string ClientId { get; set; } = string.Empty;

    [Category(Startup), Description("机器人鉴权Token")]
    public string Token { get; set; } = string.Empty;

    [Category(Startup), Description("机器人响应频道id")]
    public string ChannelId { get; set; } = string.Empty;

    [Category(Startup), Description("是否撤回交换消息")]
    public bool WithdrawTradeMessage { get; set; } = false;
}
