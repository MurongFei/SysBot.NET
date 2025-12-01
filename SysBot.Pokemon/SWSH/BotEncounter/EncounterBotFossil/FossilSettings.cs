using System.ComponentModel;

namespace SysBot.Pokemon;

public class FossilSettings
{
    private const string Fossil = "化石设置";
    private const string Counts = "计数统计";
    public override string ToString() => "化石机器人设置";

    [Category(Fossil), Description("要寻找的化石宝可梦物种。")]
    public FossilSpecies Species { get; set; } = FossilSpecies.Dracozolt;

    /// <summary>
    /// 注入化石碎片的开关。
    /// </summary>
    [Category(Fossil), Description("注入化石碎片的开关。")]
    public bool InjectWhenEmpty { get; set; }
}
