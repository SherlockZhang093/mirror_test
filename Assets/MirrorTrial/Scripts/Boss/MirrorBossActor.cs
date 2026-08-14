using System;
using System.Collections;
using MirrorTrial.Combat;
using MirrorTrial.Enemies;
using MirrorTrial.Player;
using UnityEngine;

namespace MirrorTrial.Boss
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class MirrorBossActor : MonoBehaviour
    {
        public enum State { Dormant, Intro, Duel, Chase, Tell, Attack, Recovery, PhaseChange, Dead }

        [SerializeField] MirrorBossProfile profile;
        [SerializeField] Transform target;
        [SerializeField] Hitbox swordHitbox;
        [SerializeField] BoxCollider2D swordCollider;
        [SerializeField] SpriteRenderer sprite;
        [SerializeField] Color mirrorTint = new Color(0.32f, 0.52f, 0.92f, 1f);

        Rigidbody2D body;
        Animator animator;
        EnemyDamageVisual damageVisual;
        Coroutine action;
        int hp;
        int phase = 1;
        float pauseTimer;
        float arenaLeft = float.NegativeInfinity;
        float arenaRight = float.PositiveInfinity;
        bool facingRight = true;
        MirrorBossMove lastMove;
        int sameMoveCount;

        public State CurrentState { get; private set; } = State.Dormant;
        public int CurrentHitPoints => hp;
        public int MaxHitPoints => profile ? profile.maxHitPoints : 1;
        public int Phase => phase;
        public string DisplayName => profile ? profile.displayName : "Mirror Boss";
        public event Action<MirrorBossActor> Activated;
        public event Action<MirrorBossActor, int, int> HealthChanged;
        public event Action<MirrorBossActor, int> PhaseChanged;
        public event Action<MirrorBossActor> Defeated;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            animator = GetComponentInChildren<Animator>(true);
            if (!sprite) sprite = GetComponentInChildren<SpriteRenderer>(true);
            damageVisual = GetComponent<EnemyDamageVisual>();
            if (!damageVisual) damageVisual = gameObject.AddComponent<EnemyDamageVisual>();
            damageVisual.Bind(sprite);
            if (!swordHitbox) swordHitbox = GetComponentInChildren<Hitbox>(true);
            if (!swordCollider && swordHitbox) swordCollider = swordHitbox.GetComponent<BoxCollider2D>();
            body.freezeRotation = true;
            hp = MaxHitPoints;
            SetHitbox(false);
            if (sprite) sprite.color = mirrorTint;
            if (!profile) Debug.LogError("[MirrorBossActor] Missing Boss Profile.", this);
        }

        void Update()
        {
            if (!profile || CurrentState == State.Dormant || CurrentState == State.Intro ||
                CurrentState == State.Attack || CurrentState == State.Tell ||
                CurrentState == State.Recovery || CurrentState == State.PhaseChange || CurrentState == State.Dead)
                return;

            FindTarget();
            if (!target) return;
            var delta = target.position.x - transform.position.x;
            Face(delta);
            var distance = Mathf.Abs(delta);
            pauseTimer -= Time.deltaTime;
            if (distance > profile.stopDistance)
            {
                CurrentState = State.Chase;
                Move(Mathf.Sign(delta));
                if (distance >= profile.pursuitDistance && pauseTimer <= 0f) StartMove(MirrorBossMove.PursuitSlash);
                return;
            }
            Stop();
            CurrentState = State.Duel;
            if (pauseTimer <= 0f) StartMove(ChooseMove());
        }

        void FixedUpdate()
        {
            if (float.IsInfinity(arenaLeft) || float.IsInfinity(arenaRight)) return;
            var position = body.position;
            position.x = Mathf.Clamp(position.x, arenaLeft, arenaRight);
            body.position = position;
        }

        public void ConfigureArena(float left, float right)
        {
            arenaLeft = Mathf.Min(left, right);
            arenaRight = Mathf.Max(left, right);
        }

        public void Activate()
        {
            if (!profile || CurrentState != State.Dormant) return;
            FindTarget();
            CurrentState = State.Intro;
            Activated?.Invoke(this);
            action = StartCoroutine(Intro());
        }

        IEnumerator Intro()
        {
            Stop();
            Play("SwordIdle");
            yield return new WaitForSeconds(0.8f);
            CurrentState = State.Duel;
            ResetPause();
            action = null;
        }

        MirrorBossMove ChooseMove()
        {
            var chosen = phase >= 2 && UnityEngine.Random.value < 0.45f
                ? MirrorBossMove.BrokenTriple
                : (UnityEngine.Random.value < 0.3f ? MirrorBossMove.HeavySlash : MirrorBossMove.MirrorDouble);
            if (chosen == lastMove && sameMoveCount >= 2)
                chosen = chosen == MirrorBossMove.MirrorDouble && phase >= 2 ? MirrorBossMove.BrokenTriple : MirrorBossMove.MirrorDouble;
            return chosen;
        }

        void StartMove(MirrorBossMove move)
        {
            if (action != null || CurrentState == State.Dead) return;
            sameMoveCount = move == lastMove ? sameMoveCount + 1 : 1;
            lastMove = move;
            action = StartCoroutine(ExecuteMove(move));
        }

        IEnumerator ExecuteMove(MirrorBossMove move)
        {
            Stop();
            if (move == MirrorBossMove.MirrorDouble)
            {
                yield return Strike(profile.doubleFirst);
                yield return Strike(profile.doubleSecond);
            }
            else if (move == MirrorBossMove.BrokenTriple)
            {
                yield return Strike(profile.tripleFirst);
                yield return Strike(profile.tripleSecond);
                yield return Strike(profile.tripleFinisher);
            }
            else if (move == MirrorBossMove.HeavySlash) yield return Strike(profile.heavySlash);
            else yield return Strike(profile.pursuitSlash);

            SetHitbox(false);
            Stop();
            if (CurrentState != State.Dead && CurrentState != State.PhaseChange)
            {
                CurrentState = State.Duel;
                Play("SwordIdle");
                ResetPause();
            }
            action = null;
        }

        IEnumerator Strike(MirrorBossAttack attackData)
        {
            CurrentState = State.Tell;
            Play(attackData.animationState);
            yield return new WaitForSeconds(attackData.tell);
            if (CurrentState == State.Dead) yield break;
            CurrentState = State.Attack;
            ConfigureHitbox(attackData);
            if (attackData.advanceDuration > 0f)
                body.velocity = new Vector2((facingRight ? 1f : -1f) * attackData.advanceSpeed, body.velocity.y);
            SetHitbox(true);
            yield return new WaitForSeconds(attackData.activeTime);
            SetHitbox(false);
            if (attackData.advanceDuration > attackData.activeTime)
                yield return new WaitForSeconds(attackData.advanceDuration - attackData.activeTime);
            Stop();
            CurrentState = State.Recovery;
            var multiplier = phase >= 3 ? profile.phaseThreeRecoveryMultiplier : 1f;
            yield return new WaitForSeconds(attackData.recovery * multiplier);
        }

        void ConfigureHitbox(MirrorBossAttack data)
        {
            if (!swordHitbox) return;
            var direction = facingRight ? Vector2.right : Vector2.left;
            swordHitbox.Configure(new DamagePayload(gameObject, data.damage,
                new Vector2(direction.x * data.knockback.x, data.knockback.y), direction, data.hitStop,
                data.interruptPower, data.poiseDamage, data.playerHitReaction, data.breaksSuperArmor));
            if (!swordCollider) return;
            swordCollider.offset = new Vector2(direction.x * data.hitboxOffset.x, data.hitboxOffset.y);
            swordCollider.size = data.hitboxSize;
        }

        void OnDamagePayloadReceived(DamagePayload payload)
        {
            if (CurrentState == State.Dead || CurrentState == State.PhaseChange) return;
            var beforeDamage = hp;
            var requestedDamage = Mathf.Max(0, payload.damage);
            hp = Mathf.Max(0, hp - requestedDamage);
            var actualDamage = beforeDamage - hp;
            if (actualDamage > 0)
            {
                DamageDealtEvents.RaisePlayerDamageDealt(new DamageDealtResult(payload.source, gameObject, requestedDamage, actualDamage));
                if (damageVisual) damageVisual.PlayDamage(payload.hitFlashType, actualDamage);
            }
            HealthChanged?.Invoke(this, hp, MaxHitPoints);
            if (hp <= 0) { Die(); return; }
            body.velocity = payload.knockback;
            Play("HitDamage");
            var ratio = hp / (float)MaxHitPoints;
            if (phase == 1 && ratio <= profile.phaseTwoAt) ChangePhase(2);
            else if (phase == 2 && ratio <= profile.phaseThreeAt) ChangePhase(3);
        }

        void ChangePhase(int next)
        {
            if (action != null) StopCoroutine(action);
            SetHitbox(false);
            action = StartCoroutine(PhaseTransition(next));
        }

        IEnumerator PhaseTransition(int next)
        {
            CurrentState = State.PhaseChange;
            Stop();
            phase = next;
            PhaseChanged?.Invoke(this, phase);
            Play("SwordGuard");
            yield return new WaitForSeconds(profile.phaseTransitionDuration);
            Play("SwordIdle");
            CurrentState = State.Duel;
            ResetPause();
            action = null;
        }

        void Die()
        {
            if (CurrentState == State.Dead) return;
            if (action != null) StopCoroutine(action);
            action = null;
            SetHitbox(false);
            Stop();
            CurrentState = State.Dead;
            Play("Die");
            Defeated?.Invoke(this);
        }

        void FindTarget()
        {
            if (target && target.gameObject.activeInHierarchy) return;
            var player = FindObjectOfType<PlayerInputReader>();
            if (player) target = player.transform;
        }

        void Move(float direction)
        {
            var desired = direction * profile.moveSpeed;
            var x = Mathf.MoveTowards(body.velocity.x, desired, profile.acceleration * Time.deltaTime);
            body.velocity = new Vector2(x, body.velocity.y);
            Play("SwordRun", 0.08f);
        }

        void Stop() => body.velocity = new Vector2(0f, body.velocity.y);

        void Face(float delta)
        {
            if (Mathf.Abs(delta) < 0.05f) return;
            facingRight = delta > 0f;
            if (sprite) sprite.flipX = !facingRight;
        }

        void ResetPause()
        {
            var multiplier = phase >= 3 ? profile.phaseThreePauseMultiplier : 1f;
            pauseTimer = UnityEngine.Random.Range(profile.duelPause.x, profile.duelPause.y) * multiplier;
        }

        void SetHitbox(bool enabled)
        {
            if (swordHitbox) swordHitbox.SetActive(enabled);
        }

        void Play(string state, float transition = 0.04f)
        {
            if (animator && !string.IsNullOrEmpty(state)) animator.CrossFade(state, transition);
        }
    }
}
