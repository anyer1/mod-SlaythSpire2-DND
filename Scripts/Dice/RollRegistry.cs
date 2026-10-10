using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace TrpgMod.Scripts.Dice;

/// <summary>
/// 把「本次出牌」的判定结果与增幅快照暂存起来，供 AfterCardOnPlay 还原。
/// 用 ConditionalWeakTable 以 CardPlay 实例为键，出牌结束即自动回收，不会泄漏。
/// </summary>
public sealed class RollEntry
{
    /// <summary>本次出牌的 d6 判定结果；诅咒判定（d20）时为空。</summary>
    public DiceRollResult? Result;

    /// <summary>被 ×2.5 的 DynamicVar 原始值快照（变量名 → 原 BaseValue）。未增幅时为 null。</summary>
    public Dictionary<string, decimal>? ScaledVars;
}

public static class RollRegistry
{
    private static readonly ConditionalWeakTable<CardPlay, RollEntry> Entries = new();

    public static void Set(CardPlay cardPlay, RollEntry entry)
    {
        Entries.Remove(cardPlay);
        Entries.Add(cardPlay, entry);
    }

    public static bool TryGet(CardPlay? cardPlay, out RollEntry entry)
    {
        if (cardPlay is null || !Entries.TryGetValue(cardPlay, out RollEntry? found) || found is null)
        {
            entry = null!;
            return false;
        }

        entry = found;
        return true;
    }

    public static void Remove(CardPlay cardPlay)
    {
        Entries.Remove(cardPlay);
    }
}

/// <summary>
/// 大成功增幅：直接把卡牌动态变量（伤害、格挡、抽牌数、能量、debuff 层数…）
/// 的 BaseValue 临时 ×2.5，出牌结束后还原。
/// 游戏文档明确说明 BaseValue 是「执行会改变游戏状态的运算时应使用的值」，
/// 所以这是覆盖全部可数值化效果的唯一汇合点，无需为每种效果单独找拦截点。
/// </summary>
public static class DynamicVarScaler
{
    /// <summary>
    /// 结构型变量，不能缩放：
    /// Repeat（多段次数）缩放会变成「段数 × 伤害」双重增幅。
    /// 注意：CalculationBase / ExtraDamage(CalculationExtra) 虽是 Calculated 变量的输入，
    /// 但引擎结算时是按「base + extra × 计数」重算的（见 sts2.xml 中 CalculatedDamageVar 注释），
    /// 所以这些输入量必须跟着一起缩放，否则 CalculatedDamage / CalculatedBlock 这类牌的大成功会完全失效。
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

    /// <summary>把卡牌所有可数值化动态变量 ×multiplier，返回原始值快照用于还原。</summary>
    public static Dictionary<string, decimal> Scale(CardModel card, decimal multiplier)
    {
        var snapshot = new Dictionary<string, decimal>(StringComparer.Ordinal);

        foreach (KeyValuePair<string, DynamicVar> pair in card.DynamicVars)
        {
            if (IsMetaVar(pair.Key) || pair.Value.BaseValue == 0m)
            {
                continue;
            }

            snapshot[pair.Key] = pair.Value.BaseValue;
            pair.Value.BaseValue = Math.Floor(pair.Value.BaseValue * multiplier);
        }

        return snapshot;
    }

    /// <summary>按快照还原动态变量。</summary>
    public static void Restore(CardModel card, Dictionary<string, decimal> snapshot)
    {
        foreach (KeyValuePair<string, decimal> pair in snapshot)
        {
            if (card.DynamicVars.TryGetValue(pair.Key, out DynamicVar? value) && value is not null)
            {
                value.BaseValue = pair.Value;
            }
        }
    }
}