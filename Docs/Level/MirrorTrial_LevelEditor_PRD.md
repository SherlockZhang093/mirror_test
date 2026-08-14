# 《镜中试炼》关卡编辑器策划案

## 1. 设计定位

《镜中试炼》的关卡编辑器不是通用地图编辑器，也不是复杂节点式逻辑编辑器。它是服务本 Demo 快速落地的专用关卡搭建工具。

编辑器的核心目标是让策划能直接在 Unity Scene 中完成关卡主体搭建：摆放怪物生成点、绘制关卡大致结构、配置触发区域、串联战斗波次、镜子门、Boss 战和能力解锁流程。

本编辑器优先解决三个问题：

- 策划能在场景里直接摆内容，而不是频繁改代码。
- 关卡结构、战斗区域、刷怪点、镜子门流程都能可视化检查。
- 关卡逻辑通过 Trigger 和事件配置解耦，避免写死在单个场景脚本中。

一句话概括：

```text
让《镜中试炼》的核心关卡闭环变成可摆放、可视化、可配置、可校验的 Scene 驱动流程。
```

## 2. 使用目标

策划使用该编辑器时，应能完成以下工作：

1. 在 Scene 中画出一段关卡的大致范围。
2. 标记玩家出生点、敌人生成点、镜子门、Boss 入口和返回点。
3. 配置玩家进入某个区域后触发战斗。
4. 配置战斗区域内按波次刷怪。
5. 配置清场后打开边界或激活镜子门。
6. 配置镜子门进入对应 Boss 场景或 Boss 区域。
7. 配置 Boss 击败后返回现实层、解锁能力、镜子门破碎并打开前路。
8. 在 Scene 视图中直接看见所有关键逻辑点和连接关系。

## 3. 编辑器不做什么

为了保证 Demo 能短时间完成，本编辑器明确不做以下内容：

- 不做通用 2D Tile 地图编辑器。
- 不做完整节点式蓝图系统。
- 不做大型剧情编辑器。
- 不做完整技能编辑器。
- 不做运行时玩家自定义关卡。
- 不做复杂 AI 行为树编辑器。

编辑器只服务本项目的核心关卡结构：现实层推进、战斗波次、镜子门、镜像层 Boss、能力解锁。

## 4. 核心关卡闭环

编辑器需要支持的最小闭环如下：

```text
玩家进入现实层关卡
-> 触发战斗区域
-> 锁定区域边界
-> 按波次生成敌人
-> 清场检测
-> 打开区域边界
-> 激活镜子门
-> 进入镜像层 Boss 战
-> Boss 被击败
-> 解锁玩家能力
-> 返回现实层
-> 镜子门破碎
-> 前路打开
```

关卡编辑器的所有模块，都围绕这个闭环设计。

## 5. 编辑器核心模块

### 5.1 关卡段落 Level Segment

关卡段落是现实层推进的基本单位。一个关卡可以由多个段落组成，每个段落负责一段移动、战斗或镜子门流程。

基础字段：

| 字段 | 作用 |
| - | - |
| Segment ID | 段落编号，例如 Reality_01_A |
| Segment Name | 策划可读名称 |
| Bounds | 段落大致范围 |
| Entry Trigger | 玩家进入段落的触发区域 |
| Combat Areas | 当前段落包含的战斗区域 |
| Mirror Gate | 当前段落连接的镜子门 |
| Next Segment | 完成后开启的下一段 |

Scene 表现：

- 用半透明矩形显示段落范围。
- 用箭头显示玩家推进方向。
- 在 Scene 中显示段落名称。
- 选中段落后显示内部所有触发器、波次点和连接关系。

### 5.2 战斗区域 Combat Area

战斗区域负责控制小型战斗闭环。玩家进入后锁住区域，按配置刷怪，清场后释放区域。

基础字段：

| 字段 | 作用 |
| - | - |
| Area ID | 战斗区域编号 |
| Trigger Shape | 触发区域形状 |
| Lock Gates | 战斗开始后关闭的边界 |
| Wave List | 当前区域的波次列表 |
| Clear Condition | 清场条件 |
| On Clear Events | 清场后触发的事件 |

常用清场条件：

- 当前区域所有敌人死亡。
- 指定关键敌人死亡。
- 所有波次刷完且敌人清空。

常用清场事件：

- 打开区域边界。
- 激活镜子门。
- 激活下一段关卡。
- 播放提示或演出。

### 5.3 怪物生成点 Spawn Point

怪物生成点必须能直接在 Scene 中摆放和拖动。它是关卡编辑器第一优先级功能。

基础字段：

| 字段 | 作用 |
| - | - |
| Spawn ID | 生成点编号 |
| Enemy Type | 默认敌人类型 |
| Facing Direction | 出生朝向 |
| Spawn Delay | 默认延迟 |
| Patrol Path | 可选巡逻路径 |
| Group Tag | 分组标签 |

Scene 表现：

- 近战敌人生成点用红色图标。
- 远程敌人生成点用蓝色图标。
- Boss 或精英敌人生成点用紫色图标。
- 显示编号，例如 S01、S02、S03。
- 显示朝向箭头。

生成点应支持两种用法：

1. 直接指定敌人类型，用于快速搭 Demo。
2. 只作为位置点，由 Wave 配置决定具体生成什么敌人。

### 5.4 波次 Wave

波次负责描述一次战斗中敌人的生成顺序和节奏。

基础字段：

| 字段 | 作用 |
| - | - |
| Wave ID | 波次编号 |
| Start Condition | 波次开始条件 |
| Spawn Entries | 本波生成内容 |
| Next Wave Delay | 下一波延迟 |
| Clear Condition | 本波完成条件 |

Spawn Entry 字段：

| 字段 | 作用 |
| - | - |
| Enemy Type | 敌人类型 |
| Spawn Point | 使用哪个生成点 |
| Count | 数量 |
| Interval | 同组生成间隔 |
| Delay | 本条目延迟 |

示例：

| 波次 | 内容 | 设计目的 |
| - | - | - |
| Wave 1 | 2 个影卫 | 教玩家基础近战 |
| Wave 2 | 2 个影卫 + 1 个棱光射手 | 引入远程压力 |
| Wave 3 | 1 个影卫 + 2 个棱光射手 | 镜子门前制造站位压力 |

### 5.5 区域边界 Area Gate

区域边界用于在战斗期间临时阻挡玩家，保证战斗区域成立。

基础字段：

| 字段 | 作用 |
| - | - |
| Gate ID | 边界编号 |
| Initial State | 初始开启或关闭 |
| Close Event | 关闭触发 |
| Open Event | 打开触发 |
| Visual Object | 绑定显示对象 |
| Collider Object | 绑定碰撞对象 |

Scene 表现：

- 用竖线或半透明墙显示边界。
- 开启状态显示绿色。
- 关闭状态显示红色。
- 选中战斗区域时，高亮该区域绑定的所有边界。

### 5.6 镜子门 Mirror Gate

镜子门是关卡结构的核心节点，不只是传送门。它负责连接现实层、镜像层、Boss 战、能力夺回和前路打开。

基础字段：

| 字段 | 作用 |
| - | - |
| Gate ID | 镜子门编号 |
| Required Condition | 激活条件 |
| Target Encounter | 连接的 Boss 战 |
| Return Point | Boss 后返回点 |
| Reward Ability | 击败后解锁能力 |
| Break Events | 镜子破碎后事件 |
| Next Segment | 破碎后开启的关卡段落 |

状态：

| 状态 | 说明 |
| - | - |
| Locked | 尚未满足进入条件 |
| Active | 可交互进入镜像层 |
| Completed | Boss 已击败 |
| Broken | 镜子破碎，前路打开 |

Scene 表现：

- 显示镜子门编号。
- 显示连接 Boss 名称。
- 用连线连接 Return Point。
- 已配置奖励能力时显示能力名。

### 5.7 通用触发器 Level Event Trigger

通用触发器是关卡逻辑解耦的核心。它负责“检测条件”和“抛出事件”，不直接写死具体结果。

触发类型：

| 类型 | 说明 |
| - | - |
| On Player Enter | 玩家进入区域 |
| On Player Exit | 玩家离开区域 |
| On Wave Clear | 波次清空 |
| On Area Clear | 战斗区域清空 |
| On Boss Defeated | Boss 击败 |
| On Ability Unlocked | 能力解锁 |
| On Mirror Broken | 镜子门破碎 |

事件结果：

| 事件 | 作用 |
| - | - |
| Start Wave | 开始波次 |
| Lock Gate | 锁住区域边界 |
| Open Gate | 打开区域边界 |
| Activate Mirror | 激活镜子门 |
| Teleport Player | 传送玩家 |
| Unlock Ability | 解锁能力 |
| Enable Segment | 启用下一段关卡 |
| Play Feedback | 播放反馈 |

设计原则：

```text
Trigger 只负责触发。
事件接收者负责执行。
关卡流程通过配置连接，不写死在一个 MonoBehaviour 里。
```

## 6. Scene 可视化需求

为了让策划能快速搭 Demo，Scene 视图必须能看懂关卡逻辑。

### 6.1 必须显示的内容

| 内容 | 可视化方式 |
| - | - |
| 关卡段落范围 | 半透明矩形 |
| 玩家推进方向 | 箭头 |
| 战斗区域 | 黄色边框 |
| 触发区域 | 青色半透明区域 |
| 怪物生成点 | 图标 + 编号 |
| 区域边界 | 红/绿竖线 |
| 镜子门 | 特殊图标 + Gate ID |
| 返回点 | 回环箭头图标 |
| 事件连接 | 虚线 |

### 6.2 选中对象时的表现

选中 Combat Area 时：

- 高亮该区域的 Trigger。
- 高亮该区域绑定的 Spawn Point。
- 高亮该区域绑定的 Area Gate。
- 显示 Wave 数量和清场条件。

选中 Mirror Gate 时：

- 显示激活条件。
- 显示目标 Boss。
- 显示返回点。
- 显示奖励能力。
- 显示破碎后打开的下一段。

选中 Spawn Point 时：

- 显示敌人类型。
- 显示出生朝向。
- 显示所属波次。
- 显示所属战斗区域。

## 7. 配置校验

编辑器需要提供基础校验，避免 Demo 运行时才发现流程断掉。

必须校验：

- Combat Area 没有绑定 Trigger。
- Combat Area 没有配置 Wave。
- Wave 中存在空 Spawn Point。
- Spawn Point 没有敌人类型。
- Area Gate 没有绑定 Collider。
- Mirror Gate 没有配置 Target Encounter。
- Mirror Gate 没有配置 Return Point。
- Boss Encounter 没有配置奖励能力。
- Trigger 配置了事件但没有接收对象。
- Next Segment 为空导致流程中断。

校验结果分级：

| 级别 | 说明 |
| - | - |
| Error | 会导致流程无法运行 |
| Warning | 可以运行但可能不符合设计预期 |
| Info | 给策划的提示 |

## 8. 第一版最小实现范围

第一版不追求完整工具链，只做能支撑 Demo 的核心功能。

### 必做

1. Spawn Point 场景摆放与 Gizmo 显示。
2. Combat Area Trigger 区域绘制。
3. Wave Spawner 按配置生成敌人。
4. Area Gate 战斗开始关闭、清场后打开。
5. Mirror Gate 激活、进入 Boss、返回、破碎。
6. Level Event Trigger 基础事件配置。
7. Scene 中显示生成点、战斗区域、镜子门、连接关系。
8. 基础配置校验。

### 暂缓

1. 独立 EditorWindow。
2. 节点式流程编辑。
3. 复杂可视化时间轴。
4. 自动生成完整关卡。
5. 完整技能编辑器。

第一版优先使用 Custom Inspector + Gizmo + Handles 完成。

## 9. 推荐技术结构

建议新增目录：

```text
Assets/MirrorTrial
  /Scripts
    /Level
    /Editor
    /Combat
    /Boss
    /Abilities
  /Prefabs
    /Level
    /Enemies
    /Bosses
  /Configs
    /Levels
    /Waves
    /Bosses
    /Abilities
```

建议核心脚本：

| 脚本 | 作用 |
| - | - |
| LevelSegment | 管理关卡段落 |
| CombatArea | 管理战斗区域 |
| SpawnPoint | 敌人生成点 |
| WaveSpawner | 波次刷怪 |
| AreaGate | 区域边界 |
| MirrorGate | 镜子门流程 |
| LevelEventTrigger | 通用触发器 |
| LevelEventReceiver | 通用事件接收 |
| LevelValidationUtility | 配置校验 |
| LevelGizmoDrawer | Scene 可视化 |

数据优先采用 MonoBehaviour 场景组件，必要时再补 ScriptableObject。原因是本 Demo 的核心需求是 Scene 内直接搭建，过早配置表化会降低搭建速度。

## 10. 关卡搭建示例

第一关现实层可以这样搭：

```text
Reality_01
  Segment_A：基础移动与 1 波影卫
  Segment_B：2 波混合敌人
  Segment_C：镜子门前战斗
  MirrorGate_Blade：进入刃镜 Boss
```

流程：

1. 玩家进入 Segment_A。
2. 触发 CombatArea_A。
3. 关闭左右 AreaGate。
4. Wave 1 生成 2 个影卫。
5. 清场后打开右侧边界。
6. 玩家进入 Segment_B。
7. Wave 1 生成 2 个影卫。
8. Wave 2 生成 1 个棱光射手。
9. 清场后进入 Segment_C。
10. 镜子门前生成最终一波敌人。
11. 清场后 MirrorGate_Blade 激活。
12. 玩家进入刃镜 Boss。
13. 击败 Boss 后解锁 MirrorBlade。
14. 返回现实层。
15. 镜子门破碎，开启后续段落。

## 11. 答辩展示重点

答辩时重点展示编辑器如何服务玩法，而不是展示编辑器有多少按钮。

展示顺序建议：

1. 在 Scene 中展示关卡段落、战斗区域、出生点和镜子门。
2. 选中 Combat Area，展示它绑定了哪些 Spawn Point 和 Wave。
3. 修改一波敌人数量或生成点，运行后看到战斗节奏变化。
4. 展示清场后 Area Gate 打开、Mirror Gate 激活。
5. 展示 Mirror Gate 连接 Boss、返回点和奖励能力。
6. 展示配置校验，说明缺配置时编辑器能提前提示。

核心表达：

```text
这个编辑器让关卡逻辑从代码里解耦出来，变成策划可以在 Scene 里直接搭建和验证的内容。
```

## 12. 分阶段实现计划

### 阶段一：场景点位与区域可视化

目标：

- 能摆 Spawn Point。
- 能画 Combat Area。
- 能显示 Area Gate。
- 能显示 Mirror Gate 和 Return Point。

验收：

- Scene 中能看懂第一关的大致结构。
- 策划能拖动点位调整刷怪位置。

### 阶段二：战斗波次跑通

目标：

- 玩家进入 Combat Area 后触发 Wave。
- Wave 按 Spawn Point 生成敌人。
- 清场后打开 Area Gate。

验收：

- 第一关现实层可以完成 3 波战斗。
- 修改 Wave 配置后，运行表现会变化。

### 阶段三：镜子门流程跑通

目标：

- 清场后激活镜子门。
- 玩家进入镜像层。
- Boss 死亡后返回现实层。
- 解锁能力并破碎镜子门。

验收：

- 完成“现实层 -> Boss -> 能力获得 -> 返回现实层”的闭环。

### 阶段四：配置校验与答辩展示

目标：

- 增加配置缺失提示。
- 增加 Scene 连接线。
- 增加 Inspector 中的流程摘要。

验收：

- 答辩时能清楚展示工具价值。
- 缺少关键配置时，编辑器能提前提示。

## 13. 最终目标

该关卡编辑器最终要达成的不是“功能很多”，而是“Demo 能快速被搭出来，并且关卡逻辑清楚可控”。

成功标准：

- 策划能直接在 Scene 里搭一段现实层关卡。
- 怪物生成点、战斗区域、镜子门和返回点都能可视化。
- 波次、清场、开门、传送、能力解锁都通过 Trigger 和事件配置连接。
- 修改关卡节奏时，不需要改核心代码。
- 答辩时能证明项目具备技术策划能力：内容生产、流程解耦、配置化、可视化和可校验。
