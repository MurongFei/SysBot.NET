using Kook;

namespace SysBot.Pokemon.Kook;

public static class CardHelper
{
    // 主题颜色
    private static readonly Color InfoColor = new(87, 138, 242);     // 蓝色 - 信息
    private static readonly Color SuccessColor = new(61, 185, 133);  // 绿色 - 成功
    private static readonly Color WarningColor = new(250, 166, 26);  // 黄色 - 警告
    private static readonly Color DangerColor = new(240, 71, 71);    // 红色 - 错误

    /// <summary>
    /// 创建交易初始化卡片
    /// </summary>
    public static CardBuilder CreateTradeInitCard(string userName, string pokemonName)
    {
        return new CardBuilder()
            .WithTheme(CardTheme.Info)
            .WithColor(InfoColor)
            .AddModule(new HeaderModuleBuilder().WithText("交易初始化"))
            .AddModule(new SectionModuleBuilder()
                .WithText($"用户: {userName}\n宝可梦: {pokemonName}"))
            .AddModule(new SectionModuleBuilder()
                .WithText("正在初始化交易\n正在连接"))
            .AddModule(new ContextModuleBuilder()
                .AddElement(new KMarkdownElementBuilder().WithContent("交易密码：请查看私信")));
    }

    /// <summary>
    /// 创建交易搜索卡片
    /// </summary>
    public static CardBuilder CreateTradeSearchingCard(string userName, string statusMessage, string trainerInfo, string inGameName)
    {
        return new CardBuilder()
            .WithTheme(CardTheme.Warning)
            .WithColor(WarningColor)
            .AddModule(new HeaderModuleBuilder().WithText("搜索交易伙伴"))
            .AddModule(new SectionModuleBuilder()
                .WithText($"用户: {userName}\n状态: {statusMessage}{trainerInfo}"))
            .AddModule(new SectionModuleBuilder()
                .WithText($"机器人游戏名: {inGameName}"))
            .AddModule(new ContextModuleBuilder()
                .AddElement(new KMarkdownElementBuilder().WithContent("请确保已输入正确的交易密码")));
    }

    /// <summary>
    /// 创建交易完成卡片
    /// </summary>
    public static CardBuilder CreateTradeFinishedCard(string userName, string message)
    {
        return new CardBuilder()
            .WithTheme(CardTheme.Success)
            .WithColor(SuccessColor)
            .AddModule(new HeaderModuleBuilder().WithText("交易完成"))
            .AddModule(new SectionModuleBuilder()
                .WithText($"用户: {userName}\n结果: {message}"))
            .AddModule(new ContextModuleBuilder()
                .AddElement(new KMarkdownElementBuilder().WithContent("交易成功完成")));
    }

    /// <summary>
    /// 创建交易取消卡片
    /// </summary>
    public static CardBuilder CreateTradeCanceledCard(string userName, string reason)
    {
        return new CardBuilder()
            .WithTheme(CardTheme.Danger)
            .WithColor(DangerColor)
            .AddModule(new HeaderModuleBuilder().WithText("交易取消"))
            .AddModule(new SectionModuleBuilder()
                .WithText($"用户: {userName}\n原因: {reason}"))
            .AddModule(new ContextModuleBuilder()
                .AddElement(new KMarkdownElementBuilder().WithContent("如需重新交易，请重新提交请求")));
    }

    /// <summary>
    /// 创建队列添加卡片
    /// </summary>
    public static CardBuilder CreateQueueAddedCard(string userName, string routineName, int position, string pokeName, string waitTime)
    {
        var card = new CardBuilder()
            .WithTheme(CardTheme.Info)
            .WithColor(InfoColor)
            .AddModule(new HeaderModuleBuilder().WithText("已加入队列"))
            .AddModule(new SectionModuleBuilder()
                .WithText($"用户: {userName}\n类型: {routineName}\n当前位置: {position}"));

        // 添加预计等待时间（如果提供了）
        if (!string.IsNullOrEmpty(waitTime))
            card.AddModule(new SectionModuleBuilder().WithText(waitTime));

        // 添加宝可梦名称（如果提供了）
        if (!string.IsNullOrEmpty(pokeName))
            card.AddModule(new SectionModuleBuilder().WithText(pokeName));

        return card;
    }

    /// <summary>
    /// 创建错误卡片
    /// </summary>
    public static CardBuilder CreateErrorCard(string title, string message)
    {
        return new CardBuilder()
            .WithTheme(CardTheme.Danger)
            .WithColor(DangerColor)
            .AddModule(new HeaderModuleBuilder().WithText(title))
            .AddModule(new SectionModuleBuilder().WithText(message))
            .AddModule(new ContextModuleBuilder()
                .AddElement(new KMarkdownElementBuilder().WithContent("请检查后重试")));
    }

    /// <summary>
    /// 创建宝可梦详细信息卡片
    /// </summary>
    public static CardBuilder CreatePokemonDetailCard(string speciesName, string genderSymbol, string userName, int level,
        string nature, string ability, string item, string ball, string ivs, string evs,
        string moves, string teraType, int position, string routineName, string waitTime)
    {
        var card = new CardBuilder()
            .WithTheme(CardTheme.Info)
            .WithColor(InfoColor)
            .AddModule(new HeaderModuleBuilder().WithText($"{speciesName} {genderSymbol}"))
            .AddModule(new DividerModuleBuilder())
            .AddModule(new SectionModuleBuilder()
                .WithText($"训练家: {userName}\n等级: {level}\n性格: {nature}\n特性: {ability}\n持有物: {item}\n球种: {ball}"));

        // 添加个体值信息
        if (!string.IsNullOrEmpty(ivs))
        {
            card.AddModule(new DividerModuleBuilder())
                .AddModule(new SectionModuleBuilder().WithText("个体值 (IVs)"))
                .AddModule(new SectionModuleBuilder().WithText(ivs));
        }

        // 添加努力值信息
        if (!string.IsNullOrEmpty(evs))
        {
            card.AddModule(new DividerModuleBuilder())
                .AddModule(new SectionModuleBuilder().WithText("努力值 (EVs)"))
                .AddModule(new SectionModuleBuilder().WithText(evs));
        }

        // 添加招式信息
        if (!string.IsNullOrEmpty(moves))
        {
            card.AddModule(new DividerModuleBuilder())
                .AddModule(new SectionModuleBuilder().WithText("招式信息"))
                .AddModule(new SectionModuleBuilder().WithText(moves));
        }

        // 添加太晶属性（如果可用）
        if (!string.IsNullOrEmpty(teraType))
        {
            card.AddModule(new DividerModuleBuilder())
                .AddModule(new SectionModuleBuilder().WithText($"太晶属性: {teraType}"));
        }

        // 添加队列信息
        card.AddModule(new DividerModuleBuilder())
            .AddModule(new ContextModuleBuilder()
                .AddElement(new KMarkdownElementBuilder().WithContent($"队列位置: {position} | 交易类型: {GetRoutineDisplayName(routineName)} | 预计等待: {waitTime}")));

        return card;
    }

    /// <summary>
    /// 获取交易类型的显示名称
    /// </summary>
    private static string GetRoutineDisplayName(string routineName)
    {
        return routineName switch
        {
            "LinkTrade" => "链接交易",
            "Clone" => "克隆",
            "Dump" => "导出",
            "SeedCheck" => "种子检查",
            _ => routineName
        };
    }
}
