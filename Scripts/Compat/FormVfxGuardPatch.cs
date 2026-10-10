using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace TrpgMod.Scripts.Compat;

/// <summary>
/// 兼容性守卫（原版崩溃修复）：
/// 部分自定义角色 Mod 的贴图场景不含 %FormVfx 节点，导致 <see cref="NCreatureVisuals.FormVfxHolder"/>
/// 恒为 null；而原版 AddFormVfx / RemoveFormVfx 在 holder 为 null 时直接抛 InvalidOperationException。
/// 生物死亡时 StartDeathAnim 会调用 RemoveFormVfx，异常会一路击穿
/// CreatureCmd.Kill → AttackCommand → 怪物行动 → 战斗回合循环，日志报
/// 「turn loop died ... the combat is stuck until the room is restarted」，战斗彻底卡死只能强退。
///
/// 处理：holder 为 null 时直接跳过原方法（没有 form vfx 可加/可移除，等价于空操作）。
/// 正常角色（场景里带 %FormVfx）行为完全不变。
/// </summary>
[HarmonyPatch]
public static class FormVfxGuardPatch
{
    [HarmonyPatch(typeof(NCreatureVisuals), nameof(NCreatureVisuals.RemoveFormVfx))]
    [HarmonyPrefix]
    private static bool RemoveFormVfxPrefix(NCreatureVisuals __instance)
    {
        if (__instance.FormVfxHolder is not null)
        {
            return true;
        }

        Log.Warn("[TRPG] 该生物贴图缺少 %FormVfx 节点，跳过 RemoveFormVfx（避免战斗卡死）");
        return false;
    }

    [HarmonyPatch(typeof(NCreatureVisuals), nameof(NCreatureVisuals.AddFormVfx))]
    [HarmonyPrefix]
    private static bool AddFormVfxPrefix(NCreatureVisuals __instance)
    {
        if (__instance.FormVfxHolder is not null)
        {
            return true;
        }

        Log.Warn("[TRPG] 该生物贴图缺少 %FormVfx 节点，跳过 AddFormVfx");
        return false;
    }
}