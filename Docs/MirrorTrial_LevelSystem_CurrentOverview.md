# 《镜中试炼》关卡系统当前总览

> 更新时间：2026-07-23  
> 本文以当前仓库内容为准，用于统一说明关卡资产、场景、配置、编辑工具、运行时组件、制作流程与已知风险。旧版 PRD 和使用手册仍可作为设计背景，但若与本文冲突，应以代码和本文记录的“当前实现”为准。

## 1. 当前关卡体系

当前项目采用两套互相配合的关卡表达：

1. **Scene 场景**：保存最终的 Unity 层级、碰撞、表现、手工摆放物与运行时组件。
2. **LevelConfig 配置资产**：保存可序列化的地形、段落、刷怪点、战斗、门、镜子门和触发器数据，可生成 Scene，也可由 Scene 反向导出。

核心数据流为：

```text
LevelConfig --从配置生成场景--> Unity Scene
Unity Scene --从场景导出配置--> LevelConfig
```

`LevelManager` 是每个正式关卡场景的总控入口。场景内容建议分成：

```text
LevelManager
├─ 地形 / Geometry：平台、边界、碰撞、背景与装饰
├─ 玩法 / Gameplay：段落、触发器、战斗、门、镜子门、刷怪点
└─ 运行时 / Runtime：运行时生成的敌人、特效和临时对象
```

## 2. 当前场景清单

目录：`Assets/MirrorTrial/Scenes`

| 场景 | 当前用途与状态 |
| --- | --- |
| `Level_Reality_01.unity` | 第一关现实层，当前主要正式场景。 |
| `Level_Mirror_01.unity` | 第一关镜中层，由现实层的 `MirrorGate_Blade` 指向。 |
| `Level_Reality_02.unity` | 第二关现实层，已包含大量丛林关卡与美术摆放内容。 |
| `Level_Mirror_02.unity` | 第二关镜中层，当前体积较小，仍需确认玩法与美术完成度。 |
| `level_01.unity` | 旧版或遗留第一关场景；命名方式与当前规范不同。 |
| `MirrorTrial_TestGym.unity` | 测试场，用于角色、战斗和机制调试，不属于正式关卡流程。 |

### Build Settings 当前状态

`ProjectSettings/EditorBuildSettings.asset` 当前启用了：

- `Assets/Scenes/SampleScene.unity`
- `Assets/MirrorTrial/Scenes/Level_Reality_01_temp.unity`
- `Assets/MirrorTrial/Scenes/Level_Mirror_01.unity`

其中 `Level_Reality_01_temp.unity` 已不在场景目录中，是失效引用。当前 `Level_Reality_01.unity`、`Level_Reality_02.unity`、`Level_Mirror_02.unity` 尚未全部正确加入 Build Settings。正式打包前必须修复。

## 3. LevelConfig 配置清单

目录：`Assets/MirrorTrial/LevelConfigs`

| 配置文件 | `levelId` | 当前内容 |
| --- | --- | --- |
| `Level_Reality_01.asset` | `Level_Reality_01` | 第一关主配置；含 11 个地形条目、1 个战斗区和 1 个镜子门。 |
| `Level_Mirror_01.asset` | `Level_Mirror_01` | 镜中关卡 1；当前配置列表基本为空。 |
| `Level_Reality_02.asset` | `Level_Reality_02` | 现实关卡 2；当前配置列表基本为空。 |
| `Level_Mirror_02.asset` | `Level_Mirror_02` | 镜中关卡 2；当前配置列表基本为空。 |
| `Level_Reality_01 1.asset` ～ `Level_Reality_01 5.asset` | 均为 `Level_Reality_01` | 历史重复配置，内容基本为空，不应继续作为正式配置使用。 |

第一关主配置当前记录：

- 玩家出生点：`(-14.44, -1.76, 0)`
- 战斗区：`Combat_A_01`
- 镜子门：`MirrorGate_Blade`
- 镜中目标场景：`Level_Mirror_01`
- 镜子门耐久：3
- 当前没有序列化段落、刷怪点、门或触发器
- `Combat_A_01` 当前没有波次

重要：Level 02 的正式 Scene 已有较多内容，但对应 `LevelConfig` 基本为空。此时直接“从配置生成场景”会生成接近空白的新场景；若确认覆盖，将破坏当前手工搭建结果。应先从正式 Scene 导出配置并核对，再把配置作为生成源。

## 4. LevelConfig 数据结构

定义文件：`Assets/MirrorTrial/Scripts/LevelConfig.cs`

`LevelConfig` 包含：

- 基础信息：`levelId`、`displayName`、`isMirrorLevel`
- 预制体引用：玩家、默认敌人、默认地形精灵
- 玩家出生点
- `geometry`：地形、边界、平台及平台视觉编号
- `segments`：段落范围与初始启用状态
- `spawnPoints`：刷怪位置、职责、敌人、AI Profile、生命和速度覆盖值、朝向
- `encounters`：战斗区、清场条件、锁门列表和波次
- `gates`：普通区域门
- `mirrorGates`：镜子门、目标镜中场景、耐久、奖励、返回点与下一段
- `triggers`：触发时机、条件和动作

### 触发器能力

触发时机：

- 手动触发
- 玩家进入 / 离开
- 战斗清场
- Boss 击败
- 镜子门击碎 / 完成
- 段落启用

条件：

- 段落未完成
- 战斗未开始 / 已清场
- 镜子门已击碎 / 完好
- 玩家拥有能力

动作：

- 开始战斗 / 指定波次
- 关门 / 开门
- 击碎镜子门
- 传送玩家
- 解锁能力
- 启用段落
- 播放提示

## 5. 运行时核心组件

目录：`Assets/MirrorTrial/Scripts/Level`

| 组件 | 职责 |
| --- | --- |
| `LevelManager` | 关卡总控、根节点管理、玩法对象收集和玩家出生。 |
| `LevelSegment` | 表示关卡分段和启用状态。 |
| `LevelTrigger` | 在指定时机检查条件并执行动作。 |
| `CombatEncounter` | 启动战斗、控制波次、记录存活敌人、清场和锁门。 |
| `SpawnPoint` | 定义敌人出生位置、默认敌人、AI 参数和朝向。 |
| `AreaGate` | 控制普通门的开启、关闭、碰撞和表现。 |
| `MirrorGate` | 管理镜子门状态、耐久、镜中场景、奖励与下一段。 |
| `MirrorTransitionBridge` | 在现实层与镜中层之间切换，并保存返回上下文。 |
| `MirrorReturnOnClear` | 监听镜中战斗清场并触发返回现实层。 |
| `MirrorSceneCameraIntro` | 镜中场景载入后的镜头引导与战斗启动。 |
| `MirrorShatterEffect` | 镜子破碎表现。 |
| `MirrorBattleVisuals` / `MirrorReflectionController` | 镜中战斗和镜面表现。 |
| `LevelExit` | 进入出口后加载指定下一场景。 |
| `GameFlow` | 管理入口场景、第一现实关卡及现实关卡顺序。 |
| `LevelValidationUtility` | 检查关卡对象配置完整性。 |
| 相机相关组件 | `CameraDirector`、镜头预设、目标、序列触发、跟随与边界限制。 |

镜中关卡由配置生成时，会自动添加 `MirrorReturnOnClear`；若场景只有一个战斗区，会自动绑定该战斗区。存在多个战斗区时必须人工确认监听目标。

## 6. 编辑器工具

### Tools/镜像试炼/关卡

实现文件：`Assets/MirrorTrial/Editor/Level/LevelConfigMenu.cs`

- **从场景导出当前关卡配置**：读取当前 Scene 中的 `LevelManager` 和玩法对象，写回配置资产。
- **从配置生成场景**：选择一个 `LevelConfig` 后生成同名 Scene；同名场景存在时会要求确认覆盖。
- **新建现实关卡**：扫描配置和场景，创建下一个 `Level_Reality_XX`。
- **新建镜中关卡**：扫描配置和场景，创建下一个 `Level_Mirror_XX`。
- **批量生成所有配置场景**：对全部配置执行生成。该操作风险最高，尤其是在配置为空或存在重复 `levelId` 时。

当前新建逻辑会自动递增编号，不再固定覆盖 `Level_Reality_01`。例如已有 `Level_Reality_01`、`Level_Reality_02` 后，下一个现实关卡是 `Level_Reality_03`。

### 关卡编辑器

菜单：`Tools/镜像试炼/关卡编辑器`

`LevelEditorWindow` 和 `LevelManagerEditor` 提供关卡对象创建、收集、编辑与校验入口。推荐通过 `LevelManager` 管理 Gameplay 对象，避免把逻辑组件散落到场景根节点。

### Level 02 专用工具

目录：`Assets/MirrorTrial/Editor/Level`

- `MirrorTrial/Level 02/Build Jungle Art Only`
- `MirrorTrial/Level 02/Build Route Colliders Only`
- `MirrorTrial/Level 02/Dress Art From Route Colliders`
- `MirrorTrial/Level 02/Slice Platform Pillar`

相关实现：

- `Level02JungleArtImporter`
- `Level02JungleArtBuilder`
- `Level02JungleCollisionBuilder`
- `Level02PlatformArtDresser`
- `Level02PlatformPillarSlicer`

这些工具用于第二关丛林美术导入、场景装饰、路线碰撞和平台柱体切片。美术规范见 `Assets/MirrorTrial/Docs/Level_02_ArtStyle.md`。

## 7. 推荐的安全制作流程

### 新建关卡

1. 使用“新建现实关卡”或“新建镜中关卡”，让工具分配下一个编号。
2. 确认生成的配置和 Scene 名称一致。
3. 在 Scene 中通过 `LevelManager` 搭建地形和 Gameplay。
4. 运行关卡校验。
5. 从 Scene 导出配置。
6. 检查配置内容不是空列表。
7. 只有在确认配置完整后，才测试“从配置生成场景”。
8. 把正式场景加入 Build Settings，并移除失效场景。

### 编辑已有手工场景

1. 先保存 Scene，并提交 Git 或制作备份。
2. 不要直接执行“批量生成所有配置场景”。
3. 先从 Scene 导出配置。
4. 对比配置中的地形、战斗、镜门和触发器数量。
5. 如需回生成，先生成到测试路径或确保已有可恢复版本。

### 现实层与镜中层

1. 现实层 `MirrorGate.mirrorSceneName` 必须与镜中 Scene 文件名完全一致。
2. 镜中场景应设置 `isMirrorLevel = true`。
3. 镜中场景至少应有一个明确的目标 `CombatEncounter`。
4. 清场后由 `MirrorReturnOnClear` 调用 `MirrorTransitionBridge` 返回现实层。
5. 返回后再处理奖励能力、镜门状态和下一段开放。

## 8. 命名规范

| 类型 | 推荐格式 | 示例 |
| --- | --- | --- |
| 现实场景 / 配置 | `Level_Reality_XX` | `Level_Reality_02` |
| 镜中场景 / 配置 | `Level_Mirror_XX` | `Level_Mirror_02` |
| 段落 | `SEG_区域_用途` | `SEG_B_Combat` |
| 战斗区 | `Combat_区域_序号` | `Combat_A_01` |
| 波次 | `Wave_XX` 或 `WXX_用途` | `W02_Crossfire` |
| 刷怪点 | `SP_区域_序号_职责` | `SP_B02_Ranged` |
| 门 | `Gate_区域_用途` | `Gate_B_Exit` |
| 镜子门 | `MirrorGate_能力或用途` | `MirrorGate_Blade` |
| 触发器 | `TR_来源_结果` | `TR_CombatB_Clear_OpenGate` |

`levelId` 必须全局唯一，并与正式配置文件名、Scene 文件名一致。不要通过复制 `.asset` 文件制造同 ID 的配置。

## 9. 当前已知风险与待处理事项

### P0：应尽快处理

1. **Build Settings 有失效场景**：仍指向已删除的 `Level_Reality_01_temp.unity`。
2. **重复配置 ID**：`Level_Reality_01 1.asset`～`5.asset` 与主配置共享 `Level_Reality_01`，会影响自动编号、批量生成和人工选择。
3. **Level 02 Scene 与配置不同步**：现实层场景内容丰富，但 `Level_Reality_02.asset` 基本为空。
4. **批量生成有覆盖风险**：批量入口没有逐场景确认；空配置和重复 ID 可能生成空场景或覆盖同名场景。

### P1：制作前应核对

1. 第一关主配置中的 `Combat_A_01` 没有波次，无法依靠配置完整复现战斗。
2. 第一关主配置没有段落、刷怪点、门和触发器，Scene 中若存在这些内容应重新导出。
3. `Level_Mirror_01`、`Level_Mirror_02` 配置均为空，应确认镜中场景是否以手工 Scene 为准。
4. `level_01.unity` 与当前命名规范不一致，应确认是历史备份、测试场还是仍被运行时代码引用。
5. 所有跨场景加载名称都要求目标 Scene 已加入 Build Settings。

## 10. 相关文档

- `Docs/MirrorTrial_LevelEditor_Manual.md`：旧版 LevelManager 编辑说明，部分字段和流程可能已过时。
- `Docs/MirrorTrial_LevelManager_UsageGuide.md`：系统评估、搭建教程与早期风险分析。
- `Docs/MirrorTrial_LevelEditor_PRD.md`：关卡编辑器产品需求背景。
- `Docs/关卡编辑器SO驱动重构.md`：Scene 与 ScriptableObject 双向转换重构记录。
- `Assets/MirrorTrial/Docs/Level_02_ArtStyle.md`：第二关丛林美术规范。

## 11. 当前维护原则

- Scene 是最终可玩的表现载体，LevelConfig 是可再生的数据源；两者必须保持同步。
- 在配置未验证完整前，不得用它覆盖重要手工场景。
- 所有生成、批量生成和跨场景跳转，都必须先检查名称、唯一 ID 和 Build Settings。
- 每次大规模生成或关卡重构前，先提交 Git 快照。
