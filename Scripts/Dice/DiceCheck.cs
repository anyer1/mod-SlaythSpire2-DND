using MegaCrit.Sts2.Core.Random;

namespace TrpgMod.Scripts.Dice;

/// <summary>
/// 判定内核（纯逻辑，无游戏依赖），对应设计文档 §3 / §4。
/// 阈值不再用正态近似，而是对 n 颗 d6 的精确组合数分布直接反推，
/// 使四档概率在小骰池（n = 1、2）下也稳定，并让 n = 1 也能出现极值档。
/// </summary>
public static class DiceCheck
{
    /// <summary>n 颗 d6 掷出总和 s 的方案数（索引即 s，未覆盖的和为 0）。按骰数建表，n = 0 表示「一个都不掷」。</summary>
    private static readonly long[][] SumCounts = BuildSumCounts();

    /// <summary>各骰数的总方案数 6^n。</summary>
    private static readonly long[] TotalCounts = BuildTotalCounts();

    /// <summary>
    /// DC 表（不含加值与压制）：取使 P(S ≥ t) 最接近 <see cref="TrpgConfig.TargetSuccOrBetter"/> 的最小 t。
    /// </summary>
    private static readonly int[] DcByDice = BuildAtLeastThresholds(TrpgConfig.TargetSuccOrBetter);

    /// <summary>
    /// 大成功阈表：取使 P(S ≥ t) 最接近 <see cref="TrpgConfig.TargetCrit"/> 的最小 t。
    /// n = 1 时自然得到 6，即「掷 6 大成功」。
    /// </summary>
    private static readonly int[] CritByDice = BuildAtLeastThresholds(TrpgConfig.TargetCrit);

    /// <summary>
    /// 大失败阈表：取使 P(S &lt; t) 首次 ≥ <see cref="TrpgConfig.TargetFumble"/> 的最小 t。
    /// n = 1 时自然得到 2，即「掷 1 大失败」。
    /// </summary>
    private static readonly int[] FumbleByDice = BuildFumbleThresholds();

    /// <summary>面板值 → 骰数：n = clamp(round(面板值 / 3.5), 1, 8)。</summary>
    public static int DiceCountFor(decimal panelValue)
    {
        double raw = (double)panelValue * TrpgConfig.PoolFactor;
        int n = (int)Math.Round(raw, MidpointRounding.AwayFromZero);
        return Math.Clamp(n, TrpgConfig.MinDice, TrpgConfig.MaxDice);
    }

    /// <summary>
    /// 掷骰并定档。
    /// 属性加值与敌人压制都作用在 DC 上（<c>DC = 基准 − 属性加值 + 压制</c>），骰和只由骰子决定，
    /// 因此加值只影响成功 / 失败的边界，不会改变大成功与大失败的概率（§4）。
    /// </summary>
    public static DiceRollResult Evaluate(decimal panelValue, int attributeBonus, Rng rng, int pressureDelta = 0)
    {
        int n = DiceCountFor(panelValue);

        var faces = new int[n];
        int sum = 0;
        for (int i = 0; i < n; i++)
        {
            faces[i] = rng.NextInt(1, TrpgConfig.DieFaces + 1);
            sum += faces[i];
        }

        int dc = DcByDice[n] - attributeBonus + pressureDelta;
        int crit = CritByDice[n];
        int fumble = FumbleByDice[n];

        // 极值档先判：大成功 / 大失败只由骰和决定，属性加值与敌人压制都改不动它们。
        // 若先判 DC，加值很高时 DC 会跌到大失败阈以下，把大失败档整段吞掉（n = 1 掷 1 就不再是大失败）。
        RollTier tier;
        if (sum >= crit)
        {
            tier = RollTier.CritSuccess;
        }
        else if (sum < fumble)
        {
            tier = RollTier.Fumble;
        }
        else if (sum >= dc)
        {
            tier = RollTier.Success;
        }
        else
        {
            tier = RollTier.Fail;
        }

        return new DiceRollResult(panelValue, n, faces, sum, attributeBonus, dc, crit, fumble, tier);
    }

    /// <summary>精确组合数 DP：SumCounts[n][s] = n 颗 d6 掷出总和 s 的方案数。</summary>
    private static long[][] BuildSumCounts()
    {
        int max = TrpgConfig.MaxDice;
        var table = new long[max + 1][];
        table[0] = new long[] { 1 };

        for (int n = 1; n <= max; n++)
        {
            long[] prev = table[n - 1];
            var current = new long[TrpgConfig.DieFaces * n + 1];
            for (int s = 0; s < prev.Length; s++)
            {
                if (prev[s] == 0)
                {
                    continue;
                }

                for (int face = 1; face <= TrpgConfig.DieFaces; face++)
                {
                    current[s + face] += prev[s];
                }
            }

            table[n] = current;
        }

        return table;
    }

    private static long[] BuildTotalCounts()
    {
        var counts = new long[TrpgConfig.MaxDice + 1];
        long total = 1;
        for (int n = 0; n <= TrpgConfig.MaxDice; n++)
        {
            counts[n] = total;
            total *= TrpgConfig.DieFaces;
        }

        return counts;
    }

    /// <summary>
    /// 反推「达成或超过」型阈值：在 t ∈ [最小可能骰和, 6n + 1] 中取概率最接近 target 的**最小** t。
    /// 上界 6n + 1 表示 P = 0（永远无法达成），作为兜底项。
    /// </summary>
    private static int[] BuildAtLeastThresholds(double target)
    {
        var table = new int[TrpgConfig.MaxDice + 1];
        for (int n = TrpgConfig.MinDice; n <= TrpgConfig.MaxDice; n++)
        {
            int best = TrpgConfig.DieFaces * n + 1;
            double bestDiff = double.MaxValue;

            for (int t = n; t <= TrpgConfig.DieFaces * n + 1; t++)
            {
                double diff = Math.Abs(ProbabilityAtLeast(n, t) - target);
                if (diff < bestDiff - 1e-9)
                {
                    bestDiff = diff;
                    best = t;
                }
            }

            table[n] = best;
        }

        return table;
    }

    /// <summary>反推大失败阈值：取使 P(S &lt; t) 首次 ≥ target 的最小 t。</summary>
    private static int[] BuildFumbleThresholds()
    {
        var table = new int[TrpgConfig.MaxDice + 1];
        for (int n = TrpgConfig.MinDice; n <= TrpgConfig.MaxDice; n++)
        {
            long[] counts = SumCounts[n];
            long cumulative = 0;
            int chosen = n;     // 兜底：连最小骰和都达不到目标概率时，取最小可能骰和（即没有大失败档）

            for (int t = n + 1; t <= TrpgConfig.DieFaces * n + 1; t++)
            {
                cumulative += counts[t - 1];    // 累加 s = t − 1，累加完即 P(S < t) 的分子
                if ((double)cumulative / TotalCounts[n] >= TrpgConfig.TargetFumble)
                {
                    chosen = t;
                    break;
                }
            }

            table[n] = chosen;
        }

        return table;
    }

    /// <summary>P(S ≥ t)：n 颗 d6 掷出总和 ≥ t 的精确概率。</summary>
    private static double ProbabilityAtLeast(int n, int t)
    {
        long[] counts = SumCounts[n];
        long atLeast = 0;
        for (int s = Math.Max(t, 0); s < counts.Length; s++)
        {
            atLeast += counts[s];
        }

        return (double)atLeast / TotalCounts[n];
    }
}