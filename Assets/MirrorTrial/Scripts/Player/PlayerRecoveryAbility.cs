using System.Collections;
using Platformer.Mechanics;
using UnityEngine;

namespace MirrorTrial.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInputReader), typeof(PlayerHealthReserve), typeof(Health))]
    [RequireComponent(typeof(PlayerMotor), typeof(PlayerAnimationDriver))]
    public sealed class PlayerRecoveryAbility : MonoBehaviour, IInterruptiblePlayerAction
    {
        [Header("Recovery Skill")]
        [SerializeField] AnimationClip recoveryClip;
        [SerializeField, Min(0.01f)] float castDuration = 0.8f;
        [SerializeField, Range(0f, 1f)] float commitNormalizedTime = 0.65f;

        PlayerInputReader input;
        PlayerHealthReserve reserve;
        Health health;
        PlayerMotor motor;
        PlayerAnimationDriver animationDriver;
        PlayerAbilityLoadout abilityLoadout;
        Coroutine routine;
        int reservedAmount;
        bool committed;

        public bool IsCasting => routine != null;
        public event System.Action<bool> CastStateChanged;

        void Awake()
        {
            input = GetComponent<PlayerInputReader>();
            reserve = GetComponent<PlayerHealthReserve>();
            health = GetComponent<Health>();
            motor = GetComponent<PlayerMotor>();
            animationDriver = GetComponent<PlayerAnimationDriver>();
            abilityLoadout = GetComponent<PlayerAbilityLoadout>();
        }

        void Update()
        {
            if (!input.RecoverPressed) return;

            Trace(
                $"[G输入] 已检测到G。HP={(health ? health.CurrentHP : 0)}/{(health ? health.maxHP : 0)}, " +
                $"储备={(reserve ? reserve.Current : 0)}/{(reserve ? reserve.Capacity : 0)}, " +
                $"Casting={routine != null}, AbilityBusy={(abilityLoadout && abilityLoadout.IsBusy)}");

            if (routine != null)
            {
                Trace("[G输入][拒绝] 已经在施法。");
                return;
            }
            if (abilityLoadout && abilityLoadout.IsBusy)
            {
                Trace("[G输入][拒绝] 其他技能正在执行。");
                return;
            }
            TryCast();
        }

        public bool TryCast()
        {
            if (routine != null)
            {
                Trace("[恢复技能][拒绝] 已经在施法。");
                return false;
            }
            if (!reserve.TryReserveForFullHeal(out reservedAmount))
            {
                Trace("[恢复技能][拒绝] 储备系统未批准本次恢复。");
                return false;
            }
            routine = StartCoroutine(CastRoutine());
            Trace($"[恢复技能][开始] 预扣={reservedAmount}, 施法时间={castDuration:0.###}s");
            return true;
        }

        IEnumerator CastRoutine()
        {
            committed = false;
            CastStateChanged?.Invoke(true);
            motor.MovementLocked = true;
            animationDriver.ForceState(PlayerActionState.Cast);
            if (recoveryClip) animationDriver.PlayActionClip(recoveryClip, castDuration);

            var commitDelay = castDuration * commitNormalizedTime;
            if (commitDelay > 0f) yield return new WaitForSeconds(commitDelay);
            CommitHeal();

            var recovery = Mathf.Max(0f, castDuration - commitDelay);
            if (recovery > 0f) yield return new WaitForSeconds(recovery);
            Finish();
        }

        // May also be called by an Animation Event named CommitHeal.
        public void CommitHeal()
        {
            if (routine == null || committed) return;
            committed = true;
            var healed = health.Heal(reservedAmount);
            Trace(
                $"[恢复技能][结算] 预扣={reservedAmount}, 实际恢复={healed}, " +
                $"HP={health.CurrentHP}/{health.maxHP}");
        }

        public void CancelCurrentAction(PlayerActionCancelReason reason)
        {
            if (routine == null) return;
            StopCoroutine(routine);
            routine = null;
            reservedAmount = 0;
            committed = false;
            motor.MovementLocked = false;
            animationDriver.StopActionClip();
            animationDriver.ClearForcedState(PlayerActionState.Cast);
            CastStateChanged?.Invoke(false);
        }

        void Finish()
        {
            routine = null;
            reservedAmount = 0;
            committed = false;
            motor.MovementLocked = false;
            animationDriver.StopActionClip();
            animationDriver.ClearForcedState(PlayerActionState.Cast);
            CastStateChanged?.Invoke(false);
        }

        void OnDisable()
        {
            CancelCurrentAction(PlayerActionCancelReason.Death);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        void Trace(string message)
        {
            Debug.Log("[HealthResourceTrace]" + message, this);
        }
    }
}
