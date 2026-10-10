# mod-SlaythSpire2-DND

塔2跑团mod — 把《杀戮尖塔2》的战斗结算改造为 d6 骰池 + 四档判定。每打出一张牌都会掷一次骰池，结果分为**大成功（×2.5）/ 成功（×1.0~×1.4）/ 失败（整张牌不生效）/ 大失败（不生效 + 反噬）**。

## 安装（玩家）

1. 先装前置 mod **STS2-RitsuLib ≥ 0.6.7**，放进 `Slay the Spire 2\mods\`（[项目地址](https://github.com/anyer1/STS2-RitsuLib)）
2. 从 [Releases](../../releases) 下载 `TrpgMod.zip`，解压得到 `TrpgMod` 文件夹
3. 把 `TrpgMod` 整个丢进 `Slay the Spire 2\mods\`，目录结构应为：

   ```
   Slay the Spire 2\mods\
   ├─ STS2-RitsuLib\
   └─ TrpgMod\
      ├─ TrpgMod.dll
      └─ TrpgMod.json
   ```

4. 启动游戏。日志见 `%APPDATA%\SlayTheSpire2\logs\godot.log`，出现 `TrpgMod initialized!` 即加载成功

> 游戏版本需匹配 `TrpgMod.json` 中声明的 `min_game_version`（当前 **0.111.0**），版本不符会加载失败。

## 其他

- 设计文档：[docs/设计文档.md](docs/设计文档.md)（骰池转化、判定分级参数表、诅咒 CR 表）
- 技术基线：STS2 v0.111.0 · Godot 4.5.1 Mono · .NET 9 · RitsuLib 0.6.7
- 从源码构建：`dotnet build`（需改 `TrpgMod.csproj` 里的 `<Sts2Dir>` 指向自己的游戏目录；构建后自动部署到 `mods\TrpgMod`）
