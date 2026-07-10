using UnityEngine;

namespace MirrorTrial.Player
{
    [RequireComponent(typeof(PlayerInputReader), typeof(PlayerTuning))]
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public class PlayerMotor : MonoBehaviour
    {
        [Header("Grounding")]
        [SerializeField] LayerMask groundMask = ~0;
        [SerializeField, Min(0.001f)] float groundCheckDistance = 0.04f;
        [SerializeField, Range(0f, 1f)] float minGroundNormalY = 0.9f;

        PlayerInputReader input;
        PlayerTuning tuning;
        SpriteRenderer spriteRenderer;
        PlayerStateMachine stateMachine;
        Rigidbody2D body;
        Collider2D bodyCollider;

        readonly ContactPoint2D[] contactPoints = new ContactPoint2D[8];
        readonly RaycastHit2D[] groundHits = new RaycastHit2D[8];
        ContactFilter2D groundFilter;

        float currentMoveSpeed;
        float jumpBufferCounter;
        float coyoteCounter;
        bool jumpCutRequested;
        bool facingRight = true;
        Vector2 forcedVelocity;
        float forcedVelocityTimer;

        public bool MovementLocked { get; set; }

        bool EffectiveMovementLock =>
            MovementLocked || (stateMachine != null && stateMachine.ShouldLockMovement);

        public bool FacingRight => facingRight;
        public float MoveX => input ? input.MoveX : 0f;
        public bool IsGrounded { get; private set; }
        public bool StableGrounded => IsGrounded;
        public bool IsRising => Velocity.y > 0.01f;
        public bool IsFalling => Velocity.y < -0.01f;
        public Vector2 Velocity => body ? body.velocity : Vector2.zero;

        void Awake()
        {
            input = GetComponent<PlayerInputReader>();
            tuning = GetComponent<PlayerTuning>();
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
            stateMachine = GetComponent<PlayerStateMachine>();
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();

            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            groundFilter = new ContactFilter2D
            {
                useLayerMask = true,
                useTriggers = false
            };
            groundFilter.SetLayerMask(groundMask);
        }

        void Update()
        {
            if (input.JumpPressed)
                jumpBufferCounter = tuning.movement.jumpBufferTime;

            if (input.JumpReleased)
                jumpCutRequested = true;

            UpdateFacing(EffectiveMovementLock ? 0f : input.MoveX);
        }

        void FixedUpdate()
        {
            var deltaTime = Time.fixedDeltaTime;
            RefreshGrounded();
            UpdateJumpTimers(deltaTime);

            if (forcedVelocityTimer > 0f)
            {
                forcedVelocityTimer = Mathf.Max(0f, forcedVelocityTimer - deltaTime);
                body.velocity = forcedVelocity;
                IsGrounded = forcedVelocity.y <= 0.01f && IsGrounded;
                return;
            }

            var velocity = body.velocity;
            var moveX = EffectiveMovementLock ? 0f : input.MoveX;
            var targetSpeed = moveX * tuning.movement.moveSpeed;
            var acceleration = Mathf.Abs(targetSpeed) > 0.01f
                ? (IsGrounded ? tuning.movement.acceleration : tuning.movement.airAcceleration)
                : tuning.movement.deceleration;

            currentMoveSpeed = Mathf.MoveTowards(
                currentMoveSpeed,
                targetSpeed,
                acceleration * deltaTime);
            velocity.x = currentMoveSpeed;

            if (jumpBufferCounter > 0f && coyoteCounter > 0f && !EffectiveMovementLock)
            {
                velocity.y = tuning.movement.jumpSpeed;
                jumpBufferCounter = 0f;
                coyoteCounter = 0f;
                IsGrounded = false;
            }
            else if (jumpCutRequested && velocity.y > 0f)
            {
                velocity.y *= tuning.movement.jumpCutMultiplier;
            }
            jumpCutRequested = false;

            if (!IsGrounded || velocity.y > 0.01f)
            {
                var gravityMultiplier = velocity.y < 0f
                    ? tuning.movement.fallGravityMultiplier
                    : tuning.movement.baseGravityModifier;
                velocity += Physics2D.gravity * gravityMultiplier * deltaTime;
            }
            else if (velocity.y < 0f)
            {
                velocity.y = 0f;
            }

            body.velocity = velocity;
        }

        void RefreshGrounded()
        {
            IsGrounded = HasGroundContact() || HasGroundBelow();
        }

        bool HasGroundContact()
        {
            var count = body.GetContacts(groundFilter, contactPoints);
            for (var i = 0; i < count; i++)
            {
                if (contactPoints[i].normal.y >= minGroundNormalY)
                    return true;
            }
            return false;
        }

        bool HasGroundBelow()
        {
            var count = bodyCollider.Cast(
                Vector2.down,
                groundFilter,
                groundHits,
                groundCheckDistance);

            for (var i = 0; i < count; i++)
            {
                var hit = groundHits[i];
                if (hit.collider != null && hit.normal.y >= minGroundNormalY)
                    return true;
            }
            return false;
        }

        void UpdateJumpTimers(float deltaTime)
        {
            jumpBufferCounter = Mathf.Max(0f, jumpBufferCounter - deltaTime);

            if (IsGrounded)
                coyoteCounter = tuning.movement.coyoteTime;
            else
                coyoteCounter = Mathf.Max(0f, coyoteCounter - deltaTime);
        }

        void UpdateFacing(float moveX)
        {
            if (moveX > 0.01f)
                facingRight = true;
            else if (moveX < -0.01f)
                facingRight = false;

            if (spriteRenderer)
                spriteRenderer.flipX = !facingRight;
        }

        public void ApplyForcedVelocity(Vector2 nextVelocity, float duration)
        {
            forcedVelocity = nextVelocity;
            forcedVelocityTimer = Mathf.Max(0f, duration);
            body.velocity = nextVelocity;
            if (nextVelocity.y > 0.01f)
                IsGrounded = false;
        }

        public void ApplyKnockback(Vector2 knockback, float duration)
        {
            ApplyForcedVelocity(knockback, duration);
        }
    }
}
