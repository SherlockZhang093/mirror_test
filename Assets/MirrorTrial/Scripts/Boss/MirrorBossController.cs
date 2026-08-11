using System;
using System.Collections;
using MirrorTrial.Combat;
using MirrorTrial.Feedback;
using MirrorTrial.Player;
using UnityEngine;

namespace MirrorTrial.Boss
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(Animator))]
    public sealed class MirrorBossController : MonoBehaviour
    {
        public enum BossState { Dormant, Intro, Duel, Chase, Tell, Attack, Recovery, PhaseChange, Hurt, Dead }

        [SerializeField] MirrorBossProfile profile;
        [SerializeField] Transform target;
        [SerializeField] Hitbox swordHitbox;
        [SerializeField] BoxCollider2D swordHitboxCollider;
        [SerializeField] SpriteRenderer bodyRenderer;
        [SerializeField] Color mirrorTint = new Color(0.35f, 0.55f, 0.9f, 1f);
        [SerializeField] bool activateOnEnable;

        Rigidbody2D body;
        Animator animator;
        int hitPoints;
        int phase = 1;
        float duelTimer;
        bool facingRight = true;
        bool attackRunning;
        MirrorBossMove lastMove;
        int repeatedMoveCount;
        Coroutine routine;

        public BossState CurrentState { get; private set; } = BossState.Dormant;
        public int CurrentHitPoints => hitPoints;
        public int MaxHitPoints => profile ? profile.maxHitPoints : 30;
        public int Phase => phase;
        public string DisplayName => profile ? profile.displayName : "镜中行刑者";
        public bool IsDead => CurrentState == BossState.Dead;

        public event Action<MirrorBossController> Activated;
        public event Action<MirrorBossController, int, int> HealthChanged;
        public event Action<MirrorBossController, int> PhaseChanged;
        public event Action<MirrorBossController> Defeated;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            animator = GetComponent<Animator>();
            if (!bodyRenderer) bodyRenderer = GetComponentInChildren<SpriteRenderer>(true);
            if (!swordHitbox) swordHitbox = GetComponentInChildren<Hitbox>(true);
            if (!swordHitboxCollider && swordHitbox) swordHitboxCollider = swordHitbox.GetComponent<BoxCollider2D>();
            body.freezeRotation = true;
            hitPoints = MaxHitPoints;
            SetHitbox(false);
            if (bodyRenderer) bodyRenderer.color = mirrorTint;
        }

        void OnEnable()
        {
            if (activateOnEnable) Activate();
        }

        void Update()
        {
            if (CurrentState == BossState.Dormant || CurrentState == BossState.Dead ||
                CurrentState == BossState.Intro || CurrentState == BossState.PhaseChange || attackRunning)
                return;

            AcquireTarget();
            if (!target) return;

            var delta = target.position.x - transform.position.x;
            Face(delta);
            var distance = Mathf.Abs(delta);
            if (distance > profile.stopDistance)
            {
                CurrentState = BossState.Chase;
                MoveTowards(Mathf.Sign(delta));
                if (distance >= profile.pursuitDistance && duelTimer <= 0f)
                    BeginMove(MirrorBossMove.PursuitSlash);
                return;
            }

            StopHorizontal();
            CurrentState = BossState.Duel;
            duelTimer -= Time.deltaTime;
            if (duelTimer <= 0f)
                BeginMove(ChooseMove(distance));
        }

        void FixedUpdate()
        {
            if (!profile) return;
            var p = body.position;
            p.x = Mathf.Clamp(p.x, profile.arenaXLimits.x, profile.arenaXLimits.y);
            body.position = p;
        }

        public void Activate()
        {
            if (CurrentState != BossState.Dormant) return;
            AcquireTarget();
            CurrentState = BossState.Intro;
            Activated?.Invoke(this);
            routine = StartCoroutine(IntroRoutine());
        }

        IEnumerator IntroRoutine()
        {
            StopHorizontal();
            animator.CrossFade("SwordIdle", 0.05f);
            yield return new WaitForSeconds(0.8f);
            CurrentState = BossState.Duel;
            ResetDuelTimer();
            routine = null;
        }

        MirrorBossMove ChooseMove(float distance)
        {
            MirrorBossMove chosen;
            if (distance > profile.stopDistance * 0.9f && lastMove != MirrorBossMove.HeavySlash)
                chosen = MirrorBossMove.HeavySlash;
            else if (phase >= 2 && UnityEngine.Random.value < 0.48f)
                chosen = MirrorBossMove.BrokenTriple;
            else
                chosen = UnityEngine.Random.value < 0.3f ? MirrorBossMove.HeavySlash : MirrorBossMove.MirrorDouble;

            if (chosen == lastMove && repeatedMoveCount >= 2)
                chosen = chosen == MirrorBossMove.MirrorDouble && phase >= 2 ? MirrorBossMove.BrokenTriple : MirrorBossMove.MirrorDouble;
            return chosen;
        }

        void BeginMove(MirrorBossMove move)
        {
            if (attackRunning || IsDead) return;
            if (move == lastMove) repeatedMoveCount++; else repeatedMoveCount = 1;
            lastMove = move;
            routine = StartCoroutine(MoveRoutine(move));
        }

        IEnumerator MoveRoutine(MirrorBossMove move)
        {
            attackRunning = true;
            StopHorizontal();
            switch (move)
            {
                case MirrorBossMove.MirrorDouble:
                    yield return Perform(profile.doubleFirst);
                    yield return Perform(profile.doubleSecond);
                    break;
                case MirrorBossMove.BrokenTriple:
                    yield return Perform(profile.tripleFirst);
                    yield return Perform(profile.tripleSecond);
                    yield return Perform(profile.tripleFinisher);
                    break;
                case MirrorBossMove.HeavySlash:
                    yield return Perform(profile.heavySlash);
                    break;
                case MirrorBossMove.PursuitSlash:
                    yield return Perform(profile.pursuitSlash);
                    break;
            }
            SetHitbox(false);
            StopHorizontal();
            if (!IsDead && CurrentState != BossState.PhaseChange)
            {
                CurrentState = BossState.Duel;
                ResetDuelTimer();
                animator.CrossFade("SwordIdle", 0.08f);
            }
            attackRunning = false;
            routine = null;
        }

        IEnumerator Perform(MirrorBossAttack attack)
        {
            CurrentState = BossState.Tell;
            animator.CrossFade(attack.animationState, 0.04f);
            yield return new WaitForSeconds(attack.tell);
            if (IsDead) yield break;

            CurrentState = BossState.Attack;
            ConfigureHitbox(attack);
            if (attack.advanceDuration > 0f)
                body.velocity = new Vector2((facingRight ? 1f : -1f) * attack.advanceSpeed, body.velocity.y);
            SetHitbox(true);
            yield return new WaitForSeconds(attack.activeTime);
            SetHitbox(false);
            if (attack.advanceDuration > attack.activeTime)
                yield return new WaitForSeconds(attack.advanceDuration - attack.activeTime);
            StopHorizontal();
            CurrentState = BossState.Recovery;
            var recovery = attack.recovery * (phase >= 3 ? profile.phaseThreeRecoveryMultiplier : 1f);
            yield return new WaitForSeconds(recovery);
        }

        void ConfigureHitbox(MirrorBossAttack attack)
        {
            if (!swordHitbox) return;
            var direction = facingRight ? Vector2.right : Vector2.left;
            swordHitbox.Configure(new DamagePayload(gameObject, attack.damage,
                new Vector2(direction.x * attack.knockback.x, attack.knockback.y), direction, attack.hitStop,
                attack.interruptPower, attack.poiseDamage, attack.playerHitReaction, attack.breaksSuperArmor));
            if (swordHitboxCollider)
            {
                swordHitboxCollider.offset = new Vector2((facingRight ? 1f : -1f) * attack.hitboxOffset.x, attack.hitboxOffset.y);
                swordHitboxCollider.size = attack.hitboxSize;
            }
        }

        void OnDamagePayloadReceived(DamagePayload payload)
        {
            if (IsDead || CurrentState == BossState.PhaseChange) return;
            var beforeDamage = hitPoints;
            var requestedDamage = Mathf.Max(0, payload.damage);
            hitPoints = Mathf.Max(0, hitPoints - requestedDamage);
            var actualDamage = beforeDamage - hitPoints;
            if (actualDamage > 0)
                DamageDealtEvents.RaisePlayerDamageDealt(new DamageDealtResult(payload.source, gameObject, requestedDamage, actualDamage));
            HealthChanged?.Invoke(this, hitPoints, MaxHitPoints);
            if (hitPoints <= 0) { Die(); return; }

            body.velocity = payload.knockback;
            animator.CrossFade("Hurt", 0.03f);
            CameraFeedbackService.RequestHit(payload);
            var ratio = hitPoints / (float)MaxHitPoints;
            if (phase == 1 && ratio <= profile.phaseTwoAt) StartPhase(2);
            else if (phase == 2 && ratio <= profile.phaseThreeAt) StartPhase(3);
        }

        void StartPhase(int nextPhase)
        {
            if (routine != null) StopCoroutine(routine);
            SetHitbox(false);
            attackRunning = false;
            routine = StartCoroutine(PhaseRoutine(nextPhase));
        }

        IEnumerator PhaseRoutine(int nextPhase)
        {
            CurrentState = BossState.PhaseChange;
            StopHorizontal();
            phase = nextPhase;
            PhaseChanged?.Invoke(this, phase);
            animator.CrossFade("SwordGuard", 0.08f);
            yield return new WaitForSeconds(profile.phaseTransitionDuration);
            animator.CrossFade("SwordIdle", 0.08f);
            CurrentState = BossState.Duel;
            ResetDuelTimer();
            routine = null;
        }

        void Die()
        {
            if (IsDead) return;
            if (routine != null) StopCoroutine(routine);
            SetHitbox(false);
            attackRunning = false;
            CurrentState = BossState.Dead;
            StopHorizontal();
            animator.CrossFade("Dead", 0.05f);
            Defeated?.Invoke(this);
        }

        void AcquireTarget()
        {
            if (target && target.gameObject.activeInHierarchy) return;
            var input = FindObjectOfType<PlayerInputReader>();
            if (input) target = input.transform;
        }

        void MoveTowards(float direction)
        {
            var targetSpeed = direction * profile.moveSpeed;
            var x = Mathf.MoveTowards(body.velocity.x, targetSpeed, profile.acceleration * Time.deltaTime);
            body.velocity = new Vector2(x, body.velocity.y);
            animator.CrossFade("SwordRun", 0.08f);
        }

        void StopHorizontal() => body.velocity = new Vector2(0f, body.velocity.y);

        void Face(float delta)
        {
            if (Mathf.Abs(delta) < 0.05f) return;
            facingRight = delta > 0f;
            if (bodyRenderer) bodyRenderer.flipX = !facingRight;
        }

        void ResetDuelTimer()
        {
            var scale = phase >= 3 ? profile.phaseThreePauseMultiplier : 1f;
            duelTimer = UnityEngine.Random.Range(profile.duelPause.x, profile.duelPause.y) * scale;
        }

        void SetHitbox(bool active)
        {
            if (swordHitbox) swordHitbox.SetActive(active);
        }
    }
}
