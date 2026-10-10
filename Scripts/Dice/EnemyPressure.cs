using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace TrpgMod.Scripts.Dice;

/// <summary>
/// 敌人压制 Δ：加到 DC 上，抵消玩家随幕数变强带来的判定优势。
/// 对应设计文档 §6：Act1 0 / Act2 +1 / Act3 +2，精英再 +1，Boss 再 +2。
/// </summary>
public static class EnemyPressure
{
    public static int DeltaFor(CardPlay play)
    {
        // Player.RunState 是 IRunState；幕数在接口上，当前房间只在 RunState 上。
        if (play.Player?.RunState is not RunState run)
        {
            return 0;
        }

        int delta = run.CurrentActIndex switch
        {
            <= 0 => 0,
            1 => TrpgConfig.PressureAct2,
            _ => TrpgConfig.PressureAct3,
        };

        RoomType roomType = run.CurrentRoom?.RoomType ?? RoomType.Unassigned;
        delta += roomType switch
        {
            RoomType.Elite => TrpgConfig.PressureElite,
            RoomType.Boss => TrpgConfig.PressureBoss,
            _ => 0,
        };

        return delta;
    }
}