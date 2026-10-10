using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Random;
using STS2RitsuLib;
using STS2RitsuLib.Cards;
using TrpgMod.Scripts.Dice;
using TrpgMod.Scripts.Ui;

namespace TrpgMod.Scripts.Content;

/// <summary>
/// 判定层入口：每张牌打出时掷骰定档，对应设计文档 §11.1 的时序。
/// 由 RitsuLib 的 CardOnPlayHook 在卡牌自身 OnPlay 之前调用。
/// </summary>
public sealed class DiceJudgementListener : ICardOnPlayHookListener
{
    /// <summary>判定 RNG 流标识（同一局内两端一致）。</summary>
    private const string DiceStream = "TrpgDice";

    /// <summary>
    /// 取本局判定 RNG：用 RitsuLib 的 run RNG 流，多人模式下两端共享同一序列，保证骰池与惩罚骰同步（§10-2）。
    /// </summary>
    private static Rng DiceFor(CardPlay play) =>
        RitsuLibFramework.GetModRunRng(play.Player, Entry.ModId, DiceStream);

    public async Task<bool> BeforeCardOnPlay(BeforeCardOnPlayContext context)
    {
        CardPlay play = context.CardPlay;
        CardModel card = play.Card;
        CardType type = card.Type;

        // §5.1：状态牌不判定，原样生效。
        if (type == CardType.Status)
        {
            return false;
        }

        // §8：诅咒牌被强制打出时转入独立 d20 诅咒判定。
        if (type == CardType.Curse)
        {
            return await JudgeCurseAsync(context);
        }

        // §5.3：没有可数值化效果（纯触发型能力牌等）→ 跳过判定，直接生效。
        if (!CardPanel.TryGetPanel(card, out decimal panel))
        {
            Log.Info($"[TRPG] {card.Title}（{type}）无数值面板 → 跳过判定，直接生效");
            return false;
        }

        // §10-1：X 费牌的面板值 = 单点面板 × X（开打前的剩余能量即 X）。
        if (card.EnergyCost.CostsX)
        {
            int x = Math.Max(1, card.EnergyCost.GetAmountToSpend());
            panel *= x;
        }

        Creature? self = play.Player?.Creature;
        int bonus = CardPanel.AttributeBonus(type, self);
        int pressure = EnemyPressure.DeltaFor(play);
        Rng dice = DiceFor(play);
        DiceRollResult roll = DiceCheck.Evaluate(panel, bonus, dice, pressure);

        // 演出：先掷骰动画揭晓点数与档位，再走后续结算（§11.1 时序）。
        await DiceUi.PlayRollAsync(roll);

        var entry = new RollEntry { Result = roll };

        // 大成功：整张牌的动态变量 ×2.5；
        // 成功：随超出 DC 的幅度在 ×1.00~×1.40 间浮动（§4 / §5）。
        // （伤害 / 格挡 / 层数 / 抽牌 / 能量…全部在同一个汇合点缩放。）
        if (roll.EffectMultiplier > 1m)
        {
            entry.ScaledVars = DynamicVarScaler.Scale(card, roll.EffectMultiplier);
        }

        RollRegistry.Set(play, entry);

        string scaled = entry.ScaledVars is { Count: > 0 } vars ? $"，增幅 {vars.Count} 项数值" : string.Empty;
        string pressed = pressure > 0 ? $"，压制 +{pressure}" : string.Empty;
        Log.Info($"[TRPG] {card.Title}（{type}，面板 {panel}{pressed}）→ {roll.Describe()}{scaled}");

        // 成功 / 大成功：原版结算照常走（大成功已在上面放大过数值）。
        if (roll.Tier is RollTier.Success or RollTier.CritSuccess)
        {
            return false;
        }

        // 大失败：不生效 + 反噬。
        if (roll.Tier == RollTier.Fumble)
        {
            await Backlash.ApplyAsync(context, roll, dice);
        }

        // 失败：整张牌不生效（返回 true 跳过卡牌自身的 OnPlay）。
        return true;
    }

    public Task AfterCardOnPlay(AfterCardOnPlayContext context)
    {
        // 还原被放大的动态变量，避免同一张牌第二次打出时继续叠乘。
        if (RollRegistry.TryGet(context.CardPlay, out RollEntry entry) && entry.ScaledVars is not null)
        {
            DynamicVarScaler.Restore(context.CardPlay.Card, entry.ScaledVars);
        }

        RollRegistry.Remove(context.CardPlay);
        return Task.CompletedTask;
    }

    /// <summary>§8：诅咒判定 —— 免疫 / 免疫并转为增益 / 照常生效 / 效果翻倍。</summary>
    private static async Task<bool> JudgeCurseAsync(BeforeCardOnPlayContext context)
    {
        Player player = context.CardPlay.Player;
        int pressure = EnemyPressure.DeltaFor(context.CardPlay);
        CurseRollResult roll = CurseCheck.Evaluate(player, pressure, DiceFor(context.CardPlay));

        // 演出：诅咒判定用单颗 d20，揭晓点数 / CR / DC 与档位（§11.2）。
        await DiceUi.PlayCurseRollAsync(roll);

        Log.Info($"[TRPG] 诅咒牌 {context.CardPlay.Card.Title} → {roll.Describe()}");

        switch (roll.Tier)
        {
            case RollTier.CritSuccess:
                await GrantCurseCritAsync(context, player);
                return true;    // 免疫

            case RollTier.Success:
                return true;    // 免疫

            case RollTier.Fumble:
                // 大失败：诅咒效果翻倍（放大其动态变量，AfterCardOnPlay 还原）。
                RollRegistry.Set(context.CardPlay, new RollEntry
                {
                    ScaledVars = DynamicVarScaler.Scale(context.CardPlay.Card, TrpgConfig.CurseFumbleMultiplier),
                });
                return false;   // 照常生效（已翻倍）

            default:
                return false;   // 失败：照常生效
        }
    }

    /// <summary>§8 大成功：免疫诅咒并转化为增益（力量 / 敏捷各 +N）。</summary>
    private static async Task GrantCurseCritAsync(BeforeCardOnPlayContext context, Player player)
    {
        Creature? self = player.Creature;
        if (self is null)
        {
            return;
        }

        Log.Info($"[TRPG] 诅咒大成功：免疫并转为增益（力量 +{TrpgConfig.CurseCritBonus}，敏捷 +{TrpgConfig.CurseCritBonus}）");
        await PowerCmd.Apply<StrengthPower>(context.ChoiceContext, self, TrpgConfig.CurseCritBonus, self, null);
        await PowerCmd.Apply<DexterityPower>(context.ChoiceContext, self, TrpgConfig.CurseCritBonus, self, null);
    }
}