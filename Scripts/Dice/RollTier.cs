namespace TrpgMod.Scripts.Dice;

/// <summary>四档判定结果，对应设计文档 §4。</summary>
public enum RollTier
{
    /// <summary>大成功：原判断增幅 ×2.5。</summary>
    CritSuccess,

    /// <summary>成功：原值生效。</summary>
    Success,

    /// <summary>失败：整张牌不生效。</summary>
    Fail,

    /// <summary>大失败：不生效 + 反噬。</summary>
    Fumble,
}

/// <summary>一次判定的全部中间量，便于日志输出、骰池 UI 渲染与后续打点复用。</summary>
public readonly record struct DiceRollResult(
    decimal PanelValue,
    int DiceCount,
    IReadOnlyList<int> Faces,
    int DiceSum,
    int AttributeBonus,
    int Dc,
    int CritThreshold,
    int FumbleThreshold,
    RollTier Tier)
{
    /// <summary>
    /// 本次判定对卡牌数值的实际倍率（§4 / §5）：
    /// 大成功 = <see cref="TrpgConfig.CritMultiplier"/>；
    /// 成功 = 1 + <see cref="TrpgConfig.SuccessFloatMax"/> × (骰和 − DC) / (大成功阈 − DC)，即随超出 DC 的幅度在 1.00~1.40 之间浮动；
    /// 失败 / 大失败 = 0（整张牌不生效）。
    /// </summary>
    public decimal EffectMultiplier
    {
        get
        {
            switch (Tier)
            {
                case RollTier.CritSuccess:
                    return TrpgConfig.CritMultiplier;

                case RollTier.Success:
                {
                    int span = CritThreshold - Dc;
                    if (span <= 0)
                    {
                        return 1m;
                    }

                    decimal excess = Math.Clamp(DiceSum - Dc, 0, span);
                    return 1m + TrpgConfig.SuccessFloatMax * excess / span;
                }

                default:
                    return 0m;
            }
        }
    }

    public string TierName => Tier switch
    {
        RollTier.CritSuccess => "大成功",
        RollTier.Success => "成功",
        RollTier.Fail => "失败",
        _ => "大失败",
    };

    /// <summary>
    /// 形如「大成功（8d6=28，DC=20，大成功阈=33，大失败阈=21，倍率 ×2.50）（属性 +3 已计入 DC）」。
    /// 属性加值 / 敌人压制已并入 DC，骰和只由骰子决定。
    /// </summary>
    public string Describe()
    {
        string bonus = AttributeBonus > 0 ? $"（属性 +{AttributeBonus} 已计入 DC）" : string.Empty;
        return $"{TierName}（{DiceCount}d6={DiceSum}，DC={Dc}，大成功阈={CritThreshold}，大失败阈={FumbleThreshold}，倍率 ×{EffectMultiplier:0.00}）{bonus}";
    }
}