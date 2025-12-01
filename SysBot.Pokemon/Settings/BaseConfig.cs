using System.ComponentModel;

namespace SysBot.Pokemon;

/// <summary>
/// 控制台无关设置
/// </summary>
public abstract class BaseConfig
{
    protected const string FeatureToggle = "功能开关";
    protected const string Operation = "操作设置";
    private const string Debug = "调试设置";

    [Category(FeatureToggle), Description("启用后，当机器人未处理任何内容时，会偶尔按下B按钮（以避免休眠）。")]
    public bool AntiIdle { get; set; }

    [Category(FeatureToggle), Description("启用文本日志。重启以应用更改。")]
    public bool LoggingEnabled { get; set; } = true;

    [Category(FeatureToggle), Description("保留的旧文本日志文件的最大数量。设置此值 <= 0 以禁用日志清理。重启以应用更改。")]
    public int MaxArchiveFiles { get; set; } = 14;

    [Category(Debug), Description("在程序启动时跳过创建机器人；有助于测试集成。")]
    public bool SkipConsoleBotCreation { get; set; }

    [Category(Operation)]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public LegalitySettings Legality { get; set; } = new();

    [Category(Operation)]
    [TypeConverter(typeof(ExpandableObjectConverter))]
    public FolderSettings Folder { get; set; } = new();

    public abstract bool Shuffled { get; }
}
