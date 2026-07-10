# Mirror Trial Player Framework First Pass

## Goal

This pass creates the smallest tunable Player framework for Mirror Trial without changing the existing template `Assets/Prefabs/Player.prefab`.

The new code lives under `Assets/MirrorTrial` and is separated by responsibility:

```text
Assets/MirrorTrial/Scripts/Player
Assets/MirrorTrial/Scripts/Combat
```

## First Scripts

| Script | Folder | Responsibility |
| - | - | - |
| `PlayerInputReader` | `Player` | Reads movement, jump, attack, MirrorBlade, EchoDash inputs. |
| `PlayerTuning` | `Player` | Exposes movement, combat, and hurt-feel parameters in the Inspector. |
| `PlayerMotor` | `Player` | Handles horizontal movement, acceleration, jump buffer, coyote time, jump cut, fall gravity, facing. |
| `PlayerAnimationDriver` | `Player` | Maps Player state to the new imported animation state names and plays them with `CrossFade`. |
| `PlayerCombat` | `Player` | Runs first-pass normal attack timing: startup, active frames, recovery, movement lock. |
| `PlayerActionState` | `Player` | Shared enum for animation and action states. |
| `DamagePayload` | `Combat` | Hit data: source, damage, knockback, direction, hit stop. |
| `Hitbox` | `Combat` | Trigger box that sends `DamagePayload` to a `Hurtbox`. |
| `Hurtbox` | `Combat` | Receives hit payloads and forwards them upward to owner components. |

## Suggested Test Prefab Setup

Do not replace the original template Player yet. Create a Mirror Trial test player prefab later under:

```text
Assets/MirrorTrial/Prefabs/Characters/Player_MirrorTrial.prefab
```

Recommended components:

```text
Player_MirrorTrial
  Transform
  Rigidbody2D
  BoxCollider2D
  SpriteRenderer
  Animator
  PlayerInputReader
  PlayerTuning
  PlayerMotor
  PlayerAnimationDriver
  PlayerCombat
  Hurtbox
```

Add a child object for the normal attack hitbox:

```text
AttackHitbox
  BoxCollider2D, Is Trigger = true
  Hitbox
```

Then assign `AttackHitbox` to `PlayerCombat.attackHitbox`.

## Animator Setup

Use:

```text
Assets/DeadRevolver/PixelPrototypePlayerSprites/Art/Animations/Animators/PlayerAnimator.controller
```

The first animation mapping is:

| Player State | Animation State |
| - | - |
| Idle | `Idle` |
| Run | `Run` |
| JumpRise | `JumpRise` |
| JumpFall | `JumpFall` |
| Land | `Land` |
| Attack | `SwordAttack` |
| Cast | `AirSlash` |
| Dash | `Dash` |
| Hurt | `HitDamage` |
| Dead | `Die` |

## First Tuning Targets

Start with these values and tune by feel:

| Area | Parameter | Initial Value |
| - | - | - |
| Movement | MoveSpeed | 5 |
| Movement | Acceleration | 50 |
| Movement | Deceleration | 60 |
| Movement | AirAcceleration | 30 |
| Movement | JumpSpeed | 7.5 |
| Movement | CoyoteTime | 0.08 |
| Movement | JumpBufferTime | 0.10 |
| Attack | AttackStartup | 0.08 |
| Attack | AttackActiveTime | 0.10 |
| Attack | AttackRecovery | 0.20 |
| Attack | AttackMoveLock | 0.18 |
| Attack | HitStop | 0.04 |


## Auto Setup Tool

A Unity Editor setup tool has been added at:

```text
Assets/MirrorTrial/Editor/PlayerFrameworkSetup.cs
```

After Unity finishes compiling scripts, run this menu item:

```text
Tools/Mirror Trial/Create Player Test Prefab
```

It creates and selects:

```text
Assets/MirrorTrial/Prefabs/Characters/Player_MirrorTrial.prefab
```

The generated prefab is wired with:

- New DeadRevolver `PlayerAnimator.controller`.
- Initial `Idle01` sprite.
- `PlayerInputReader`, `PlayerTuning`, `PlayerMotor`, `PlayerAnimationDriver`, `PlayerCombat`.
- `Hurtbox`, `PlayerDamageReceiver`, `Health`, `Rigidbody2D`, `BoxCollider2D`.
- Child `AttackHitbox` with `Hitbox` assigned to `PlayerCombat`.

This keeps the original template `Assets/Prefabs/Player.prefab` untouched.
## Next Step

The next implementation pass should create `Player_MirrorTrial.prefab`, wire these components, and test only three things:

1. Move and jump feel.
2. Animation state switching.
3. Normal attack hitbox timing.

MirrorBlade and EchoDash should wait until this base loop feels stable.

