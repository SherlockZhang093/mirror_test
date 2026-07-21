using System;
using System.Collections;
using MirrorTrial.Combat;
using MirrorTrial.Feedback;
using MirrorTrial.Level;
using Platformer.Mechanics;
using UnityEngine;

namespace MirrorTrial.Player
{
    [RequireComponent(typeof(PlayerInputReader), typeof(PlayerMotor), typeof(PlayerAnimationDriver))]
    [RequireComponent(typeof(PlayerBodyStateController), typeof(PlayerActionInterruptor))]
    public class PlayerDamageReceiver : MonoBehaviour
    {
        PlayerInputReader input;
        PlayerTuning tuning;
        PlayerMotor motor;
        PlayerAnimationDriver animationDriver;
        Health health;
        PlayerCombat combat;
        PlayerBodyStateController bodyState;
        PlayerActionInterruptor interruptor;
        PlayerReactionAnimationSet reactionAnimations;
        SpriteRenderer spriteRenderer;

        [Header("Respawn")]
        [SerializeField, Min(0f)] float respawnDelay = 2f;
        [SerializeField, Min(0f)] float respawnInvincibility = 1.5f;

        bool hurtInvincible;
        int externalInvincibilityCount;
        Coroutine hurtRoutine;
        Coroutine respawnRoutine;

        public bool IsInvincible => hurtInvincible || externalInvincibilityCount > 0;
        public event Action ExternalHitIgnored;

        void Awake()
        {
            input = GetComponent<PlayerInputReader>();
            tuning = GetComponent<PlayerTuning>();
            motor = GetComponent<PlayerMotor>();
            animationDriver = GetComponent<PlayerAnimationDriver>();
            health = GetComponent<Health>();
            combat = GetComponent<PlayerCombat>();
            bodyState = GetComponent<PlayerBodyStateController>();
            interruptor = GetComponent<PlayerActionInterruptor>();
            reactionAnimations = PlayerReactionAnimationSet.Load();

            if (!GetComponent<PlayerHealthBarUI>())
                gameObject.AddComponent<PlayerHealthBarUI>();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }

        public void SetExternalInvincible(float duration)
        {
            if (duration <= 0f)
                return;
            StartCoroutine(ExternalInvincibleRoutine(duration));
        }

        IEnumerator ExternalInvincibleRoutine(float duration)
        {
            externalInvincibilityCount++;
            yield return new WaitForSeconds(duration);
            externalInvincibilityCount = Mathf.Max(0, externalInvincibilityCount - 1);
        }

        void OnDamagePayloadReceived(DamagePayload payload)
        {
            if (hurtInvincible)
                return;
            if (externalInvincibilityCount > 0)
            {
                ExternalHitIgnored?.Invoke();
                return;
            }
            if (combat && combat.TryBlockIncomingHit())
                return;

            var resolution = PlayerHitResolver.Resolve(payload, bodyState);
            if (!resolution.ApplyDamage && resolution.Reaction == PlayerHitReaction.None)
                return;

            if (resolution.ApplyDamage && health)
            {
                health.Damage(payload.damage);
                if (!health.IsAlive)
                {
                    if (hurtRoutine != null)
                    {
                        StopCoroutine(hurtRoutine);
                        hurtRoutine = null;
                    }
                    if (interruptor)
                        interruptor.CancelAll(PlayerActionCancelReason.Death);
                    if (bodyState)
                        bodyState.ClearAllModifiers();
                    SetVisible(true);
                    animationDriver.ForceState(PlayerActionState.Dead);
                    input.InputEnabled = false;
                    motor.MovementLocked = true;
                    if (respawnRoutine == null)
                        respawnRoutine = StartCoroutine(RespawnRoutine());
                    return;
                }
            }

            if (resolution.InterruptAction && interruptor)
                interruptor.CancelAll(
                    resolution.Reaction == PlayerHitReaction.Knockdown
                        ? PlayerActionCancelReason.Knockdown
                        : PlayerActionCancelReason.Hit);

            if (resolution.ApplyKnockback)
                motor.ApplyKnockback(payload.knockback, tuning.hurt.knockbackDuration);

            if (!resolution.PlayFullBodyReaction)
            {
                HitStopService.Request(payload.hitStop * 0.5f);
                return;
            }

            if (hurtRoutine != null)
                StopCoroutine(hurtRoutine);

            hurtRoutine = StartCoroutine(HurtRoutine(payload, resolution.Reaction));
        }

        IEnumerator RespawnRoutine()
        {
            if (respawnDelay > 0f)
                yield return new WaitForSeconds(respawnDelay);

            var bridge = MirrorTransitionBridge.Instance;
            if (bridge && bridge.IsInMirror)
            {
                bridge.ReturnToRealityAfterPlayerDeath();
                yield return null;
            }

            var levelManager = FindObjectOfType<LevelManager>();
            var spawn = levelManager ? levelManager.PlayerSpawn : null;
            if (spawn)
                transform.SetPositionAndRotation(spawn.position, spawn.rotation);
            else
                Debug.LogWarning("[PlayerDamageReceiver] No LevelManager player spawn was found; reviving in place.", this);

            motor.CancelForcedVelocity();
            if (bodyState)
                bodyState.ClearAllModifiers();
            animationDriver.StopActionClip();
            animationDriver.ClearForcedState(PlayerActionState.Dead);
            health.RestoreFull();
            SetVisible(true);
            motor.MovementLocked = false;
            input.InputEnabled = true;

            if (respawnInvincibility > 0f)
                SetExternalInvincible(respawnInvincibility);
            respawnRoutine = null;
        }
        IEnumerator HurtRoutine(DamagePayload payload, PlayerHitReaction reaction)
        {
            var hurt = tuning.hurt;
            var hurtLockTime = GetReactionDuration(reaction, hurt);
            var knockdown = reaction == PlayerHitReaction.Knockdown;
            var totalControlLock = knockdown
                ? hurt.knockdownAnimationTime + hurt.knockdownGroundTime + hurt.getUpTime
                : hurtLockTime;

            hurtInvincible = true;
            input.InputEnabled = false;
            motor.MovementLocked = true;
            animationDriver.ForceState(PlayerActionState.Hurt);
            PlayReactionAnimation(reaction, knockdown ? hurt.knockdownAnimationTime : hurtLockTime);

            HitStopService.Request(Mathf.Max(hurt.hurtHitStop, payload.hitStop));

            yield return SetHiddenFor(hurt.hurtFlashTime);

            var remainingHurtLock = Mathf.Max(
                0f,
                (knockdown ? hurt.knockdownAnimationTime : hurtLockTime) - hurt.hurtFlashTime);
            if (remainingHurtLock > 0f)
                yield return new WaitForSeconds(remainingHurtLock);

            if (knockdown)
            {
                if (hurt.knockdownGroundTime > 0f)
                    yield return new WaitForSeconds(hurt.knockdownGroundTime);

                PlayReactionAnimation(PlayerHitReaction.GetUp, hurt.getUpTime);
                if (hurt.getUpTime > 0f)
                    yield return new WaitForSeconds(hurt.getUpTime);
            }

            animationDriver.StopActionClip();
            input.InputEnabled = true;
            motor.MovementLocked = false;
            animationDriver.ClearForcedState(PlayerActionState.Hurt);

            var protectionTime = knockdown
                ? Mathf.Max(hurt.invincibleTime, totalControlLock + hurt.getUpProtectionTime)
                : hurt.invincibleTime;
            var remainingInvincible = Mathf.Max(0f, protectionTime - totalControlLock);
            if (remainingInvincible > 0f)
                yield return BlinkDuringInvincibility(remainingInvincible, hurt.invincibleBlinkInterval);

            SetVisible(true);
            hurtInvincible = false;
            hurtRoutine = null;
        }

        float GetReactionDuration(PlayerHitReaction reaction, PlayerTuning.HurtTuning hurt)
        {
            switch (reaction)
            {
                case PlayerHitReaction.HeavyHurt:
                    return hurt.hurtLockTime * hurt.heavyHurtDurationMultiplier;
                case PlayerHitReaction.Launch:
                    return hurt.launchHurtTime;
                case PlayerHitReaction.Stunned:
                    return hurt.stunnedTime;
                case PlayerHitReaction.ShockLight:
                    return hurt.shockLightTime;
                case PlayerHitReaction.ShockHeavy:
                    return hurt.shockHeavyTime;
                default:
                    return hurt.hurtLockTime;
            }
        }

        void PlayReactionAnimation(PlayerHitReaction reaction, float duration)
        {
            var clip = reactionAnimations != null ? reactionAnimations.Get(reaction) : null;
            if (clip)
                animationDriver.PlayActionClip(clip, duration);
            else
                animationDriver.PlayStateImmediately(PlayerActionState.Hurt);
        }

        IEnumerator SetHiddenFor(float duration)
        {
            if (!spriteRenderer || duration <= 0f)
                yield break;

            SetVisible(false);
            yield return new WaitForSecondsRealtime(duration);
            SetVisible(true);
        }

        IEnumerator BlinkDuringInvincibility(float duration, float interval)
        {
            if (!spriteRenderer || interval <= 0f)
            {
                yield return new WaitForSeconds(duration);
                yield break;
            }

            var elapsed = 0f;
            var visible = true;
            while (elapsed < duration)
            {
                visible = !visible;
                SetVisible(visible);
                var step = Mathf.Min(interval, duration - elapsed);
                yield return new WaitForSecondsRealtime(step);
                elapsed += step;
            }
        }

        void SetVisible(bool visible)
        {
            if (spriteRenderer)
                spriteRenderer.enabled = visible;
        }

        void OnDisable()
        {
            if (hurtRoutine != null)
            {
                StopCoroutine(hurtRoutine);
                hurtRoutine = null;
            }
            hurtInvincible = false;
            externalInvincibilityCount = 0;
            if (respawnRoutine != null)
            {
                StopCoroutine(respawnRoutine);
                respawnRoutine = null;
            }
            SetVisible(true);
        }
    }
}
