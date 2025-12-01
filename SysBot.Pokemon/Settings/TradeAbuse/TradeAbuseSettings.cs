using System.ComponentModel;

namespace SysBot.Pokemon;

public class TradeAbuseSettings
{
    private const string Monitoring = "监控设置";
    public override string ToString() => "交易滥用监控设置";

    [Category(Monitoring), Description("当一个人在少于设定值（分钟）内再次出现时，将发送通知。")]
    public double TradeCooldown { get; set; }

    [Category(Monitoring), Description("当一个人忽略交易冷却时间时，回显消息将包含他们的任天堂账户ID。")]
    public bool EchoNintendoOnlineIDCooldown { get; set; } = true;

    [Category(Monitoring), Description("如果不为空，提供的字符串将附加到回显警报中，以在用户违反交易冷却时间时通知您指定的人。对于Discord，使用<@用户ID号>来提及。")]
    public string CooldownAbuseEchoMention { get; set; } = string.Empty;

    [Category(Monitoring), Description("当一个人在少于设定值（分钟）内使用不同的Discord/Twitch账户出现时，将发送通知。")]
    public double TradeAbuseExpiration { get; set; } = 120;

    [Category(Monitoring), Description("当检测到一个人使用多个Discord/Twitch账户时，回显消息将包含他们的任天堂账户ID。")]
    public bool EchoNintendoOnlineIDMulti { get; set; } = true;

    [Category(Monitoring), Description("当检测到一个人发送给多个游戏内账户时，回显消息将包含他们的任天堂账户ID。")]
    public bool EchoNintendoOnlineIDMultiRecipients { get; set; } = true;

    [Category(Monitoring), Description("当检测到一个人使用多个Discord/Twitch账户时，将采取此操作。")]
    public TradeAbuseAction TradeAbuseAction { get; set; } = TradeAbuseAction.Quit;

    [Category(Monitoring), Description("当一个人因使用多个账户在游戏中被阻止时，他们的在线ID将被添加到BannedIDs中。")]
    public bool BanIDWhenBlockingUser { get; set; } = true;

    [Category(Monitoring), Description("如果不为空，提供的字符串将附加到回显警报中，以在发现用户使用多个账户时通知您指定的人。对于Discord，使用<@用户ID号>来提及。")]
    public string MultiAbuseEchoMention { get; set; } = string.Empty;

    [Category(Monitoring), Description("如果不为空，提供的字符串将附加到回显警报中，以在发现用户在游戏内发送给多个玩家时通知您指定的人。对于Discord，使用<@用户ID号>来提及。")]
    public string MultiRecipientEchoMention { get; set; } = string.Empty;

    [Category(Monitoring), Description("被禁止的在线ID，将触发交易退出或游戏内阻止。")]
    public RemoteControlAccessList BannedIDs { get; set; } = new();

    [Category(Monitoring), Description("当遇到被禁止ID的人时，在退出交易前在游戏内阻止他们。")]
    public bool BlockDetectedBannedUser { get; set; } = true;

    [Category(Monitoring), Description("如果不为空，提供的字符串将附加到回显警报中，以在用户匹配被禁止ID时通知您指定的人。对于Discord，使用<@用户ID号>来提及。")]
    public string BannedIDMatchEchoMention { get; set; } = string.Empty;

    [Category(Monitoring), Description("当检测到滥用Ledy昵称交换的人时，回显消息将包含他们的任天堂账户ID。")]
    public bool EchoNintendoOnlineIDLedy { get; set; } = true;

    [Category(Monitoring), Description("如果不为空，提供的字符串将附加到回显警报中，以在用户违反Ledy交易规则时通知您指定的人。对于Discord，使用<@用户ID号>来提及。")]
    public string LedyAbuseEchoMention { get; set; } = string.Empty;
}
