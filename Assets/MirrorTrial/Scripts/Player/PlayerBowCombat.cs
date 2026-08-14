using System;
using System.Collections;
using MirrorTrial.Abilities;
using MirrorTrial.Combat;
using UnityEngine;

namespace MirrorTrial.Player
{
    [RequireComponent(typeof(PlayerInputReader), typeof(PlayerWeaponController), typeof(PlayerAnimationDriver))]
    public sealed class PlayerBowCombat : MonoBehaviour, IInterruptiblePlayerAction
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
        PlayerCombat combat;
        PlayerDamageReceiver damageReceiver;
        float chargeStartedAt;
        bool drawing;
        bool fullyDrawn;
        Coroutine recoveryRoutine;
        PlayerBowComboSettings activeSettings;
        PlayerInputCommand activeAttackInput = PlayerInputCommand.PrimaryAttack;

        public bool IsBusy => drawing || recoveryRoutine != null;
        public bool CanDodgeCancel => recoveryRoutine != null;
        public event Action DrawStarted;
        public event Action ArrowReleased;

        void Awake()
        {
            input = GetComponent<PlayerInputReader>();
            weapons = GetComponent<PlayerWeaponController>();
            animationDriver = GetComponent<PlayerAnimationDriver>();
            motor = GetComponent<PlayerMotor>();
            tuning = GetComponent<PlayerTuning>();
            combat = GetComponent<PlayerCombat>();
            damageReceiver = GetComponent<PlayerDamageReceiver>();
        }

        void Update()
        {
            if (!weapons || weapons.CurrentWeapon != PlayerWeaponType.Bow || recoveryRoutine != null)
                return;
            var attackInput = ResolveAttackInput();
            if (!drawing && input.WasPressed(attackInput))
                BeginDraw(attackInput);
            if (!drawing)
                return;
            var cancelInput = activeSettings != null ? activeSettings.cancelInput : PlayerInputCommand.SecondaryAttack;
            if (input.WasPressed(cancelInput))
            {
                CancelDraw();
                return;
            }
            var charge = Time.time - chargeStartedAt;
            var drawDuration = activeSettings.drawClip ? activeSettings.drawClip.length : activeSettings.minimumChargeTime;
            if (!fullyDrawn && charge >= drawDuration)
            {
                fullyDrawn = true;
                if (activeSettings.fullDrawClip) animationDriver.PlayActionClip(activeSettings.fullDrawClip, activeSettings.fullDrawClip.length);
                else animationDriver.ForceState(PlayerActionState.BowFull);
            }
            if (input.WasReleased(activeAttackInput))
                ReleaseArrow(charge);
        }

        PlayerInputCommand ResolveAttackInput()
        {
            PlayerComboGraph graph;
            PlayerComboMove move;
            return combat && combat.TryGetBowCombo(out graph, out move)
                ? graph.entryInput
                : PlayerInputCommand.PrimaryAttack;
        }

        void BeginDraw(PlayerInputCommand attackInput)
        {
            if (attackInput == PlayerInputCommand.PrimaryAttack && damageReceiver)
                damageReceiver.CancelHurtInvincibilityForAttack();

            activeSettings = ResolveSettings();
            activeAttackInput = attackInput;
            drawing = true;
            fullyDrawn = false;
            chargeStartedAt = Time.time;
            motor.MovementLocked = true;
            DrawStarted?.Invoke();
            animationDriver.ForceState(PlayerActionState.Attack);
            if (activeSettings.drawClip) animationDriver.PlayActionClip(activeSettings.drawClip, activeSettings.drawClip.length);
            else animationDriver.ForceState(PlayerActionState.BowDraw);
        }

        PlayerBowComboSettings ResolveSettings()
        {
            PlayerComboGraph graph;
            PlayerComboMove move;
            return combat && combat.TryGetBowCombo(out graph, out move)
                ? move.bowShot
                : BuildComboSettingsFromLegacy();
        }

        public PlayerBowComboSettings BuildComboSettingsFromLegacy()
        {
            var sourceTuning = tuning ? tuning : GetComponent<PlayerTuning>();
            var settings = new PlayerBowComboSettings
            {
                enabled = true,
                drawClip = drawClip,
                fullDrawClip = fullDrawClip,
                fireClip = fireClip
            };
            if (!sourceTuning) return settings;
            var a = sourceTuning.abilities;
            settings.minimumChargeTime = a.bowMinChargeTime;
            settings.maximumChargeTime = a.bowMaxChargeTime;
            settings.recovery = a.bowRecovery;
            settings.minimumDamage = a.bowMinDamage;
            settings.maximumDamage = a.bowMaxDamage;
            settings.minimumSpeed = a.bowMinSpeed;
            settings.maximumSpeed = a.bowMaxSpeed;
            settings.range = a.bowRange;
            settings.knockback = a.bowKnockback;
            settings.hitStop = a.bowHitStop;
            return settings;
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
            activeSettings = null;
        }

        public bool TryCancelForDodge()
        {
            if (!CanDodgeCancel)
                return false;
            CancelCurrentAction(PlayerActionCancelReason.Dodge);
            return true;
        }

        public void CancelCurrentAction(PlayerActionCancelReason reason)
        {
            if (recoveryRoutine != null)
            {
                StopCoroutine(recoveryRoutine);
                recoveryRoutine = null;
            }

            CancelDraw();
            animationDriver.ClearForcedState(PlayerActionState.BowFire);
        }

        void OnDisable()
        {
            CancelCurrentAction(PlayerActionCancelReason.Hit);
        }

        void ReleaseArrow(float chargeTime)
        {
            drawing = false;
            fullyDrawn = false;
            var t = Mathf.InverseLerp(activeSettings.minimumChargeTime, activeSettings.maximumChargeTime, chargeTime);
            var direction = motor.FacingRight ? Vector2.right : Vector2.left;
            var spawn = arrowSpawnPoint ? arrowSpawnPoint.position : transform.position + new Vector3(fallbackSpawnOffset.x * direction.x, fallbackSpawnOffset.y, 0f);
            var arrow = BowArrowProjectile.Create(spawn);
            var damage = Mathf.RoundToInt(Mathf.Lerp(activeSettings.minimumDamage, activeSettings.maximumDamage, t));
            var speed = Mathf.Lerp(activeSettings.minimumSpeed, activeSettings.maximumSpeed, t);
            arrow.Launch(new DamagePayload(gameObject, damage, activeSettings.knockback, direction,
                activeSettings.hitStop, hitFlashType: activeSettings.hitFlashType),
                direction, speed, activeSettings.range);
            ArrowReleased?.Invoke();
            if (activeSettings.fireClip) animationDriver.PlayActionClip(activeSettings.fireClip, activeSettings.recovery);
            else animationDriver.ForceState(PlayerActionState.BowFire);
            recoveryRoutine = StartCoroutine(Recovery());
        }

        IEnumerator Recovery()
        {
            yield return new WaitForSeconds(activeSettings.recovery);
            motor.MovementLocked = false;
            animationDriver.StopActionClip();
            animationDriver.ClearForcedState(PlayerActionState.Attack);
            animationDriver.ClearForcedState(PlayerActionState.BowDraw);
            animationDriver.ClearForcedState(PlayerActionState.BowFull);
            animationDriver.ClearForcedState(PlayerActionState.BowFire);
            activeSettings = null;
            recoveryRoutine = null;
        }
    }
}
