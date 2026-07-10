using System.Collections;
using MirrorTrial.Combat;
using UnityEngine;

namespace MirrorTrial.Player
{
    [RequireComponent(typeof(PlayerInputReader), typeof(PlayerTuning), typeof(PlayerMotor))]
    [RequireComponent(typeof(PlayerAnimationDriver))]
    public class PlayerCombat : MonoBehaviour
    {
        [SerializeField] Hitbox attackHitbox;

        PlayerInputReader input;
        PlayerTuning tuning;
        PlayerMotor motor;
        PlayerAnimationDriver animationDriver;

        Coroutine attackRoutine;

        public bool IsAttacking => attackRoutine != null;

        void Awake()
        {
            input = GetComponent<PlayerInputReader>();
            tuning = GetComponent<PlayerTuning>();
            motor = GetComponent<PlayerMotor>();
            animationDriver = GetComponent<PlayerAnimationDriver>();

            if (attackHitbox)
                attackHitbox.SetActive(false);
        }

        void Update()
        {
            if (input.AttackPressed && attackRoutine == null)
                attackRoutine = StartCoroutine(AttackRoutine());
        }

        IEnumerator AttackRoutine()
        {
            var combat = tuning.combat;
            animationDriver.ForceState(PlayerActionState.Attack);
            motor.MovementLocked = true;

            yield return new WaitForSeconds(combat.attackStartup);

            if (attackHitbox)
            {
                var direction = motor.FacingRight ? Vector2.right : Vector2.left;
                var hitboxTransform = attackHitbox.transform;
                var localPosition = hitboxTransform.localPosition;
                localPosition.x = Mathf.Abs(localPosition.x) * direction.x;
                hitboxTransform.localPosition = localPosition;

                attackHitbox.Configure(new DamagePayload(gameObject, combat.attackDamage, combat.attackKnockback, direction, combat.hitStop));
                attackHitbox.SetActive(true);
            }

            yield return new WaitForSeconds(combat.attackActiveTime);

            if (attackHitbox)
                attackHitbox.SetActive(false);

            var remainingLock = Mathf.Max(0f, combat.attackMoveLock - combat.attackStartup - combat.attackActiveTime);
            if (remainingLock > 0f)
                yield return new WaitForSeconds(remainingLock);

            motor.MovementLocked = false;

            var remainingRecovery = Mathf.Max(0f, combat.attackRecovery - remainingLock);
            if (remainingRecovery > 0f)
                yield return new WaitForSeconds(remainingRecovery);

            animationDriver.ClearForcedState(PlayerActionState.Attack);
            attackRoutine = null;
        }
    }
}

