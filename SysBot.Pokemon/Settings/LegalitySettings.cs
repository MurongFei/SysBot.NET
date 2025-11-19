using PKHeX.Core;
using PKHeX.Core.AutoMod;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace SysBot.Pokemon;

public class LegalitySettings
{
    private string DefaultTrainerName = "SysBot";
    private const string Generate = "生成设置";
    private const string Misc = "杂项设置";
    public override string ToString() => "合法性生成设置";

    // 生成设置
    [Category(Generate), Description("奇迹卡片的MGDB目录路径。")]
    public string MGDBPath { get; set; } = string.Empty;

    [Category(Generate), Description("包含训练家数据的PKM文件文件夹，用于重新生成的PKM文件。")]
    public string GeneratePathTrainerInfo { get; set; } = string.Empty;

    [Category(Generate), Description("不匹配任何提供的PKM文件的PKM文件的默认原训练家名称。")]
    public string GenerateOT
    {
        get => DefaultTrainerName;
        set
        {
            if (!StringsUtil.IsSpammyString(value))
                DefaultTrainerName = value;
        }
    }

    [Category(Generate), Description("不匹配任何提供的训练家数据文件的请求的默认16位训练家ID（TID）。这应该是一个5位数。")]
    public ushort GenerateTID16 { get; set; } = 12345;

    [Category(Generate), Description("不匹配任何提供的训练家数据文件的请求的默认16位秘密ID（SID）。这应该是一个5位数。")]
    public ushort GenerateSID16 { get; set; } = 54321;

    [Category(Generate), Description("不匹配任何提供的PKM文件的PKM文件的默认语言。")]
    public LanguageID GenerateLanguage { get; set; } = LanguageID.English;

    [Category(Generate), Description("生成宝可梦时搜索遭遇的方法。\"NativeOnly\"仅搜索当前游戏对，\"NewestFirst\"从最新游戏开始搜索，\"PriorityOrder\"使用\"GameVersionPriority\"设置中指定的顺序。")]
    public GameVersionPriorityType GameVersionPriority { get; set; } = GameVersionPriorityType.NativeOnly;

    [Category(Generate), Description("指定用于生成遭遇的游戏的顺序。将PrioritizeGame设置为\"true\"以启用。")]
    public List<GameVersion> PriorityOrder { get; set; } = Enum.GetValues<GameVersion>().Where(GameUtil.IsValidSavedVersion).Reverse().ToList();

    [Category(Generate), Description("为任何生成的宝可梦设置所有可能的合法缎带。")]
    public bool SetAllLegalRibbons { get; set; }

    [Category(Generate), Description("为任何生成的宝可梦设置匹配的球（基于颜色）。")]
    public bool SetMatchingBalls { get; set; } = true;

    [Category(Generate), Description("如果合法，强制使用指定的球。")]
    public bool ForceSpecifiedBall { get; set; } = true;

    [Category(Generate), Description("假设50级设置为100级竞技设置。")]
    public bool ForceLevel100for50 { get; set; }

    [Category(Generate), Description("在交易必须在Switch游戏之间旅行的宝可梦时需要HOME追踪器。")]
    public bool EnableHOMETrackerCheck { get; set; }

    [Category(Generate), Description("宝可梦遭遇类型的尝试顺序。")]
    public List<EncounterTypeGroup> PrioritizeEncounters { get; set; } =
    [
        EncounterTypeGroup.Egg, EncounterTypeGroup.Slot,
        EncounterTypeGroup.Static, EncounterTypeGroup.Mystery,
        EncounterTypeGroup.Trade,
    ];

    [Category(Generate), Description("为支持它的游戏（仅SWSH）添加战斗版本，以便在在线竞技对战中使用前代宝可梦。")]
    public bool SetBattleVersion { get; set; }

    [Category(Generate), Description("如果提供了非法设置，机器人将创建一个彩蛋宝可梦。")]
    public bool EnableEasterEggs { get; set; }

    [Category(Generate), Description("允许用户在Showdown设置中提交自定义OT、TID、SID和OT性别。")]
    public bool AllowTrainerDataOverride { get; set; }

    [Category(Generate), Description("允许用户使用批量编辑器命令提交进一步的自定义。")]
    public bool AllowBatchCommands { get; set; } = true;

    [Category(Generate), Description("生成设置时在取消之前花费的最大时间（秒）。这可以防止困难的设置冻结机器人。")]
    public int Timeout { get; set; } = 15;

    // 杂项设置

    [Category(Misc), Description("为克隆和用户请求的PKM文件清零HOME追踪器。建议保持禁用以避免创建无效的HOME数据。")]
    public bool ResetHOMETracker { get; set; } = false;

    [Category(Misc), Description("通过交易伙伴覆盖训练家数据")]
    public bool UseTradePartnerInfo { get; set; } = true;
}
