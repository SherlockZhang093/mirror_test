using MirrorTrial.Combat;
using MirrorTrial.Level;
using UnityEngine;

namespace MirrorTrial.Enemies
{
    [RequireComponent(typeof(Hurtbox))]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public class EnemyAI : MonoBehaviour
    {
        [Header("AI 配置")]
        [ChineseLabel("AI行为配置")] [Tooltip("引用的 EnemyAIProfile 资产")] [SerializeField] EnemyAIProfile profile;

        [Header("运行覆盖")]
        [ChineseLabel("覆盖血量")] [Tooltip("正数=覆盖Profile的最大生命值，-1=不覆盖")] public int overrideMaxHitPoints = -1;
        [ChineseLabel("覆盖移速")] [Tooltip("正数=覆盖Profile的移动速度，-1=不覆盖")] public float overrideMoveSpeed = -1f;

        [Header("调试")]
        [ChineseLabel("显示Gizmos")] [Tooltip("是否在选中时显示侦测/攻击范围")] [SerializeField] bool showDebugGizmos = true;

        public enum State
        {
            [InspectorName("待机")] Idle,
            [InspectorName("追击")] Chase,
            [InspectorName("攻击")] Attack,
            [InspectorName("受击")] Hurt,
            [InspectorName("死亡")] Dead
        }

        public State CurrentState { get; private set; } = State.Idle;
        public EnemyAIProfile Profile => profile;
        public Vector2 FacingDirection { get; private set; } = Vector2.right;

        int currentHitPoints;
        float stateTimer;
        float attackCooldownTimer;
        float hurtTimer;
        bool attackLanded;
        bool isGrounded;

        Rigidbody2D body;
        Collider2D bodyCollider;
        SpriteRenderer spriteRenderer;
        Transform player;
        int patrolIndex;
        Vector2[] patrolPath;
        Vector2 patrolTarget;
        float patrolWaitTimer;
        bool waitingAtPatrolPoint;

        ContactFilter2D groundFilter = new ContactFilter2D();
        readonly RaycastHit2D[] groundHits = new RaycastHit2D[4];
        readonly RaycastHit2D[] wallHits = new RaycastHit2D[4];

        public int CurrentHitPoints => currentHitPoints;
        public int MaxHitPoints => overrideMaxHitPoints > 0 ? overrideMaxHitPoints : (profile ? profile.maxHitPoints : 2);
        public float MoveSpeed => overrideMoveSpeed > 0 ? overrideMoveSpeed : (profile ? profile.moveSpeed : 2.5f);

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

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
            body.gravityScale = 1f;
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
            var spawnPoint = GetComponentInParent<SpawnPoint>();
            if (spawnPoint && spawnPoint.PatrolPath.Length > 0)
            {
                patrolPath = new Vector2[spawnPoint.PatrolPath.Length];
                for (var i = 0; i < spawnPoint.PatrolPath.Length; i++)
                    patrolPath[i] = spawnPoint.PatrolPath[i].position;
            }
            patrolTarget = transform.position;

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
                TransitionTo(State.Idle);

            if (CurrentState == State.Dead) return;

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
            if (player == null) return;

            float distanceToPlayer = Vector2.Distance(transform.position, player.position);
            bool canDetect = CanDetectPlayer(distanceToPlayer);

            if (canDetect)
            {
                TransitionTo(State.Chase);
                return;
            }

            if (!profile || !profile.enablePatrol || patrolPath == null || patrolPath.Length == 0) return;

            if (waitingAtPatrolPoint)
            {
                patrolWaitTimer -= Time.deltaTime;
                if (patrolWaitTimer <= 0f) waitingAtPatrolPoint = false;
                return;
            }

            Vector2 toTarget = patrolTarget - (Vector2)transform.position;
            if (toTarget.magnitude < 0.1f)
            {
                waitingAtPatrolPoint = true;
                patrolWaitTimer = profile.patrolWaitTime;
                patrolIndex = (patrolIndex + 1) % patrolPath.Length;
                patrolTarget = patrolPath[patrolIndex];
                SetVelocityX(0f);
            }
            else
            {
                FacingDirection = new Vector2(Mathf.Sign(toTarget.x), 0f);
                MoveTowards(patrolTarget);
            }
        }

        void UpdateChase()
        {
            if (player == null)
            {
                TransitionTo(State.Idle);
                return;
            }

            float distanceToPlayer = Vector2.Distance(transform.position, player.position);
            if (distanceToPlayer > (profile ? profile.loseInterestRange : 12f))
            {
                TransitionTo(State.Idle);
                return;
            }

            Vector2 toPlayer = (Vector2)(player.position - transform.position);
            FacingDirection = new Vector2(Mathf.Sign(toPlayer.x), 0f);

            float stopDist = profile ? profile.stopDistance : 1.2f;
            if (distanceToPlayer <= stopDist && attackCooldownTimer <= 0f)
            {
                TransitionTo(State.Attack);
                return;
            }

            MoveTowards(player.position);
        }

        void UpdateAttack()
        {
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
            if (profile && profile.IsRanged)
            {
                FireProjectile();
            }
            else
            {
                MeleeAttack();
            }
        }

        void MeleeAttack()
        {
            float range = profile ? profile.attackRange : 1.2f;
            var center = (Vector2)transform.position + FacingDirection * range * 0.5f;
            var size = new Vector2(range, 0.8f);
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
                        0f
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
                    0f
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

        bool CanDetectPlayer(float distance)
        {
            if (player == null) return false;
            float detectRange = profile ? profile.detectionRange : 8f;
            if (distance > detectRange) return false;

            if (profile && profile.useForwardDetection)
            {
                Vector2 toPlayer = (Vector2)(player.position - transform.position);
                float angle = Vector2.Angle(FacingDirection, toPlayer);
                if (angle > profile.forwardDetectionAngle) return false;
            }

            return true;
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

            currentHitPoints -= payload.damage;
            hurtTimer = profile ? profile.hurtStun : 0.25f;

            if (payload.knockback.magnitude > 0.01f && body != null)
                body.AddForce(payload.knockback, ForceMode2D.Impulse);

            TransitionTo(State.Hurt);

            if (currentHitPoints <= 0)
                Die();
        }

        void Die()
        {
            CurrentState = State.Dead;
            SetVelocityX(0f);
            if (bodyCollider) bodyCollider.enabled = false;
            var hurtbox = GetComponent<Hurtbox>();
            if (hurtbox) hurtbox.enabled = false;
            Invoke(nameof(DestroySelf), profile ? profile.deathFadeDelay : 0.3f);
        }

        void DestroySelf()
        {
            Destroy(gameObject);
        }

        void OnDrawGizmosSelected()
        {
            if (!showDebugGizmos || profile == null) return;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, profile.detectionRange);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, profile.attackRange);
        }
    }
}
