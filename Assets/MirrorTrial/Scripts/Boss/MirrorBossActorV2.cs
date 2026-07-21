using System;
using System.Collections;
using MirrorTrial.Combat;
using MirrorTrial.Player;
using UnityEngine;

namespace MirrorTrial.Boss
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(Hurtbox))]
    public sealed class MirrorBossActorV2 : MonoBehaviour
    {
        public enum State { Dormant, Intro, Approach, Windup, Combo, Recovery, PhaseChange, Dead }

        [SerializeField] MirrorBossSimpleProfile profile;
        [SerializeField] Hitbox swordHitbox;
        [SerializeField] BoxCollider2D swordCollider;
        [SerializeField] SpriteRenderer sprite;
        [SerializeField] Color phaseOneTint = new Color(0.35f, 0.58f, 1f, 1f);
        [SerializeField] Color phaseTwoTint = new Color(0.48f, 0.38f, 1f, 1f);
        [SerializeField] Color phaseThreeTint = new Color(0.88f, 0.3f, 0.62f, 1f);
        [SerializeField, Tooltip("由 NodeCanvas Behaviour Tree 负责决策。")] bool behaviourTreeControlled = true;

        Rigidbody2D body;
        Animator animator;
        Transform target;
        Coroutine action;
        int hitPoints;
        int phase = 1;
        bool facingRight = true;
        bool activated;
        bool introCompleted;
        int requestedAnimation;
        float stunnedUntil;
        float arenaLeft = float.NegativeInfinity;
        float arenaRight = float.PositiveInfinity;

        public State CurrentState { get; private set; } = State.Dormant;
        public int CurrentHitPoints => hitPoints;
        public int MaxHitPoints => profile ? profile.maxHitPoints : 1;
        public int Phase => phase;
        public bool IsActivated => activated;
        public bool IntroCompleted => introCompleted;
        public bool IsStunned => CurrentState != State.Dead && Time.time < stunnedUntil;
        public float DistanceToTarget => target ? Mathf.Abs(target.position.x - transform.position.x) : float.PositiveInfinity;
        public float AttackRange => profile ? profile.attackRange : 0f;
        public float CurrentWindupProgress { get; private set; }
        public bool AttackHitboxActive { get; private set; }
        public string CurrentAttackAnimation { get; private set; }
        public float CurrentAttackProgress { get; private set; }
        public string DisplayName => profile ? profile.displayName : "镜中行刑者";
        public event Action<MirrorBossActorV2> Activated;
        public event Action<MirrorBossActorV2, int, int> HealthChanged;
        public event Action<MirrorBossActorV2, int> PhaseChanged;
        public event Action<MirrorBossActorV2> Defeated;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            animator = GetComponentInChildren<Animator>(true);
            if (!sprite) sprite = GetComponentInChildren<SpriteRenderer>(true);
            if (!swordHitbox) swordHitbox = GetComponentInChildren<Hitbox>(true);
            if (!swordCollider && swordHitbox) swordCollider = swordHitbox.GetComponent<BoxCollider2D>();
            body.freezeRotation = true;
            hitPoints = MaxHitPoints;
            SetHitbox(false);
            ApplyPhaseTint();
        }

        void Update()
        {
            if (behaviourTreeControlled) return;
            if (!profile || CurrentState != State.Approach || !target) return;
            var delta = target.position.x - transform.position.x;
            Face(delta);
            if (Mathf.Abs(delta) > profile.attackRange)
            {
                Move(Mathf.Sign(delta));
                return;
            }
            StopHorizontal();
            action = StartCoroutine(ComboRoutine());
        }

        void FixedUpdate()
        {
            if (float.IsInfinity(arenaLeft) || float.IsInfinity(arenaRight)) return;
            var p = body.position;
            p.x = Mathf.Clamp(p.x, arenaLeft, arenaRight);
            body.position = p;
        }

        public bool BTTickApproach()
        {
            if (!profile || !target || CurrentState == State.Dead || CurrentState == State.PhaseChange) return false;
            CurrentState = State.Approach;
            var delta = target.position.x - transform.position.x;
            Face(delta);
            if (Mathf.Abs(delta) > profile.attackRange)
            {
                Move(Mathf.Sign(delta));
                return false;
            }
            StopHorizontal();
            Play("SwordIdle", 0.06f);
            return true;
        }

        public void BTBeginIntro()
        {
            if (CurrentState == State.Dead) return;
            CurrentState = State.Intro;
            StopHorizontal();
            Play("SwordIdle");
        }

        public void BTFinishIntro()
        {
            introCompleted = true;
            if (CurrentState != State.Dead && CurrentState != State.PhaseChange) CurrentState = State.Approach;
        }

        public bool BTBeginCombo()
        {
            if (!profile || !target || CurrentState == State.Dead || CurrentState == State.PhaseChange || action != null) return false;
            action = StartCoroutine(ComboRoutine());
            return true;
        }

        public void BTBeginRecovery()
        {
            if (CurrentState == State.Dead || CurrentState == State.PhaseChange) return;
            CurrentState = State.Recovery;
            SetHitbox(false);
            StopHorizontal();
            Play("SwordIdle", 0.08f);
        }

        public void BTTickRecoveryApproach()
        {
            if (!profile || !target || CurrentState != State.Recovery) return;
            SetHitbox(false);
            var delta = target.position.x - transform.position.x;
            Face(delta);
            if (Mathf.Abs(delta) > profile.attackRange)
                Move(Mathf.Sign(delta), profile.cooldownMoveSpeedMultiplier);
            else
            {
                StopHorizontal();
                Play("SwordIdle", 0.08f);
            }
        }
        public void BTFinishRecovery()
        {
            if (CurrentState == State.Recovery) CurrentState = State.Approach;
        }

        public void BTStopHorizontal() => StopHorizontal();

        public void BTPlayStunned()
        {
            if (CurrentState == State.Dead) return;
            SetHitbox(false);
            StopHorizontal();
            Play("HitDamage", 0.03f);
        }
        public void ConfigureArena(float left, float right)
        {
            arenaLeft = Mathf.Min(left, right);
            arenaRight = Mathf.Max(left, right);
        }

        public void Activate(Transform playerTarget)
        {
            if (!profile || !playerTarget || activated) return;
            target = playerTarget;
            activated = true;
            Activated?.Invoke(this);
            if (behaviourTreeControlled) return;
            CurrentState = State.Intro;
            action = StartCoroutine(IntroRoutine());
        }

        IEnumerator IntroRoutine()
        {
            StopHorizontal();
            Play("SwordIdle");
            yield return new WaitForSeconds(profile.introDuration);
            CurrentState = State.Approach;
            action = null;
        }

        IEnumerator ComboRoutine()
        {
            CurrentState = State.Combo;
            SetHitbox(false);
            StopHorizontal();
            Face(target.position.x - transform.position.x);
            var combo = phase == 1 ? profile.phaseOneCombo : phase == 2 ? profile.phaseTwoCombo : profile.phaseThreeCombo;
            foreach (var step in combo)
            {
                if (CurrentState != State.Combo) yield break;
                yield return PlayComboStep(step);
                if (step.gapAfter > 0f) yield return new WaitForSeconds(step.gapAfter);
            }
            SetHitbox(false);
            animator.speed = 1f;
            CurrentState = State.Recovery;
            Play("SwordIdle", 0.08f);
            action = null;
            if (behaviourTreeControlled) yield break;
            var range = phase == 1 ? profile.phaseOneRecovery : phase == 2 ? profile.phaseTwoRecovery : profile.phaseThreeRecovery;
            yield return new WaitForSeconds(UnityEngine.Random.Range(range.x, range.y));
            if (CurrentState == State.Recovery) CurrentState = State.Approach;
            action = null;
        }
        IEnumerator PlayComboStep(MirrorBossComboStepV2 step)
        {
            SetHitbox(false);
            animator.speed = step.playbackSpeed;
            var hash = Animator.StringToHash(step.animationState);
            requestedAnimation = hash;
            CurrentAttackAnimation = step.animationState;
            CurrentAttackProgress = 0f;
            animator.CrossFade(hash, 0.03f, 0, 0f);

            var entryTimeout = 0.5f;
            while (entryTimeout > 0f)
            {
                var info = animator.GetCurrentAnimatorStateInfo(0);
                if (info.shortNameHash == hash) break;
                entryTimeout -= Time.deltaTime;
                yield return null;
            }

            step.EnsureHitboxKeys();
            var configuredWindupFrame = Mathf.Clamp(step.windupFrame, 0, step.animationFrameCount - 1);
            var firstActiveFrame = FindFirstActiveFrame(step);
            var windupConsumed = step.windupFrame < 0 || step.windupHoldDuration <= 0f || configuredWindupFrame >= firstActiveFrame;
            var windupEndTime = 0f;
            while (CurrentState == State.Combo || CurrentState == State.Windup)
            {
                var info = animator.GetCurrentAnimatorStateInfo(0);
                if (info.shortNameHash != hash)
                {
                    SetHitbox(false);
                    yield return null;
                    continue;
                }

                var progress = info.normalizedTime;
                CurrentAttackProgress = progress;
                var frame = Mathf.FloorToInt(Mathf.Clamp01(progress) * Mathf.Max(1, step.animationFrameCount - 1));

                if (!windupConsumed && frame >= configuredWindupFrame)
                {
                    if (CurrentState != State.Windup)
                    {
                        CurrentState = State.Windup;
                        SetHitbox(false);
                        StopHorizontal();
                        animator.speed = 0f;
                        windupEndTime = Time.time + step.windupHoldDuration;
                    }

                    CurrentWindupProgress = step.windupHoldDuration <= 0f
                        ? 1f
                        : Mathf.Clamp01(1f - (windupEndTime - Time.time) / step.windupHoldDuration);
                    if (Time.time < windupEndTime)
                    {
                        yield return null;
                        continue;
                    }

                    windupConsumed = true;
                    CurrentWindupProgress = 0f;
                    CurrentState = State.Combo;
                    animator.speed = step.playbackSpeed;
                    continue;
                }

                PlayerAttackHitboxKey key;
                if (TryEvaluateHitboxKey(step, frame, out key) && key.enabled)
                {
                    ConfigureHitbox(step, key.offset, key.size);
                    SetHitbox(true);
                }
                else
                {
                    SetHitbox(false);
                }

                if (progress >= 1f) break;
                if (AttackHitboxActive && step.advanceSpeed > 0f)
                    body.velocity = new Vector2((facingRight ? 1f : -1f) * step.advanceSpeed, body.velocity.y);
                else
                    StopHorizontal();
                yield return null;
            }
            SetHitbox(false);
            CurrentAttackAnimation = null;
            CurrentAttackProgress = 0f;
            StopHorizontal();
        }

        void ConfigureHitbox(MirrorBossComboStepV2 step, Vector2 offset, Vector2 size)
        {
            if (!swordHitbox) return;
            var direction = facingRight ? Vector2.right : Vector2.left;
            swordHitbox.Configure(new DamagePayload(gameObject, 1,
                new Vector2(direction.x * step.knockback.x, step.knockback.y), direction, step.hitStop,
                step.interruptPower, step.poiseDamage, step.playerHitReaction, step.breaksSuperArmor));
            if (!swordCollider) return;
            if (step.mirrorHitboxByFacing) offset.x = Mathf.Abs(offset.x) * direction.x;
            swordCollider.offset = offset;
            swordCollider.size = new Vector2(Mathf.Max(0.01f, size.x), Mathf.Max(0.01f, size.y));
        }

        static int FindFirstActiveFrame(MirrorBossComboStepV2 step)
        {
            for (var frame = 0; frame < Mathf.Max(1, step.animationFrameCount); frame++)
            {
                PlayerAttackHitboxKey key;
                if (TryEvaluateHitboxKey(step, frame, out key) && key.enabled) return frame;
            }
            return int.MaxValue;
        }
        static bool TryEvaluateHitboxKey(MirrorBossComboStepV2 step, int frame, out PlayerAttackHitboxKey result)
        {
            result = null;
            if (step.hitboxKeys == null || step.hitboxKeys.Count == 0) return false;
            PlayerAttackHitboxKey previous = null;
            PlayerAttackHitboxKey next = null;
            foreach (var key in step.hitboxKeys)
            {
                if (key == null) continue;
                if (key.frame <= frame && (previous == null || key.frame >= previous.frame)) previous = key;
                if (key.frame > frame && (next == null || key.frame < next.frame)) next = key;
            }
            if (previous == null) return false;
            result = previous;
            if (next == null || previous.interpolation != AttackHitboxInterpolation.Linear ||
                !previous.enabled || !next.enabled || next.frame <= previous.frame) return true;
            var t = Mathf.InverseLerp(previous.frame, next.frame, frame);
            result = new PlayerAttackHitboxKey
            {
                frame = frame,
                enabled = true,
                offset = Vector2.Lerp(previous.offset, next.offset, t),
                size = Vector2.Lerp(previous.size, next.size, t),
                interpolation = previous.interpolation
            };
            return true;
        }

        void OnDamagePayloadReceived(DamagePayload payload)
        {
            if (CurrentState == State.Dead || CurrentState == State.PhaseChange) return;
            hitPoints = Mathf.Max(0, hitPoints - Mathf.Max(0, payload.damage));
            HealthChanged?.Invoke(this, hitPoints, MaxHitPoints);
            if (hitPoints <= 0) { Die(); return; }

            var ratio = hitPoints / (float)MaxHitPoints;
            if (phase == 1 && ratio <= profile.phaseTwoAt) { BeginPhase(2); return; }
            if (phase == 2 && ratio <= profile.phaseThreeAt) { BeginPhase(3); return; }

            if (CurrentState != State.Combo && CurrentState != State.Windup)
            {
                stunnedUntil = Time.time + 0.28f;
                body.velocity = payload.knockback * 0.25f;
                Play("HitDamage");
            }
        }

        void BeginPhase(int nextPhase)
        {
            CancelAction();
            action = StartCoroutine(PhaseRoutine(nextPhase));
        }

        IEnumerator PhaseRoutine(int nextPhase)
        {
            CurrentState = State.PhaseChange;
            phase = nextPhase;
            ApplyPhaseTint();
            PhaseChanged?.Invoke(this, phase);
            Play("SwordGuard", 0.08f);
            yield return new WaitForSeconds(profile.phaseTransitionDuration);
            Play("SwordIdle", 0.08f);
            CurrentState = State.Approach;
            action = null;
        }

        void Die()
        {
            CancelAction();
            CurrentState = State.Dead;
            Play("Die", 0.05f);
            Defeated?.Invoke(this);
        }

        void CancelAction()
        {
            if (action != null) StopCoroutine(action);
            action = null;
            CurrentWindupProgress = 0f;
            SetHitbox(false);
            if (animator) animator.speed = 1f;
            StopHorizontal();
        }

        void Move(float direction, float speedMultiplier = 1f)
        {
            var desired = direction * profile.moveSpeed * Mathf.Clamp01(speedMultiplier);
            body.velocity = new Vector2(Mathf.MoveTowards(body.velocity.x, desired, profile.acceleration * Time.deltaTime), body.velocity.y);
            Play("SwordWalk", 0.08f);
        }

        void Face(float delta)
        {
            if (Mathf.Abs(delta) < 0.05f) return;
            facingRight = delta > 0f;
            if (sprite) sprite.flipX = !facingRight;
        }

        void StopHorizontal() => body.velocity = new Vector2(0f, body.velocity.y);
        void SetHitbox(bool value)
        {
            AttackHitboxActive = value;
            if (swordHitbox) swordHitbox.SetActive(value);
        }

        void Play(string state, float transition = 0.04f)
        {
            if (!animator) return;
            var hash = Animator.StringToHash(state);
            var current = animator.GetCurrentAnimatorStateInfo(0);
            if (current.shortNameHash == hash && !animator.IsInTransition(0))
            {
                requestedAnimation = hash;
                return;
            }
            if (requestedAnimation == hash && animator.IsInTransition(0)) return;
            requestedAnimation = hash;
            animator.CrossFade(hash, transition);
        }

        void ApplyPhaseTint()
        {
            if (sprite) sprite.color = phase == 1 ? phaseOneTint : phase == 2 ? phaseTwoTint : phaseThreeTint;
        }
    }
}
