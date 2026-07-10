using Platformer.Mechanics;
using UnityEngine;

namespace MirrorTrial.Player
{
    [RequireComponent(typeof(PlayerInputReader), typeof(PlayerTuning), typeof(SpriteRenderer))]
    public class PlayerMotor : KinematicObject
    {
        PlayerInputReader input;
        PlayerTuning tuning;
        SpriteRenderer spriteRenderer;
        PlayerStateMachine stateMachine;
        BoxCollider2D bodyCollider;

        float currentMoveSpeed;
        float jumpBufferCounter;
        float coyoteCounter;
        bool facingRight = true;
        Vector2 forcedVelocity;
        float forcedVelocityTimer;

        public bool MovementLocked { get; set; }

        [Header("Ground snap (稳定化)")]
        [Tooltip("角色底部向下探测这个距离内有地面，就吸附并清零下落速度，消除落地抖动")]
        [SerializeField] float groundSnapDistance = 0.12f;
        [Tooltip("参与地面探测的层（默认 Everything）")]
        [SerializeField] LayerMask groundMask = ~0;

        bool stableGrounded;
        readonly RaycastHit2D[] snapHits = new RaycastHit2D[8];

        // 权威锁：状态机说要锁（Attack/Hurt/Dead）或旧的 MovementLocked 布尔任一为真即锁
        bool EffectiveMovementLock =>
            MovementLocked || (stateMachine != null && stateMachine.ShouldLockMovement);
        public bool FacingRight => facingRight;
        public float MoveX => input ? input.MoveX : 0f;
        public bool IsRising => velocity.y > 0.01f;
        public bool IsFalling => velocity.y < -0.01f;
        public Vector2 Velocity => velocity;

        // 状态机 / 动画应读这个稳定的落地标志，而不是基类每帧横跳的 IsGrounded
        public bool StableGrounded => stableGrounded;

        void Awake()
        {
            input = GetComponent<PlayerInputReader>();
            tuning = GetComponent<PlayerTuning>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            stateMachine = GetComponent<PlayerStateMachine>();
            bodyCollider = GetComponent<BoxCollider2D>();
            // KinematicObject.groundNormal defaults to (0,0) which kills horizontal movement.
            // Initialize to up so air movement works before first ground contact.
            groundNormal = Vector2.up;
        }

        protected override void Update()
        {
            UpdateJumpTimers();
            base.Update();
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();
            StabilizeGrounding();
        }

        /// <summary>
        /// 落地稳定化：从 collider 底部向下做一次 BoxCast，
        /// 若地面在 groundSnapDistance 内且当前正在下落 / 静止，就精确吸附到地面表面并清零下落速度。
        /// 这打断了原版 KinematicObject "沉入地面→重力重新加→再穿透" 的抖动死循环。
        /// </summary>
        void StabilizeGrounding()
        {
            if (bodyCollider == null)
            {
                stableGrounded = IsGrounded;
                return;
            }

            // 上升阶段（起跳/被击飞）不吸附，否则会把跳跃吃掉
            if (velocity.y > 0.01f)
            {
                stableGrounded = false;
                return;
            }

            Bounds b = bodyCollider.bounds;
            Vector2 origin = new Vector2(b.center.x, b.min.y + shellRadius);
            Vector2 size = new Vector2(Mathf.Max(0.02f, b.size.x - shellRadius * 2f), shellRadius);

            int count = Physics2D.BoxCastNonAlloc(
                origin, size, 0f, Vector2.down,
                snapHits, groundSnapDistance + shellRadius, groundMask);

            float bestGap = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                var h = snapHits[i];
                if (h.collider == null) continue;
                if (h.collider.isTrigger) continue;
                if (h.collider.transform.IsChildOf(transform)) continue; // 忽略自身/子物体
                if (h.normal.y < minGroundNormalY) continue;              // 只吸附够平的地面
                if (h.distance < bestGap)
                {
                    bestGap = h.distance;
                    found = true;
                }
            }

            if (found)
            {
                // 把角色底部精确贴到地面（保留 shellRadius 薄壳，避免穿插）
                float correction = bestGap - shellRadius;
                if (Mathf.Abs(correction) > 0.0001f)
                    body.position += Vector2.down * correction;

                if (velocity.y < 0f)
                    velocity.y = 0f;

                if (!stableGrounded)
                    Debug.Log($"[Snap] 稳定落地 pos.y={body.position.y:F3} gap={bestGap:F3}  ✅");
                stableGrounded = true;
            }
            else
            {
                stableGrounded = false;
            }
        }

        public void ApplyForcedVelocity(Vector2 nextVelocity, float duration)
        {
            forcedVelocity = nextVelocity;
            forcedVelocityTimer = Mathf.Max(0f, duration);
            velocity = nextVelocity;
        }

        public void ApplyKnockback(Vector2 knockback, float duration)
        {
            ApplyForcedVelocity(knockback, duration);
        }

        void UpdateJumpTimers()
        {
            if (input.JumpPressed)
                jumpBufferCounter = tuning.movement.jumpBufferTime;
            else
                jumpBufferCounter -= Time.deltaTime;

            if (stableGrounded)
                coyoteCounter = tuning.movement.coyoteTime;
            else
                coyoteCounter -= Time.deltaTime;
        }

        protected override void ComputeVelocity()
        {
            if (forcedVelocityTimer > 0f)
            {
                forcedVelocityTimer -= Time.deltaTime;
                targetVelocity = forcedVelocity;
                velocity.y = forcedVelocity.y;
                return;
            }

            var moveX = EffectiveMovementLock ? 0f : input.MoveX;
            var targetSpeed = moveX * tuning.movement.moveSpeed;
            var accel = Mathf.Abs(targetSpeed) > 0.01f
                ? (IsGrounded ? tuning.movement.acceleration : tuning.movement.airAcceleration)
                : tuning.movement.deceleration;

            currentMoveSpeed = Mathf.MoveTowards(currentMoveSpeed, targetSpeed, accel * Time.deltaTime);

            if (jumpBufferCounter > 0f && coyoteCounter > 0f && !EffectiveMovementLock)
            {
                velocity.y = tuning.movement.jumpSpeed;
                jumpBufferCounter = 0f;
                coyoteCounter = 0f;
            }
            else if (input.JumpReleased && velocity.y > 0f)
            {
                velocity.y *= tuning.movement.jumpCutMultiplier;
            }

            gravityModifier = velocity.y < 0f
                ? tuning.movement.fallGravityMultiplier
                : tuning.movement.baseGravityModifier;

            UpdateFacing(moveX);
            targetVelocity = new Vector2(currentMoveSpeed, 0f);
        }

        void UpdateFacing(float moveX)
        {
            if (moveX > 0.01f)
                facingRight = true;
            else if (moveX < -0.01f)
                facingRight = false;

            spriteRenderer.flipX = !facingRight;
        }
    }
}
