# 敌兵编辑器与公共击飞系统 PRD

## 1. 目标

为普通敌人和 Boss 建立统一的敌兵配置入口与可复用受击状态。第一阶段重点完成公共击飞状态，保证任何玩家技能只要发送 `HitReactionType.Launch`，目标就必须立即中断当前行为并按技能给出的击退向量进入击飞。

## 2. 范围

包含：

- `EnemyAI` 普通敌人。
- `MirrorBossActorV2` 与 `MirrorBoss.prefab`。
- 敌兵 Profile 中的受击与击飞动画配置。
- Rigidbody2D 击飞、空中、下落、落地和恢复状态。
- AI/行为树在击飞期间的暂停，以及落地后的恢复。

不包含：

- 玩家受击和玩家击飞。
- TestGym 训练假人。
- 技能伤害、击退方向和力度的重新计算。

## 3. 核心规则

1. 技能是击飞结果的唯一权威来源。
2. `DamagePayload.reaction == HitReactionType.Launch` 时，敌兵必须进入公共击飞状态。
3. 公共击飞状态完整使用 `DamagePayload.knockback`，不得按敌人类型缩减、降级或拒绝。
4. 击飞会中断普通敌人 AI、Boss 连招、Boss 前摇与 Boss 恢复行为。
5. 敌兵编辑器只配置击飞的动画表现、落地表现与恢复时间。
6. 没有配置动画或 Animator 中缺少状态时，物理击飞仍必须正常完成。

## 4. 状态流程

```text
任意地面行为
  -> 收到 Launch
  -> Launch（应用技能给出的初速度）
  -> Airborne（上升）
  -> Falling（下降）
  -> Landing（接触地面）
  -> 原敌兵的可行动状态
```

击飞过程中，原 AI 不得写入 Rigidbody2D 速度。落地后普通敌人回到 Idle，Boss 回到阶段检查或 Approach。

## 5. 编辑器字段

`EnemyLaunchSettings`：

- 起飞动画状态名。
- 空中动画状态名。
- 下落动画状态名。
- 落地动画状态名。
- 落地恢复时间。
- 地面检测距离。

空动画名称表示不主动切换该阶段动画。

## 6. 数据归属

- 普通敌人：`EnemyAIProfile.launchSettings`。
- Boss：`MirrorBossSimpleProfile.launchSettings`。
- 技能：继续配置 `HitReactionType.Launch` 与 `customKnockback`。

## 7. 兼容与迁移

- 保留原有轻受击和重受击处理。
- 原 `EnemyAI` 中针对 Launch 的一次性 `AddForce` 迁移到公共控制器。
- 原 `MirrorBossActorV2` 的 `payload.knockback * 0.25f` 不再用于 Launch。
- 旧 Profile 未配置新字段时使用默认动画名称和落地恢复时间。

## 8. 验收标准

1. 普通敌人收到 Launch 后完整使用技能击退向量离地。
2. Boss 在追击、前摇、连招和恢复状态收到 Launch 后均会被中断并离地。
3. 击飞期间 AI 与行为树不会覆盖物理速度。
4. 上升、下降、落地动画可分别配置。
5. 未配置击飞动画时仍能完成击飞和落地恢复。
6. 落地后普通敌人和 Boss 能恢复原有战斗逻辑。
7. 玩家受击系统不发生任何变化。
