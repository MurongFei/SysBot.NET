using PKHeX.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace SysBot.Pokemon;

public class StopConditionSettings
{
    private const string StopConditions = "停止条件";
    public override string ToString() => "停止条件设置";

    [Category(StopConditions), Description("仅在此物种的宝可梦上停止。如果设置为\"None\"则无限制。")]
    public Species StopOnSpecies { get; set; }

    [Category(StopConditions), Description("仅在此形态ID的宝可梦上停止。如果留空则无限制。")]
    public int? StopOnForm { get; set; }

    [Category(StopConditions), Description("仅在指定性格的宝可梦上停止。")]
    public Nature TargetNature { get; set; } = Nature.Random;

    [Category(StopConditions), Description("最小接受的个体值，格式为 HP/攻击/防御/特攻/特防/速度。使用\"x\"表示不检查的个体值，\"/\"作为分隔符。")]
    public string TargetMinIVs { get; set; } = "";

    [Category(StopConditions), Description("最大接受的个体值，格式为 HP/攻击/防御/特攻/特防/速度。使用\"x\"表示不检查的个体值，\"/\"作为分隔符。")]
    public string TargetMaxIVs { get; set; } = "";

    [Category(StopConditions), Description("选择要停止的闪光类型。")]
    public TargetShinyType ShinyTarget { get; set; } = TargetShinyType.DisableOption;

    [Category(StopConditions), Description("允许过滤最小或最大体型来停止。")]
    public TargetHeightType HeightTarget { get; set; } = TargetHeightType.DisableOption;

    [Category(StopConditions), Description("仅在拥有证章的宝可梦上停止。")]
    public bool MarkOnly { get; set; }

    [Category(StopConditions), Description("要忽略的证章列表，用逗号分隔。使用完整名称，例如\"Uncommon Mark, Dawn Mark, Prideful Mark\"。")]
    public string UnwantedMarks { get; set; } = "";

    [Category(StopConditions), Description("当EncounterBot或Fossilbot找到匹配的宝可梦时，按住捕获按钮录制30秒视频片段。")]
    public bool CaptureVideoClip { get; set; }

    [Category(StopConditions), Description("在遭遇匹配后，按下捕获按钮前等待的额外时间（毫秒），适用于EncounterBot或Fossilbot。")]
    public int ExtraTimeWaitCaptureVideo { get; set; } = 10000;

    [Category(StopConditions), Description("如果设置为TRUE，同时匹配ShinyTarget和TargetIVs设置。否则，查找ShinyTarget或TargetIVs匹配。")]
    public bool MatchShinyAndIV { get; set; } = true;

    [Category(StopConditions), Description("如果不为空，提供的字符串将前置到找到的结果日志消息中，以回显警报给您指定的人。对于Discord，使用<@用户ID号>来提及。")]
    public string MatchFoundEchoMention { get; set; } = string.Empty;

    public static bool EncounterFound<T>(T pk, int[] targetminIVs, int[] targetmaxIVs, StopConditionSettings settings, IReadOnlyList<string>? marklist) where T : PKM
    {
        // 如果指定了性格和物种，则进行匹配。
        if (settings.StopOnSpecies != Species.None && settings.StopOnSpecies != (Species)pk.Species)
            return false;

        if (settings.StopOnForm.HasValue && settings.StopOnForm != pk.Form)
            return false;

        if (settings.TargetNature != Nature.Random && settings.TargetNature != pk.Nature)
            return false;

        // 如果没有证章，或者有不需要的证章，则返回。
        var unmarked = pk is IRibbonIndex m && !HasMark(m);
        var unwanted = marklist is not null && pk is IRibbonIndex m2 && settings.IsUnwantedMark(GetMarkName(m2), marklist);
        if (settings.MarkOnly && (unmarked || unwanted))
            return false;

        if (settings.ShinyTarget != TargetShinyType.DisableOption)
        {
            bool shinymatch = settings.ShinyTarget switch
            {
                TargetShinyType.AnyShiny => pk.IsShiny,
                TargetShinyType.NonShiny => !pk.IsShiny,
                TargetShinyType.StarOnly => pk.IsShiny && pk.ShinyXor != 0,
                TargetShinyType.SquareOnly => pk.ShinyXor == 0,
                TargetShinyType.DisableOption => true,
                _ => throw new ArgumentException(nameof(TargetShinyType)),
            };

            // 如果我们只需要匹配其中一个条件并且它闪光匹配，返回true。
            // 如果我们需要匹配两个条件，而它没有闪光匹配，返回false。
            if (!settings.MatchShinyAndIV && shinymatch)
                return true;
            if (settings.MatchShinyAndIV && !shinymatch)
                return false;
        }

        if (settings.HeightTarget != TargetHeightType.DisableOption && pk is PK8 p)
        {
            var value = p.HeightScalar;
            bool heightmatch = settings.HeightTarget switch
            {
                TargetHeightType.MinOnly => value is 0,
                TargetHeightType.MaxOnly => value is 255,
                TargetHeightType.MinOrMax => value is 0 or 255,
                _ => throw new ArgumentException(nameof(TargetHeightType)),
            };

            if (!heightmatch)
                return false;
        }

        // 重新排列速度到最后。
        Span<int> pkIVList = stackalloc int[6];
        pk.GetIVs(pkIVList);
        (pkIVList[5], pkIVList[3], pkIVList[4]) = (pkIVList[3], pkIVList[4], pkIVList[5]);

        for (int i = 0; i < 6; i++)
        {
            if (targetminIVs[i] > pkIVList[i] || targetmaxIVs[i] < pkIVList[i])
                return false;
        }
        return true;
    }

    public static void InitializeTargetIVs(PokeTradeHubConfig config, out int[] min, out int[] max)
    {
        min = ReadTargetIVs(config.StopConditions, true);
        max = ReadTargetIVs(config.StopConditions, false);
    }

    private static int[] ReadTargetIVs(StopConditionSettings settings, bool min)
    {
        int[] targetIVs = new int[6];
        char[] split = ['/'];

        string[] splitIVs = min
            ? settings.TargetMinIVs.Split(split, StringSplitOptions.RemoveEmptyEntries)
            : settings.TargetMaxIVs.Split(split, StringSplitOptions.RemoveEmptyEntries);

        // 只接受最多6个值。如果未提供6个值，则用默认值填充。
        // 任何不是整数的内容都将被视为通配符。
        for (int i = 0; i < 6; i++)
        {
            if (i < splitIVs.Length)
            {
                var str = splitIVs[i];
                if (int.TryParse(str, out var val))
                {
                    targetIVs[i] = val;
                    continue;
                }
            }
            targetIVs[i] = min ? 0 : 31;
        }
        return targetIVs;
    }

    private static bool HasMark(IRibbonIndex pk)
    {
        for (var mark = RibbonIndex.MarkLunchtime; mark <= RibbonIndex.MarkSlump; mark++)
        {
            if (pk.GetRibbon((int)mark))
                return true;
        }
        return false;
    }

    public static ReadOnlySpan<BattleTemplateToken> TokenOrder =>
    [
        BattleTemplateToken.FirstLine,
        BattleTemplateToken.Shiny,
        BattleTemplateToken.Nature,
        BattleTemplateToken.IVs,
    ];

    public static string GetPrintName(PKM pk)
    {
        const LanguageID lang = LanguageID.English;
        var settings = new BattleTemplateExportSettings(TokenOrder, lang);
        var set = ShowdownParsing.GetShowdownText(pk, settings);

        // 由于我们可以匹配最小/最大体型以传输到未来游戏，因此显示它。
        if (pk is IScaledSize p)
            set += $"\nHeight: {p.HeightScalar}";

        // 如果有证章，则添加证章。
        if (pk is IRibbonIndex r)
        {
            var rstring = GetMarkName(r);
            if (!string.IsNullOrEmpty(rstring))
                set += $"\nPokémon has the **{GetMarkName(r)}**!";
        }
        return set;
    }

    public static void ReadUnwantedMarks(StopConditionSettings settings, out IReadOnlyList<string> marks) =>
        marks = settings.UnwantedMarks.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList();

    public virtual bool IsUnwantedMark(string mark, IReadOnlyList<string> marklist) => marklist.Contains(mark);

    public static string GetMarkName(IRibbonIndex pk)
    {
        for (var mark = RibbonIndex.MarkLunchtime; mark <= RibbonIndex.MarkSlump; mark++)
        {
            if (pk.GetRibbon((int)mark))
                return GameInfo.Strings.Ribbons.GetName($"Ribbon{mark}");
        }
        return "";
    }
}

public enum TargetShinyType
{
    DisableOption,  // 不关心
    NonShiny,       // 仅匹配非闪光
    AnyShiny,       // 匹配任何闪光，无论类型
    StarOnly,       // 仅匹配星星闪光
    SquareOnly,     // 仅匹配方块闪光
}

public enum TargetHeightType
{
    DisableOption,  // 不关心
    MinOnly,        // 仅0体型
    MaxOnly,        // 仅255体型
    MinOrMax,       // 0或255体型
}
