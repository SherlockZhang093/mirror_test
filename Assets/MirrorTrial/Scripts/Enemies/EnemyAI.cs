using MirrorTrial.Combat;
using MirrorTrial.Level;
using System.Collections;
using UnityEngine;

namespace MirrorTrial.Enemies
{
    [RequireComponent(typeof(Hurtbox))]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class EnemyAI : MonoBehaviour
    {
        [Header("AI配置")]
        [ChineseLabel("专属AI配置")] [Tooltip("该敌兵Prefab专属的AI配置资源。")] [SerializeField] EnemyAIProfile profile;

        [Header("单独覆盖") ]
        [ChineseLabel("单独生命值")] [Tooltip("正数覆盖默认生命值，-1使用默认值。")] public int overrideMaxHitPoints = -1;
        [ChineseLabel("单独移动速度")] [Tooltip("正数覆盖默认速度，-1使用默认值。")] public float overrideMoveSpeed = -1f;

        [Header("关卡内单只敌兵配置")]
        [SerializeField] Vector2 overrideDetectionSize = new Vector2(-1f, -1f);
        [SerializeField] float patrolLeftOffset = -2f;
        [SerializeField] float patrolRightOffset = 2f;

        [Header("攻击特效")]
        [ChineseLabel("攻击特效预制体")] [SerializeField] GameObject attackEffectPrefab;
        [ChineseLabel("武器或手部挂点")] [SerializeField] Transform attackEffectPoint;
        [ChineseLabel("特效保留时间")] [Min(0.01f)] [SerializeField] float attackEffectLifetime = 1f;
        [ChineseLabel("根据朝向翻转特效")] [SerializeField] bool mirrorAttackEffectByFacing = true;

        [Header("辅助显示")]
        [ChineseLabel("显示辅助线")] [Tooltip("选中敌兵时显示探测范围和巡逻边界。")] [SerializeField] bool showDebugGizmos = true;
        public enum State
        {
            [InspectorName("巡逻")] Idle,
            [InspectorName("追击")] Chase,
            [InspectorName("攻击")] Attack,
            [InspectorName("受击")] Hurt,
            [InspectorName("死亡")] Dead,
            [InspectorName("击飞")] Launch
        }
        public State CurrentState { get; private set; } = State.Idle;
        public EnemyAIProfile Profile => profile;
        public Vector2 FacingDirection { get; private set; } = Vector2.right;

        int currentHitPoints;
        float stateTimer;
        [SerializeField] bool passiveTestTarget;
        float attackCooldownTimer;
        float hurtTimer;
        bool attackLanded;
        bool pendingDeath;
        bool isGrounded;

        Rigidbody2D body;
        Collider2D bodyCollider;
        SpriteRenderer spriteRenderer;
        EnemyDamageVisual damageVisual;
        EnemyLaunchController2D launchController;
        Transform player;
        Vector2 patrolOrigin;
        float patrolTargetX;
        float patrolWaitTimer;
        bool waitingAtPatrolPoint;
        bool patrolMovingRight = true;

        ContactFilter2D groundFilter = new ContactFilter2D();
        readonly RaycastHit2D[] groundHits = new RaycastHit2D[4];
        readonly RaycastHit2D[] wallHits = new RaycastHit2D[4];

        public int CurrentHitPoints => currentHitPoints;
        public int MaxHitPoints => overrideMaxHitPoints > 0 ? overrideMaxHitPoints : (profile ? profile.maxHitPoints : 2);
        public float MoveSpeed => overrideMoveSpeed > 0 ? overrideMoveSpeed : (profile ? profile.moveSpeed : 2.5f);
        public Vector2 DetectionSize => IsValidSize(overrideDetectionSize) ? overrideDetectionSize : Vector2.one * (profile ? profile.detectionRange * 2f : 16f);
        public Vector2 AttackSize => new Vector2(profile ? profile.attackRange : 1.2f, 0.8f);
        public float PatrolLeftOffset => Mathf.Min(patrolLeftOffset, patrolRightOffset);
        public float PatrolRightOffset => Mathf.Max(patrolLeftOffset, patrolRightOffset);
        public Vector2 PatrolOrigin => Application.isPlaying ? patrolOrigin : (Vector2)transform.position;
        public bool HasDetectionOverride => IsValidSize(overrideDetectionSize);

        static bool IsValidSize(Vector2 size) => size.x >= 0f && size.y >= 0f;

        public void SetProfile(EnemyAIProfile nextProfile)
        {
            profile = nextProfile;
            if (currentHitPoints > MaxHitPoints)
                currentHitPoints = MaxHitPoints;
        }

        public void SetOverrideHitPoints(int value)
        {
            overrideMaxHitPoints = value;
            currentHitPoints = Mathf.Clamp(currentHitPoints, 0, MaxHitPoints);
        }

        public void SetOverrideMoveSpeed(float value)
        {
            overrideMoveSpeed = value;
        }

        public void SetInitialFacing(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0.001f) return;
            FacingDirection = new Vector2(Mathf.Sign(direction.x), 0f);
            UpdateVisualFacing();
        }

        public void SetPatrolPath(Transform[] path)
        {
            if (path == null || path.Length == 0) return;

            var minX = float.PositiveInfinity;
            var maxX = float.NegativeInfinity;
            for (var i = 0; i < path.Length; i++)
            {
                if (!path[i]) continue;
                minX = Mathf.Min(minX, path[i].position.x);
                maxX = Mathf.Max(maxX, path[i].position.x);
            }

            if (float.IsInfinity(minX) || float.IsInfinity(maxX)) return;
            patrolOrigin = transform.position;
            patrolLeftOffset = minX - patrolOrigin.x;
            patrolRightOffset = maxX - patrolOrigin.x;
            ResetPatrolTarget();
        }

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
            body.gravityScale = 1f;
            launchController = GetComponent<EnemyLaunchController2D>();
            if (!launchController) launchController = gameObject.AddComponent<EnemyLaunchController2D>();
            launchController.Configure(profile ? profile.launchSettings : null);
            if (!GetComponent<EnemyLaunchAnimationPresenter>()) gameObject.AddComponent<EnemyLaunchAnimationPresenter>();
            if (!GetComponent<EnemyLaunchVfxPresenter>()) gameObject.AddComponent<EnemyLaunchVfxPresenter>();
            launchController.LaunchCompleted += OnLaunchCompleted;
            damageVisual = GetComponent<EnemyDamageVisual>();
            if (!damageVisual) damageVisual = gameObject.AddComponent<EnemyDamageVisual>();
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            currentHitPoints = MaxHitPoints;

            groundFilter.useTriggers = false;
            groundFilter.useLayerMask = true;
            groundFilter.SetLayerMask(LayerMask.GetMask("Ground"));

            FacingDirection = spriteRenderer && !spriteRenderer.flipX ? Vector2.right : Vector2.left;
        }

        void Start()
        {
            patrolOrigin = transform.position;
            ResetPatrolTarget();

            var reader = FindObjectOfType<MirrorTrial.Player.PlayerInputReader>();
            if (reader) player = reader.transform;
        }

        void Update()
        {
            if (attackCooldownTimer > 0f) attackCooldownTimer -= Time.deltaTime;
            if (hurtTimer > 0f) hurtTimer -= Time.deltaTime;

            if (player == null || !player.gameObject.activeInHierarchy)
            {
                var reader = FindObjectOfType<MirrorTrial.Player.PlayerInputReader>();
                if (reader) player = reader.transform;
            }

            if (CurrentState == State.Hurt && hurtTimer <= 0f)
            {
                if (pendingDeath)
                    Die();
                else
                    TransitionTo(State.Idle);
            }

            if (CurrentState == State.Dead) return;
            if (CurrentState == State.Launch)
            {
                UpdateVisualFacing();
                return;
            }

            if (passiveTestTarget && CurrentState != State.Hurt)
            {
                if (CurrentState != State.Idle)
                    TransitionTo(State.Idle);
                SetVelocityX(0f);
                UpdateVisualFacing();
                return;
            }

            switch (CurrentState)
            {
                case State.Idle:
                    UpdateIdle();
                    break;
                case State.Chase:
                    UpdateChase();
                    break;
                case State.Attack:
                    UpdateAttack();
                    break;
            }

            UpdateVisualFacing();
        }

        void FixedUpdate()
        {
            if (CurrentState == State.Dead) return;
            CheckGrounded();
        }

        void UpdateIdle()
        {
            if (player != null && CanDetectPlayer())
            {
                TransitionTo(State.Chase);
                return;
            }

            UpdatePatrol();
        }

        void UpdatePatrol()
        {
            if (waitingAtPatrolPoint)
            {
                patrolWaitTimer -= Time.deltaTime;
                SetVelocityX(0f);
                if (patrolWaitTimer <= 0f)
                    waitingAtPatrolPoint = false;
                return;
            }

            var targetX = patrolTargetX;
            var delta = targetX - transform.position.x;
            if (Mathf.Abs(delta) <= 0.08f)
            {
                SetVelocityX(0f);
                patrolMovingRight = !patrolMovingRight;
                patrolTargetX = patrolOrigin.x + (patrolMovingRight ? PatrolRightOffset : PatrolLeftOffset);
                patrolWaitTimer = profile ? Mathf.Max(0f, profile.patrolWaitTime) : 0f;
                waitingAtPatrolPoint = patrolWaitTimer > 0f;
                return;
            }

            var direction = Mathf.Sign(delta);
            var blocked = profile && profile.wallCheckDistance > 0f && WallInDirection(direction);
            var ledge = profile && profile.ledgeCheckDistance > 0f && !GroundAhead(direction);
            if (blocked || ledge)
            {
                SetVelocityX(0f);
                patrolMovingRight = !patrolMovingRight;
                patrolTargetX = patrolOrigin.x + (patrolMovingRight ? PatrolRightOffset : PatrolLeftOffset);
                return;
            }

            FacingDirection = new Vector2(direction, 0f);
            SetVelocityX(direction * MoveSpeed);
        }

        void ResetPatrolTarget()
        {
            patrolMovingRight = FacingDirection.x >= 0f;
            patrolTargetX = patrolOrigin.x + (patrolMovingRight ? PatrolRightOffset : PatrolLeftOffset);
            waitingAtPatrolPoint = false;
        }

        void UpdateChase()
        {
            if (player == null)
            {
                TransitionTo(State.Idle);
                return;
            }

            if (!CanDetectPlayer())
            {
                TransitionTo(State.Idle);
                return;
            }

            Vector2 toPlayer = (Vector2)(player.position - transform.position);
            FacingDirection = new Vector2(Mathf.Sign(toPlayer.x), 0f);

            var stopDistance = profile ? profile.stopDistance : 1.2f;
            if (Vector2.Distance(transform.position, player.position) <= stopDistance && attackCooldownTimer <= 0f)
            {
                TransitionTo(State.Attack);
                return;
            }

            MoveTowards(player.position);
        }

        void UpdateAttack()
        {
            if (player == null || !CanDetectPlayer())
            {
                TransitionTo(State.Idle);
                return;
            }

            if (stateTimer <= 0f)
            {
                attackCooldownTimer = profile ? profile.attackCooldown : 1.2f;
                TransitionTo(State.Idle);
                return;
            }

            stateTimer -= Time.deltaTime;
            float windup = profile ? profile.attackWindup : 0.35f;
            float delay = profile ? profile.damageDelay : 0.1f;

            if (!attackLanded && stateTimer <= windup - delay)
            {
                attackLanded = true;
                PerformAttack();
            }
        }

        void PerformAttack()
        {
            PlayAttackEffect();

            if (profile && profile.IsRanged)
            {
                FireProjectile();
            }
            else
            {
                MeleeAttack();
            }
        }

        void PlayAttackEffect()
        {
            if (!attackEffectPrefab) return;

            var origin = (Vector2)transform.position;
            var position = attackEffectPoint ? (Vector2)attackEffectPoint.position : origin + FacingDirection * AttackSize.x * 0.5f;
            if (attackEffectPoint)
            {
                var offset = position - origin;
                position.x = origin.x + Mathf.Abs(offset.x) * Mathf.Sign(FacingDirection.x);
            }

            var rotation = attackEffectPoint ? attackEffectPoint.rotation : Quaternion.identity;
            var effect = Instantiate(attackEffectPrefab, position, rotation);
            if (mirrorAttackEffectByFacing)
            {
                var scale = effect.transform.localScale;
                scale.x = Mathf.Abs(scale.x) * Mathf.Sign(FacingDirection.x);
                effect.transform.localScale = scale;
            }

            Destroy(effect, Mathf.Max(0.01f, attackEffectLifetime));
        }
        void MeleeAttack()
        {
            var size = AttackSize;
            var center = (Vector2)transform.position + FacingDirection * size.x * 0.5f;
            var hits = Physics2D.OverlapBoxAll(center, size, 0f);
            foreach (var hit in hits)
            {
                if (hit.CompareTag("Player"))
                {
                    var payload = new DamagePayload(
                        gameObject,
                        profile ? profile.attackDamage : 1,
                        FacingDirection * (profile ? profile.knockbackForce : 4f),
                        FacingDirection,
                        0f,
                        profile ? profile.interruptPower : 1,
                        profile ? profile.poiseDamage : 1f,
                        profile ? profile.playerHitReaction : HitReactionType.LightHurt,
                        profile && profile.breaksSuperArmor
                    );
                    hit.SendMessage("OnDamagePayloadReceived", payload, SendMessageOptions.DontRequireReceiver);
                }
            }
        }

        void FireProjectile()
        {
            if (profile == null || profile.projectilePrefab == null) return;
            Vector2 spawn = (Vector2)transform.position + FacingDirection * profile.projectileSpawnOffset.x + Vector2.up * profile.projectileSpawnOffset.y;
            var instance = Instantiate(profile.projectilePrefab, spawn, Quaternion.identity);
            var projectile = instance.GetComponent<MirrorTrial.Abilities.MirrorBladeProjectile>();
            if (projectile != null)
            {
                var payload = new DamagePayload(
                    gameObject,
                    profile.attackDamage,
                    FacingDirection * profile.knockbackForce,
                    FacingDirection,
                    0f,
                    profile.interruptPower,
                    profile.poiseDamage,
                    profile.playerHitReaction,
                    profile.breaksSuperArmor
                );
                projectile.Launch(payload, FacingDirection, MoveSpeed * 2f, 8f);
            }
            else
            {
                var rb = instance.GetComponent<Rigidbody2D>();
                if (rb) rb.velocity = FacingDirection * MoveSpeed * 2f;
            }
        }

        void MoveTowards(Vector2 target)
        {
            float moveDir = Mathf.Sign(target.x - transform.position.x);
            float speed = MoveSpeed;

            bool blocked = profile && profile.wallCheckDistance > 0f && WallInDirection(moveDir);
            bool ledge = profile && profile.ledgeCheckDistance > 0f && !GroundAhead(moveDir);

            if (blocked || (ledge && !profile.canTurnAtLedge))
            {
                SetVelocityX(0f);
                return;
            }

            if (ledge && profile.canTurnAtLedge)
            {
                FacingDirection = -FacingDirection;
                SetVelocityX(0f);
                return;
            }

            SetVelocityX(moveDir * speed);
        }

        bool CanDetectPlayer()
        {
            if (player == null) return false;
            if (!ContainsCentered(player.position, DetectionSize)) return false;

            return true;
        }

        bool ContainsCentered(Vector2 point, Vector2 size)
        {
            var delta = point - (Vector2)transform.position;
            return Mathf.Abs(delta.x) <= size.x * 0.5f && Mathf.Abs(delta.y) <= size.y * 0.5f;
        }
        void CheckGrounded()
        {
            if (bodyCollider == null) return;
            var count = bodyCollider.Cast(Vector2.down, groundFilter, groundHits, 0.05f);
            isGrounded = false;
            for (var i = 0; i < count; i++)
            {
                if (groundHits[i].normal.y >= 0.6f)
                {
                    isGrounded = true;
                    break;
                }
            }
        }

        bool GroundAhead(float direction)
        {
            Vector2 origin = (Vector2)transform.position + Vector2.right * direction * (bodyCollider ? bodyCollider.bounds.extents.x : 0.4f);
            float distance = profile ? profile.ledgeCheckDistance : 0.5f;
            var hit = Physics2D.Raycast(origin, Vector2.down, distance, LayerMask.GetMask("Ground"));
            return hit.collider != null;
        }

        bool WallInDirection(float direction)
        {
            Vector2 origin = (Vector2)transform.position + Vector2.up * 0.3f;
            float distance = profile ? profile.wallCheckDistance : 0.3f;
            var hit = Physics2D.Raycast(origin, Vector2.right * direction, distance, LayerMask.GetMask("Ground"));
            return hit.collider != null;
        }

        void SetVelocityX(float x)
        {
            var v = body.velocity;
            v.x = x;
            body.velocity = v;
        }

        void UpdateVisualFacing()
        {
            if (!profile || !profile.flipVisualByVelocity || !spriteRenderer) return;
            if (Mathf.Abs(body.velocity.x) > 0.1f)
                spriteRenderer.flipX = body.velocity.x < 0f;
            else if (Mathf.Abs(FacingDirection.x) > 0.1f)
                spriteRenderer.flipX = FacingDirection.x < 0f;
        }

        public void TransitionTo(State next)
        {
            if (CurrentState == State.Dead && next != State.Dead) return;
            CurrentState = next;
            stateTimer = 0f;
            attackLanded = false;

            switch (next)
            {
                case State.Idle:
                    SetVelocityX(0f);
                    break;
                case State.Attack:
                    stateTimer = profile ? profile.attackWindup : 0.35f;
                    if (profile && profile.lockMovementWhileAttacking) SetVelocityX(0f);
                    break;
                case State.Hurt:
                    SetVelocityX(0f);
                    break;
            }
        }

        void OnDamagePayloadReceived(DamagePayload payload)
        {
            if (CurrentState == State.Dead) return;

            var beforeDamage = currentHitPoints;
            currentHitPoints = Mathf.Max(0, currentHitPoints - payload.damage);
            if (damageVisual)
                damageVisual.PlayDamage(beforeDamage - currentHitPoints, MaxHitPoints);

            pendingDeath = currentHitPoints <= 0;
            if (pendingDeath)
            {
                Die();
                return;
            }

            // The active launch sequence owns movement and reaction presentation. Hits still
            // deal damage, but cannot restart launch, redirect it, or interrupt get-up armor.
            if (launchController && launchController.IsLaunching)
                return;

            if (payload.reaction == HitReactionType.Launch)
            {
                TransitionTo(State.Launch);
                launchController.Configure(profile ? profile.launchSettings : null);
                launchController.BeginLaunch(payload.knockback);
                return;
            }
            if (payload.reaction == HitReactionType.None)
            {
                if (pendingDeath) Die();
                return;
            }

            var baseHurtTime = profile ? profile.hurtStun : 0.25f;
            hurtTimer = payload.reaction == HitReactionType.Launch ? baseHurtTime * 1.45f
                : payload.reaction == HitReactionType.HeavyHurt ? baseHurtTime * 1.2f
                : baseHurtTime;

            TransitionTo(State.Hurt);

            if (payload.knockback.sqrMagnitude > 0.0001f && body)
            {
                if (payload.reaction == HitReactionType.Launch)
                    body.velocity = new Vector2(body.velocity.x, Mathf.Max(0f, body.velocity.y));
                body.AddForce(payload.knockback, ForceMode2D.Impulse);
            }
        }

        void OnLaunchCompleted()
        {
            if (CurrentState != State.Launch) return;
            if (pendingDeath) Die();
            else TransitionTo(State.Idle);
        }

        void OnDestroy()
        {
            if (launchController) launchController.LaunchCompleted -= OnLaunchCompleted;
        }

        void Die()
        {
            pendingDeath = false;
            CurrentState = State.Dead;
            SetVelocityX(0f);
            if (bodyCollider) bodyCollider.enabled = false;
            var hurtbox = GetComponent<Hurtbox>();
            if (hurtbox) hurtbox.enabled = false;
            StartCoroutine(DeathBlinkRoutine());
        }

        IEnumerator DeathBlinkRoutine()
        {
            if (!spriteRenderer)
            {
                yield return new WaitForSeconds(profile ? profile.deathFadeDelay : 0.3f);
                DestroySelf();
                yield break;
            }

            int blinkCount = profile ? Mathf.Max(0, profile.deathBlinkCount) : 2;
            float interval = profile ? Mathf.Max(0.01f, profile.deathBlinkInterval) : 0.08f;

            for (var i = 0; i < blinkCount; i++)
            {
                spriteRenderer.enabled = false;
                yield return new WaitForSeconds(interval);
                spriteRenderer.enabled = true;
                yield return new WaitForSeconds(interval);
            }

            DestroySelf();
        }

        void DestroySelf()
        {
            if (spriteRenderer) spriteRenderer.enabled = true;
            Destroy(gameObject);
        }

        void OnDrawGizmosSelected()
        {
            if (!showDebugGizmos) return;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position, DetectionSize);
            Gizmos.color = Color.green;
            var origin = PatrolOrigin;
            Gizmos.DrawLine(new Vector3(origin.x + PatrolLeftOffset, origin.y), new Vector3(origin.x + PatrolRightOffset, origin.y));
        }
    }
}
