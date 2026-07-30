# MirrorTrial 玩家运动控制现状调研

> 调研范围：`Assets/MirrorTrial` 中当前玩家 Prefab 与运行时代码。  
> 调研日期：2026-07-23。  
> 结论基于静态代码与 Prefab 配置检查，未包含 Unity Play Mode 手感实测。

## 1. 结论摘要

当前实际使用的是一套基于 **Unity 旧 Input Manager + Rigidbody2D 动态刚体 + 手动重力 + 轻量状态机** 的 2D 横版角色控制器，核心 Prefab 为：

- `Assets/MirrorTrial/Prefabs/Characters/Player_MirrorTrial.prefab`

核心链路为：

```text
PlayerInputReader（Update 采样输入）
        ↓
PlayerMotor（Update 缓存跳跃/朝向，FixedUpdate 写 Rigidbody2D.velocity）
        ↓
PlayerStateMachine（归纳 Idle/Run/Jump/Land/动作状态）
        ↓
PlayerAnimationDriver（按状态播放动画）
```

闪避、EchoDash、攻击、受击等系统不另外移动 Transform，而是通过 `PlayerMotor` 的“移动锁”和“强制速度”接口接管角色运动。因此，`Rigidbody2D.velocity` 是当前玩家位移的主要权威数据。

项目中仍保留 Unity 2D Platformer 模板的 `Assets/Scripts/Mechanics/PlayerController.cs` 和 `KinematicObject.cs`，但它们不是 `Player_MirrorTrial` Prefab 当前运动链的一部分，应视为遗留/模板代码，避免与现用系统混淆。

## 2. 组件职责

| 组件 | 当前职责 |
| --- | --- |
| `PlayerInputReader` | 每帧从旧 Input Manager 读取水平/垂直轴、跳跃和各动作按键；支持编辑器预览输入 |
| `PlayerMotor` | 水平加减速、跳跃、土狼时间、跳跃缓存、可变跳高、手动重力、接地检测、朝向、冲刺/击退强制速度 |
| `PlayerTuning` | 集中保存移动、闪避、能力、战斗和受击参数，直接序列化在 Prefab 上 |
| `PlayerStateMachine` | 将接地、速度、输入和外部动作请求归纳为唯一动作状态 |
| `PlayerAnimationDriver` | 将动作状态映射为 Animator 状态，并按技能/动作时长调整播放 |
| `PlayerDodgeController` | 处理 Shift 闪避、输入缓存、冷却、无敌帧、完美闪避事件 |
| `PlayerAbilityLoadout` | 处理 Q 对应的 EchoDash，以及 MirrorBlade；通过强制速度驱动冲刺 |
| `PlayerCombat` / `PlayerDamageReceiver` | 在攻击、受击等阶段锁移动，必要时施加强制速度或击退 |

## 3. 输入层

`PlayerInputReader` 的执行顺序为 `-90`，在普通脚本之前采样输入。当前仍使用 `UnityEngine.Input`，不是新版 Input System。

### 3.1 移动相关输入

| 输入 | 配置 | 输出 |
| --- | --- | --- |
| 水平移动 | `Horizontal` | `MoveX`，使用 `GetAxisRaw`，通常为 -1/0/1 |
| 垂直输入 | `Vertical` | `MoveY`，运动器当前未直接使用；战斗可用于下蹲类动作判断 |
| 跳跃 | `Jump` | `JumpPressed` / `JumpReleased` |
| 闪避 | `LeftShift` | `DodgePressed` |
| EchoDash | `Q` | `MobilitySkill` |

`InputEnabled = false` 时，移动轴被清零，当帧按下/松开状态被清除。镜头演出、关卡门、死亡或 EchoDash 等系统可借此整体关闭玩家输入。

## 4. 基础移动实现

### 4.1 刚体配置

`PlayerMotor.Awake()` 会强制设置：

- `RigidbodyType2D.Dynamic`
- `gravityScale = 0`
- 冻结旋转
- 插值 `Interpolate`
- 连续碰撞检测 `Continuous`

Prefab 上还配置了线性阻力 `2`，但水平和纵向速度会由代码持续直接写入，因此实际运动主要由 `PlayerMotor` 控制，而不是依赖刚体自然加速。

### 4.2 水平移动

每个 `FixedUpdate` 计算：

```text
目标速度 = MoveX × moveSpeed
当前水平速度 = MoveTowards(当前移动速度, 目标速度, 加速度 × fixedDeltaTime)
Rigidbody2D.velocity.x = 当前水平速度
```

加速度选择规则：

- 有移动输入且接地：`acceleration`
- 有移动输入且在空中：`airAcceleration`
- 没有移动输入且接地：`deceleration`
- 没有移动输入且在空中：`airDeceleration`
- 移动被锁定：将输入视为 0，角色按 `deceleration` 刹停

角色朝向在 `Update` 中由水平输入更新，并通过 `SpriteRenderer.flipX` 翻面。移动锁定时不会因当前输入改变朝向。

### 4.3 跳跃与可变跳高

按下跳跃时，`Update` 将跳跃缓存计时器设为 `jumpBufferTime`；`FixedUpdate` 中只要跳跃缓存和土狼时间都大于 0，且当前未锁移动，就把纵向速度直接设为 `jumpSpeed`。

系统包含两种平台动作容错：

- **土狼时间**：刚离开平台后，在 `coyoteTime` 内仍可起跳。
- **跳跃缓存**：落地前提前按跳跃，在 `jumpBufferTime` 内接地后会立即起跳。

松开跳跃时，如果仍在上升，纵向速度乘以 `jumpCutMultiplier`，从而实现轻点短跳、长按高跳。

### 4.4 重力

刚体自身重力关闭，代码每个物理帧手动累加：

```text
velocity += Physics2D.gravity × 重力倍率 × fixedDeltaTime
```

- 上升阶段使用 `baseGravityModifier`
- 下落阶段使用 `fallGravityMultiplier`
- 稳定接地且纵向速度向下时直接归零

这意味着全局 `Physics2D.gravity` 仍会影响玩家，但玩家自己的 `gravityScale` 不参与最终计算。

## 5. 接地检测

接地结果只由 `PlayerMotor` 维护，`PlayerStateMachine`、闪避和动画都读取同一个 `IsGrounded`。

每个物理帧同时使用两种检测，只要任意一种成立即视为接地：

1. `Rigidbody2D.GetContacts`：检查已有碰撞接触点。
2. `Collider2D.Cast(Vector2.down)`：向下投射一小段距离，提前发现脚下地面。

当前 Prefab 配置：

- Ground Mask：位掩码 `2048`，即第 11 层
- 向下检测距离：`0.04`
- 最小地面法线 Y：`0.9`
- 忽略 Trigger

`normal.y >= 0.9` 意味着只有较平缓的表面会被认为是地面；陡坡或墙面不会提供接地状态。

## 6. 当前实际参数

以下数值来自 `Player_MirrorTrial.prefab`，不是仅来自脚本默认值。

### 6.1 基础移动

| 参数 | 当前值 | 含义 |
| --- | ---: | --- |
| `moveSpeed` | 5 | 最大水平速度 |
| `acceleration` | 30 | 地面起步/变向加速度 |
| `deceleration` | 20 | 无输入或锁移动时的刹车速度 |
| `airAcceleration` | 30 | 空中水平控制加速度 |
| `airDeceleration` | 3 | 空中松开方向后的减速度，用于保留跳跃惯性 |
| `jumpSpeed` | 7.5 | 起跳瞬间纵向速度 |
| `baseGravityModifier` | 1 | 上升阶段重力倍率 |
| `fallGravityMultiplier` | 1.15 | 下落阶段重力倍率 |
| `jumpCutMultiplier` | 0.55 | 松开跳跃后的上升速度保留比例 |
| `coyoteTime` | 0.08 秒 | 离地后仍可起跳的窗口 |
| `jumpBufferTime` | 0.10 秒 | 提前按跳的缓存窗口 |

### 6.2 普通闪避

| 参数 | 当前值 |
| --- | ---: |
| 距离 | 3.2 |
| 持续时间 | 0.22 秒 |
| 推导速度 | 约 14.55 单位/秒 |
| 无敌开始 | 第 0.03 秒 |
| 无敌持续 | 0.14 秒 |
| 冷却 | 0.28 秒 |
| 输入缓存 | 0.10 秒 |
| 空中闪避 | 关闭 |

闪避方向优先使用当前水平输入；没有输入时使用角色面向。闪避期间锁定普通移动，并用 `ApplyForcedVelocity` 保持恒定水平速度。

### 6.3 EchoDash

| 参数 | 当前值 |
| --- | ---: |
| 距离 | 5 |
| 持续时间 | 0.18 秒 |
| 推导速度 | 约 27.78 单位/秒 |
| 无敌时间 | 0.15 秒 |
| 冷却 | 3 秒 |

EchoDash 只按当前面向冲刺，不读取当帧方向输入；期间同时关闭输入、锁普通移动，并通过强制速度移动。

## 7. 强制位移与移动锁

`PlayerMotor` 提供两种控制权：

### 7.1 移动锁

有效移动锁由两部分合并：

```text
EffectiveMovementLock = Motor.MovementLocked || StateMachine.ShouldLockMovement
```

状态机在 `Attack`、`Dodge`、`Hurt`、`Dead` 时要求锁移动。攻击、弓蓄力、技能、受击等组件也会直接设置 `MovementLocked`。

锁移动不会冻结物理，而是把水平输入视为 0，并按减速度把 `currentMoveSpeed` 拉向 0；同时禁止起跳和转向。

### 7.2 强制速度

闪避、EchoDash、击退和部分攻击命中反作用统一调用：

```csharp
ApplyForcedVelocity(Vector2 velocity, float duration)
```

在计时器有效期间，`FixedUpdate` 直接把刚体速度写成该固定值并提前返回，因此普通水平控制、跳跃和手动重力暂时都不执行。中断动作时可调用 `CancelForcedVelocity()`，默认会把刚体速度清零。

## 8. 状态与动画如何跟随运动

基础状态由 `PlayerStateMachine` 解算：

```text
外部动作请求
  > 空中：JumpRise / JumpFall
  > 刚落地：Land（默认保持约 0.1667 秒）
  > 地面有水平输入：Run
  > Idle
```

`PlayerAnimationDriver` 再把这些状态映射到 `Idle`、`Run`、`JumpRise`、`JumpFall`、`Land`、`Dash` 等 Animator 状态。普通运动没有使用 Root Motion，动画只是表现层，实际位置仍由刚体速度决定。

## 9. 值得注意的现状与风险

### 9.1 现有优点

- 输入、运动、状态、动画职责清晰，基础移动只有一个速度写入中心。
- 接地结果集中在 `PlayerMotor`，避免多个组件各自判断地面。
- 已具备土狼时间、跳跃缓存和可变跳高，基础平台手感能力完整。
- 闪避、冲刺、击退共用强制速度接口，扩展新动作相对直接。
- Prefab 参数集中在 `PlayerTuning`，策划可直接在 Inspector 调整。

### 9.2 潜在问题

1. **强制速度期间完全跳过重力。** 闪避、EchoDash 或击退持续期间，纵向速度保持常量；若在空中使用或受到斜向击退，轨迹会呈直线而不是抛物线。这是当前实现特性，是否符合设计需要通过实机确认。
2. **移动锁与强制速度是两个独立机制。** 多个系统都直接写 `MovementLocked` 布尔值；如果两个动作重叠，一个动作结束时写回 `false`，理论上可能提前解除另一个动作的锁。当前依赖动作互斥/中断管理来避免冲突，没有引用计数或锁所有者机制。
3. **动作请求也是单槽位。** `PlayerStateMachine.requestedAction` 只有一个值，并非优先级栈；后来的请求会覆盖前者。中断系统需要保证各动作先正确清理。
4. **状态机读取输入存在一帧时序差。** 状态机执行顺序 `-100`，输入读取器为 `-90`，因此状态机在 `Update` 中看到的 `MoveX` 是上一帧采样值。物理运动仍读取本帧稍后更新的值，但 Run/Idle 动画切换可能晚一帧。
5. **Prefab 根对象当前仍在 Default 层。** Prefab 有 `Player` Tag，但根对象 `m_Layer: 0`。代码的接地层过滤可避免自检，但若项目其他碰撞矩阵预期玩家位于专用 Player 层，需要再次核对。
6. **旧新两套玩家代码并存。** `Assets/Scripts` 的模板控制器仍在仓库中，后续搜索或挂组件时容易误用，应通过文档、目录命名或清理策略明确边界。
7. **当前结论尚未经过 Play Mode 验证。** 特别需要实测坡面、平台边缘、低帧率输入缓存、攻击/受击/闪避互相中断以及高速冲刺穿透。

## 10. 建议的验证清单

- 站立、起步、反向、松手刹停是否符合预期。
- 轻点与长按跳跃高度是否明显区分。
- 离开平台 0.08 秒内起跳，以及落地前 0.10 秒按跳是否稳定触发。
- 斜坡法线接近 0.9 阈值时是否出现接地抖动。
- 攻击或受击结束后，移动锁是否总能恢复。
- 闪避被受击、死亡或场景演出打断时，速度和无敌状态是否正确清理。
- EchoDash 在台阶、薄墙、高速碰撞情况下是否会穿透或卡住。
- 角色在移动输入和面向不一致时，闪避与 EchoDash 的方向是否符合玩家预期。
- 确认 Default 层是否是玩家 Prefab 的有意配置。

## 11. 关键文件索引

- `Assets/MirrorTrial/Prefabs/Characters/Player_MirrorTrial.prefab`
- `Assets/MirrorTrial/Scripts/Player/PlayerInputReader.cs`
- `Assets/MirrorTrial/Scripts/Player/PlayerMotor.cs`
- `Assets/MirrorTrial/Scripts/Player/PlayerTuning.cs`
- `Assets/MirrorTrial/Scripts/Player/PlayerStateMachine.cs`
- `Assets/MirrorTrial/Scripts/Player/PlayerAnimationDriver.cs`
- `Assets/MirrorTrial/Scripts/Player/PlayerDodgeController.cs`
- `Assets/MirrorTrial/Scripts/Player/PlayerAbilityLoadout.cs`
- `Assets/MirrorTrial/Scripts/Player/PlayerCombat.cs`
- `Assets/MirrorTrial/Scripts/Player/PlayerDamageReceiver.cs`

