using Godot;
using MegaCrit.Sts2.Core.Logging;
using TrpgMod.Scripts.Dice;

namespace TrpgMod.Scripts.Ui;

/// <summary>
/// 骰池 UI 的接线层，对应设计文档 §11.2：
/// 把 <see cref="DicePanel"/> 挂到根视口的独立 CanvasLayer 上（不依赖战斗界面 NCombatUi 结构），
/// 并给判定层提供播放入口。
/// </summary>
public static class DiceUi
{
    /// <summary>面板所在 CanvasLayer 的层级（越高越靠前，Godot 上限 128）。</summary>
    private const int LayerIndex = 128;

    /// <summary>当前骰池面板；首次播放时惰性创建。</summary>
    private static DicePanel? _panel;

    /// <summary>在 Entry.Init 中调用一次。面板改为首次播放时惰性创建，此处无需注册。</summary>
    public static void Register()
    {
    }

    /// <summary>取（必要时创建）骰池面板；创建一次后随根视口长期存活。</summary>
    private static DicePanel? EnsurePanel()
    {
        if (_panel is not null && GodotObject.IsInstanceValid(_panel) && _panel.IsInsideTree())
        {
            return _panel;
        }

        if (Engine.GetMainLoop() is not SceneTree tree || tree.Root is null)
        {
            return null;
        }

        var layer = new CanvasLayer { Layer = LayerIndex, Name = "TrpgDiceLayer" };
        tree.Root.AddChild(layer);

        _panel = new DicePanel { Name = "TrpgDicePanel" };
        layer.AddChild(_panel);

        Log.Info("[TRPG] 骰池 UI 已挂载到根视口（CanvasLayer 128）");
        return _panel;
    }

    /// <summary>
    /// 播放一次掷骰演出。UI 只是演出层——任何异常都不影响判定结果，故内部吞掉并记录日志。
    /// </summary>
    public static async Task PlayRollAsync(DiceRollResult roll)
    {
        DicePanel? panel = EnsurePanel();
        if (panel is null)
        {
            return;
        }

        try
        {
            await panel.PlayRollAsync(roll);
        }
        catch (Exception ex)
        {
            Log.Warn($"[TRPG] 骰池 UI 播放失败（不影响判定）：{ex.Message}");
        }
    }

    /// <summary>播放一次诅咒判定（单颗 d20）演出；同样吞掉异常，不影响判定结果。</summary>
    public static async Task PlayCurseRollAsync(CurseRollResult roll)
    {
        DicePanel? panel = EnsurePanel();
        if (panel is null)
        {
            return;
        }

        try
        {
            await panel.PlayCurseRollAsync(roll);
        }
        catch (Exception ex)
        {
            Log.Warn($"[TRPG] 诅咒骰池 UI 播放失败（不影响判定）：{ex.Message}");
        }
    }
}