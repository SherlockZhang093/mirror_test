# 镜中试炼 · 关卡编辑器 SO 驱动通用化重构记录

> 本文档汇总本轮对《镜中试炼》关卡编辑器所做的全部修改：把原本"硬编码生成第 1 关"的方案，
> 重构为**通用、ScriptableObject（SO）驱动**的关卡编辑器——每个关卡都能用同一套工具，
> 直接在 Unity 场景里拼，再导出/导入 SO。

---

## 1. 背景与目标

| 项目 | 旧方案 | 新方案 |
| --- | --- | --- |
| 关卡生成 | `Level01Setup` / `Level01MirrorSetup` 硬编码生成"第 1 关" | 通用编辑器，任意关卡复用同一套工具 |
| 数据载体 | 场景即数据（易被手改、不便版本控制/AI 读取） | `LevelConfig` ScriptableObject 作为纯文本源数据 |
| 镜子门 | 每关写死 | 每个**现实关卡一个镜子门**；进入镜子门 = 进入一个新关卡（镜中关卡） |
| 镜中关卡 | 单独一套 setup 脚本 | 同样走 SO 模型，底层复用同一套组件，仅逻辑入口不同 |

关键决策（与开发者敲定）：
- 美术资源后续替换到 `Assets/Art/pingtai/slices/pingtai_01..13.png`，编辑器须对缺失 sprite 容错。
- SO 里 **position 全部用 Vector3**，**跨引用（trigger/segment/encounter/gate/mirrorGate/spawnPoint）走纯文本字符串 ID**，保证 SO 是纯文本、对 AI 与 Git 友好。

---

## 2. 架构总览

```
LevelManager（场景唯一总控）
├─ 场景分层：Geometry(地形) / Gameplay(段落/触发器/战斗/门/镜子门/刷怪点) / Runtime(运行时生成)
└─ 数据层 + 工具层（均在 MirrorTrial.Editor.Level）
     ├─ LevelConfig.cs        (Assets/MirrorTrial/Scripts)        数据模型 SO
     ├─ LevelConfigExporter.cs                                         场景 → SO
     ├─ LevelConfigImporter.cs                                         SO → 场景
     └─ LevelConfigMenu.cs                                            编辑器菜单
```

设计原则（沿用既有机制）：`Trigger = When + Where + Conditions + Actions`，通过 Actions 串联流程；
校验沿用 `LevelValidationUtility.Validate`（在 `LevelManagerEditor` 的"校验关卡"按钮里）。

---

## 3. 数据模型 — `LevelConfig.cs`

文件：`Assets/MirrorTrial/Scripts/LevelConfig.cs`（命名空间 `MirrorTrial.Level`）

`LevelConfig` 顶层字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `levelId` | `string` | 关卡唯一 ID（存档/跨场景引用） |
| `displayName` | `string` | 显示名 |
| `isMirrorLevel` | `bool` | 是否镜中关卡（导出时反推，导入时决定挂 `MirrorReturnOnClear`） |
| `playerPrefab` / `enemyPrefab` / `defaultSprite` | 引用 | 可选预制体/精灵引用 |
| `playerSpawn` | `Vector3` | 玩家出生点世界坐标 |
| `geometry` / `segments` / `spawnPoints` / `encounters` / `gates` / `mirrorGates` / `triggers` | `List<...Entry>` | 七大条目集合 |

条目类（均为 `[System.Serializable]`，**position 均用 `Vector3`**）：

- `GeometryEntry`：`name, position, size(Vector2), isBoundary, isPlatform, platformVisualIndex, color`
- `SegmentEntry`：`segmentId, displayName, position, size(Vector2), startEnabled`
- `SpawnPointEntry`：`spawnId, position, role(SpawnPointRole), enemyPrefab, aiProfile, overrideHitPoints, overrideMoveSpeed, facingDegrees`
- `EncounterEntry`：`encounterId, position, size, startOnPlayerEnter, clearCondition(CombatClearCondition), lockGateIds(List<string>), waves(List<WaveEntry>)`
- `WaveEntry`：`waveId, clearCondition, nextWaveDelay, spawnEntries(List<WaveSpawnEntryData>)`
- `WaveSpawnEntryData`：`spawnPointId(string), count, delay, interval, enemyPrefab`
- `GateEntry`：`gateId, position, initialOpen`
- `MirrorGateEntry`：`gateId, position, mirrorSceneName, hitPoints, returnPointPosition(Vector3), rewardAbility, nextSegmentId(string), platformVisualIndex, color`
- `TriggerEntry`：`triggerId, position, when(LevelTriggerWhen), shape(LevelTriggerShape), boxSize, circleRadius, enabledAtStart, oneShot, gizmoColor, conditions, actions`
- `ConditionEntry`：`type(LevelConditionType), targetId(string), requiredAbility(MirrorRewardAbility), invert`
- `ActionEntry`：`type(LevelActionType), targetId(string), targetPosition(Vector3), waveIndex, ability(MirrorRewardAbility), feedbackMessage, delay`

> 枚举（`LevelTriggerWhen` / `LevelTriggerShape` / `LevelConditionType` / `LevelActionType` / `MirrorRewardAbility` / `CombatClearCondition` / `SpawnPointRole`）均复用 `LevelCommon.cs` 中已有定义。

---

## 4. 导出器 — `LevelConfigExporter.cs`

文件：`Assets/MirrorTrial/Editor/Level/LevelConfigExporter.cs`

**调用入口**
- `ExportCurrentScene(assetPath=null)`：查找场景内 `LevelManager` → 调 `Export`。
- `Export(LevelManager, assetPath)`：在 `Assets/MirrorTrial/LevelConfigs/` 下创建/更新 `.asset`，填充后 `SaveAssets`。

**`FillConfig` 流程**：写入 `levelId/displayName/playerSpawn`，清空七类集合，依次 `ExportGeometry / Segments / SpawnPoints / Encounters / Gates / MirrorGates / Triggers`，最后反推：
```csharp
config.isMirrorLevel = config.mirrorGates.Count == 0
                      && Object.FindObjectOfType<MirrorReturnOnClear>() != null;
```

**逐类映射要点**
- 几何：遍历 `GeometryRoot` 子节点，取 `BoxCollider2D.size`、名称是否含 `Boundary` / 前缀 `Platform_` / 含 `PlatformVisual` 子节点、平台贴图索引（`pingtai_##` 文件名解析）。
- 段落/刷怪点/战斗/门/镜子门/触发器：用 `SerializedObject` 读取私有 `[SerializeField]` 字段（如 `startEnabled`、`facingDegrees`、`waves`、`lockGates` 等）。
- 条件/动作：`targetId` 按类型把对象引用还原成其 ID 字符串（段→`segmentId`、战斗→`encounterId`、镜子门→`gateId`、传送→`Transform.name`）。

---

## 5. 导入器 — `LevelConfigImporter.cs`

文件：`Assets/MirrorTrial/Editor/Level/LevelConfigImporter.cs`

**`GenerateScene(LevelConfig, scenePath=null)`** 流程：
1. 目标路径默认 `Assets/MirrorTrial/Scenes/{levelId}.unity`；始终 **`NewScene` 后覆盖保存**（数据驱动，配置即真相，避免重新生成时对象叠加）。
2. `EnsureMainCamera()`：缺主相机则建一个并挂 `CameraFollow2D`。
3. 建/取 `LevelManager`，写 `levelId/displayName/geometryRoot(地形)/gameplayRoot(玩法)/runtimeRoot(运行时)`。
4. 建 id→组件字典，按依赖顺序创建：
   - `BuildGeometry` → 平台用 `LevelPlatformVisualUtility.ApplyPlatformVisual`
   - 段落、刷怪点、战斗（含 `waves`/`spawnEntries`/`lockGates` 数组）、门、镜子门
   - 触发器（`conditions`/`actions` 数组）
5. **跨引用解析**：建完所有组件后，用 id 字典把 `ConditionEntry.targetId` / `ActionEntry.targetId` 还原成对应 `LevelSegment` / `CombatEncounter` / `AreaGate` / `MirrorGate` / `SpawnPoint` 引用；传送动作创建 `TeleportTarget` 标记。
6. 建 `PlayerSpawn` 标记并赋给 `LevelManager.playerSpawn`。
7. 若 `isMirrorLevel`，在 `玩法` 下挂 `MirrorReturnOnClear`。

> 关键技术点：组件数据多为私有 `[SerializeField]`，统一用 `SerializedObject` + `FindProperty` 写入，
> 已逐一核对所有属性名与运行时组件完全一致（见第 9 节）。

---

## 6. 菜单 — `LevelConfigMenu.cs`

文件：`Assets/MirrorTrial/Editor/Level/LevelConfigMenu.cs`

菜单路径 `Tools/镜像试炼/关卡/`：

| 菜单项 | 行为 |
| --- | --- |
| 从场景导出当前关卡配置 | 调 `LevelConfigExporter.ExportCurrentScene()` |
| 从配置生成场景 | 需 Project 窗口选中一个 `LevelConfig` 资产，调 `GenerateScene` |
| 新建现实关卡 | 创建 `LevelConfig`（`isMirrorLevel=false`）并即时生成空场景 |
| 新建镜中关卡 | 创建 `LevelConfig`（`isMirrorLevel=true`）并即时生成空场景 |
| 批量生成所有配置场景 | 遍历 `LevelConfigs/` 下所有 `LevelConfig` 逐个 `GenerateScene` |

---

## 7. 删除的旧脚本（开发者要求"直接删除吧"）

以下脚本已重命名为 `.bak`（退出编译、可一键恢复），其"生成第 1 关"菜单随之失效：

- `Assets/MirrorTrial/Editor/Level/Level01Setup.cs` (+ `.meta`)
- `Assets/MirrorTrial/Editor/Level/Level01MirrorSetup.cs` (+ `.meta`)
- `Assets/MirrorTrial/Editor/Level/Level01AutoRun.cs` (+ `.meta`)

> 已 grep 确认：除这三个文件自身外，工程内无任何其他代码引用它们，删除安全。

---

## 8. 使用流程（操作指南）

1. **新建关卡**：菜单 `Tools/镜像试炼/关卡/新建现实关卡`（或新建镜中关卡）→ 自动生成 `LevelConfig` 资产 + 空场景。
2. **拼场景**：选中场景中的 `LevelManager`，在 Inspector 用"添加玩法对象"按钮加 段落 / 触发器 / 战斗区 / 镜子门 / 门 / 刷怪点；在 `地形` 下手摆平台与边界。
3. **贴美术**：选中平台物体，用 `LevelManagerEditor` 的 Platform Art 面板点 `01–13` 应用切片精灵（缺失时自动容错）。
4. **导出**：`Tools/镜像试炼/关卡/从场景导出当前关卡配置` → 生成/更新 `{levelId}.asset`（纯文本，可进 Git、可被 AI 读取）。
5. **回灌/重建**：改动 SO 后点 `从配置生成场景`（或 `批量生成所有配置场景`）即可整关从数据重建。

---

## 9. 关键技术注意 / 踩坑

- **本机文件系统坑（重要）**：通过 Write 工具直接写入 `G:/mirror_test/**` 的 `.cs` 会被写坏成全 `\x00`（"No such device"）。
  可靠写法：Write 工具只写到 `C:/Users/.../AppData/Local/Temp/`，由内嵌 Python 用 `open(path,'w',encoding='utf-8')` 落到项目路径；
  Bash heredoc 内联 `python3 - <<'PYEOF'` 含 `r'''` 会触发 bash 引号解析错误，一律用"Temp 脚本 + 运行"模式。
- **运行时脚本不能引用 `UnityEditor`**；`ChineseLabel` / `[InspectorName]` 实现 Inspector 全中文化。
- **Importer 的 `FindProperty` 字段名已逐一核对**：`LevelManager`(levelId/levelDisplayName/geometryRoot/gameplayRoot/runtimeRoot/playerSpawn)、`LevelSegment`(segmentId/displayName/startEnabled/boundsCollider)、`SpawnPoint`(spawnId/role/defaultEnemyPrefab/aiProfile/overrideHitPoints/overrideMoveSpeed/facingDegrees)、`CombatEncounter`(encounterId/startOnPlayerEnter/clearCondition/lockGates/waves 及 WaveDefinition/WaveSpawnEntry 子字段)、`AreaGate`(gateId/initialOpen/gateCollider/visualObject)、`MirrorGate`(gateId/mirrorSceneName/hitPoints/rewardAbility/nextSegment/returnPoint/visualObject)、`LevelTrigger`(triggerId/enabledAtStart/oneShot/when/shape/boxSize/circleRadius/gizmoColor/conditions/actions 及 LevelCondition/LevelAction 子字段)。
- **美术资源**：平台贴图后续替换到 `Assets/Art/pingtai/slices/pingtai_01..13.png`；`LevelPlatformVisualUtility.GetPlatformSprite` 对缺失 sprite 返回 `null`，导入时不挂精灵，不报错。

---

## 10. 验证状态与遗留

**已做（静态核对）**
- 四个核心文件括号平衡检查通过。
- Importer 所有 `FindProperty` 名称与运行时组件 `[SerializeField]` 字段逐一对应一致。
- 旧脚本已禁用、调试残留已清理。

**未做（需 Unity 内验证）**
- 本环境无法运行 Unity 编译，未做实际编译与"导出→生成"一轮实测。
- 建议：在 Unity 打开项目让其重编译，新建一个现实关卡 → 拼地形/战斗/镜子门 → 导出 SO → 从 SO 生成一次，确认往返一致。

**建议后续**
- 让 `LevelValidationUtility` 增加针对 `LevelConfig` 的校验（如镜子门 `mirrorSceneName` 指向的镜中场景是否存在）。
- 可选：把"场景→SO→场景"的往返写成单元测试或编辑器自校验按钮。
