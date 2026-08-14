# 镜头震动统一接入规范

## 文档用途

本文档是 MirrorTrial 项目镜头震动功能的唯一接入说明。

后续任何 AI 或开发者在设计攻击命中、角色落地、爆炸、机关、Boss 技能、场景破坏等镜头震动时，都必须先阅读本文档，并复用现有统一震动系统。

不要为单独功能创建新的相机震动脚本、协程、噪声偏移或 Cinemachine Impulse Source。

## 唯一调用入口

统一入口：

```csharp
MirrorTrial.Feedback.CameraShakeService
```

脚本顶部引用：

```csharp
using MirrorTrial.Feedback;
```

不关心方向时：

```csharp
CameraShakeService.Shake(0.5f);
```

需要指定冲击方向时：

```csharp
CameraShakeService.Shake(Vector2.down, 0.8f);
```

`power` 会被自动限制在 `0～1` 范围内。

## 参数含义

### power

`power` 表示本次震动的相对强度：

| 强度 | 建议用途 |
| --- | --- |
| `0.1～0.25` | 轻攻击、小型机关、轻微环境反馈 |
| `0.3～0.5` | 普通攻击命中、角色落地、中型碰撞 |
| `0.55～0.75` | 重击、击飞、Boss 普通技能、较大机关 |
| `0.8～1.0` | 爆炸、Boss 大招、场景破坏、演出高潮 |

除非是关键演出，不要频繁使用 `1.0`。连续高强度震动会降低重要攻击的辨识度，也容易造成视觉疲劳。

### direction

`direction` 表示冲击传递方向，系统会自动归一化。

常见写法：

```csharp
// 向下冲击，例如重物落地
CameraShakeService.Shake(Vector2.down, 0.7f);

// 沿攻击方向冲击
CameraShakeService.Shake(attackDirection, 0.5f);

// 爆炸方向：从爆炸中心指向玩家或镜头目标
Vector2 direction = (target.position - explosionPosition).normalized;
CameraShakeService.Shake(direction, 0.9f);
```

如果方向为零向量，系统会使用 `Vector2.right` 作为安全默认方向。

## 攻击命中的接入规则

标准攻击不要在攻击输入、攻击动画开始或生成 Hitbox 时震动。应当在确认有效命中目标后震动，避免挥空也产生命中反馈。

当前标准 `Hitbox` 已经处理以下逻辑：

- 通过 `DamagePayload.feedback.requestCamera` 决定是否请求镜头震动。
- 通过 `DamagePayload.feedback.cameraPower` 设置震动强度。
- 同一次攻击判定命中多个目标时，只触发一次共享镜头反馈。
- 旧攻击数据没有新反馈配置时，可根据伤害、击退和顿帧估算震动强度。

因此，使用标准 `Hitbox` 的攻击通常只需要配置反馈数据，不要再额外调用 `CameraShakeService.Shake`，否则可能产生双重震动。

只有自行实现碰撞与伤害逻辑的特殊技能，才需要在确认命中后直接调用统一入口。

## 非攻击事件的接入示例

### 重物落地

```csharp
void OnHeavyObjectLanded()
{
    CameraShakeService.Shake(Vector2.down, 0.75f);
}
```

### 爆炸

```csharp
void PlayExplosionFeedback(Vector2 explosionPosition, Transform target)
{
    Vector2 direction = target
        ? ((Vector2)target.position - explosionPosition).normalized
        : Vector2.right;

    CameraShakeService.Shake(direction, 0.9f);
}
```

### Boss 蓄力完成

```csharp
void OnBossChargeCompleted()
{
    CameraShakeService.Shake(0.35f);
}
```

如果 Boss 技能随后还会造成强烈命中，应让蓄力震动明显弱于最终命中震动。

## 系统结构

当前调用链：

```text
游戏事件或攻击命中
        ↓
CameraShakeService.Shake(direction, power)
        ↓
CameraDirector.Shake(direction, power)
        ↓
CinemachineImpulseSource.GenerateImpulseWithVelocity(...)
        ↓
PlayerCamera / ShotCamera 的 CinemachineImpulseListener
        ↓
画面震动
```

主要文件：

| 文件 | 职责 |
| --- | --- |
| `Assets/MirrorTrial/Scripts/Feedback/CameraShakeService.cs` | 所有游戏逻辑使用的唯一公共入口 |
| `Assets/MirrorTrial/Scripts/Feedback/CameraFeedbackService.cs` | 攻击命中兼容层及命中强度估算 |
| `Assets/MirrorTrial/Scripts/Level/CameraDirector.cs` | 执行 Cinemachine Impulse，管理监听器 |
| `Assets/MirrorTrial/Scripts/Level/CameraTuning.cs` | 全局震动强度、增益和持续时间配置 |
| `Assets/MirrorTrial/Scripts/Level/CameraDirectorBootstrap.cs` | 进入场景时自动建立并绑定相机系统 |

## 全局参数

全局参数位于 `CameraTuning`：

| 参数 | 当前默认值 | 作用 |
| --- | ---: | --- |
| `lightHitStrength` | `0.07` | `power = 0` 对应的实际 Impulse 强度 |
| `heavyHitStrength` | `0.3` | `power = 1` 对应的实际 Impulse 强度 |
| `impulseGain` | `0.7` | 相机监听器的统一增益 |
| `impulseDuration` | `0.11` 秒 | 单次 Impulse 持续时间 |

业务功能通常只调整自己的 `power`，不要为了一个技能修改全局参数。只有需要调整整个游戏的镜头手感时，才修改 `CameraTuning`。

## 禁止事项

后续实现镜头震动时，不允许：

- 直接修改主相机或虚拟相机的 `transform.position`。
- 在业务脚本中直接调用 `GenerateImpulse`。
- 为单个攻击或机关新增 `CinemachineImpulseSource`。
- 在角色、敌人、Projectile 或相机跟随脚本中实现独立的随机噪声震动。
- 使用协程反复移动相机来模拟震动。
- 标准 `Hitbox` 已请求震动后，再手动请求第二次震动。
- 把镜头震动与顿帧、音效或伤害写死为不可拆分的一套逻辑。

如果现有统一入口不能满足新需求，应扩展 `CameraShakeService` 和 `CameraDirector`，而不是绕过它们创建第二套系统。

## AI 实施检查清单

其他 AI 在新增或修改镜头震动前，必须逐项确认：

1. 该事件是否真的需要震动，还是动画、音效或特效已经足够。
2. 如果是攻击，震动是否发生在有效命中之后，而不是攻击开始时。
3. 标准 `Hitbox` 是否已经自动产生震动，避免重复调用。
4. 是否只调用了 `CameraShakeService.Shake(...)`。
5. `power` 是否处于合理等级，关键演出是否明显强于普通反馈。
6. 方向是否与攻击、落地或爆炸冲击一致。
7. 是否避免在每帧、持续碰撞或循环触发器中无节制调用。
8. 是否没有创建新的相机震动实现或额外 Impulse Source。
9. 修改后是否在 Unity 中测试普通镜头和剧情镜头下的表现。
10. 是否检查一次事件只产生一次预期震动。

## 给后续 AI 的直接指令

当需求中出现“镜头震动”“屏幕震动”“攻击震屏”“落地震动”“爆炸晃动”等描述时：

1. 首先阅读本文档。
2. 优先使用现有 `CameraShakeService.Shake`。
3. 攻击系统优先配置 `DamagePayload.feedback`，不要重复调用。
4. 不新建另一套震动系统。
5. 若确需扩展能力，保持 `CameraShakeService` 为唯一公共入口，并同步更新本文档。
