using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Cards;
using TrpgMod.Scripts.Dice;

namespace TrpgMod.Scripts.Content;

/// <summary>能力牌大失败惩罚骰的类别，对应设计文档 §5.2。</summary>
public enum PenaltyKind
{
    /// <summary>失去能量。</summary>
    LoseEnergy,

    /// <summary>自身获得虚弱。</summary>
    Weak,

    /// <summary>自身受到伤害（无视格挡）。</summary>
    SelfDamage,

    /// <summary>自身获得易伤。</summary>
    Vulnerable,

    /// <summary>自身失去力量。</summary>
    StrengthDown,

    /// <summary>自身获得 Doom。</summary>
    Doom,
}

/// <summary>
/// 大失败反噬，对应设计文档 §5.1 / §5.2。
/// </summary>
public static class Backlash
{
    /// <summary>
    /// 能力牌惩罚判定骰（1d6 固定哈希表）。
    /// 注：M1 把设计文档里第 5 项「洗 1 张状态牌入抽牌堆」换成「失去 1 点力量」，
    /// 因为造牌需要尚未确认的卡牌实例化 API；确认后再换回。
    /// </summary>
    private static readonly Dictionary<int, (PenaltyKind Kind, decimal Amount)> PowerFumbleTable = new()
    {
        [1] = (PenaltyKind.LoseEnergy, 1m),
        [2] = (PenaltyKind.Weak, 1m),
        [3] = (PenaltyKind.SelfDamage, 4m),
        [4] = (PenaltyKind.Vulnerable, 1m),
        [5] = (PenaltyKind.StrengthDown, 1m),
        [6] = (PenaltyKind.Doom, 2m),
    };

    /// <summary>结算大失败反噬。</summary>
    public static async Task ApplyAsync(BeforeCardOnPlayContext context, DiceRollResult roll, Rng rng)
    {
        Creature? self = context.CardPlay.Player?.Creature;
        if (self is null || !self.IsAlive)
        {
            return;
        }

        if (context.CardPlay.Card.Type == CardType.Power)
        {
            int face = rng.NextInt(1, TrpgConfig.DieFaces + 1);
            (PenaltyKind kind, decimal amount) = PowerFumbleTable[face];
            Log.Info($"[TRPG] 能力牌大失败：惩罚骰 = {face} → {kind} {amount}");
            await ApplyPenaltyAsync(context.ChoiceContext, self, kind, amount);
            return;
        }

        // 攻击 / 技能：自身受到 面板值 × 0.5 伤害（无视格挡），向下取整。
        decimal backlash = Math.Floor(roll.PanelValue * TrpgConfig.BacklashRatio);
        if (backlash <= 0m)
        {
            return;
        }

        Log.Info($"[TRPG] 反噬：自身受到 {backlash} 点伤害（无视格挡）");
        await DealUnblockableAsync(context.ChoiceContext, self, backlash);
    }

    private static async Task ApplyPenaltyAsync(
        PlayerChoiceContext choiceContext, Creature self, PenaltyKind kind, decimal amount)
    {
        switch (kind)
        {
            case PenaltyKind.LoseEnergy:
                if (self.Player is not null)
                {
                    await PlayerCmd.LoseEnergy(amount, self.Player);
                }
                break;

            case PenaltyKind.Weak:
                await PowerCmd.Apply<WeakPower>(choiceContext, self, amount, self, null);
                break;

            case PenaltyKind.SelfDamage:
                await DealUnblockableAsync(choiceContext, self, amount);
                break;

            case PenaltyKind.Vulnerable:
                await PowerCmd.Apply<VulnerablePower>(choiceContext, self, amount, self, null);
                break;

            case PenaltyKind.StrengthDown:
                await PowerCmd.Apply<StrengthPower>(choiceContext, self, -amount, self, null);
                break;

            case PenaltyKind.Doom:
                await PowerCmd.Apply<DoomPower>(choiceContext, self, amount, self, null);
                break;
        }
    }

    /// <summary>无视格挡的自身伤害（ValueProp.Unblockable）。</summary>
    private static async Task DealUnblockableAsync(PlayerChoiceContext choiceContext, Creature self, decimal amount)
    {
        await CreatureCmd.Damage(
            choiceContext,
            self,
            amount,
            ValueProp.Unblockable,
            dealer: null,
            cardSource: null,
            cardPlay: null);
    }
}