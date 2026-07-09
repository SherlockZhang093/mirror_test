# 《镜中试炼》AI 项目大纲

本文档给后续 AI 或开发者快速读取项目用。读完后应能理解：这是一个什么项目、要做什么、不该做什么、资源怎么找、实现顺序是什么。

## 项目一句话

《镜中试炼》是一个 Unity 2D 横版动作闯关 Demo：玩家在现实层推进，遇到镜子后进入镜像层，击败使用特定能力的“镜像自我”，夺回该能力，并在后续关卡和最终 Boss 中使用它。

## 核心体验

```text
Boss 先展示能力
-> 玩家观察前摇、预警和破绽
-> 玩家击败 Boss
-> 玩家获得该能力的玩家版
-> 后续关卡验证新能力
-> 最终 Boss 组合考验全部能力
```

重点不是做完整商业游戏，而是做一个结构清晰、可演示、能体现技术策划能力的 Demo。

## 项目边界

必须做：

1. 2 个现实层关卡。
2. 3 个镜像层 Boss 战：刃镜、回声、镜域之主。
3. 2 个可夺取能力：镜刃、回声冲刺。
4. 近战小怪、远程小怪。
5. 镜子门流程：进入、Boss 战、夺能、返回、碎裂开路。
6. 轻量 Ability System：玩家、小怪、Boss 共用技能配置思路。

暂不做：

1. 通用大型技能引擎。
2. 节点式技能编辑器。
3. 完整 Buff/Debuff 系统。
4. 联网、热更新、装备、技能树。
5. 大量新美术和复杂动画。

## 当前项目情况

Unity：2022.3.62f3c1。

技术基础：

| 内容 | 路径 |
| - | - |
| 玩家 prefab | `Assets/Prefabs/Player.prefab` |
| 敌人 prefab | `Assets/Prefabs/Enemy.prefab` |
| 示例场景 | `Assets/Scenes/SampleScene.unity` |
| 玩家控制 | `Assets/Scripts/Mechanics/PlayerController.cs` |
| 敌人控制 | `Assets/Scripts/Mechanics/EnemyController.cs` |
| 生命组件 | `Assets/Scripts/Mechanics/Health.cs` |
| 移动基类 | `Assets/Scripts/Mechanics/KinematicObject.cs` |
| 动画控制 | `Assets/Scripts/Mechanics/AnimationController.cs` |
| 角色动画 | `Assets/Character/Animations` |
| 角色 Sprite | `Assets/Character/Sprites` |
| 环境资源 | `Assets/Environment` |
| 额外特效/材质 | `Assets/Mod Assets` |

## 建议目录结构

后续实现时建议新增：

```text
Assets/MirrorTrial
  /Art
    /Characters
    /Environment
    /VFX
  /Prefabs
    /Characters
    /Enemies
    /Bosses
    /Abilities
    /Level
  /Configs
    /Abilities
    /Bosses
    /Levels
    /Waves
  /Scenes
  /Scripts
    /Abilities
    /Combat
    /Boss
    /Level
    /Feedback
  /Editor
```

原则：不要直接把新系统散放在模板原始目录里。模板代码可以接入，但《镜中试炼》自己的内容要有独立命名空间和目录。

## 资源使用策略

### 关卡

现实层使用现有 Environment Tiles/Sprites 搭建，先保证玩法路径。

镜像层用同一套地形资源换色或加后处理氛围：

| 场景 | 视觉方向 |
| - | - |
| 刃镜 | 赤红、红色轮廓光、简单长平台 |
| 回声 | 蓝紫、残影、断层小台 |
| 镜域之主 | 金黑、宽平台、两侧低台 |

### 角色

玩家、本体 Boss、最终 Boss 都优先复用 Player prefab 和 Player 动画。

镜像 Boss 区分方式：

1. SpriteRenderer tint。
2. 轮廓光或发光材质。
3. 不同技能特效。
4. 入场和击败演出。

小怪先复用 Enemy prefab：

1. 影卫：近战追击。
2. 棱光射手：远程站桩/后退射击。

### 特效

先用现有 Trail、Particle、Additive/Emissive 材质做占位。特效优先表达功能：

1. Boss 前摇必须清晰。
2. 危险区域必须可读。
3. 玩家命中必须有反馈。
4. 能力获得必须有仪式感。

## 关卡设计摘要

| 段落 | 内容 |
| - | - |
| 第一关现实层 | 约 5 屏，3 波小怪，终点镜子门 |
| 刃镜 Boss | 15m 单平台，镜刃能力教学 |
| 第二关现实层 | 约 5 屏，远程怪压力更高 |
| 回声 Boss | 20m 平台 + 2m 断层，回声冲刺教学 |
| 尾声现实层 | 约 2 屏，无小怪，叙事推进 |
| 最终 Boss | 22m 宽平台 + 两侧低台，组合考验 |

## 角色与数值摘要

玩家：

| 动作 | 参数 |
| - | - |
| 移动 | 5 m/s |
| 跳跃 | 高度约 2.5m |
| 闪避 | 3m，0.25s，0.2s i-frame，1s CD |
| 普攻 | 三段 8/8/12，总时长 0.9s |
| 生命 | 100 |

小怪：

| 类型 | HP | 说明 |
| - | - | - |
| 影卫 | 30 | 近战追击，基础沙包 |
| 棱光射手 | 20 | 远程射击，保持距离 |

Boss：

| Boss | HP | 能力主题 |
| - | - | - |
| 刃镜 | 200 | 镜刃、位移斩、三连镜刃 |
| 回声 | 250 | 冲刺、残影、残影爆炸 |
| 镜域之主 | 400 | 镜刃 + 回声组合 |

## 技能系统目标

参考 GAS 的思想，但只做项目需要的轻量版本。

核心分层：

```text
AbilitySystemComponent
  -> 管理角色拥有的技能、冷却、标签、释放请求

AbilityConfig
  -> 描述技能是什么，用哪个模板，有哪些变体

AbilityVariantConfig
  -> 描述 Player/Boss/Enemy 的数值、时序、条件、反馈

GameplayEffectConfig
  -> 描述命中后的规则结果：伤害、击退、无敌、生成对象、解锁技能

FeedbackProfile
  -> 描述动画、特效、音效、震动、顿帧
```

## 技能模板

最小可用模板：

| 模板 | 用途 |
| - | - |
| MeleeHit | 普攻、小怪近战、Boss 追击斩 |
| LinearProjectile | 镜刃、小怪射击、Boss 弹道 |
| DashAttack | 回声冲刺、位移斩 |
| DelayedArea | 残影爆炸、延迟爆点 |
| Sequence | 三连镜刃、最终组合技 |
| RadialPulse | 最终 Boss 全屏脉冲 |

## 能力配置摘要

### MirrorBlade

| 字段 | Player | Boss |
| - | - | - |
| 模板 | LinearProjectile | LinearProjectile |
| 伤害 | 15 | 18 |
| 前摇 | 0.15s | 0.6s |
| 后摇 | 0.2s | 0.6s |
| 冷却 | 1.5s | 3.0s |
| 弹速 | 12m/s | 9m/s |
| 射程 | 10m | 12m |
| 穿透 | 2 | 0 |
| 反馈 | 蓝白碎镜 | 红色蓄力和碎镜 |

### EchoDash

| 字段 | Player | Boss |
| - | - | - |
| 模板 | DashAttack | DashAttack |
| 距离 | 5m | 6m |
| 持续 | 0.18s | 0.15s |
| i-frame | 0.15s | 无或免碰撞 |
| 冷却 | 3s | 3s |
| 残影 | 0.5s 后爆炸 | 0.8s 后爆炸 |

## 后续 AI 工作规则

1. 不要直接大规模重写模板项目。
2. 先读本大纲和实现计划，再读 iWiki PRD。
3. 先做可玩切片，再抽象系统。
4. 新内容优先放在 `Assets/MirrorTrial`。
5. 玩家、Boss、小怪的技能要尽量走同一套 Ability 配置思路。
6. Boss 技能必须保留前摇、预警、后摇、可惩罚窗口。
7. 玩家技能必须优先保证响应速度和命中反馈。
8. 资源不足时先用占位资源，不阻塞玩法闭环。
9. 每个阶段都要能在 Unity 里验收，而不是只完成代码结构。

## 推荐第一个开发任务

第一个真正编码任务建议是：

```text
建立 MirrorTrial 目录结构
-> 扩展 Health 支持数值伤害
-> 做 MirrorBlade 的最小运行时
-> 做 BladeMirror 单 Boss 测试场
-> 击败 Boss 后解锁玩家版 MirrorBlade
```

不要一开始做完整 Ability Editor。编辑器应在核心技能跑通后再做。

