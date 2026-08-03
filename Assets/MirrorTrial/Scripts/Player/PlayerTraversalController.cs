using System.Collections;
using UnityEngine;

namespace MirrorTrial.Player
{
    [DefaultExecutionOrder(-70)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInputReader), typeof(PlayerMotor))]
    [RequireComponent(typeof(PlayerStateMachine), typeof(PlayerAnimationDriver))]
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class PlayerTraversalController : MonoBehaviour, IInterruptiblePlayerAction
    {
        enum TraversalMode
        {
            None,
            LedgeHang,
            LedgeClimb,
            MonkeyBarIdle
        }

        [Header("Ledge Detection")]
        [SerializeField] LayerMask ledgeMask = ~0;
        [SerializeField, Min(0.01f)] float wallCheckDistance = 0.28f;
        [SerializeField, Min(0.01f)] float topProbeHeight = 0.55f;
        [SerializeField, Min(0.05f)] float topProbeDepth = 1.35f;
        [SerializeField, Min(0f)] float topSurfaceInset = 0.08f;
        [SerializeField, Range(0f, 1f)] float minTopNormalY = 0.8f;

        [Header("Ledge Alignment")]
        [SerializeField, Min(0f)] float hangHorizontalOffset = 0.28f;
        [SerializeField, Min(0f)] float hangRootBelowLedge = 1.25f;
        [SerializeField, Min(0f)] float standSurfaceInset = 0.12f;
        [SerializeField, Min(0f)] float standSkin = 0.03f;
        [SerializeField, Min(0.05f)] float ledgeClimbDuration = 0.6666666f;
        [SerializeField, Min(0f)] float recatchDelay = 0.15f;

        PlayerInputReader input;
        PlayerMotor motor;
        PlayerStateMachine stateMachine;
        PlayerAnimationDriver animationDriver;
        Rigidbody2D body;
        Collider2D bodyCollider;

        TraversalMode mode;
        Coroutine climbRoutine;
        Vector2 lockedRootPosition;
        Vector2 climbStandPosition;
        Transform monkeyBarAnchor;
        Vector2 monkeyBarRootOffset;
        float canCatchAgainTime;

        public bool IsLedgeHanging => mode == TraversalMode.LedgeHang;
        public bool IsLedgeClimbing => mode == TraversalMode.LedgeClimb;
        public bool IsMonkeyBarIdle => mode == TraversalMode.MonkeyBarIdle;
        public bool IsTraversing => mode != TraversalMode.None;

        void Awake()
        {
            input = GetComponent<PlayerInputReader>();
            motor = GetComponent<PlayerMotor>();
            stateMachine = GetComponent<PlayerStateMachine>();
            animationDriver = GetComponent<PlayerAnimationDriver>();
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
        }

        void OnDisable()
        {
            CancelCurrentAction(PlayerActionCancelReason.Hit);
        }

        void Update()
        {
            switch (mode)
            {
                case TraversalMode.LedgeHang:
                    KeepRootLocked();
                    if (input.JumpPressed)
                        climbRoutine = StartCoroutine(ClimbLedge());
                    else if (input.MoveY < -0.5f)
                        ExitTraversal(Vector2.down);
                    return;

                case TraversalMode.LedgeClimb:
                    KeepRootLocked();
                    return;

                case TraversalMode.MonkeyBarIdle:
                    if (!monkeyBarAnchor)
                        ExitTraversal(Vector2.zero);
                    else
                    {
                        lockedRootPosition = (Vector2)monkeyBarAnchor.position + monkeyBarRootOffset;
                        KeepRootLocked();
                    }
                    return;
            }

            if (Time.time >= canCatchAgainTime)
                TryCatchLedge();
        }

        void FixedUpdate()
        {
            if (mode != TraversalMode.None)
                KeepRootLocked();
        }

        void TryCatchLedge()
        {
            if (!input.InputEnabled || motor.IsGrounded || !motor.IsFalling ||
                stateMachine.IsInActionState || motor.TraversalLocked)
                return;

            var bounds = bodyCollider.bounds;
            var direction = motor.FacingRight ? 1f : -1f;
            var wallOrigin = new Vector2(
                bounds.center.x,
                bounds.max.y - Mathf.Min(0.32f, bounds.size.y * 0.25f));
            var wallHit = Physics2D.Raycast(
                wallOrigin,
                Vector2.right * direction,
                wallCheckDistance,
                ledgeMask);
            if (!IsUsableHit(wallHit) || Mathf.Abs(wallHit.normal.x) < 0.8f)
                return;

            var upperOrigin = new Vector2(bounds.center.x, bounds.max.y + standSkin);
            var upperHit = Physics2D.Raycast(
                upperOrigin,
                Vector2.right * direction,
                wallCheckDistance,
                ledgeMask);
            if (IsUsableHit(upperHit))
                return;

            var topOrigin = new Vector2(
                wallHit.point.x + direction * topSurfaceInset,
                bounds.max.y + topProbeHeight);
            var topHit = Physics2D.Raycast(
                topOrigin,
                Vector2.down,
                topProbeDepth,
                ledgeMask);
            if (!IsUsableHit(topHit) || topHit.normal.y < minTopNormalY ||
                topHit.point.y <= wallOrigin.y + standSkin)
                return;

            var rootToBottom = body.position.y - bounds.min.y;
            var standPosition = new Vector2(
                wallHit.point.x + direction * (bounds.extents.x + standSurfaceInset),
                topHit.point.y + rootToBottom + standSkin);
            if (!HasStandingClearance(standPosition, bounds))
                return;

            lockedRootPosition = new Vector2(
                wallHit.point.x - direction * hangHorizontalOffset,
                topHit.point.y - hangRootBelowLedge);
            climbStandPosition = standPosition;
            EnterLedgeHang();
        }

        bool HasStandingClearance(Vector2 rootPosition, Bounds currentBounds)
        {
            var rootDelta = rootPosition - body.position;
            var center = (Vector2)currentBounds.center + rootDelta + Vector2.up * standSkin;
            var size = (Vector2)currentBounds.size;
            size.x *= 0.9f;
            size.y *= 0.9f;
            var overlaps = Physics2D.OverlapBoxAll(center, size, 0f, ledgeMask);
            for (var i = 0; i < overlaps.Length; i++)
            {
                var overlap = overlaps[i];
                if (overlap && overlap != bodyCollider && !overlap.isTrigger)
                    return false;
            }
            return true;
        }

        static bool IsUsableHit(RaycastHit2D hit)
        {
            return hit.collider && !hit.collider.isTrigger;
        }

        void EnterLedgeHang()
        {
            mode = TraversalMode.LedgeHang;
            motor.CancelForcedVelocity();
            motor.TraversalLocked = true;
            KeepRootLocked();
            animationDriver.ForceState(PlayerActionState.LedgeHang);
            animationDriver.PlayStateImmediately(PlayerActionState.LedgeHang);
        }

        IEnumerator ClimbLedge()
        {
            mode = TraversalMode.LedgeClimb;
            animationDriver.ForceState(PlayerActionState.LedgeClimb);
            animationDriver.PlayStateImmediately(PlayerActionState.LedgeClimb);
            yield return new WaitForSeconds(ledgeClimbDuration);

            body.position = climbStandPosition;
            lockedRootPosition = climbStandPosition;
            climbRoutine = null;
            ExitTraversal(Vector2.zero);
        }

        void KeepRootLocked()
        {
            if (!body)
                return;
            body.position = lockedRootPosition;
            body.velocity = Vector2.zero;
        }

        public bool EnterMonkeyBarIdle(Transform anchor)
        {
            return EnterMonkeyBarIdle(anchor, Vector2.zero);
        }

        public bool EnterMonkeyBarIdle(Transform anchor, Vector2 playerRootOffset)
        {
            if (!anchor || IsTraversing || stateMachine.IsInActionState)
                return false;

            monkeyBarAnchor = anchor;
            monkeyBarRootOffset = playerRootOffset;
            lockedRootPosition = (Vector2)anchor.position + playerRootOffset;
            mode = TraversalMode.MonkeyBarIdle;
            motor.CancelForcedVelocity();
            motor.TraversalLocked = true;
            KeepRootLocked();
            animationDriver.ForceState(PlayerActionState.MonkeyBarIdle);
            animationDriver.PlayStateImmediately(PlayerActionState.MonkeyBarIdle);
            return true;
        }

        public void ExitMonkeyBarIdle(Vector2 releaseVelocity)
        {
            if (mode == TraversalMode.MonkeyBarIdle)
                ExitTraversal(releaseVelocity);
        }

        void ExitTraversal(Vector2 releaseVelocity)
        {
            if (climbRoutine != null)
            {
                StopCoroutine(climbRoutine);
                climbRoutine = null;
            }

            animationDriver.ClearForcedState(PlayerActionState.LedgeHang);
            animationDriver.ClearForcedState(PlayerActionState.LedgeClimb);
            animationDriver.ClearForcedState(PlayerActionState.MonkeyBarIdle);
            mode = TraversalMode.None;
            monkeyBarAnchor = null;
            motor.TraversalLocked = false;
            canCatchAgainTime = Time.time + recatchDelay;

            if (releaseVelocity.sqrMagnitude > 0.0001f)
                motor.ApplyForcedVelocity(releaseVelocity, 0.08f);
        }

        public void CancelCurrentAction(PlayerActionCancelReason reason)
        {
            if (mode != TraversalMode.None)
                ExitTraversal(Vector2.zero);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            var collider = GetComponent<Collider2D>();
            var currentMotor = GetComponent<PlayerMotor>();
            if (!collider || !currentMotor)
                return;

            var bounds = collider.bounds;
            var direction = currentMotor.FacingRight ? 1f : -1f;
            var origin = new Vector3(
                bounds.center.x,
                bounds.max.y - Mathf.Min(0.32f, bounds.size.y * 0.25f),
                transform.position.z);
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(origin, origin + Vector3.right * direction * wallCheckDistance);
        }
#endif
    }
}
