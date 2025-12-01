using Kook;
using Kook.Commands;
using Kook.Net;
using Kook.WebSocket;
using PKHeX.Core;
using System.Threading.Tasks;

namespace SysBot.Pokemon.Kook;

public static class QueueHelper<TPKM> where TPKM : PKM, new()
{
    private const uint MaxTradeCode = 9999_9999;

    public static async Task AddToQueueAsync(SocketCommandContext context, int code, string trainer, RequestSignificance sig, TPKM trade, PokeRoutineType routine, PokeTradeType type, SocketUser trader)
    {
        if ((uint)code > MaxTradeCode)
        {
            if (context.Channel != null)
                await context.Channel.SendTextAsync("交易码应在 00000000-99999999 范围内！").ConfigureAwait(false);
            return;
        }

        try
        {
            const string helper = "已将您添加到队列中！当您的交易开始时，我会在这里通知您。";
            var test = await trader.SendTextAsync(helper).ConfigureAwait(false);

            // 尝试添加到队列
            var result = AddToTradeQueue(context, trade, code, trainer, sig, routine, type, trader, out var msg, out var position, out var routineName, out var pokeName, out var waitTime);

            if (result)
            {
                // 1. 首先发送宝可梦详细信息卡片（最上层）
                if (trade.Species != 0) // 确保是有效的宝可梦
                {
                    var detailCard = CreatePokemonDetailCardData(trade, trader.Username, position, routineName, waitTime ?? "");
                    await context.Channel.SendCardAsync(detailCard.Build()).ConfigureAwait(false);
                }

                // 2. 然后发送队列状态卡片
                var queueCard = CardHelper.CreateQueueAddedCard(trader.Username, routineName, position, pokeName ?? "", waitTime ?? "");
                await context.Channel.SendCardAsync(queueCard.Build()).ConfigureAwait(false);
            }
            else
            {
                // 发送错误卡片
                var errorCard = CardHelper.CreateErrorCard("队列添加失败", msg);
                await context.Channel.SendCardAsync(errorCard.Build()).ConfigureAwait(false);
            }

            // 在私信中通知
            await trader.SendTextAsync($"{msg}\n您的交易码将是 {Format.Bold($"{code:0000 0000}")}.").ConfigureAwait(false);

            // 清理工作
            if (result)
            {
                if (!context.IsPrivate && IsTraditionalTradeCommand(context.Message) && context.Channel != null)
                    await context.Message.DeleteAsync(RequestOptions.Default).ConfigureAwait(false);
            }
        }
        catch (HttpException ex)
        {
            await HandleKookExceptionAsync(context, trader, ex).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 准备宝可梦详细信息卡片数据
    /// </summary>
    private static CardBuilder CreatePokemonDetailCardData(TPKM pokemon, string userName, int position, string routineName, string waitTime)
    {
        var speciesName = ShowdownTranslator<TPKM>.GameStringsZh.Species[pokemon.Species];
        var genderSymbol = pokemon.Gender == 0 ? "♂" : pokemon.Gender == 1 ? "♀" : "⚥";

        // 获取性格、特性、持有物等信息
        var nature = GetNatureDisplayName(pokemon.Nature);
        var ability = GetAbilityDisplayName(pokemon.Ability);
        var item = GetItemDisplayName(pokemon.HeldItem);
        var ball = GetBallDisplayName(pokemon.Ball);
        var teraType = GetTeraTypeDisplayName(pokemon);

        // 构建个体值字符串（优化排版）
        var ivs = FormatStats(pokemon.IV_HP, pokemon.IV_ATK, pokemon.IV_DEF, pokemon.IV_SPA, pokemon.IV_SPD, pokemon.IV_SPE);

        // 构建努力值字符串（优化排版）
        var evs = FormatStats(pokemon.EV_HP, pokemon.EV_ATK, pokemon.EV_DEF, pokemon.EV_SPA, pokemon.EV_SPD, pokemon.EV_SPE);

        // 获取招式信息
        var moves = GetMovesDisplay(pokemon);

        return CardHelper.CreatePokemonDetailCard(
            speciesName, genderSymbol, userName, pokemon.CurrentLevel,
            nature, ability, item, ball, ivs, evs, moves,
            teraType, position, routineName, waitTime
        );
    }

    /// <summary>
    /// 格式化个体值和努力值显示（优化排版）
    /// </summary>
    private static string FormatStats(int hp, int atk, int def, int spa, int spd, int spe)
    {
        // 第一行：HP、攻击、防御
        var line1 = $"HP: {hp,-3}   攻击: {atk,-3}   防御: {def,-3}";

        // 第二行：特攻、特防、速度
        var line2 = $"特攻: {spa,-3}   特防: {spd,-3}   速度: {spe,-3}";

        return $"{line1}\n{line2}";
    }

    /// <summary>
    /// 获取性格显示名称
    /// </summary>
    private static string GetNatureDisplayName(Nature nature)
    {
        var natures = new[]
        {
            "勤奋", "怕寂寞", "勇敢", "固执", "顽皮", "大胆", "坦率", "悠闲", "淘气", "乐天",
            "害羞", "急躁", "认真", "爽朗", "天真", "内敛", "慢吞吞", "冷静", "温和", "温顺",
            "自大", "慎重", "浮躁", "轻浮", "慢吞吞"
        };
        return (int)nature < natures.Length ? natures[(int)nature] : nature.ToString();
    }

    /// <summary>
    /// 获取特性显示名称
    /// </summary>
    private static string GetAbilityDisplayName(int ability)
    {
        try
        {
            var abilityName = ShowdownTranslator<TPKM>.GameStringsZh.Ability[ability];
            return abilityName ?? $"特性 {ability}";
        }
        catch
        {
            return $"特性 {ability}";
        }
    }

    /// <summary>
    /// 获取道具显示名称
    /// </summary>
    private static string GetItemDisplayName(int item)
    {
        if (item == 0)
            return "无";

        try
        {
            var itemName = ShowdownTranslator<TPKM>.GameStringsZh.Item[item];
            return itemName ?? $"道具 {item}";
        }
        catch
        {
            return $"道具 {item}";
        }
    }

    /// <summary>
    /// 获取球种显示名称
    /// </summary>
    private static string GetBallDisplayName(int ball)
    {
        var balls = new[]
        {
            "大师球", "超级球", "高级球", "精灵球", "狩猎球", "网纹球", "潜水球", "巢穴球",
            "重复球", "计时球", "豪华球", "治愈球", "先机球", "黑暗球", "速度球", "等级球",
            "诱饵球", "沉重球", "甜蜜球", "友友球", "月亮球", "竞争球", "梦境球", "究极球"
        };
        return ball < balls.Length ? balls[ball] : $"球种 {ball + 1}";
    }

    /// <summary>
    /// 获取太晶属性显示名称
    /// </summary>
    private static string GetTeraTypeDisplayName(TPKM pokemon)
    {
        if (pokemon is PK9 pk9)
        {
            var teraTypes = new[]
            {
                "一般", "格斗", "飞行", "毒", "地面", "岩石", "虫", "幽灵", "钢", "火", "水",
                "草", "电", "超能力", "冰", "龙", "恶", "妖精"
            };
            var teraType = (int)pk9.TeraType;
            return teraType < teraTypes.Length ? teraTypes[teraType] : null;
        }
        return null;
    }

    /// <summary>
    /// 获取招式信息
    /// </summary>
    private static string GetMovesDisplay(TPKM pokemon)
    {
        var moves = new List<string>();
        var moveNames = ShowdownTranslator<TPKM>.GameStringsZh.Move;

        for (int i = 0; i < 4; i++)
        {
            var move = pokemon.GetMove(i);
            if (move != 0)
            {
                try
                {
                    var moveName = moveNames[(int)move];
                    moves.Add($"招式{i + 1}: {moveName}");
                }
                catch
                {
                    moves.Add($"招式{i + 1}: {move}");
                }
            }
        }

        return moves.Count > 0 ? string.Join("\n", moves) : "";
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

    public static Task AddToQueueAsync(SocketCommandContext context, int code, string trainer, RequestSignificance sig, TPKM trade, PokeRoutineType routine, PokeTradeType type)
    {
        return AddToQueueAsync(context, code, trainer, sig, trade, routine, type, context.User);
    }

    // 新增辅助方法：判断是否为传统交易命令
    private static bool IsTraditionalTradeCommand(SocketUserMessage msg)
    {
        return msg.Content.StartsWith("$trade");
    }

    private static bool AddToTradeQueue(SocketCommandContext context, TPKM pk, int code, string trainerName, RequestSignificance sig, PokeRoutineType type, PokeTradeType t, SocketUser trader, out string msg, out int position, out string routineName, out string pokeName, out string waitTime)
    {
        var user = trader;
        var userID = user.Id;
        var name = user.Username;

        var trainer = new PokeTradeTrainerInfo(trainerName, userID);

        var notifier = new KookTradeNotifier<TPKM>(pk, trainer, code, user, context.Channel as SocketTextChannel);

        var detail = new PokeTradeDetail<TPKM>(pk, trainer, notifier, t, code, sig == RequestSignificance.Favored);
        var trade = new TradeEntry<TPKM>(detail, userID, type, name);

        var hub = KookBot<TPKM>.Runner.Hub;
        var Info = hub.Queues.Info;
        var added = Info.AddToTradeQueue(trade, userID, sig == RequestSignificance.Owner);

        position = 0;
        routineName = "";
        pokeName = "";
        waitTime = "";

        if (added == QueueResultAdd.AlreadyInQueue)
        {
            msg = "抱歉，您已经在队列中了。";
            return false;
        }

        var positionInfo = Info.CheckPosition(userID, type);
        position = positionInfo.Position;

        routineName = type.ToString() switch
        {
            "LinkTrade" => "连接交易",
            "Clone" => "克隆",
            "Dump" => "导出",
            "SeedCheck" => "种子检查",
            _ => type.ToString()
        };

        if (t == PokeTradeType.Specific && pk.Species != 0)
            pokeName = $"接收: {ShowdownTranslator<TPKM>.GameStringsZh.Species[pk.Species]}";

        msg = $"{user.Username} - 已添加到 {routineName} 队列。当前位置: {position}。";

        var botct = Info.Hub.Bots.Count;

        if (position > 1)
        {
            var eta = Info.Hub.Config.Queues.EstimateDelay(position, botct);
            waitTime = $"约 {eta:F1} 分钟";
            msg += $" {waitTime}。";
        }
        else
        {
            waitTime = "立即开始";
        }

        if (!string.IsNullOrEmpty(pokeName))
            msg += $" {pokeName}.";

        return true;
    }

    private static async Task HandleKookExceptionAsync(SocketCommandContext context, SocketUser trader, HttpException ex)
    {
        string message = string.Empty;
        switch (ex.KookCode)
        {
            case KookErrorCode.MissingPermissions:
                {
                    if (context.Channel is IGuildChannel guildChannel && context.Guild != null)
                    {
                        var permissions = context.Guild.CurrentUser.GetPermissions(guildChannel);
                        if (!permissions.SendMessages)
                        {
                            message = "您必须授予我\"发送消息\"权限！";
                            Base.LogUtil.LogError(message, "QueueHelper");
                            return;
                        }
                        if (!permissions.ManageMessages)
                        {
                            var owner = KookBotSettings.Manager.Owner;
                            message = $"{MentionUtils.KMarkdownMentionUser(owner)} 您必须授予我\"管理消息\"权限！";
                        }
                    }
                }
                break;
            default:
                {
                    message = ex.KookCode != null ? $"Kook 错误 {(int)ex.KookCode}: {ex.Reason}" : $"HTTP 错误 {(int)ex.HttpCode}: {ex.Message}";
                }
                break;
        }
        if (context.Channel != null)
            await context.Channel.SendTextAsync(message).ConfigureAwait(false);
    }
}
