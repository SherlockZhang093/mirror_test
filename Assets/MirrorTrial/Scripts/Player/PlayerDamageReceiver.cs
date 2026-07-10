using System.Collections;
using MirrorTrial.Combat;
using Platformer.Mechanics;
using UnityEngine;

namespace MirrorTrial.Player
{
    [RequireComponent(typeof(PlayerInputReader), typeof(PlayerMotor), typeof(PlayerAnimationDriver))]
    public class PlayerDamageReceiver : MonoBehaviour
    {
        PlayerInputReader input;
        PlayerTuning tuning;
        PlayerMotor motor;
        PlayerAnimationDriver animationDriver;
        Health health;

        bool invincible;
        Coroutine hurtRoutine;

        void Awake()
        {
            input = GetComponent<PlayerInputReader>();
            tuning = GetComponent<PlayerTuning>();
            motor = GetComponent<PlayerMotor>();
            animationDriver = GetComponent<PlayerAnimationDriver>();
            health = GetComponent<Health>();
        }

        public void SetExternalInvincible(float duration)
        {
            StartCoroutine(ExternalInvincibleRoutine(duration));
        }

        IEnumerator ExternalInvincibleRoutine(float duration)
        {
            invincible = true;
            yield return new WaitForSeconds(duration);
            invincible = false;
        }

        void OnDamagePayloadReceived(DamagePayload payload)
        {
            if (invincible)
                return;

            if (health)
            {
                health.Decrement();
                if (!health.IsAlive)
                {
                    animationDriver.ForceState(PlayerActionState.Dead);
                    input.InputEnabled = false;
                    motor.MovementLocked = true;
                    return;
                }
            }

            if (hurtRoutine != null)
                StopCoroutine(hurtRoutine);

            hurtRoutine = StartCoroutine(HurtRoutine(payload));
        }

        IEnumerator HurtRoutine(DamagePayload payload)
        {
            var hurt = tuning.hurt;
            invincible = true;
            input.InputEnabled = false;
            motor.MovementLocked = true;
            animationDriver.ForceState(PlayerActionState.Hurt);

            var direction = payload.direction.x >= 0f ? 1f : -1f;
            motor.ApplyKnockback(new Vector2(hurt.knockback.x * direction, hurt.knockback.y), hurt.hurtLockTime);

            yield return new WaitForSeconds(hurt.hurtLockTime);

            input.InputEnabled = true;
            motor.MovementLocked = false;
            animationDriver.ClearForcedState(PlayerActionState.Hurt);

            var remainingInvincible = Mathf.Max(0f, hurt.invincibleTime - hurt.hurtLockTime);
            if (remainingInvincible > 0f)
                yield return new WaitForSeconds(remainingInvincible);

            invincible = false;
            hurtRoutine = null;
        }
    }
}
