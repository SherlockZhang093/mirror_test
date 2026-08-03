# MirrorTrial 关卡编辑器使用说明书（LevelManager 总控版）

## 版本说明

本说明书对应以 `LevelManager` 为唯一入口的关卡编辑器实现。与旧版不同，策划不再手动创建散落的组件对象，而是选中 `LevelManager` 后，通过 Inspector 面板中的按钮统一添加、管理和校验关卡内容。

---

## 1. 核心设计

### 1.1 一个场景，一个总控

每个关卡场景应只有一个 `LevelManager`：

```text
Level_Root
  LevelManager          <- 总控入口
  Geometry              <- 真实地形、平台、墙、装饰
  Gameplay              <- 由 LevelManager 创建的逻辑对象
    Segments
    Triggers
    Encounters
    Gates
    MirrorGates
    SpawnPoints
  Runtime               <- 运行时生成的敌人、特效
```

### 1.2 两层分离

| 层级 | 放什么 | 不由什么决定 |
| - | - | - |
| Geometry | 地面、平台、墙、装饰、Collider2D | 不决定关卡流程 |
| Gameplay | Segment、Trigger、Encounter、Gate、MirrorGate、SpawnPoint | 不画地形，只覆盖逻辑范围 |

---

## 2. 场景搭建流程

### 第一步：新建空场景

1. 创建根节点 `Level_Root`。
2. 在 `Level_Root` 下创建一个空物体，添加 `LevelManager` 组件。

### 第二步：摆 Geometry（真实地形）

在 `Geometry` 下手动摆放：

- 地面（带 BoxCollider2D / TilemapCollider2D）
- 平台（带 BoxCollider2D）
- 墙（带 BoxCollider2D）
- 装饰背景

这些只是普通的 Unity 物件，不需要任何关卡脚本。

### 第三步：配置 LevelManager 基础信息

选中 `LevelManager`，在 Inspector 中配置：

| 字段 | 说明 |
| - | - |
| Level Id | 关卡唯一标识 |
| Level Display Name | 关卡显示名 |
| Player Spawn | 玩家出生点 Transform |
| Geometry Root | 地形根节点 |
| Gameplay Root | 逻辑对象根节点 |
| Runtime Root | 运行时生成对象根节点 |

### 第四步：通过 LevelManager 添加逻辑对象

在 Inspector 的 `Add Gameplay Objects` 区域点击按钮：

- **+ Segment**：创建段落
- **+ Trigger**：创建触发器
- **+ Combat**：创建战斗遭遇
- **+ Mirror**：创建镜子门
- **+ Gate**：创建区域门
- **+ Spawn**：创建刷怪点

每个按钮会在 `Gameplay` 下对应分类中创建命名好的对象。

### 第五步：配置每个对象

#### Segment

- Segment Id / Display Name
- Bounds Collider：段落范围
- Start Enabled：是否初始启用

#### Trigger

- Trigger Id
- When：触发时机（OnPlayerEnter / OnEncounterClear / OnMirrorBroken 等）
- Shape：Box / Circle
- Size / Radius：范围
- Conditions：触发条件
- Actions：触发后执行的动作
- Listen Targets：监听目标（用于非空间触发）

#### Combat Encounter

- Encounter Id
- Start On Player Enter：是否玩家进入即开战
- Clear Condition：清场条件
- Lock Gates：战斗开始时关闭的门
- Waves：波次配置
  - enemyPrefab
  - spawnPoint
  - count / interval / delay

#### Mirror Gate

- Gate Id
- Target Encounter：目标 Boss 名
- Encounter Entry Point：进入 Boss 区域的位置
- Return Point：击败 Boss 后返回位置
- Reward Ability：击败后解锁的能力
- Next Segment：破碎后开启的下一段

#### Area Gate

- Gate Id
- Initial Open：初始是否开启
- Gate Collider：碰撞体（关闭时启用）
- Visual Object：显示对象

#### Spawn Point

- Spawn Id
- Role：Melee / Ranged / EliteOrBoss
- Default Enemy Prefab：默认敌人
- Facing Degrees：出生朝向
- Patrol Path：可选巡逻路径点

### 第六步：绑定 Trigger 动作

Trigger 是关卡流程的核心。每个 Trigger 包含 When + Conditions + Actions。

示例：玩家进入 Segment A 触发战斗

```text
Trigger: TR_A_EnterCombat
When: OnPlayerEnter
Shape: Box
Conditions:
  - EncounterNotStarted: Combat_A_01
Actions:
  - LockGate: Gate_A_L
  - LockGate: Gate_A_R
  - StartEncounter: Combat_A_01
```

示例：清场后开门并激活镜子门

```text
Trigger: TR_A_ClearCombat
When: OnEncounterClear
Listen Target: Combat_A_01
Actions:
  - OpenGate: Gate_A_R
  - ActivateMirrorGate: MirrorGate_Blade
```

### 第七步：运行校验

点击 Inspector 中的 `Validate Level` 按钮，检查：

- 是否配置了 PlayerSpawn
- 每个 Combat Encounter 是否有波次
- 每个 Wave 是否配置了敌人
- 每个 MirrorGate 是否有 ReturnPoint
- 每个 Trigger 的 Action 是否绑定了目标
- 每个 Gate 是否有 Collider

### 第八步：Play 测试

进入 Play 模式，验证：

1. 玩家出生在 PlayerSpawn
2. 进入 Trigger 区域触发战斗
3. 战斗区关闭门，刷怪
4. 清场后开门
5. 镜子门激活，玩家进入
6. Boss 死亡后返回，解锁能力，破碎门，开启下一段

---

## 3. 推荐工作流

```text
1. 创建场景 + LevelManager
2. 摆 Geometry（地面、平台、墙）
3. 设置 PlayerSpawn
4. 通过 LevelManager 添加 Segment
5. 通过 LevelManager 添加 Trigger + Combat Encounter
6. 配 Wave 和 Spawn Point
7. 添加 Gate / MirrorGate
8. 连接 Trigger Actions
9. Validate
10. Play
```

---

## 4. 注意事项

- **Trigger 的 BoxCollider2D 会自动勾选 IsTrigger**，但区域触发器只在 `When` 为 `OnPlayerEnter/Exit` 时检查玩家碰撞。
- **Combat Encounter 的 BoxCollider2D 也会自动勾选 IsTrigger**，用于检测玩家进入。
- **MirrorGate 的 Collider2D 同样为 Trigger**，用于玩家进入后传送。
- **Geometry 下的物件需要自己添加 Collider2D**，否则玩家会穿墙或穿地。
- **LevelManager 会在 Awake 时自动收集 Gameplay 下的所有组件**，无需手动注册。

---

## 5. 常见问题

### Q1：为什么添加了 Trigger 但玩家进入没反应？
- 检查 Trigger 的 `When` 是否为 `OnPlayerEnter`。
- 检查 Trigger 的 BoxCollider2D 是否勾选了 IsTrigger。
- 检查玩家是否有 `Player` Tag 或 `PlayerInputReader` 组件。
- 检查 Conditions 是否全部满足。

### Q2：清场后为什么不触发下一个动作？
- 清场 Trigger 的 `When` 应为 `OnEncounterClear`。
- 必须正确设置 `Listen Target` 为对应的 Combat Encounter。
- 触发器的 `One Shot` 为 true 时，触发后不会再次触发。

### Q3：为什么门不关？
- 检查 Combat Encounter 的 `Lock Gates` 是否绑定了对应的 AreaGate。
- 检查 AreaGate 的 `Gate Collider` 是否正确。

### Q4：为什么镜子门传送不了？
- 检查 MirrorGate 的 `Encounter Entry Point` 是否配置。
- 检查 MirrorGate 是否已被 `Activate`。
- 检查玩家是否触发了 MirrorGate 的 Collider2D。

---

## 6. 版本对比

| 特性 | 旧版 | 新版 |
| - | - | - |
| 入口 | 手动创建散落组件 | LevelManager 总控 |
| 创建对象 | 手动创建 GameObject + 挂脚本 | LevelManager 按钮一键创建 |
| 引用绑定 | 手动拖拽或命名自动绑定 | 通过 Trigger Actions 配置 |
| 场景结构 | 分散 | 清晰的 Geometry/Gameplay/Runtime 分层 |
| 校验 | 独立菜单 | 集成在 LevelManager Inspector 中 |
