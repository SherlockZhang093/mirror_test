using System.Collections;
using MirrorTrial.Abilities;
using MirrorTrial.Combat;
using UnityEngine;

namespace MirrorTrial.Player
{
    [RequireComponent(typeof(PlayerInputReader), typeof(PlayerTuning), typeof(PlayerMotor))]
    [RequireComponent(typeof(PlayerAnimationDriver))]
    public class PlayerAbilityLoadout : MonoBehaviour, IInterruptiblePlayerAction
    {
        [SerializeField] AnimationClip mirrorBladeClip;
        [SerializeField] MirrorBladeProjectile mirrorBladeProjectilePrefab;
        [SerializeField] Transform projectileSpawnPoint;
        [SerializeField] float projectileSpawnForwardOffset = 0.65f;
        [SerializeField] float projectileSpawnUpOffset = 1.10f;

        PlayerInputReader input;
        PlayerTuning tuning;
        PlayerMotor motor;
        PlayerAnimationDriver animationDriver;
        PlayerDamageReceiver damageReceiver;
        PlayerWeaponController weapons;

        float mirrorBladeReadyTime;
        float echoDashReadyTime;
        Coroutine mirrorBladeRoutine;
        Coroutine echoDashRoutine;

        public bool IsBusy => mirrorBladeRoutine != null || echoDashRoutine != null;

        void Awake()
        {
            input = GetComponent<PlayerInputReader>();
            tuning = GetComponent<PlayerTuning>();
            motor = GetComponent<PlayerMotor>();
            animationDriver = GetComponent<PlayerAnimationDriver>();
            damageReceiver = GetComponent<PlayerDamageReceiver>();
            weapons = GetComponent<PlayerWeaponController>();
        }

        void Update()
        {
            var abilities = tuning.abilities;

            if (abilities.mirrorBladeUnlocked && weapons && weapons.CurrentWeapon == PlayerWeaponType.Sword && input.WasPressed(PlayerInputCommand.WeaponSkill) && mirrorBladeRoutine == null && Time.time >= mirrorBladeReadyTime)
                mirrorBladeRoutine = StartCoroutine(MirrorBladeRoutine());

            if (abilities.echoDashUnlocked && input.WasPressed(PlayerInputCommand.MobilitySkill) && echoDashRoutine == null && Time.time >= echoDashReadyTime)
                echoDashRoutine = StartCoroutine(EchoDashRoutine());
        }

        IEnumerator MirrorBladeRoutine()
        {
            var ability = tuning.abilities;
            mirrorBladeReadyTime = Time.time + ability.mirrorBladeCooldown;
            motor.MovementLocked = true;
            animationDriver.ForceState(PlayerActionState.Attack);
            if (mirrorBladeClip) animationDriver.PlayActionClip(mirrorBladeClip, ability.mirrorBladeStartup + ability.mirrorBladeRecovery);
            else animationDriver.ForceState(PlayerActionState.Cast);

            yield return new WaitForSeconds(ability.mirrorBladeStartup);

            SpawnMirrorBlade();

            yield return new WaitForSeconds(ability.mirrorBladeRecovery);

            motor.MovementLocked = false;
            animationDriver.StopActionClip();
            animationDriver.ClearForcedState(PlayerActionState.Attack);
            animationDriver.ClearForcedState(PlayerActionState.Cast);
            mirrorBladeRoutine = null;
        }

        IEnumerator EchoDashRoutine()
        {
            var ability = tuning.abilities;
            echoDashReadyTime = Time.time + ability.echoDashCooldown;
            motor.MovementLocked = true;
            input.InputEnabled = false;
            animationDriver.ForceState(PlayerActionState.Dash);

            var direction = motor.FacingRight ? Vector2.right : Vector2.left;
            var dashSpeed = ability.echoDashDistance / Mathf.Max(0.01f, ability.echoDashDuration);
            motor.ApplyForcedVelocity(direction * dashSpeed, ability.echoDashDuration);

            if (damageReceiver && ability.echoDashInvincibleTime > 0f)
                damageReceiver.SetExternalInvincible(ability.echoDashInvincibleTime);

            yield return new WaitForSeconds(ability.echoDashDuration);

            input.InputEnabled = true;
            motor.MovementLocked = false;
            animationDriver.ClearForcedState(PlayerActionState.Dash);
            echoDashRoutine = null;
        }

        void SpawnMirrorBlade()
        {
            if (!mirrorBladeProjectilePrefab)
                return;

            var ability = tuning.abilities;
            var direction = motor.FacingRight ? Vector2.right : Vector2.left;
            var spawnPosition = projectileSpawnPoint
                ? projectileSpawnPoint.position
                : transform.position + new Vector3(projectileSpawnForwardOffset * direction.x, projectileSpawnUpOffset, 0f);

            var projectile = Instantiate(mirrorBladeProjectilePrefab, spawnPosition, Quaternion.identity);
            var payload = new DamagePayload(gameObject, ability.mirrorBladeDamage, ability.mirrorBladeKnockback, direction, ability.mirrorBladeHitStop);
            projectile.Launch(payload, direction, ability.mirrorBladeSpeed, ability.mirrorBladeRange);
        }

        public void CancelCurrentAction(PlayerActionCancelReason reason)
        {
            if (mirrorBladeRoutine != null)
            {
                StopCoroutine(mirrorBladeRoutine);
                mirrorBladeRoutine = null;
            }
            if (echoDashRoutine != null)
            {
                StopCoroutine(echoDashRoutine);
                echoDashRoutine = null;
            }

            input.InputEnabled = true;
            motor.MovementLocked = false;
            motor.CancelForcedVelocity();
            animationDriver.StopActionClip();
            animationDriver.ClearForcedState(PlayerActionState.Attack);
            animationDriver.ClearForcedState(PlayerActionState.Cast);
            animationDriver.ClearForcedState(PlayerActionState.Dash);
        }

        void OnDisable()
        {
            CancelCurrentAction(PlayerActionCancelReason.Hit);
        }
    }
}
