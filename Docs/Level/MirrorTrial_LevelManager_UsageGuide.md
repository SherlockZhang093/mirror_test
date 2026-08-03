# 《镜中试炼》LevelManager 系统评估与使用教程

## 1. 结论

这套系统的方向正确，已经具备一个横版动作 Demo 所需的关卡搭建骨架：

- `LevelManager` 统一收集本关的玩法对象，并维护场景层级。
- `SpawnPoint` 在 Scene 中可视化刷怪位置、敌人职责和朝向。
- `CombatEncounter` 管理进入战斗、关门、按波次生成敌人和清场开门。
- `LevelTrigger` 使用“条件 + 动作”配置事件，可将触发来源与执行对象解耦。
- `MirrorGate` 已有激活、传送、奖励能力、返回和开启下一段的状态框架。
- 自定义 Inspector 可以直接创建常用关卡对象，并提供基础校验。

它适合在短时间内完成《镜中试炼》的单关可玩切片，也适合作为后续关卡内容生产的第一版工具。它目前还不是一个可稳定扩展到大量复杂关卡的完整系统；在正式用它堆内容前，必须先修复第 3 节的 P0 问题。

## 2. 系统职责与对象关系

```
LevelManager
|- 地形（GeometryRoot）        只放 Tile、碰撞、背景等表现与物理内容
|- 玩法（GameplayRoot）        放所有可配置的关卡逻辑对象
|  |- 段落（LevelSegment）     定义关卡的空间分段和解锁状态
|  |- 触发器（LevelTrigger）   在特定时机检查条件，执行动作
|  |- 战斗区（CombatEncounter）管理波次、刷怪、封门和清场
|  |- 镜子门（MirrorGate）     管理镜层入口、奖励和回归
|  |- 门（AreaGate）           管理路径阻断与开放
|  `- 刷怪点（SpawnPoint）     定义敌人出生位置、朝向、默认预制体
`- 运行时（RuntimeRoot）       建议用于运行时生成物或临时对象
```

实际事件链应保持为：**触发器负责“何时发生”，对象组件负责“如何执行”。**

例如，玩家进入战斗区时由 `CombatEncounter` 关门和开始波次；战斗清场后，`LevelTrigger` 监听清场事件，再配置“激活镜子门”或“开启后续道路”。不要在触发器脚本中写死某个具体关卡对象的逻辑。

## 3. 当前可行性评估

### 可直接使用的能力

| 能力 | 当前状态 | 用途 |
| --- | --- | --- |
| 场景对象归类与自动收集 | 可用 | 通过 `LevelManager` 统一管理本关玩法对象。 |
| Scene 中画刷怪点、战斗区、段落、门、镜门 | 可用 | 可快速检查关卡范围、出生位置和阻断关系。 |
| 敌人按刷怪点生成 | 可用 | 波次条目可以指定敌人预制体，或使用刷怪点默认敌人。 |
| 战斗开始时关门 | 可用 | `CombatEncounter.lockGates` 直接配置。 |
| 触发器条件与动作 | 基础可用 | 适合启动战斗、开关门、传送、解锁能力、激活镜门。 |
| 镜门状态与奖励能力 | 基础可用 | 已有 Locked / Active / Completed / Broken 状态。 |
| Inspector 创建对象和基础校验 | 可用 | 适合 Demo 阶段的快速搭建。 |

### P0：先修复再开始配完整关卡

1. **战斗区的全局清场判定有逻辑错误。**
   `CombatEncounter.TryClear()` 对 `AllEnemiesDefeated` 只检查当前存活敌人数，不检查是否已经生成完全部波次。第一波敌人死完时，后续波次可能尚未开始，战斗就会被错误地标记为完成；而另一种清场条件又因协程引用未清空，可能永远无法完成。

   修正原则：只有“全部波次生成完成”且“存活敌人数为 0”时，才允许调用 `ClearEncounter()`。波次协程结束时要显式清空运行状态。

2. **`LevelActionType.StartWave` 目前为空实现。**
   Inspector 能选择“开始波次”，但运行时不会做任何事。第一版可以先移除此动作选项，或者在 `CombatEncounter` 中补一个按索引启动指定波次的公开接口。

3. **镜子门没有接入 Boss 战完成回调。**
   `MirrorGate.targetEncounter` 目前只是字符串，进入镜门只会传送玩家；`MarkBossDefeated(player)` 没有被 Boss 或战斗系统自动调用。因此“击败 Boss -> 奖励能力 -> 返回现实层 -> 开启下一段”必须额外手动接线，或补一个 Boss 战斗完成事件。

4. **`LevelSegment.startEnabled` 只更新状态，不会在开局禁用对象。**
   目前它不会在 `Awake` 中调用 `SetActive(false)`。若设计意图是隐藏/禁用未解锁段落，需要统一段落的可见性、碰撞和逻辑启用策略。

### P1：建议在内容量增加前补齐

1. `LevelTrigger` 的圆形/矩形仅影响 Gizmo，实际碰撞器始终是 `BoxCollider2D`，两者会出现配置和实际触发范围不一致。
2. 枚举里已声明 `OnBossDefeated`、`OnSegmentEnabled`，但没有对应订阅或触发实现；目前不要把它们用于内容配置。
3. `enabledAtStart` 实际上等价于“永久禁用此触发器”，没有运行时启用接口；建议改名或增加 `Enable/Disable`。
4. `LevelSegment` 的“完成”语义和 `SegmentNotCompleted` 条件不准确，现有实现用的是 `IsEnabled` 代替完成状态。建议拆成 `IsEnabled` 与 `IsCompleted`。
5. 校验器通过反射读取私有波次字段，短期能工作，长期维护成本高；应为 `CombatEncounter` 暴露只读 `Waves`。
6. 当前只做了基础校验，尚未校验 ID 重复、动作循环、镜门入口、Boss 目标、触发器 Collider、波次数量和敌人出生落点。

### 策划判断

建议把该系统定位为“**单关、线性流程、战斗房间驱动的 Scene 配置工具**”。对于现实层 -> 战斗房 -> 镜门 -> Boss -> 能力回收 -> 新区域这种结构，它是可行的。不要在当前阶段把它扩展成通用节点编辑器、多分支任务系统或全局剧情系统；先让一个完整闭环稳定跑通，性价比最高。

## 4. 搭建前的场景规范

### 4.1 创建根对象

1. 在关卡 Scene 新建空物体，命名为 `Level_Reality_01`。
2. 挂载 `LevelManager`。
3. 在 `LevelManager` Inspector 中设置：
   - `Level Id`：例如 `Level_Reality_01`。
   - `Level Display Name`：例如 `现实层 - 初醒`。
   - `Player Spawn`：拖入玩家首次落地的位置。
4. 点击一次 Inspector 中的“全部收集”。如未手动指定根节点，运行时会使用或创建 `地形`、`玩法`、`运行时` 三个子节点。

### 4.2 场景层级约定

将 Tilemap、地面碰撞、背景、装饰放在 `地形` 下；将段落、战斗区、门、触发器、镜门、刷怪点放在 `玩法` 下；不要把玩法对象散落在场景根节点或地形节点下，否则自动收集与内容检查会失去意义。

推荐的层级示例：

```
Level_Reality_01
|- 地形
|  |- Tilemap_Ground
|  |- Collision_Ground
|  `- Background
|- 玩法
|  |- 段落
|  |  |- SEG_A_Intro
|  |  |- SEG_B_Combat
|  |  `- SEG_C_Mirror
|  |- 战斗区
|  |  `- Combat_A_01
|  |- 门
|  |  `- Gate_CombatExit
|  |- 镜子门
|  |  `- MirrorGate_Blade
|  |- 刷怪点
|  |  |- SP_A01_Melee
|  |  `- SP_A02_Ranged
|  `- 触发器
|     `- TR_ClearCombat_ActivateMirror
`- 运行时
```

## 5. 详细搭建教程：现实层战斗 -> 镜子门

下面以“玩家进入战斗区，清完两波敌人后开启镜子门”为例。这是当前系统最适合的第一条可玩流程。

### 第一步：划分关卡段落

1. 选中 `LevelManager`，在 Inspector 的“添加玩法对象”中点击“+ 段落”。
2. 将对象重命名为 `SEG_A_Intro`，在 Inspector 填入相同的 `Segment ID`，并填写显示名。
3. 调整其 `BoxCollider2D` 覆盖该段实际可玩的空间；在 `Bounds Collider` 字段拖入该 Collider。
4. 对战斗房间和镜门房间重复操作，分别命名 `SEG_B_Combat`、`SEG_C_Mirror`。
5. Scene 中会以紫色边框显示段落范围。段落的用途是帮助策划划分空间和控制解锁，不应替代实际地形碰撞。

### 第二步：放置刷怪点

1. 在 `LevelManager` Inspector 点击“+ 刷怪点”。
2. 将对象放到合适的平台或地面，命名为 `SP_B01_Melee`。
3. 配置：
   - `Spawn ID`：`SP_B01_Melee`。
   - `Role`：近战 / 远程 / 精英 Boss。该项主要用于可视化和策划识别。
   - `Default Enemy Prefab`：拖入默认敌人预制体。
   - `Facing Degrees`：横版面向右为 `0`，面向左为 `180`。
   - `Patrol Path`：如敌人需要巡逻，拖入路径点；当前战斗生成逻辑尚未消费此字段，先作为内容预留。
4. 复制刷怪点并摆放为远程点、侧后方点和高台点。Scene 中红色代表近战、蓝色代表远程、紫色代表精英或 Boss，箭头代表朝向。

策划建议：第一波用正面近战建立压力，第二波再加入高台远程与侧方近战。刷怪点不应落在玩家视野中心或无法到达的地形内部。

### 第三步：配置战斗区与波次

1. 在 `LevelManager` Inspector 点击“+ 战斗”。
2. 命名为 `Combat_B_01`，将其 `BoxCollider2D` 调整到玩家应触发战斗的区域，并保持 `Is Trigger` 勾选。
3. 配置 `Encounter ID`：`Combat_B_01`。
4. 保持 `Start On Player Enter` 勾选；这样玩家第一次进入范围即自动开战。
5. 在 `Lock Gates` 数组中填入战斗开始时要关闭的门，详见下一步。
6. 在 `Waves` 数组设置为 `2`，每一个元素就是一波。
7. 配置 Wave 1：
   - `Wave ID`：`W01_FrontGuard`。
   - `Clear Condition`：当前版本建议选“敌人全灭”。
   - `Spawn Entries` 数组设为 `1`。
   - 条目中指定 `Spawn Point = SP_B01_Melee`，`Count = 2`，`Interval = 0.5`。
8. 配置 Wave 2：
   - `Wave ID`：`W02_Crossfire`。
   - `Spawn Entries` 数组设为 `2`。
   - 条目 A：近战刷怪点，数量 `2`，间隔 `0.5`。
   - 条目 B：远程刷怪点，数量 `1`，首只延迟 `0.8`。
   - `Next Wave Delay` 可设为 `1.0`，为玩家留出节奏变化。
9. 每个条目可直接填 `Enemy Prefab`。若未填，系统会使用对应 `SpawnPoint.DefaultEnemyPrefab`。

注意：在第 3 节 P0 问题修复前，不要依赖两波以上的“清场开门”结果做正式验证。当前实现可能第一波结束就误判完成，也可能无法完成。

### 第四步：配置封门与开门

1. 在 `LevelManager` Inspector 点击“+ 门”，命名为 `Gate_B_CombatExit`。
2. 调整它自身或单独的 `BoxCollider2D`，使其恰好阻挡离开战斗区的通路。
3. 在 `Gate Collider` 拖入实际阻挡玩家的 Collider。
4. 在 `Visual Object` 拖入门、力场或封锁特效对象。若该对象上有 `SpriteRenderer`，系统会以绿色表示开启、红色表示关闭。
5. 将 `Initial Open` 设为勾选：常态开放，战斗开始时被 `CombatEncounter` 关闭。
6. 回到 `Combat_B_01`，把这个门拖入 `Lock Gates`。战斗开始自动关门，战斗清场自动开门。

如果需要形成真正的“房间锁定”，需同时配置入口门和出口门。入口门在玩家进入后关闭，出口门在清场后打开；否则玩家可能从来路离开战斗区域。

### 第五步：配置镜子门

1. 在 `LevelManager` Inspector 点击“+ 镜子门”，命名为 `MirrorGate_Blade`。
2. 把其 Collider 调整到玩家可接触的位置，并保持 `Is Trigger`。
3. 配置：
   - `Gate ID`：`MirrorGate_Blade`。
   - `Encounter Entry Point`：Boss 镜层入口位置。
   - `Return Point`：击败 Boss 后返回现实层的位置。
   - `Reward Ability`：`MirrorBlade`。
   - `Next Segment`：例如 `SEG_D_BladeRoute`。
   - `Teleport On Enter`：勾选。
4. 镜子门初始为 `Locked`，必须通过后续触发器激活后才允许传送。

### 第六步：用 LevelTrigger 在清场后激活镜门

1. 在 `LevelManager` Inspector 点击“+ 触发器”，命名为 `TR_CombatB_Clear_ActivateMirror`。
2. 触发器本身的 Collider 对这条事件链并不重要，但建议摆在战斗区附近，便于 Scene 中定位和检查。
3. 设置：
   - `Trigger ID`：`TR_CombatB_Clear_ActivateMirror`。
   - `When`：`On Encounter Clear`。
   - `Encounter Target`：拖入 `Combat_B_01`。
   - `One Shot`：勾选。
4. 在 `Actions` 数组新增一项：
   - `Type`：`Activate Mirror Gate`。
   - `Mirror Gate`：拖入 `MirrorGate_Blade`。
5. 可再新增一项 `Play Feedback`，填写临时调试文字。当前仅输出日志，后续可替换为 UI、音效、镜面特效或镜头事件。

这条配置表达的是：**Combat_B_01 清场 -> TR_CombatB_Clear_ActivateMirror 被唤起 -> MirrorGate_Blade 进入 Active 状态。** 触发器不需要知道镜门如何表现，镜门也不需要知道是哪场战斗解锁它。

### 第七步：接入 Boss 完成事件

当前版本没有自动桥接。临时 Demo 有两种做法：

1. 在 Boss 死亡逻辑中直接调用 `MirrorGate.MarkBossDefeated(player)`。
2. 为 Boss 战的 `CombatEncounter.OnCleared` 增加一个桥接组件，由桥接组件调用 `MirrorGate.MarkBossDefeated(player)`。

建议采用第二种。这样 Boss、镜门、奖励仍然保持分离：Boss 战只报告“已清场”，桥接组件负责把这个结果转换为镜门完成；镜门自身负责返程、解锁能力和启用下一段。

在未接入该桥接前，玩家即使进入 Boss 区并击败 Boss，也不会自动获得能力或返回现实层。

## 6. Trigger 配置速查

| 需求 | Trigger 的 When | 目标引用 | Action |
| --- | --- | --- | --- |
| 进入区域开始战斗 | On Player Enter | 无 | Start Encounter -> 对应 CombatEncounter |
| 战斗清场开门 | On Encounter Clear | Encounter Target | Open Gate -> 对应 AreaGate |
| 战斗清场激活镜门 | On Encounter Clear | Encounter Target | Activate Mirror Gate -> 对应 MirrorGate |
| 玩家进入后传送 | On Player Enter | 无 | Teleport Player -> 目标点 |
| 玩家有能力才能开门 | On Player Enter + Player Has Ability | 所需能力 | Open Gate -> 对应 AreaGate |
| 镜门破碎后开启新段 | On Mirror Broken | Mirror Gate Target | Enable Segment -> 对应 LevelSegment |

其中“战斗清场开门”可直接依赖 `CombatEncounter.lockGates` 自动完成；只有当开门是额外演出、跨区域联动或不同于战斗封门时，才建议用 Trigger 再配置一次。

## 7. Scene 检查与调试流程

1. 选中 `LevelManager`，点击“全部收集”，确认段落、触发器、战斗区、镜门、门、刷怪点的数量正确。
2. 点击“校验关卡”，优先解决 Error，再确认 Warning 是否符合设计意图。
3. 在 Scene 视图检查：紫色为段落，黄色为战斗区，青色为通用触发器，红/蓝/紫为不同职责的刷怪点，红/绿为门，灰/蓝/橙/红为不同镜门状态。
4. 运行后按以下顺序走一遍：出生 -> 进入战斗区 -> 门关闭 -> 第一波生成 -> 第二波生成 -> 清场 -> 门打开 -> 镜门激活 -> 进入镜层 -> Boss 完成 -> 获得能力并返回。
5. 每次调整波次、门或镜门引用后都重新执行校验。不要只依赖 Scene 可视化，引用丢失仍会导致运行时流程断裂。

## 8. 给策划的内容配置原则

- 一个战斗房间只负责一个明确目的：教学、压力测试、节奏转换或能力检验。不要在一个房间同时塞入所有敌人类型和所有机制。
- 波次按“先建立规则，再制造组合压力”的顺序设计。第一波教敌人，第二波考位置和优先级，第三波才适合做节奏峰值。
- 门是节奏工具，不只是阻挡物。关门代表承诺一段战斗；开门代表玩家获得前进许可或新的选择。
- Trigger 的命名采用 `TR_来源_结果`，如 `TR_CombatB_Clear_ActivateMirror`。对象 ID 应稳定，不使用临时的 `New GameObject`、`Trigger (1)`。
- 每个镜门只绑定一个明确奖励与后续验证空间。获得“镜刃”后，应尽快安排需要远程能力解决的门、机关或敌人站位。
- 先完成一条可玩的线性流程，再补复用和泛化。当前系统的价值在于快速迭代，不在于覆盖所有类型的关卡。

## 9. 第一关推荐配置表

| 阶段 | 空间 | 玩家目标 | 系统配置 |
| --- | --- | --- | --- |
| A | `SEG_A_Intro` | 熟悉移动与近战 | 无战斗或单个巡逻敌人 |
| B | `SEG_B_Combat` | 学习敌人优先级 | `Combat_B_01`，两波敌人，入口/出口门锁定 |
| C | `SEG_C_Mirror` | 进入镜层挑战 | 清场 Trigger 激活 `MirrorGate_Blade` |
| D | Boss 镜层 | 击败镜像自我 | Boss 完成桥接到 `MirrorGate_Blade.MarkBossDefeated` |
| E | `SEG_D_BladeRoute` | 立刻使用镜刃 | 镜门破碎后启用，安排远程破除或击杀验证 |

## 10. 上线前最低验收清单

- 每个 `LevelSegment` 都有唯一 ID 和有效的 Bounds Collider。
- 每个 `CombatEncounter` 都至少有一波，且每个刷怪条目都能解析到敌人预制体。
- 每个需要封锁的战斗区都配置了门，且门拥有实际 Collider。
- 每个 Event Trigger 都有唯一 ID、明确触发时机和完整目标引用。
- 每个镜门都配置了入口点、返回点、奖励能力和后续段落。
- Boss 死亡确实调用了镜门完成流程。
- 多波清场逻辑修复后，至少连续测试三次，确认不会提前开门、不会卡在关门状态、不会重复发奖励。
- Inspector 的“校验关卡”没有 Error。

## 11. 建议的下一步开发顺序

1. 修复战斗区的波次完成与清场判定。
2. 补齐 Boss 战清场到镜门完成的桥接。
3. 让触发器形状与实际 Collider 同步，并移除未实现事件选项或补齐实现。
4. 增加 ID 重复、引用缺失和逻辑循环校验。
5. 在此基础上再考虑做独立的关卡编辑窗口或可视化事件连线。

