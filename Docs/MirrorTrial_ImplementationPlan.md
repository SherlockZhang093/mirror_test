# 《镜中试炼》实现计划

## 目标

把当前 Unity 2D Platformer 模板逐步改造成《镜中试炼》的可演示 Demo。现阶段不急着写完整系统，先明确资源缺口、实现顺序、验收标准和后续 AI 的工作边界。

核心闭环：

```text
现实层推进 -> 清小怪 -> 镜子挡路 -> 进入镜像层 Boss
-> 击败镜像自我 -> 获得能力 -> 回到现实层继续推进
-> 最终 Boss 综合考验已获得能力
```

## 当前项目基础

项目类型：Unity 2022.3.62f3c1，2D Platformer 模板，URP + Cinemachine。

已有可用内容：

| 类型 | 当前资源 |
| - | - |
| 角色 | `Assets/Prefabs/Player.prefab`、`Assets/Prefabs/Enemy.prefab` |
| 角色动画 | Player Idle/Run/Jump/Land/Hurt/Death/Victory/Spawn，Enemy Idle/Run/Hurt/Death |
| 关卡基础 | `Assets/Scenes/SampleScene.unity`，Environment Tiles/Sprites |
| 特效占位 | Trail Prefabs、Particle Prefabs、Bounce Effect Prefabs、自定义材质 |
| 代码基础 | PlayerController、EnemyController、Health、KinematicObject、AnimationController |

当前判断：项目适合先做“模板复用 + 占位资源驱动”的垂直切片，不建议一开始追求完整美术规格。

## 阶段 0：资源定位与补齐清单

目标：先知道“现有能复用什么，必须补什么”。

### 关卡资源

| 需求 | 现有可用 | 缺口 |
| - | - | - |
| 现实层平地推进 | Ground、Floating tiles、Building、Nature sprites | 需要按策划重搭 5 屏 + 5 屏 + 2 屏结构 |
| 高台远程怪点位 | Floating tiles 可用 | 需要明确每波 spawn marker |
| 镜像层 Boss 平台 | Ground tiles 可用 | 需要独立 Boss arena 场景或 prefab |
| 镜子门 | 可用 Crystal/Archway/材质占位 | 需要镜子门 prefab、交互触发、碎裂状态 |
| 镜域氛围 | Colored environment sprites、Additive/Emissive materials | 需要红、蓝紫、金黑三套视觉区分 |

优先资源任务：

1. 用现有 tiles 搭 `Level_Reality_01`，只要求路径和战斗区域成立。
2. 做 `MirrorGate` 占位 prefab，可以先用 Crystal/Archway + 发光材质。
3. 做 `Arena_BladeMirror`，15m 单平台。
4. 做 `Arena_Echo`，20m 主平台 + 右侧 2m 断层小台。
5. 做 `Arena_FinalMirror`，22m 宽平台 + 两侧低平台。

### 角色资源

| 角色 | 现有可用 | 处理方式 |
| - | - | - |
| 玩家 | Player prefab + Player 动画 | 直接作为主角基础 |
| 近战小怪：影卫 | Enemy prefab + Enemy 动画 | 先复用 Enemy，调暗色材质 |
| 远程小怪：棱光射手 | Enemy prefab 可占位 | 需要远程攻击前摇/发射动画，可先用 Idle + 特效替代 |
| 刃镜 Boss | Player prefab + Player 动画 | 复用玩家外形，红色 tint/outline/特效区分 |
| 回声 Boss | Player prefab + Player 动画 | 复用玩家外形，蓝紫 tint/残影区分 |
| 最终 Boss | Player prefab + Player 动画 | 复用玩家外形，金黑 tint + 组合特效区分 |

优先资源任务：

1. 建立 `PlayerMirror_Red`、`PlayerMirror_Purple`、`PlayerMirror_Gold` 三个外观 prefab。
2. 不新增复杂动画，先复用 Player 动画控制器。
3. 技能前摇用特效和停顿表达，降低动画制作压力。
4. 残影可以用复制 SpriteRenderer + 半透明材质先实现。

### 特效资源

| 需求 | 可先复用 | 后续增强 |
| - | - | - |
| 镜刃弹道 | Trail Prefabs、LaserTrailMaterial、Additive 材质 | 做蓝白/红色两版 projectile prefab |
| 命中碎镜 | Confetti/Bounce Effect 占位 | 替换成碎片粒子 |
| Boss 蓄力预警 | Emissive Red/Blue 材质、简单圆形/线形 VFX | 做清晰 WarningVfx |
| 回声残影 | SpriteRenderer ghost + Trail | 增加爆炸预警闪烁 |
| 全屏脉冲 | 简单圆环/屏幕闪白 | 最终 Boss 专用金色脉冲 |

## 阶段 1：第一条可玩切片

目标：先做“玩家能打、Boss 能放镜刃、击败后能解锁镜刃”。

范围：

1. 玩家基础移动沿用模板。
2. Health 从 1 点生命扩展为数值生命。
3. 普攻可以先用简单近战 hitbox。
4. 实现镜刃的玩家版和 Boss 版。
5. 做刃镜 Boss 最小 AI：近距离追击斩，远距离镜刃波。
6. 击败 Boss 后解锁玩家 K 键镜刃。

验收：

1. 玩家进入刃镜场地后可以和 Boss 对战。
2. Boss 镜刃有明显前摇和红色预警。
3. 玩家击败 Boss 后可以释放玩家版镜刃。
4. 玩家版和 Boss 版来自同一套配置思路，而不是完全分开的硬编码。

## 阶段 2：现实层波次与镜子门

目标：让 Demo 从“单 Boss 测试”变成“现实层推进 -> 镜像层”的流程。

范围：

1. `WaveConfig` 或简化版 wave data。
2. 小怪出生点、战斗区域锁定、清场检测。
3. 第一关 3 波小怪。
4. 镜子门靠近交互。
5. 进入镜像层、击败 Boss、返回现实层、镜子碎裂。

验收：

1. 第一关现实层可以完整推进到镜子门。
2. Wave 1/2/3 按策划顺序出现。
3. 清完 Wave 3 后镜子门激活。
4. Boss 战结束后返回现实层并打开前路。

## 阶段 3：轻量技能管线

目标：把阶段 1 的临时技能整理成 PRD 中的轻量 Ability System。

建议模块：

| 模块 | 作用 |
| - | - |
| AbilityConfig | ScriptableObject，描述技能模板和变体 |
| AbilityVariantConfig | Player/Boss/Enemy 版本参数 |
| AbilitySystemComponent | 持有技能、检查冷却、发起释放 |
| AbilityExecutor | 根据模板执行 MeleeHit/LinearProjectile/DashAttack 等 |
| GameplayEffectConfig | 伤害、击退、无敌、生成对象、解锁能力 |
| FeedbackProfile | 动画、特效、音效、震动、顿帧 |
| BossAbilitySelector | 按阶段、距离、冷却、权重选技能 |

优先模板：

1. MeleeHit：普攻、小怪近战、Boss 追击斩。
2. LinearProjectile：镜刃、小怪射击。
3. DashAttack：回声冲刺、位移斩。
4. DelayedArea：残影爆炸。
5. Sequence：三连镜刃、最终组合技。
6. RadialPulse：最终 Boss 全屏脉冲，可最后做。

## 阶段 4：第二能力与第二关

目标：加入回声 Boss 和回声冲刺，让玩家获得第二个能力。

范围：

1. 第二关现实层 3 波小怪，加入更多远程怪。
2. EchoDash 玩家版：5m 冲刺、0.15s i-frame、留下残影。
3. EchoDash Boss 版：冲刺突进、残影、连续冲刺。
4. DelayedArea 支撑残影爆炸。
5. 击败回声 Boss 后解锁 L 键回声冲刺。

验收：

1. 玩家能用镜刃处理第二关远程怪。
2. 回声 Boss 的冲刺和残影有可读预警。
3. 玩家获得回声冲刺后，能用 i-frame 规避攻击。

## 阶段 5：最终 Boss 与组合技

目标：展示“能力复用”和“最终综合考验”。

范围：

1. 最终 Boss P1 使用镜刃系能力。
2. P2 加入回声冲刺系能力。
3. Sequence 实现 `TripleMirrorBlade` 和 `MirrorEchoCombo`。
4. HP <= 80 触发 `MirrorCollapsePulse`。

验收：

1. 最终 Boss 至少有两个阶段。
2. 最终组合技由已有能力组合，不写成完全独立逻辑。
3. 玩家需要使用镜刃输出、回声冲刺规避关键攻击。

## 阶段 6：编辑器与答辩展示

目标：让“技术策划能力”被看见。

优先做自定义 Inspector，不急着做完整 EditorWindow。

展示点：

1. 同一个 MirrorBlade 配置里有 PlayerVariant 和 BossVariant。
2. BossVariant 有 castTime、warningVfx、recoveryTime、punishWindow。
3. PlayerVariant 响应更快、冷却更短、反馈更爽。
4. BossConfig 引用 AbilityConfig，不硬编码招式。
5. 修改配置后，运行时表现变化。

可选增强：

1. Tools/Mirror Trial/Ability Editor 三栏编辑器。
2. Ability 配置校验。
3. JSON 导出给 AI 或策划审阅。

## 推荐执行顺序

1. 资源归档：整理现有资源，建立 MirrorTrial 文件夹结构。
2. 第一 Boss 切片：刃镜 Boss + 镜刃解锁。
3. 第一关流程：现实层波次 + 镜子门 + 返回。
4. 技能管线抽象：把临时代码整理成 Ability/Effect/Feedback。
5. 第二 Boss 切片：回声冲刺 + 残影爆炸。
6. 最终 Boss：组合技 + 双阶段。
7. 编辑器展示：Inspector 校验和 Variant 对比。
8. 打磨：手感、反馈、数值、答辩路线。

## 风险与控制

| 风险 | 控制方式 |
| - | - |
| 一开始系统做太大 | 先做切片，再抽象 |
| 美术资源不足 | 玩家/Boss/小怪先复用现有 prefab，靠颜色和特效区分 |
| 动画不足 | 前摇、后摇、命中反馈先用特效、停顿和音效表达 |
| Boss AI 复杂度失控 | 先做条件 + 权重选择，不做行为树编辑器 |
| 编辑器耗时过多 | 先做自定义 Inspector，不做节点编辑器 |

