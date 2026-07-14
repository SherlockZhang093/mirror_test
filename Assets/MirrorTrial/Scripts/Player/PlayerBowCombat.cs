using System.Collections;
using MirrorTrial.Abilities;
using MirrorTrial.Combat;
using UnityEngine;

namespace MirrorTrial.Player
{
    [RequireComponent(typeof(PlayerInputReader), typeof(PlayerWeaponController), typeof(PlayerAnimationDriver))]
    public sealed class PlayerBowCombat : MonoBehaviour
    {
        [SerializeField] AnimationClip drawClip;
        [SerializeField] AnimationClip fullDrawClip;
        [SerializeField] AnimationClip fireClip;
        [SerializeField] Transform arrowSpawnPoint;
        [SerializeField] Vector2 fallbackSpawnOffset = new Vector2(0.65f, 0.9f);

        PlayerInputReader input;
        PlayerWeaponController weapons;
        PlayerAnimationDriver animationDriver;
        PlayerMotor motor;
        PlayerTuning tuning;
        float chargeStartedAt;
        bool drawing;
        bool fullyDrawn;
        Coroutine recoveryRoutine;

        public bool IsBusy => drawing || recoveryRoutine != null;

        void Awake()
        {
            input = GetComponent<PlayerInputReader>();
            weapons = GetComponent<PlayerWeaponController>();
            animationDriver = GetComponent<PlayerAnimationDriver>();
            motor = GetComponent<PlayerMotor>();
            tuning = GetComponent<PlayerTuning>();
        }

        void Update()
        {
            if (!weapons || weapons.CurrentWeapon != PlayerWeaponType.Bow || recoveryRoutine != null)
                return;
            if (!drawing && input.WasPressed(PlayerInputCommand.PrimaryAttack))
                BeginDraw();
            if (!drawing)
                return;
            if (input.WasPressed(PlayerInputCommand.SecondaryAttack))
            {
                CancelDraw();
                return;
            }
            var charge = Time.time - chargeStartedAt;
            if (!fullyDrawn && charge >= tuning.abilities.bowMaxChargeTime)
            {
                fullyDrawn = true;
                if (fullDrawClip) animationDriver.PlayActionClip(fullDrawClip, fullDrawClip.length);
                else animationDriver.ForceState(PlayerActionState.BowFull);
            }
            if (input.WasReleased(PlayerInputCommand.PrimaryAttack))
                ReleaseArrow(charge);
        }

        void BeginDraw()
        {
            drawing = true;
            fullyDrawn = false;
            chargeStartedAt = Time.time;
            motor.MovementLocked = true;
            animationDriver.ForceState(PlayerActionState.Attack);
            if (drawClip) animationDriver.PlayActionClip(drawClip, tuning.abilities.bowMaxChargeTime);
            else animationDriver.ForceState(PlayerActionState.BowDraw);
        }

        void CancelDraw()
        {
            drawing = false;
            fullyDrawn = false;
            motor.MovementLocked = false;
            animationDriver.StopActionClip();
            animationDriver.ClearForcedState(PlayerActionState.Attack);
            animationDriver.ClearForcedState(PlayerActionState.BowDraw);
            animationDriver.ClearForcedState(PlayerActionState.BowFull);
        }

        void ReleaseArrow(float chargeTime)
        {
            drawing = false;
            fullyDrawn = false;
            var a = tuning.abilities;
            var t = Mathf.InverseLerp(a.bowMinChargeTime, a.bowMaxChargeTime, chargeTime);
            var direction = motor.FacingRight ? Vector2.right : Vector2.left;
            var spawn = arrowSpawnPoint ? arrowSpawnPoint.position : transform.position + new Vector3(fallbackSpawnOffset.x * direction.x, fallbackSpawnOffset.y, 0f);
            var arrow = BowArrowProjectile.Create(spawn);
            var damage = Mathf.RoundToInt(Mathf.Lerp(a.bowMinDamage, a.bowMaxDamage, t));
            var speed = Mathf.Lerp(a.bowMinSpeed, a.bowMaxSpeed, t);
            arrow.Launch(new DamagePayload(gameObject, damage, a.bowKnockback, direction, a.bowHitStop), direction, speed, a.bowRange);
            if (fireClip) animationDriver.PlayActionClip(fireClip, tuning.abilities.bowRecovery);
            else animationDriver.ForceState(PlayerActionState.BowFire);
            recoveryRoutine = StartCoroutine(Recovery());
        }

        IEnumerator Recovery()
        {
            yield return new WaitForSeconds(tuning.abilities.bowRecovery);
            motor.MovementLocked = false;
            animationDriver.StopActionClip();
            animationDriver.ClearForcedState(PlayerActionState.Attack);
            animationDriver.ClearForcedState(PlayerActionState.BowDraw);
            animationDriver.ClearForcedState(PlayerActionState.BowFull);
            animationDriver.ClearForcedState(PlayerActionState.BowFire);
            recoveryRoutine = null;
        }
    }
}
