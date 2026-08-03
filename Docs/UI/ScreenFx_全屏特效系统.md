# Mirror Trial 全屏特效系统

## 1. 文档目的

本文档说明 Mirror Trial 当前的全屏特效系统，包括系统结构、资源位置、信号调用方式、玩家受伤闪红、低血量提示、Boss 战氛围以及常见问题排查。

系统设计目标：

- 所有全屏效果由一个 Manager 统一管理。
- 玩法代码只发送语义信号，不直接操作 Canvas、Prefab、材质或 Shader。
- 全屏遮罩是独立 Prefab，方便美术单独调整和替换。
- 多种效果可以同时存在，并由 Manager 统一处理动画与混合。
- 遮罩覆盖游戏场景，但不会遮挡主要 HUD。
- 切换场景、Boss 结束或状态解除时能够正确清理持续效果。

---

## 2. 系统结构

```text
玩家受伤 / Boss 战 / 低血量 / 其他玩法系统
                        ↓
                 ScreenFx 信号总线
                        ↓
                ScreenFxManager
                        ↓
          混合强度、持续时间和显示状态
                        ↓
             ScreenFxOverlay Prefab
                        ↓
              ScreenFxOverlay Shader
```

各层职责：

| 模块 | 职责 |
| --- | --- |
| `ScreenFx` | 对外提供统一信号接口。玩法代码只调用这一层。 |
| `ScreenFxSignal` | 描述效果类型、模式、强度、持续时间、方向和来源。 |
| `ScreenFxManager` | 监听信号、维护持续状态、播放动画并驱动遮罩。 |
| `ScreenFxOverlay` | 管理独立 Canvas 和运行时材质实例。 |
| `ScreenFxOverlay.prefab` | 实际显示在屏幕上的独立全屏遮罩。 |
| `ScreenFxOverlay.shader` | 生成流动黑雾、受伤红光和低血量呼吸。 |

---

## 3. 主要文件与资源位置

### 运行时代码

```text
Assets/MirrorTrial/Scripts/Feedback/ScreenFx.cs
Assets/MirrorTrial/Scripts/Feedback/ScreenFxSignal.cs
Assets/MirrorTrial/Scripts/Feedback/ScreenFxManager.cs
Assets/MirrorTrial/Scripts/Feedback/ScreenFxOverlay.cs
```

### 遮罩资源

```text
Assets/MirrorTrial/Resources/UI/ScreenFxOverlay.prefab
Assets/MirrorTrial/Resources/UI/ScreenFxOverlay.mat
Assets/MirrorTrial/Shaders/ScreenFxOverlay.shader
```

### 编辑器工具

```text
Assets/MirrorTrial/Editor/UI/ScreenFxOverlayBuilder.cs
```

如果 Prefab 或材质丢失，可在 Unity 菜单中执行：

```text
Mirror Trial → Screen FX → Create or Repair Default Overlay
```

---

## 4. Manager 与 Prefab 生命周期

`ScreenFxManager` 在游戏启动前自动创建，并使用 `DontDestroyOnLoad` 跨场景保留。

Manager 会从以下 Resources 路径加载遮罩：

```text
UI/ScreenFxOverlay
```

运行时的正常 Hierarchy 结构为：

```text
ScreenFxManager
└── ScreenFxOverlay(Clone)
    └── Mask
```

如果 Prefab 无法加载，Manager 会自动创建一个运行时备用遮罩，避免全屏特效系统完全失效。

遮罩 Canvas 的排序值为 `50`。玩家主要 HUD 当前使用排序值 `100`，因此遮罩会覆盖游戏场景，但不会覆盖玩家血条等主要界面。

`ScreenFxOverlay.Initialize()` 会主动修正根节点和 Mask 的缩放、锚点与尺寸，防止 Prefab 变换错误导致遮罩不可见。

---

## 5. 信号类型

当前信号类型定义如下：

| 信号 | 类型 | 用途 |
| --- | --- | --- |
| `PlayerHit` | 瞬时 | 玩家实际扣血时闪红一次。 |
| `BossBattle` | 持续 | Boss 战期间保持纯黑色流动、闪烁边缘。 |
| `BossPhasePulse` | 瞬时 | Boss 转阶段时让黑雾向内产生压力脉冲。 |
| `LowHealth` | 持续 | 玩家低血量时显示轻微红色呼吸。 |
| `Death` | 瞬时 | 玩家死亡时播放更长的红黑效果。 |

系统已取消玩家轻伤、重伤的屏幕效果区分。玩家每次有效受击都是固定的一次闪红。

---

## 6. 对外调用接口

其他玩法系统不应查找或持有 `ScreenFxManager`，也不应直接修改遮罩材质。

### 播放一次性效果

```csharp
ScreenFx.Play(ScreenFxType.PlayerHit);
```

指定强度和持续时间：

```csharp
ScreenFx.Play(
    ScreenFxType.PlayerHit,
    intensity: 0.42f,
    duration: 0.32f
);
```

带受击方向：

```csharp
ScreenFx.Play(
    ScreenFxType.PlayerHit,
    0.42f,
    0.32f,
    hitDirection,
    this
);
```

### 开启持续状态

```csharp
ScreenFx.Begin(ScreenFxType.BossBattle, this);
```

### 结束持续状态

```csharp
ScreenFx.End(ScreenFxType.BossBattle, this);
```

### 强制清除一种状态

```csharp
ScreenFx.Clear(ScreenFxType.BossBattle);
```

`source` 用于区分持续状态的来源。如果多个对象同时开启同一种状态，只有相应来源都结束后，该状态才会完全关闭。

---

## 7. 玩家受伤闪红逻辑

玩家受伤接入位置：

```text
Assets/MirrorTrial/Scripts/Player/PlayerDamageReceiver.cs
```

当前规则：

1. 攻击被无敌状态忽略：不闪红。
2. 攻击被格挡：不闪红。
3. 攻击没有实际扣除生命：不闪红。
4. 攻击实际扣除一滴血：固定闪红一次。
5. 不根据受击动作、伤害值或击退力度改变强度。
6. 玩家死亡时，在普通受击信号后播放死亡红光。

固定受伤参数：

| 参数 | 当前值 |
| --- | ---: |
| 强度 | `0.42` |
| 持续时间 | `0.32 秒` |
| 边缘基础范围 | 约屏幕边缘 `10.5%` |

实际调用：

```csharp
ScreenFx.Play(ScreenFxType.PlayerHit, 0.42f, 0.32f, payload.direction, this);
```

闪红动画：

```text
前 16%：从透明快速上升到峰值
后 84%：平滑衰减到完全透明
```

效果使用 `Time.unscaledDeltaTime` 更新，因此受击顿帧期间仍能正常显示。

死亡参数：

| 参数 | 当前值 |
| --- | ---: |
| 强度 | `0.55` |
| 持续时间 | `0.55 秒` |

---

## 8. 低血量逻辑

玩家生命值发生变化时，`PlayerDamageReceiver` 会检查当前生命比例。

- 当前生命大于最大生命的 30%：关闭低血量状态。
- 当前生命大于 0 且不超过最大生命的 30%：开启低血量状态。
- 玩家死亡或对象销毁：关闭低血量状态。

低血量效果使用较弱的红色边缘，并以低频方式呼吸，不会像受击一样突然闪烁。

---

## 9. Boss 战逻辑

当前镜像 Boss 关卡实际使用：

```text
Assets/MirrorTrial/Scripts/Boss/MirrorBossBattleAreaV3.cs
```

Boss 战接入规则：

| 时机 | 发送信号 |
| --- | --- |
| Boss 战开始并创建 Boss | `Begin(BossBattle)` |
| Boss 转阶段 | `Play(BossPhasePulse)` |
| Boss 被击败 | `End(BossBattle)` |
| BattleArea 被销毁 | `End(BossBattle)` |

旧版 `MirrorBossEncounter` 也保留了相同的信号接入，避免旧场景或测试场景失去效果。

Boss 黑雾默认渐入时间为 `0.18 秒`，渐出时间为 `1.35 秒`。进入战斗时从一次较亮的黑色边缘闪烁开始，之后以约 `0.8 Hz` 的基础频率持续进行不规则明暗脉冲。

### Boss 效果 Inspector 调节

在 Project 窗口选择：

```text
Assets/MirrorTrial/Resources/UI/ScreenFxOverlay.prefab
```

然后在 `ScreenFxOverlay` 组件的 `Boss Battle Effect` 区域调节：

| Inspector 参数 | 作用 | 默认值 |
| --- | --- | ---: |
| `Boss Color` | Boss 战屏幕边缘颜色 | 纯黑 |
| `Boss Strength` | 黑边最大强度 | `0.72` |
| `Boss Fade In` | 进入战斗时的显现时间 | `0.18 秒` |
| `Boss Fade Out` | Boss 战结束后的消失时间 | `1.35 秒` |
| `Boss Flicker Frequency` | 每秒基础闪烁次数 | `0.8` |
| `Boss Flicker Amount` | 最暗与最亮之间的变化幅度 | `0.58` |

修改 Prefab 后，所有使用统一 `ScreenFx` 系统的 Boss 战都会采用新参数。

---

## 10. Shader 表现

当前 Shader 不使用青色镜裂纹。

画面边缘由以下内容组成：

- 纯黑色的基础边缘遮罩。
- 两层移动方向不同的程序噪声。
- 缓慢变化的不规则烟雾轮廓。
- Boss 战期间持续变化的全局明暗闪烁，最低强度保留约 42%，避免完全消失。
- Boss 转阶段时的向内压力脉冲。
- 玩家受击时独立计算的暗红边缘。

中央战斗区域保持透明，避免特效影响平台、敌人与攻击提示的辨识。

当前实现不需要额外 Render Texture、深度、法线或运动向量，主要开销为一次 UI 全屏透明绘制与程序噪声计算。

---

## 11. 多效果混合

Manager 当前维护以下运行时通道：

```text
Boss 常驻黑雾
玩家受伤红光
低血量红色呼吸
Boss 阶段脉冲
```

混合规则：

- Boss 黑雾与受伤红光可以同时显示。
- 红光在颜色表现上优先于黑雾。
- 低血量与受伤红光共同参与红色强度计算。
- 所有强度都经过限制，避免多次叠加形成完全不透明的屏幕。
- 连续受击会刷新当前受伤闪红，不会创建多个遮罩对象。
- 场景切换时 Manager 会清除持续状态与瞬时动画。

---

## 12. 编辑器预览

进入 Play Mode 后可以使用：

```text
F8：预览实际玩家受伤闪红
F9：开启或关闭 Boss 黑雾
```

对应菜单：

```text
Mirror Trial → Screen FX → Preview Player Hit
Mirror Trial → Screen FX → Toggle Boss Atmosphere
```

F8 使用的参数与当前实际受伤参数一致：强度 `0.42`，持续时间 `0.32 秒`。

---

## 13. 常见问题排查

### 运行后完全没有特效

检查 Hierarchy 是否存在：

```text
ScreenFxManager
└── ScreenFxOverlay(Clone)
```

如果 Manager 不存在，检查 `ScreenFxManager.Bootstrap()` 是否正常编译执行。

### Manager 存在但遮罩不可见

检查：

- `ScreenFxOverlay(Clone)` 根节点缩放是否为 `(1,1,1)`。
- `Mask` 是否铺满父节点。
- `Mask` 是否引用 `ScreenFxOverlay.mat`。
- 材质 Shader 是否为 `MirrorTrial/UI/ScreenFxOverlay`。
- Canvas 排序值是否为 `50`。

运行时 `ScreenFxOverlay.Initialize()` 会自动修复根节点和 Mask 的主要变换参数。

### 玩家受伤不闪红

检查本次攻击是否真的进入 `PlayerDamageReceiver` 并扣除了生命。被无敌或格挡拦截的攻击不会发送信号。

可以先在 Play Mode 按 `F8`：

- F8 有效果：遮罩系统正常，继续检查伤害链路。
- F8 无效果：检查 Manager、Prefab、Canvas 和材质。

### Boss 战没有黑雾

当前正式镜像 Boss 入口为 `MirrorBossBattleAreaV3`。确认场景使用的 BattleArea 脚本没有被其他版本替换，并在 Play Mode 使用 `F9` 验证遮罩本身。

### HUD 被遮罩覆盖

确保主要 HUD Canvas 排序值高于 `50`。玩家血量 HUD 当前为 `100`。

---

## 14. 后续扩展约定

增加新的全屏效果时：

1. 在 `ScreenFxType` 中增加语义类型。
2. 在 `ScreenFxManager.OnSignal()` 或相应处理函数中定义行为。
3. 如需新视觉参数，在 `ScreenFxOverlay.Apply()` 和 Shader 中增加对应通道。
4. 玩法代码只发送信号，不直接引用 Manager、Prefab 或材质。
5. 持续状态必须成对调用 `Begin` 和 `End`，并提供稳定的 `source`。

推荐调用方式：

```csharp
ScreenFx.Play(ScreenFxType.PlayerHit);
ScreenFx.Begin(ScreenFxType.BossBattle, this);
ScreenFx.End(ScreenFxType.BossBattle, this);
```

禁止在玩法代码中直接使用：

```csharp
FindObjectOfType<ScreenFxManager>();
Shader.SetGlobalFloat(...);
overlayMaterial.SetFloat(...);
```

这样可以保证所有全屏效果继续由统一系统管理。
