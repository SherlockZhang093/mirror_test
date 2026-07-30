# MirrorTrial 技能与连招编辑器交接文档

> 本文档用于把当前技能编辑器的设计意图、数据结构、运行逻辑和已知问题交接给后续开发者或新的 Codex 账号。

## 1. 编辑器入口与目标

Unity 菜单入口：

`Tools/镜像试炼/战斗/玩家按键与连招编辑器`

编辑器的目标是把两类工作放在同一个窗口中：

1. 在上方用节点图编辑一套连招的构成与分支。
2. 选中某个招式节点后，在下方直接编辑该招式自己的动画、判定、伤害、蓄力和命中反馈，并在 TestGym 中运行时预览。

主要编辑器文件：

- `Assets/MirrorTrial/Editor/PlayerInputComboEditorWindow.cs`
- `Assets/MirrorTrial/Editor/PlayerInputComboEditorWindow.Graph.cs`
- `Assets/MirrorTrial/Editor/PlayerInputComboEditorWindow.RuntimePreview.cs`
- `Assets/MirrorTrial/Editor/PlayerInputComboEditorWindow.Boss.cs`

主要运行时文件：

- `Assets/MirrorTrial/Scripts/Player/PlayerMoveComboGraph.cs`
- `Assets/MirrorTrial/Scripts/Player/PlayerCombat.cs`
- `Assets/MirrorTrial/Scripts/Player/PlayerInputReader.cs`
- `Assets/MirrorTrial/Scripts/Player/PlayerAnimationDriver.cs`

当前玩家数据存储在：

- `Assets/MirrorTrial/Prefabs/Characters/Player_MirrorTrial.prefab`

## 2. 核心数据观念

### 2.1 连招与招式分开理解

`PlayerComboGraph` 表示一整套连招图，包含：

- 使用的武器类型
- 入口按键
- 起始节点
- 招式节点列表
- 节点之间的连线列表

`PlayerComboMove` 表示图上的一个具体招式节点。

虽然不同节点可以引用同一段动画，但它们不是同一个招式的“共享实例”。每个节点都应被视为独立招式，可以拥有不同的：

- 伤害
- 击退
- 削韧
- 攻击类型
- 目标反应
- 动画速度
- 蓄力配置
- 命中反馈

换言之，“引用同一段动画”只表示视觉资源复用，不表示战斗参数共享。

### 2.2 当前仍保留的招式库

代码中保留了 `moveLibrary` / `PlayerMoveTemplate`，主要用于旧数据迁移和初始化。当前编辑体验应以连招图中的 `PlayerComboMove.move` 为准，不需要额外暴露一个让用户理解“实例化”的界面。

## 3. 连招图编辑

连招以节点和有向连线表达：

```text
第一拳 → 第二拳 ──松开──→ 快速第三拳
                 └─按住──→ 蓄力重拳
```

节点代表具体招式；连线代表从一个招式进入下一招所需的输入条件。

`PlayerComboTransition` 的核心字段：

- `fromInstanceId`：来源节点
- `toInstanceId`：目标节点
- `input`：输入按键
- `condition`：输入条件
- `windowStart` / `windowEnd`：允许接续的时间窗口
- `holdThreshold`：旧字段，目前不作为最终蓄力分支的核心判定

输入条件：

- `Press`：按下
- `Tap`：轻点后松开
- `Hold`：保持按住
- `Release`：松开

## 4. 当前连招输入规则

当前拳击连招的设计结论：

1. 第一拳播放期间再次按下普攻，会缓存进入第二拳。
2. 只要在当前动画播放结束之前按下，就应算作连招。
3. 如果动画已经完全播完后才按，则重新从第一拳开始。
4. 第二拳结束时：
   - 普攻已经松开：进入快速第三拳。
   - 普攻仍然按住：进入蓄力重拳。
5. 进入蓄力重拳后，松开按键才释放重拳。

为避免第一次按键同时触发第一拳和第二拳，`GraphMoveRoutine` 会跳过招式开始的第一帧输入判定。

多目标与多段判定的默认规则：

- 伤害：每个目标独立处理
- 命中特效：每个目标生成
- 敌人闪光：每个目标自己的逻辑处理
- 攻击者反作用：每次判定只触发一次
- 全局顿帧：每次判定只触发一次
- 镜头震动：每次判定只触发一次
- 命中音效：每次判定只播放一次

## 5. 蓄力机制

### 5.1 最终设计

蓄力不应切换到另一段独立“蓄力动画”。

正确流程：

1. 播放重拳自己的动画。
2. 播放到配置的定格位置。
3. 将同一段动画冻结在该帧。
4. 玩家继续按住时持续累计蓄力。
5. 松开后从该帧继续播放重拳剩余动画。
6. 后续有效帧产生伤害，并按蓄力程度增加伤害和击退。

这样可以避免空手拳招误播 `SwordAttack`，也不会在释放时从头重播重拳。

### 5.2 可配置参数

`PlayerComboMove` 中与蓄力相关的字段：

- `enableCharge`：是否启用蓄力
- `chargeHoldNormalizedTime`：动画定格位置，范围 0～0.95
- `minimumChargeTime`：最低有效蓄力时间
- `maximumChargeTime`：达到满蓄力需要的时间
- `fullChargeDamageMultiplier`：满蓄力伤害倍率
- `fullChargeKnockbackMultiplier`：满蓄力击退倍率
- `autoReleaseAtFullCharge`：满蓄力后是否自动释放
- `showChargeEffect`：是否显示蓄力特效
- `chargeEffectPrefab`：自定义蓄力特效
- `chargeEffectOffset`：特效相对角色的位置
- `chargeEffectScale`：特效大小

`chargeHoldNormalizedTime = 0.35` 表示动画播放到总时长的 35% 时冻结。推荐把它放在身体后拉、拳头收紧完成的位置。

旧字段 `chargeAnimation` 仍为兼容序列化而保留，但已隐藏，运行时不应再使用。

### 5.3 动画冻结实现

`PlayerAnimationDriver` 保存当前 `AnimationClipPlayable` 和原始播放速度。

- 定格：将 Playable 播放速度设为 0。
- 释放：恢复原始播放速度。
- 如果招式没有直接使用 Animation Clip，而是使用 Animator State，则通过暂时设置 `Animator.speed = 0` 实现同样效果。

## 6. 招式内部编辑

选中连招图中的节点后，下方面板直接显示该节点的招式内容。

主要内容包括：

- 动画 Clip / 动画状态
- 动画帧率与帧数
- 前摇、有效时间、后摇
- 动画速度
- 移动锁定
- 攻击判定关键帧
- 判定框位置与大小
- 伤害、击退、削韧
- 普通攻击或重击类型
- 目标受击反应
- 蓄力设置
- 命中反馈

碰撞接触点不需要建立独立系统，而应直接通过招式内部的命中判定/判定框关键帧进行配置。

## 7. 命中反馈设计

命中反馈由技能决定是否启用，并允许逐项勾选和调节：

- 命中特效
- 全局顿帧
- 镜头反馈
- 命中音效
- 是否通知 Player 执行攻击者反作用

设计原则：

- 命中特效必须服从攻击方向。
- 特效的形状、拖尾与飞散方向应与攻击方向一致。
- 普通攻击和重击需要不同反馈强度。
- 连招后段重击默认应比前段轻击有更强的顿帧、镜头、击退和特效。
- 攻击者反作用属于 Player 自身逻辑，不写死在敌人或特效中。
- 敌人闪光属于 Enemy 自身逻辑，根据实际扣血比例决定强度，不由技能配置固定闪光序列。
- 当前顿帧采用全局顿帧，不分别设置攻击者和目标顿帧。

生成的拳击蓄力特效：

- `Assets/MirrorTrial/Art/Effects/PunchCharge/punch_charge_energy.png`
- `Assets/MirrorTrial/Art/Effects/PunchCharge/PunchChargeEnergyVFX.prefab`

特效由 `PixelCombatVfx` 驱动，并随蓄力比例变化。

## 8. 目标反应与击飞

技能一侧直接决定是否要求击飞：

- `enableTargetReaction`
- `targetReaction = HitReactionType.Launch`
- `useCustomKnockback`
- `customKnockback`

当前拳击蓄力重拳已配置为：

- 重击类型
- `Launch`
- 自定义击退约 `(5.5, 3.2)`
- 满蓄力继续放大击退

普通 `EnemyAI` 已支持 `Launch`：收到击飞后会通过 Rigidbody2D 添加冲量。

### 8.1 TestGym 假人现状

`Assets/MirrorTrial/Scenes/MirrorTrial_TestGym.unity` 中的红色 `DummyTarget` 只有：

- Collider2D
- Hurtbox
- Health
- SpriteRenderer

它没有 Rigidbody2D，也没有读取 `DamagePayload.knockback` 的受击组件，因此不能真实展示击飞。这是测试环境的缺口，不是重拳没有传出 Launch。

建议增加专用训练假人受击组件，使其支持：

- 普通后退
- 重受击
- 击飞
- 落地
- 自动复位
- 是否锁定位置

### 8.2 Boss 现状

当前 Boss Prefab：

- `Assets/MirrorTrial/Prefabs/Boss/MirrorBoss.prefab`

实际使用：

- `MirrorBossActorV2`

Boss 当前没有根据 `HitReactionType` 分类处理受击：

- 正常状态只承受 `payload.knockback * 0.25`
- `Combo` 或 `Windup` 中只扣血，不播放受击且不击退
- 阶段转换期间忽略受击处理
- 如果本次伤害触发阶段转换，会直接进入转换，本次击退不会执行

因此 Boss 当前实际上是抗击飞单位。后续建议：

- 普通攻击：扣血/闪光，不位移
- 重击：重受击动画和少量水平滑退
- Launch：默认不把 Boss 当杂兵抛飞，但应有明显后仰与硬直
- 破韧后 Launch：允许短距离真实离地
- 霸体攻击期间：扣血和闪光，但不中断

## 9. TestGym 运行时预览

测试场景：

- `Assets/MirrorTrial/Scenes/MirrorTrial_TestGym.unity`

编辑器中可以启动 TestGym 预览，并在 Play Mode 中：

- 模拟轻点普攻
- 模拟按下普攻
- 模拟松开普攻
- 调整慢动作
- 重置测试
- 将编辑器中的连招数据同步到运行中的玩家

运行时桥接只把 `comboGraphs` 从 Prefab 编辑数据同步给场景中的 Player，不会把整个 Prefab 覆盖到运行对象。

运行时状态会记录：

- 当前招式节点 ID 与名称
- 最近经过的连线
- 当前蓄力秒数
- 当前蓄力比例
- 当前伤害倍率
- 当前预览状态

连招图会用高亮显示当前运行节点和连线。

`PlayerInputReader` 为预览提供：

- `PreviewTap`
- `PreviewPress`
- `PreviewRelease`

## 10. 已知问题与开发优先级

### 高优先级

1. 为 TestGym 假人增加完整受击与击飞能力。
2. 为 `MirrorBossActorV2` 增加按 `HitReactionType` 和破韧状态分类的反应。
3. 在实际运行中重新微调蓄力定格位置，确保定格发生在正确姿势。
4. 确认蓄力时间从“进入定格”开始计算，还是包含上一招末尾已经按住的时间；当前会携带连线阶段的按住时长。

### 中优先级

1. 继续简化编辑器 UI，只在选中节点后展示相关参数。
2. 将连线窗口以动画时间或帧数做更直观的可视化。
3. 增加判定框与动画逐帧联动预览。
4. 增加普通命中、重击、击飞、击倒的预览目标。
5. 为全局顿帧建立统一服务，避免多目标重复触发。

## 11. 接手时建议先阅读的文件

按以下顺序：

1. 本文档
2. `PlayerMoveComboGraph.cs`
3. `PlayerCombat.cs`
4. `PlayerAnimationDriver.cs`
5. `PlayerInputComboEditorWindow.Graph.cs`
6. `PlayerInputComboEditorWindow.RuntimePreview.cs`
7. `Player_MirrorTrial.prefab`
8. `MirrorTrial_TestGym.unity`
9. `EnemyAI.cs`
10. `MirrorBossActorV2.cs`

## 12. 给新 Codex 账号的启动提示

可以直接发送：

> 请先完整阅读 `Docs/Design/CODEX_SKILL_EDITOR_HANDOFF.md`，再检查其中列出的核心代码文件。这个项目正在开发 Unity 玩家技能与连招编辑器，请延续文档中的设计约定，不要重新建立另一套互相冲突的连招系统。开始修改前先确认当前工作树中的既有改动。

