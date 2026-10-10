using Godot;
using System.Diagnostics;
using TrpgMod.Scripts.Dice;

namespace TrpgMod.Scripts.Ui;

/// <summary>
/// 骰池展示面板，对应设计文档 §11.2 / §11.3。
/// 完全用代码构建节点（不依赖 .tscn / .pck），挂载在根视口的独立 CanvasLayer 上。
/// 表现层差异化：四档各有独立的配色、全屏闪光与面板动效（大成功金闪+脉冲、大失败红闪+抖动）。
/// </summary>
public partial class DicePanel : Control
{
    /// <summary>掷骰动画时长（秒）。</summary>
    private const float RollSeconds = 0.5f;

    /// <summary>结果展示停留时长（秒）。</summary>
    private const float HoldSeconds = 1.0f;

    private const int DieSize = 40;

    /// <summary>面板停靠位置（左上角），抖动动画以此为中心。</summary>
    private static readonly Vector2 PanelHome = new(24, 120);

    private PanelContainer _panel = null!;
    private ColorRect _flash = null!;
    private HBoxContainer _diceRow = null!;
    private Label _resultLabel = null!;
    private readonly List<Label> _dice = new();

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = 100;
        Visible = false;

        // 自身铺满视口，全屏闪光层才有尺寸（锚点相对于视口，因为父节点是 CanvasLayer）。
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _flash = new ColorRect
        {
            Color = new Color(1f, 1f, 1f, 0f),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _flash.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_flash);

        _panel = new PanelContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            Position = PanelHome,
        };
        _panel.AddThemeStyleboxOverride("panel", BuildPanelStyle(TierColor(RollTier.Success)));
        AddChild(_panel);

        var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _panel.AddChild(box);

        _diceRow = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        box.AddChild(_diceRow);

        _resultLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(0, 28),
        };
        _resultLabel.AddThemeFontSizeOverride("font_size", 20);
        box.AddChild(_resultLabel);
    }

    /// <summary>展示一次战斗判定：先滚动、再揭晓点数与档位，停留后自动隐藏。</summary>
    public async Task PlayRollAsync(DiceRollResult roll)
    {
        if (!IsInsideTree())
        {
            return;
        }

        Visible = true;
        ResetPresentation();
        BuildDice(roll.DiceCount);
        _resultLabel.Text = string.Empty;
        ApplyAccent(roll.Tier);

        if (!await AnimateRollAsync(TrpgConfig.DieFaces))
        {
            return;
        }

        int revealed = Math.Min(roll.Faces.Count, _dice.Count);
        for (int i = 0; i < revealed; i++)
        {
            _dice[i].Text = roll.Faces[i].ToString();
        }

        PlayTierFeedback(roll.Tier);

        string multiplier = roll.EffectMultiplier > 0m ? $"   ×{roll.EffectMultiplier:0.00}" : string.Empty;
        ShowResult($"{roll.TierName}{multiplier}   S={roll.DiceSum}   DC={roll.Dc}", roll.Tier);
        await HideAsync();
    }

    /// <summary>展示一次诅咒判定（单颗 d20）：滚动 → 揭晓点数 / CR / DC 与档位 → 停留 → 隐藏。</summary>
    public async Task PlayCurseRollAsync(CurseRollResult roll)
    {
        if (!IsInsideTree())
        {
            return;
        }

        Visible = true;
        ResetPresentation();
        BuildDice(1);
        _resultLabel.Text = string.Empty;
        ApplyAccent(roll.Tier);

        if (!await AnimateRollAsync(TrpgConfig.CurseDieFaces))
        {
            return;
        }

        if (_dice.Count > 0)
        {
            _dice[0].Text = roll.Roll.ToString();
        }

        PlayTierFeedback(roll.Tier);
        ShowResult($"{roll.TierName}   d20={roll.Roll}   CR={roll.Resistance}   DC={roll.Dc}", roll.Tier);
        await HideAsync();
    }

    /// <summary>滚动动画：把每颗骰子反复刷成 1..maxFace 的随机数，持续 RollSeconds。返回时是否仍在场景树内。</summary>
    private async Task<bool> AnimateRollAsync(int maxFace)
    {
        var rng = new RandomNumberGenerator();
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed.TotalSeconds < RollSeconds)
        {
            if (!IsInsideTree())
            {
                return false;
            }

            foreach (Label die in _dice)
            {
                die.Text = rng.RandiRange(1, maxFace).ToString();
            }

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        return true;
    }

    /// <summary>四档表现差异化：配色 + 全屏闪光 + 面板动效。</summary>
    private void PlayTierFeedback(RollTier tier)
    {
        switch (tier)
        {
            case RollTier.CritSuccess:
                Flash(new Color(1f, 0.85f, 0.2f), 0.38f);
                PulsePanel(1.18f);
                break;

            case RollTier.Fumble:
                Flash(new Color(1f, 0.15f, 0.12f), 0.45f);
                ShakePanel(10f);
                break;

            case RollTier.Success:
                Flash(new Color(0.55f, 0.9f, 0.55f), 0.16f);
                break;

            default:
                Flash(new Color(0.6f, 0.6f, 0.6f), 0.12f);
                break;
        }
    }

    /// <summary>全屏闪一下后淡出（大成功金、大失败红、成功淡绿、失败淡灰）。</summary>
    private void Flash(Color color, float alpha)
    {
        _flash.Color = new Color(color.R, color.G, color.B, alpha);
        Tween tween = _flash.CreateTween();
        tween.TweenProperty(_flash, "color:a", 0f, 0.4f).SetTrans(Tween.TransitionType.Sine);
    }

    /// <summary>大成功：面板弹一下再回弹。</summary>
    private void PulsePanel(float scale)
    {
        _panel.PivotOffset = _panel.Size / 2f;
        Tween tween = _panel.CreateTween();
        tween.TweenProperty(_panel, "scale", new Vector2(scale, scale), 0.08f);
        tween.TweenProperty(_panel, "scale", Vector2.One, 0.3f)
            .SetTrans(Tween.TransitionType.Elastic)
            .SetEase(Tween.EaseType.Out);
    }

    /// <summary>大失败：面板左右抖动。</summary>
    private void ShakePanel(float amplitude)
    {
        Tween tween = _panel.CreateTween();
        for (int i = 0; i < 4; i++)
        {
            tween.TweenProperty(_panel, "position", PanelHome + new Vector2(amplitude, 0f), 0.045f);
            tween.TweenProperty(_panel, "position", PanelHome - new Vector2(amplitude, 0f), 0.045f);
        }

        tween.TweenProperty(_panel, "position", PanelHome, 0.045f);
    }

    private void ResetPresentation()
    {
        _panel.Position = PanelHome;
        _panel.Scale = Vector2.One;
    }

    /// <summary>把档位配色应用到面板边框、结果文字与骰子边框。</summary>
    private void ApplyAccent(RollTier tier)
    {
        Color accent = TierColor(tier);
        _panel.AddThemeStyleboxOverride("panel", BuildPanelStyle(accent));
        _resultLabel.AddThemeColorOverride("font_color", accent);

        foreach (Label die in _dice)
        {
            if (die.GetParent() is PanelContainer frame)
            {
                frame.AddThemeStyleboxOverride("panel", BuildDieStyle(accent));
            }
        }
    }

    private void ShowResult(string text, RollTier tier)
    {
        _resultLabel.Text = text;
        _resultLabel.AddThemeColorOverride("font_color", TierColor(tier));
    }

    private async Task HideAsync()
    {
        await ToSignal(GetTree().CreateTimer(HoldSeconds), SceneTreeTimer.SignalName.Timeout);

        if (IsInsideTree())
        {
            Visible = false;
        }
    }

    private void BuildDice(int count)
    {
        foreach (Label die in _dice)
        {
            die.GetParent().QueueFree();
        }

        _dice.Clear();

        for (int i = 0; i < count; i++)
        {
            var frame = new PanelContainer { CustomMinimumSize = new Vector2(DieSize, DieSize) };
            frame.AddThemeStyleboxOverride("panel", BuildDieStyle(TierColor(RollTier.Success)));

            var face = new Label
            {
                Text = "?",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                CustomMinimumSize = new Vector2(DieSize, DieSize),
            };
            frame.AddChild(face);
            _diceRow.AddChild(frame);
            _dice.Add(face);
        }
    }

    private static StyleBoxFlat BuildPanelStyle(Color accent) => new()
    {
        BgColor = new Color(0.05f, 0.05f, 0.08f, 0.85f),
        BorderColor = accent,
        BorderWidthTop = 2,
        BorderWidthBottom = 2,
        BorderWidthLeft = 2,
        BorderWidthRight = 2,
        CornerRadiusTopLeft = 6,
        CornerRadiusTopRight = 6,
        CornerRadiusBottomLeft = 6,
        CornerRadiusBottomRight = 6,
        ContentMarginLeft = 14f,
        ContentMarginRight = 14f,
        ContentMarginTop = 10f,
        ContentMarginBottom = 10f,
    };

    private static StyleBoxFlat BuildDieStyle(Color accent) => new()
    {
        BgColor = new Color(0.10f, 0.10f, 0.14f, 0.95f),
        BorderColor = accent,
        BorderWidthTop = 2,
        BorderWidthBottom = 2,
        BorderWidthLeft = 2,
        BorderWidthRight = 2,
        CornerRadiusTopLeft = 4,
        CornerRadiusTopRight = 4,
        CornerRadiusBottomLeft = 4,
        CornerRadiusBottomRight = 4,
    };

    private static Color TierColor(RollTier tier) => tier switch
    {
        RollTier.CritSuccess => new Color(1f, 0.85f, 0.2f),
        RollTier.Success => new Color(0.55f, 0.9f, 0.55f),
        RollTier.Fail => new Color(0.75f, 0.75f, 0.75f),
        _ => new Color(1f, 0.35f, 0.35f),
    };
}