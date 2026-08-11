using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace MirrorTrial.Player
{
    [DefaultExecutionOrder(-80)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInputReader), typeof(PlayerMotor), typeof(PlayerTuning))]
    [RequireComponent(typeof(PlayerStateMachine), typeof(PlayerAnimationDriver))]
    public sealed class PlayerDodgeController : MonoBehaviour, IInterruptiblePlayerAction
    {
        [Header("Feedback Hooks")]
        [SerializeField] UnityEvent onDodgeStarted;
        [SerializeField] UnityEvent onDodgeEnded;
        [SerializeField] UnityEvent onPerfectDodge;

        PlayerInputReader input;
        PlayerMotor motor;
        PlayerTuning tuning;
        PlayerStateMachine stateMachine;
        PlayerAnimationDriver animationDriver;
        PlayerDamageReceiver damageReceiver;

        Coroutine dodgeRoutine;
        float bufferedUntil;
        float readyTime;
        bool perfectDodgeTriggered;
        bool airDodgeUsed;

        public bool IsDodging => dodgeRoutine != null;
        public event Action DodgeStarted;

        void Awake()
        {
            input = GetComponent<PlayerInputReader>();
            motor = GetComponent<PlayerMotor>();
            tuning = GetComponent<PlayerTuning>();
            stateMachine = GetComponent<PlayerStateMachine>();
            animationDriver = GetComponent<PlayerAnimationDriver>();
            damageReceiver = GetComponent<PlayerDamageReceiver>();
        }

        void OnEnable()
        {
            if (damageReceiver)
                damageReceiver.ExternalHitIgnored += OnExternalHitIgnored;
            motor.Landed += OnLanded;
        }

        void OnDisable()
        {
            if (damageReceiver)
                damageReceiver.ExternalHitIgnored -= OnExternalHitIgnored;
            motor.Landed -= OnLanded;
            CancelCurrentAction(PlayerActionCancelReason.Hit);
        }

        void Update()
        {
            if (motor.IsGrounded)
                airDodgeUsed = false;

            if (input.DodgePressed)
                bufferedUntil = Time.time + tuning.dodge.inputBufferTime;

            if (dodgeRoutine == null && Time.time <= bufferedUntil && CanStartDodge())
            {
                bufferedUntil = 0f;
                dodgeRoutine = StartCoroutine(DodgeRoutine());
            }
        }

        bool CanStartDodge()
        {
            if (!input.InputEnabled || Time.time < readyTime || stateMachine.IsInActionState)
                return false;
            if (motor.IsGrounded)
                return true;
            return tuning.dodge.allowAirDodge && !airDodgeUsed;
        }

        IEnumerator DodgeRoutine()
        {
            var settings = tuning.dodge;
            readyTime = Time.time + settings.cooldown;
            perfectDodgeTriggered = false;
            if (!motor.IsGrounded)
                airDodgeUsed = true;

            var direction = Mathf.Abs(input.MoveX) > 0.01f
                ? Mathf.Sign(input.MoveX)
                : (motor.FacingRight ? 1f : -1f);
            var speed = settings.distance / Mathf.Max(0.01f, settings.duration);

            motor.MovementLocked = true;
            animationDriver.ForceState(PlayerActionState.Dodge);
            animationDriver.PlayStateImmediately(PlayerActionState.Dodge);
            motor.ApplyForcedVelocity(Vector2.right * direction * speed, settings.duration);
            DodgeStarted?.Invoke();
            onDodgeStarted?.Invoke();

            var invincibleStart = Mathf.Min(settings.invincibleStart, settings.duration);
            if (invincibleStart > 0f)
                yield return new WaitForSeconds(invincibleStart);

            var invincibleDuration = Mathf.Min(
                settings.invincibleDuration,
                settings.duration - invincibleStart);
            if (damageReceiver && invincibleDuration > 0f)
                damageReceiver.SetExternalInvincible(invincibleDuration);

            var remaining = Mathf.Max(0f, settings.duration - invincibleStart);
            if (remaining > 0f)
                yield return new WaitForSeconds(remaining);

            FinishDodge(false);
        }

        void OnLanded(float landingSpeed)
        {
            airDodgeUsed = false;
        }

        void OnExternalHitIgnored()
        {
            if (!IsDodging || perfectDodgeTriggered)
                return;

            perfectDodgeTriggered = true;
            onPerfectDodge?.Invoke();
        }

        void FinishDodge(bool cancelVelocity)
        {
            if (cancelVelocity)
                motor.CancelForcedVelocity();

            motor.MovementLocked = false;
            animationDriver.ClearForcedState(PlayerActionState.Dodge);
            dodgeRoutine = null;
            onDodgeEnded?.Invoke();
        }

        public void CancelCurrentAction(PlayerActionCancelReason reason)
        {
            if (dodgeRoutine == null)
                return;

            StopCoroutine(dodgeRoutine);
            FinishDodge(true);
        }
    }
}
