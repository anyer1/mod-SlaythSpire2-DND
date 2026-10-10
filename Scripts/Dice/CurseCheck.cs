using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Random;

namespace TrpgMod.Scripts.Dice;

/// <summary>一次诅咒判定的中间量，对应设计文档 §7 / §8。</summary>
public readonly record struct CurseRollResult(
    int Resistance,
    int Dc,
    int Roll,
    int Total,
    RollTier Tier)
{
    public string TierName => Tier switch
    {
        RollTier.CritSuccess => "大成功",
        RollTier.Success => "成功",
        RollTier.Fail => "失败",
        _ => "大失败",
    };

    /// <summary>形如「成功（d20=13 +CR 4=17，DC=15）」。</summary>
    public string Describe() => $"{TierName}（d20={Roll} +CR {Resistance}={Total}，DC={Dc}）";
}

/// <summary>
/// 诅咒判定内核（独立 d20 系统），对应设计文档 §7 / §8。
/// CR（诅咒抗性）由牌组牌数与遗物数决定，作为玩家侧加值；DC = 基础 DC + 敌人压制 Δ。
/// </summary>
public static class CurseCheck
{
    /// <summary>CR = clamp(floor(牌数 / 4 + 遗物数 / 3), 0, 15)。</summary>
    public static int ResistanceFor(Player player)
    {
        int cards = player.Deck.Cards.Count();
        int relics = player.Relics.Count;
        int cr = cards / TrpgConfig.CurseCardsPerCr + relics / TrpgConfig.CurseRelicsPerCr;
        return Math.Clamp(cr, 0, TrpgConfig.CurseCrCap);
    }

    /// <summary>
    /// 掷 d20 定档：自然 20 = 大成功，自然 1 = 大失败，其余按 `d20 + CR ≥ DC` 分成功 / 失败。
    /// </summary>
    public static CurseRollResult Evaluate(Player player, int pressureDelta, Rng rng)
    {
        int cr = ResistanceFor(player);
        int dc = TrpgConfig.CurseBaseDc + pressureDelta;
        int roll = rng.NextInt(1, TrpgConfig.CurseDieFaces + 1);
        int total = roll + cr;

        RollTier tier;
        if (roll == TrpgConfig.CurseDieFaces)
        {
            tier = RollTier.CritSuccess;
        }
        else if (roll == 1)
        {
            tier = RollTier.Fumble;
        }
        else
        {
            tier = total >= dc ? RollTier.Success : RollTier.Fail;
        }

        return new CurseRollResult(cr, dc, roll, total, tier);
    }
}