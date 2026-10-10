namespace TrpgMod.Scripts;

/// <summary>
/// 判定系统的可调旋钮，对应设计文档 §9。
/// 改这里的数值就能整体调平衡，不需要动其它文件。
/// </summary>
public static class TrpgConfig
{
    // ---- §3 骰池转化 ----
    /// <summary>骰面数（d6）。</summary>
    public const int DieFaces = 6;

    /// <summary>面板值 → 骰数的换算系数（1 / 单骰期望 3.5）。</summary>
    public const double PoolFactor = 1.0 / 3.5;

    /// <summary>骰数下限。</summary>
    public const int MinDice = 1;

    /// <summary>骰数上限（与渲染容量一致）。</summary>
    public const int MaxDice = 8;

    // ---- §4 判定分级（阈值由精确组合数分布反推，不再用正态近似）----
    /// <summary>DC 目标概率：P(S ≥ DC) 取最接近该值的最小阈值（成功 + 大成功）。</summary>
    public const double TargetSuccOrBetter = 0.84;

    /// <summary>大成功目标概率：P(S ≥ 大成功阈) 取最接近该值的最小阈值。</summary>
    public const double TargetCrit = 0.09;

    /// <summary>大失败目标概率：P(S &lt; 大失败阈) 首次达到该值时的最小阈值。</summary>
    public const double TargetFumble = 0.06;

    // ---- §5 牌型效果 ----
    /// <summary>大成功增幅系数。</summary>
    public const decimal CritMultiplier = 2.5m;

    /// <summary>
    /// 成功档内部浮动幅度上限：倍率 = 1 + 该系数 × (骰和 − DC) / (大成功阈 − DC)。
    /// 即刚好过线 = ×1.00，贴到大成功阈 = ×(1 + 该系数)。四档阈值与概率完全不受影响。
    /// </summary>
    public const decimal SuccessFloatMax = 0.4m;

    /// <summary>大失败反噬倍率（攻击 / 技能自身受伤 = 面板值 × 倍率）。</summary>
    public const decimal BacklashRatio = 0.5m;

    // ---- §6 属性分轨 ----
    /// <summary>力量 / 敏捷 每多少点换 +1 判定加值。</summary>
    public const int AttrPerPoint = 3;

    /// <summary>判定加值上限。</summary>
    public const int AttrBonusCap = 3;

    // ---- §6 敌人压制 Δ（加到 DC 上）----
    /// <summary>Act2 压制。</summary>
    public const int PressureAct2 = 1;

    /// <summary>Act3 压制。</summary>
    public const int PressureAct3 = 2;

    /// <summary>精英房额外压制。</summary>
    public const int PressureElite = 1;

    /// <summary>Boss 房额外压制。</summary>
    public const int PressureBoss = 2;

    // ---- §7 诅咒抗性 CR ----
    /// <summary>每多少张牌（牌组）贡献 1 点 CR。</summary>
    public const int CurseCardsPerCr = 4;

    /// <summary>每多少个遗物贡献 1 点 CR。</summary>
    public const int CurseRelicsPerCr = 3;

    /// <summary>CR 上限。</summary>
    public const int CurseCrCap = 15;

    // ---- §8 诅咒判定（独立 d20）----
    /// <summary>d20 面数。</summary>
    public const int CurseDieFaces = 20;

    /// <summary>诅咒判定基础 DC（再加敌人压制 Δ，见 §6）。</summary>
    public const int CurseBaseDc = 15;

    /// <summary>大成功时给予的增益层数（力量 / 敏捷各 +N）。</summary>
    public const int CurseCritBonus = 1;

    /// <summary>大失败时诅咒效果的翻倍系数。</summary>
    public const decimal CurseFumbleMultiplier = 2.0m;
}