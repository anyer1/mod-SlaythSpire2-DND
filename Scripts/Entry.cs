using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using STS2RitsuLib;
using STS2RitsuLib.Interop;
using TrpgMod.Scripts.Content;
using TrpgMod.Scripts.Ui;

namespace TrpgMod.Scripts;

// 注册 Mod 的入口类，字符串需与初始化函数名一致
[ModInitializer(nameof(Init))]
public class Entry
{
    // 你的 modid，需与 TrpgMod.json 中的 id 保持一致
    public const string ModId = "TrpgMod";
    public static readonly Logger Logger = RitsuLibFramework.CreateLogger(ModId);

    private static Harmony? _harmony;

    public static void Init()
    {
        var assembly = Assembly.GetExecutingAssembly();

        // 兼容性守卫补丁（原版崩溃修复）：见 Scripts/Compat/FormVfxGuardPatch.cs
        _harmony = new Harmony(ModId);
        _harmony.PatchAll(assembly);

        // 让 .tscn 能加载本程序集中的自定义脚本
        RitsuLibFramework.EnsureGodotScriptsRegistered(assembly, Logger);
        // 自动注册本程序集中的内容（卡牌/遗物/能力等）
        ModTypeDiscoveryHub.RegisterModAssembly(ModId, assembly);

        // 判定层：每张牌打出时掷骰定档 + 大成功放大数值
        RitsuLibFramework.RegisterCardOnPlayHookListener(new DiceJudgementListener());

        // 骰池 UI：随战斗界面就绪自动挂载（代码构建，无需 .pck）
        DiceUi.Register();

        Log.Info("TrpgMod initialized!");
    }
}