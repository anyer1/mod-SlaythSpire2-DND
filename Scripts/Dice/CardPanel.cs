using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace TrpgMod.Scripts.Dice;

/// <summary>
/// 从卡牌读取「面板值」，以及从玩家读取属性判定加值，对应设计文档 §3 / §6。
/// </summary>
public static class CardPanel
{
    /// <summary>
    /// 面板识别「排除法」：取卡牌全部动态变量里最大的 BaseValue 作为面板值。
    /// 不按数值类型（Damage / Block / Summon / Power / Calculated…）过滤，避免出现「有的牌不判定」。
    /// 只排除非效果性的结构变量 <see cref="MetaPrefixes"/>。
    /// 面板值 &lt;= 0 时返回 false，表示这张牌没有任何可数值化效果（§5.3：跳过判定、直接生效）。
    /// </summary>
    public static bool TryGetPanel(CardModel card, out decimal panel)
    {
        panel = 0m;

        foreach (KeyValuePair<string, DynamicVar> pair in card.DynamicVars)
        {
            if (IsMetaVar(pair.Key))
            {
                continue;
            }

            if (pair.Value.BaseValue > panel)
            {
                panel = pair.Value.BaseValue;
            }
        }

        if (panel > 0m)
        {
            return true;
        }

        // 诊断：记录仍然「无数值面板」的牌，便于核对是否还有漏判。
        Log.Info($"[TRPG-DIAG] {card.Title}（{card.Type}）无数值面板，变量：{DescribeVars(card)}");
        return false;
    }

    /// <summary>
    /// 非效果性变量（不计入面板）：多段次数 / 倍率类结构变量。
    /// 键名在框架里可能带编号后缀（如 Repeat0），故按前缀匹配。
    /// </summary>
    private static readonly string[] MetaPrefixes = { "Repeat" };

    private static bool IsMetaVar(string key)
    {
        foreach (string prefix in MetaPrefixes)
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string DescribeVars(CardModel card) =>
        string.Join(", ", card.DynamicVars.Select(p => $"{p.Key}:{p.Value.GetType().Name}={p.Value.BaseValue}"));

    /// <summary>
    /// 属性加值（§6）：力量 → 攻击牌，敏捷 → 技能牌，clamp(floor(属性 / 3), 0, 3)。
    /// 能力牌 / 诅咒 / 状态无加值。
    /// </summary>
    public static int AttributeBonus(CardType type, Creature? creature)
    {
        if (creature is null)
        {
            return 0;
        }

        int raw = type switch
        {
            CardType.Attack => creature.GetPower<StrengthPower>()?.Amount ?? 0,
            CardType.Skill => creature.GetPower<DexterityPower>()?.Amount ?? 0,
            _ => 0,
        };

        if (raw <= 0)
        {
            return 0;
        }

        return Math.Clamp(raw / TrpgConfig.AttrPerPoint, 0, TrpgConfig.AttrBonusCap);
    }
}