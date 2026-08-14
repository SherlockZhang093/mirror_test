# Mirror Trial 敌人 AI 系统参考文档

> 适用对象：接手本项目的新人 / 其他 AI 助手
> 生成日期：2026-07-15
> 涵盖：敌人 AI 系统的完整架构、演变历程、使用方式、扩展路径

---

## 0. 我们做了什么（对话历程）

本项目是 Unity 2D 横版动作游戏 Mirror Trial。敌人 AI 系统是从零搭起来的，过程如下：

1. **盘点现状**：level_01 原有敌兵只是 `TrainingEnemy`（2HP、无 AI、无行为），由 3 个刷怪点、2 个战斗区、8 只训练敌组成。运行时能刷怪，但敌人不会动、不会攻击。
2. **废弃旧系统**：用户决定放弃简单敌人系统，改为构建一个**小型敌人编辑器**，要求支持：① 关卡里直接放置敌人；② 每个敌人独立配置 AI；③ 保留 WaveDefinition 波次刷怪系统。
3. **核心架构落地**：
   - `EnemyAIProfile`（ScriptableObject）：纯数据，定义全部 AI 参数
   - `EnemyAI`（MonoBehaviour）：通用状态机，读取 Profile 执行行为
   - `SpawnPoint` 扩展：携带 AI 配置引用、覆盖血量/移速
   - `CombatEncounter` 集成：生成敌人时注入 AI 配置
   - 三类预制体：近战兵 / 远程兵 / 精英镜像兵 + 对应 Profile
4. **编辑器窗口** `EnemyWaveEditor`：Scene 视图点击放置敌人 + 一键生成完整预制体
5. **全中文化**：Inspector 字段全用 `ChineseLabel` + `InspectorName` 中文化
6. **输出本参考文档**

---

## 1. 架构总览（三层分离）

```
关卡设计层 (CombatEncounter + WaveDefinition + SpawnPoint)
    | 运行时 Instantiate + ApplySpawnPointOverrides
    v
敌人实例 (GameObject: SpriteRenderer + BoxCollider2D + Hurtbox + Rigidbody2D + EnemyAI)
    | 引用
    v
EnemyAIProfile.asset (ScriptableObject 纯数据，不运行)
```

| 层 | 职责 | 文件 |
|----|------|------|
| 行为数据 | 敌人"性格"数值 | EnemyAIProfile.asset |
| 运行时行为 | 状态机执行 | EnemyAI.cs |
| 关卡放置 | 在哪 / 何时 / 多少 | SpawnPoint + CombatEncounter |

---

## 2. 核心文件清单

| 文件 | 位置 | 职责 |
|------|------|------|
| EnemyAIProfile.cs | Scripts/Enemies/ | ScriptableObject，全部 AI 参数（24 字段） |
| EnemyAI.cs | Scripts/Enemies/ | 通用状态机，RequireComponent: Hurtbox / Rigidbody2D / Collider2D |
| SpawnPoint.cs | Scripts/Level/ | 刷怪点，AI 配置覆盖 + 巡逻路径 |
| CombatEncounter.cs | Scripts/Level/ | 波次战斗，门控 + 逐波生成 + 清场判定 |
| LevelCommon.cs | Scripts/Level/ | WaveDefinition / WaveSpawnEntry 数据结构 |
| EnemyWaveEditor.cs | Editor/Enemies/ | 编辑器：一键生成预制体 + Scene 视图放置 |
| Hurtbox.cs | Scripts/Combat/ | 受击盒，SendMessageUpwards 传导伤害 |
| DamagePayload.cs | Scripts/Combat/ | readonly struct 伤害数据 |

---

## 3. EnemyAIProfile 参数速查

**基础**：maxHitPoints(2) / moveSpeed(2.5) / flipVisualByVelocity(true)
**侦测追击**：detectionRange(8) / loseInterestRange(12) / stopDistance(1.2) / useForwardDetection(false) / forwardDetectionAngle(90) / patrolWaitTime(1) / enablePatrol(false)
**攻击**：attackWindup(0.35) / attackCooldown(1.2) / attackRange(1.2) / attackDamage(1) / damageDelay(0.1) / projectilePrefab(null) / projectileSpawnOffset(0.4,0.2) / lockMovementWhileAttacking(true)
**受击死亡**：hurtStun(0.25) / knockbackForce(4) / deathFadeDelay(0.3)
**移动限制**：ledgeCheckDistance(0.5) / wallCheckDistance(0.3) / canTurnAtLedge(true)

`IsRanged` 属性 = `projectilePrefab != null`（决定是否远程攻击）

---

## 4. EnemyAI 状态机

5 个状态：`Idle`(待机) / `Chase`(追击) / `Attack`(攻击) / `Hurt`(受击) / `Dead`(死亡)

状态转换：
- Idle --(发现玩家, 距离<detectionRange)--> Chase
- Chase --(距离<=stopDistance 且冷却就绪)--> Attack
- Attack --(前摇结束)--> 近战 OverlapBox / 远程发射投射体 --> Idle(开始冷却)
- 任意非 Dead --(OnDamagePayloadReceived)--> Hurt --(硬直结束)--> Idle
- Hurt --(HP<=0)--> Dead --(deathFadeDelay 后)--> Destroy

**参数覆盖优先级**：实例 override 值 > Profile 值 > 硬编码默认值

**受击链路**：Hurtbox 接收攻击 -> `SendMessageUpwards("OnDamagePayloadReceived", payload)` -> EnemyAI 减血 + 击退 + 进入硬直

**寻找玩家**：Start 里 `FindObjectOfType<PlayerInputReader>()`；玩家通过 Tag "Player" 识别

---

## 5. SpawnPoint / CombatEncounter

**SpawnPoint 字段**：spawnId / role(近战/远程/精英) / defaultEnemyPrefab / aiProfile / overrideHitPoints / overrideMoveSpeed / facingDegrees / patrolPath

**CombatEncounter 流程**：
```
玩家进入 trigger
  -> StartEncounter() 关门
  -> 逐波 RunWave：
       SpawnEntry() 生成敌人 + ApplySpawnPointOverrides() 注入 AI 配置
       等 delay / interval / 波间延迟
  -> 全部波次完成 且 全部敌人死亡
  -> ClearEncounter() 开门 + 触发 OnCleared 事件
```
生成时通过 `SpawnedEnemyLink` 跟踪敌人，OnDestroy 时通知 CombatEncounter 减计数。

---

## 6. 编辑器使用（EnemyWaveEditor）

菜单：`Tools -> 镜像试炼 -> 关卡 -> 敌人波次编辑器`

**面板 1：一键生成预制体**
1. 选模板（自定义 / 近战兵 / 远程兵 / 精英兵）自动填入默认值
2. 配 名称 / 颜色 / 大小 / Layer / AI Profile
3. 可点"同时新建 AI Profile"生成空白配置
4. 点"生成预制体" → 自动创建含 6 组件的 .prefab
5. 勾选"生成后自动填入波次面板" → 预制体自动进入下方面板

输出路径：`Assets/MirrorTrial/Prefabs/Enemies/Characters/`

**面板 2：波次放置**
1. 选预制体 + AI 配置 + 目标 CombatEncounter
2. 设波次索引 / 数量 / 延迟 / 间隔
3. 点亮"开启 Scene 视图放置"
4. Scene 视图左键点击地面 → 自动创建 SpawnPoint 并注入波次
5. Ctrl+点击 退出

---

## 7. 预制体规范（必须 6 组件）

| 组件 | 关键设置 |
|------|---------|
| Transform | 默认 |
| SpriteRenderer | drawMode=Sliced，大小匹配视觉（EnemyAI 用它做 flipX 翻转） |
| BoxCollider2D | isTrigger=false，size 匹配 |
| Hurtbox | RequireComponent: Collider2D，接收玩家攻击 |
| Rigidbody2D | Dynamic / FreezeRotation / Interpolate / Continuous |
| EnemyAI | profile 字段绑定一个 AIProfile |

其他要求：Layer=7（非 Ground 层），Tag=Untagged，组件顺序 BoxCollider -> Hurtbox -> Rigidbody2D -> EnemyAI

---

## 8. 现有 AI 配置速查

| Profile | 定位 | HP | 速度 | 侦测 | 攻击方式 | 伤害 | 前摇 | CD | 硬直 |
|---------|------|:--:|:----:|:----:|---------|:----:|:----:|:--:|:----:|
| MeleeGruntAI | 近战小兵 | 3 | 2.5 | 8 | 近战 range=1.1 | 1 | 0.35s | 1.2s | 0.25s |
| RangedShooterAI | 远程点射 | 2 | 1.5 | 10 | 远程 range=5, stop=5 | 1 | 0.5s | 2s | 0.25s |
| EliteMirrorAI | 精英兵 | 8 | 3.5 | 9 | 近战 range=1.5 | 2 | 0.45s | 1s | 0.15s |

---

## 9. 已知限制与待办

| # | 问题 | 修复方向 |
|---|------|---------|
| 1 | 巡逻不生效（敌人非 SpawnPoint 子对象，GetComponentInParent 取不到） | 改为在 ApplySpawnPointOverrides 传递 patrolPath |
| 2 | 玩家为 null 时每帧 FindObjectOfType | 缓存 + 事件驱动 |
| 3 | 投射体硬绑 MirrorBladeProjectile | 抽象为接口 |
| 4 | 无受击无敌帧 | 加 invincibilityAfterHurt 参数 |
| 5 | Idle 不自动面向玩家 | 加 facePlayerOnDetect 参数 |
| 6 | TrainingEnemy 旧组件代码残留（Scripts/Level/TrainingEnemy.cs） | 可删除，无运行时影响 |

---

## 10. 扩展指南

- **新敌人类别**（投弹兵/护盾兵）：新建 EnemyAIProfile.asset → 编辑器生成预制体 → 关卡放置
- **新攻击方式**（扇形/弹幕）：改 `EnemyAI.PerformAttack()` + Profile 加枚举字段
- **Boss 多阶段**：EnemyAI 不支持阶段切换，给 Boss 单独写 Behavior（继承或独立）
- **新行为**（跳跃/冲锋）：加 State 枚举值 + Profile 参数 + Update 的 switch 分支
- **所有敌人交互**通过 `DamagePayload` 结构体；投射体在 `Scripts/Abilities/MirrorBladeProjectile.cs`；受击在 `Scripts/Combat/Hurtbox.cs`
