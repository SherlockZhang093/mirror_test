# 《镜中试炼》Player 3C 与手感框架策划案

## 1. 设计目标

Player 框架的目标不是单纯替换主角 Sprite，而是建立一套可以承载《镜中试炼》核心玩法的可扩展 3C 系统。

玩家需要在现实层和镜像层中完成横版动作闯关、近战攻击、远程能力释放、冲刺规避、受击反馈和能力成长。系统必须保证基础操作稳定，同时允许后续快速微调手感。

本阶段优先完成一个最小但正确的 Player 框架：

- 能稳定移动、跳跃、落地和翻面。
- 能播放新主角资源包中的基础动画。
- 能进行普通攻击，并预留连段空间。
- 能受伤、击退、短暂无敌和死亡。
- 能解锁并释放 `MirrorBlade` 与 `EchoDash`。
- 能通过参数调节移动、攻击、受击、反馈手感。

## 2. 3C 定位

这里的 3C 指 Character、Camera、Control。

### Character

主角是一个横版动作角色，初始能力较基础，后续通过击败镜像 Boss 夺回能力。

核心特征：

- 操作响应要轻快，不能拖。
- 攻击要有明确命中反馈。
- 受击要清楚，但不能长期剥夺控制。
- 能力获得后要明显改变战斗方式。

### Camera

当前阶段继续复用模板中的 Cinemachine 跟随相机。Player 框架只负责暴露角色位置和战斗事件，后续由 Feedback 或 Camera Shake 模块接入镜头反馈。

第一阶段只需要支持：

- 常规跟随。
- 命中轻微震动。
- Boss 重击或能力获得时较强震动。

### Control

玩家控制需要保持简单清晰：

| 输入 | 功能 |
| - | - |
| 左右方向 | 移动 |
| Jump | 跳跃 |
| Attack | 普通攻击 |
| MirrorBlade | 镜刃，击败刃镜后解锁 |
| EchoDash | 回声冲刺，击败回声后解锁 |

第一版可继续使用 Unity 旧 Input Manager，后续如果需要再迁移到新 Input System。

## 3. 玩家动作集

### 基础移动

玩家应具备横版平台动作的基础能力。

| 动作 | 说明 |
| - | - |
| Idle | 无输入时待机 |
| Run | 水平移动 |
| JumpRise | 起跳和上升 |
| JumpFall | 下落 |
| Land | 落地反馈 |
| Turn | 左右翻面 |

建议初始参数：

| 参数 | 初始值 | 用途 |
| - | - | - |
| MoveSpeed | 5.0 m/s | 水平最大速度 |
| Acceleration | 50 | 地面起步速度 |
| Deceleration | 60 | 松手停下速度 |
| AirAcceleration | 30 | 空中横向控制 |
| JumpSpeed | 7.5 | 起跳速度 |
| GravityScale | 1.0 | 默认重力 |
| FallGravityMultiplier | 1.4 | 下落加重 |
| JumpCutMultiplier | 0.5 | 松开跳跃后的短跳 |

### 普通攻击

普通攻击是玩家初始战斗手段，用于处理近战敌人和 Boss 破绽。

第一阶段只做一段攻击，后续扩展三段连击。

| 阶段 | 说明 |
| - | - |
| Startup | 前摇，播放攻击起手，暂不造成伤害 |
| Active | 命中帧，打开攻击判定 |
| Recovery | 后摇，限制移动和再次攻击 |
| CancelWindow | 可接下一段攻击或释放能力的窗口 |

建议初始参数：

| 参数 | 初始值 | 用途 |
| - | - | - |
| AttackStartup | 0.08s | 普攻前摇 |
| AttackActiveTime | 0.10s | 判定持续时间 |
| AttackRecovery | 0.20s | 后摇 |
| AttackDamage | 8 | 伤害 |
| AttackKnockback | 3.0 | 击退力度 |
| HitStop | 0.04s | 命中顿帧 |
| AttackMoveLock | 0.18s | 攻击时移动锁定 |

### 受击

受击的目标是表达“被打到了”，但不要让玩家长时间失去控制。

| 阶段 | 说明 |
| - | - |
| HitFlash | 角色闪白或变色 |
| HitStop | 短暂停顿，加强冲击 |
| Knockback | 根据攻击方向击退 |
| HurtLock | 短时间不可操作 |
| Invincible | 受击后短暂无敌 |

建议初始参数：

| 参数 | 初始值 | 用途 |
| - | - | - |
| HurtLockTime | 0.25s | 受击硬直 |
| InvincibleTime | 0.75s | 受击后无敌 |
| KnockbackX | 4.0 | 横向击退 |
| KnockbackY | 2.5 | 纵向击退 |
| HurtHitStop | 0.05s | 受击顿帧 |

### 死亡

死亡用于失败反馈和重生流程。第一阶段沿用模板中的重生逻辑，但动画和控制锁定改由 Player 框架统一管理。

死亡流程：

```text
HP <= 0
-> 进入 Dead 状态
-> 关闭玩家输入
-> 播放 Die 动画
-> 相机解除或停留
-> 延迟重生
-> 恢复 HP 和输入
```

## 4. 能力设计

### MirrorBlade

镜刃是第一个夺回能力，定位是远程攻击与远程敌人反制。

| 字段 | 玩家版建议值 | Boss 版建议值 |
| - | - | - |
| 类型 | LinearProjectile | LinearProjectile |
| 伤害 | 15 | 18 |
| 前摇 | 0.15s | 0.60s |
| 后摇 | 0.20s | 0.60s |
| 冷却 | 1.50s | 3.00s |
| 弹速 | 12 m/s | 9 m/s |
| 射程 | 10m | 12m |
| 穿透 | 2 | 0 |

玩家版强调爽快和成长感。Boss 版强调可读性和教学，因此前摇更长、预警更明显。

### EchoDash

回声冲刺是第二个夺回能力，定位是快速位移、规避危险和创造反击窗口。

| 字段 | 玩家版建议值 | Boss 版建议值 |
| - | - | - |
| 类型 | DashAttack | DashAttack |
| 距离 | 5m | 6m |
| 持续 | 0.18s | 0.15s |
| 无敌 | 0.15s | 无或特殊免疫 |
| 冷却 | 3.00s | 3.00s |
| 残影 | 0.5s 后消失 | 0.8s 后爆发 |

玩家版强调救命和调整站位。Boss 版强调空间压迫和残影威胁。

## 5. 状态机设计

Player 第一版使用轻量代码状态机，不急着引入大型状态机框架。

建议状态：

| 状态 | 说明 |
| - | - |
| Idle | 地面无输入 |
| Run | 地面移动 |
| JumpRise | 起跳和上升 |
| JumpFall | 下落 |
| Land | 落地短反馈 |
| Attack | 普攻 |
| Cast | 释放镜刃 |
| Dash | 回声冲刺 |
| Hurt | 受击 |
| Dead | 死亡 |

状态优先级：

```text
Dead > Hurt > Dash > Attack/Cast > Air > Ground
```

这能保证死亡和受击不会被普通移动打断，冲刺和攻击也能明确控制动画和输入响应。

## 6. 技术框架

建议新增目录：

```text
Assets/MirrorTrial/Scripts/Player
Assets/MirrorTrial/Scripts/Combat
Assets/MirrorTrial/Scripts/Abilities
Assets/MirrorTrial/Scripts/Feedback
Assets/MirrorTrial/Configs/Player
Assets/MirrorTrial/Configs/Abilities
```

建议脚本拆分：

| 脚本 | 职责 |
| - | - |
| PlayerInputReader | 读取输入，输出移动、跳跃、攻击、能力请求 |
| PlayerMotor | 处理移动、跳跃、重力、落地、翻面 |
| PlayerCombat | 管理普攻、攻击判定、受击锁定 |
| PlayerAbilityLoadout | 管理能力解锁和释放请求 |
| PlayerAnimationDriver | 根据状态播放新资源包动画 |
| PlayerTuning | 存放可调手感参数 |
| Hitbox | 攻击判定 |
| Hurtbox | 受击判定 |
| DamagePayload | 伤害、击退、来源等命中数据 |

第一阶段可以先用 MonoBehaviour 字段调参。等手感跑通后，再把参数迁移到 ScriptableObject。

## 7. 动画接入

新资源包的 `PlayerAnimator.controller` 没有 Animator 参数，更适合通过状态名直接播放。

建议由 `PlayerAnimationDriver` 统一映射：

| 游戏状态 | 动画名 |
| - | - |
| Idle | Idle |
| Run | Run |
| JumpRise | JumpRise |
| JumpFall | JumpFall |
| Land | Land |
| Attack | SwordAttack 或 PunchA |
| Cast MirrorBlade | AirSlash 或 SwordStandingSlash |
| Dash | Dash |
| Hurt | HitDamage |
| Dead | Die |

播放方式建议使用 `Animator.CrossFade`，避免硬切造成跳帧。

示例逻辑：

```text
状态变化
-> PlayerAnimationDriver 接收状态
-> 查询动画名
-> CrossFade 到目标动画
```

这样不需要批量替换旧动画 Sprite，也不用维护大量 Animator 参数。

## 8. 手感调节维度

Player 框架必须显式支持以下调节项。

### 移动手感

| 调节项 | 影响 |
| - | - |
| MoveSpeed | 跑动速度 |
| Acceleration | 起步响应 |
| Deceleration | 松手刹车感 |
| AirAcceleration | 空中可控性 |
| JumpSpeed | 跳跃高度 |
| FallGravityMultiplier | 下落重量感 |
| CoyoteTime | 离地后容错 |
| JumpBufferTime | 提前按跳容错 |

### 攻击手感

| 调节项 | 影响 |
| - | - |
| Startup | 出手快慢 |
| ActiveTime | 判定宽容度 |
| Recovery | 攻击拖不拖 |
| CancelWindow | 连段顺不顺 |
| HitStop | 命中重量感 |
| Knockback | 命中冲击感 |
| AttackMoveLock | 攻击时能否滑步 |

### 受击手感

| 调节项 | 影响 |
| - | - |
| HurtLockTime | 被打后僵直 |
| InvincibleTime | 容错和节奏 |
| KnockbackX/Y | 受击位移 |
| FlashTime | 受击可读性 |
| DeathDelay | 死亡节奏 |

## 9. 第一阶段验收标准

第一阶段完成后，应该能在 Unity 中验证：

- 玩家可稳定左右移动、跳跃、落地。
- 新主角素材能正确播放 Idle、Run、JumpRise、JumpFall、Land。
- 玩家按攻击键可以播放攻击动画并产生近战判定。
- 攻击命中敌人时有伤害、击退和顿帧。
- 玩家被攻击时有受击动画、击退、短暂无敌。
- 调整 Inspector 参数可以明显改变移动和攻击手感。
- Player 结构没有继续污染模板原始目录，核心新增内容位于 `Assets/MirrorTrial`。

## 10. 开发顺序

建议执行顺序：

1. 新建 `Assets/MirrorTrial` 目录结构。
2. 新建 Player 框架脚本，先接移动和跳跃。
3. 将当前 Player prefab 复制为 MirrorTrial 专用 Player prefab。
4. 接入新资源包 `PlayerAnimator.controller`。
5. 实现 `PlayerAnimationDriver` 的状态到动画映射。
6. 实现普攻、Hitbox、Hurtbox、DamagePayload。
7. 接入命中顿帧、击退、受击无敌。
8. 预留 `MirrorBlade` 和 `EchoDash` 能力入口。
9. 跑通后再做能力配置化。

## 11. 关键原则

不要一开始追求完整商业级角色控制器。当前目标是服务 Demo 闭环。

Player 框架要做到三件事：

- 基础操作稳定。
- 手感参数可调。
- 后续能力和 Boss 复用时不需要推倒重来。

只要这三点成立，后续刃镜、回声和最终 Boss 都能自然接到同一套玩法结构上。
