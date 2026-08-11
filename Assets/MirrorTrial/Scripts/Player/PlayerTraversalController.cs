using System.Collections;
using MirrorTrial.Level;
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
            WallCling,
            MonkeyBarIdle,
            Ladder
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
        [SerializeField] Vector2 ledgeJumpVelocity = new Vector2(2.5f, 6.5f);

        [Header("Wall Jump")]
        // Wall traversal is opt-in through a ClimbableWall component on level geometry.
        [SerializeField, Min(0.01f)] float climbableWallCheckDistance = 0.16f;
        [SerializeField, Min(0f)] float wallSlideSpeed = 1.2f;
        [SerializeField, Min(0f)] float wallGroundClearance = 0.12f;
        [SerializeField, Min(0f)] float wallJumpRecatchDelay = 0.14f;
        [SerializeField] Vector2 wallJumpVelocity = new Vector2(5.5f, 8f);

        [Header("Ladder")]
        [SerializeField, Min(0.1f)] float ladderSpeed = 2.4f;
        [SerializeField, Min(0.05f)] float ladderGrabTime = 0.15f;
        [SerializeField, Min(0.05f)] float ladderFinishTime = 0.55f;
        [SerializeField] Vector2 ladderJumpVelocity = new Vector2(2.5f, 6.5f);

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
        float ledgeWallDirection;
        bool canClimbLedge;
        Transform monkeyBarAnchor;
        Vector2 monkeyBarRootOffset;
        float canCatchAgainTime;
        float canCatchWallAgainTime;
        float wallDirection;
        LadderClimbZone ladder;
        PlayerActionState ladderAnimationState = PlayerActionState.None;
        float ladderGrabTimer;
        bool ladderAtTop;

        public bool IsLedgeHanging => mode == TraversalMode.LedgeHang;
        public bool IsLedgeClimbing => mode == TraversalMode.LedgeClimb;
        public bool IsWallClinging => mode == TraversalMode.WallCling;
        public bool IsMonkeyBarIdle => mode == TraversalMode.MonkeyBarIdle;
        public bool IsTraversing => mode != TraversalMode.None;
        public bool IsOnLadder => mode == TraversalMode.Ladder;

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
                    {
                        var hasHorizontalInput = Mathf.Abs(input.MoveX) > 0.15f;
                        var inputDirection = hasHorizontalInput
                            ? Mathf.Sign(input.MoveX)
                            : ledgeWallDirection;
                        if (inputDirection == ledgeWallDirection && canClimbLedge)
                            climbRoutine = StartCoroutine(ClimbLedge());
                        else
                            ExitTraversal(new Vector2(
                                -ledgeWallDirection * ledgeJumpVelocity.x,
                                ledgeJumpVelocity.y));
                    }
                    else if (input.MoveY < -0.5f)
                        ExitTraversal(Vector2.down);
                    return;

                case TraversalMode.LedgeClimb:
                    KeepRootLocked();
                    return;

                case TraversalMode.WallCling:
                    UpdateWallCling();
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

                case TraversalMode.Ladder:
                    UpdateLadder();
                    return;
            }

            if (Time.time >= canCatchWallAgainTime && TryCatchClimbableWall())
                return;

            if (Time.time >= canCatchAgainTime)
                TryCatchLedge();
        }

        void FixedUpdate()
        {
            if (mode == TraversalMode.WallCling)
                SlideOnWall();
            else if (mode == TraversalMode.Ladder)
                MoveOnLadder();
            else if (mode != TraversalMode.None)
                KeepRootLocked();
        }

        public bool TryEnterLadder(LadderClimbZone zone)
        {
            if (!zone || (IsTraversing && mode != TraversalMode.Ladder) || stateMachine.IsInActionState)
                return false;
            if (mode == TraversalMode.Ladder)
                return ladder == zone;

            ladder = zone;
            mode = TraversalMode.Ladder;
            ladderGrabTimer = ladderGrabTime;
            ladderAtTop = body.position.y >= ladder.TopY - 0.01f;
            motor.CancelForcedVelocity();
            motor.TraversalLocked = true;
            SetTopSupportIgnored(true);
            body.position = new Vector2(ladder.CenterX, Mathf.Clamp(body.position.y, ladder.BottomY, ladder.TopY));
            SetLadderAnimation(PlayerActionState.LadderGrab);
            return true;
        }

        void UpdateLadder()
        {
            if (!ladder)
            {
                ExitTraversal(Vector2.zero);
                return;
            }

            var jumpPressed = input.InputEnabled &&
                (input.JumpPressed || Input.GetKeyDown(KeyCode.Space));
            var moveX = GetLadderHorizontalInput();

            if (jumpPressed)
            {
                if (climbRoutine == null)
                    climbRoutine = StartCoroutine(JumpOffLadder());
                return;
            }

            ladderGrabTimer = Mathf.Max(0f, ladderGrabTimer - Time.deltaTime);
            if (ladderGrabTimer > 0f)
                return;

            ladderAtTop |= body.position.y >= ladder.TopY - 0.01f;
            if (ladderAtTop && Mathf.Abs(moveX) > 0.15f)
            {
                if (climbRoutine == null)
                    climbRoutine = StartCoroutine(FinishLadder(Mathf.Sign(moveX)));
                return;
            }

            if (!ladderAtTop && input.MoveY > 0.15f)
                SetLadderAnimation(motor.FacingRight ? PlayerActionState.LadderClimbUpRight : PlayerActionState.LadderClimbUpLeft);
            else if (input.MoveY < -0.15f)
                SetLadderAnimation(motor.FacingRight ? PlayerActionState.LadderClimbDownRight : PlayerActionState.LadderClimbDownLeft);
            else
                SetLadderAnimation(PlayerActionState.LadderIdle);
        }

        void MoveOnLadder()
        {
            if (!ladder || ladderGrabTimer > 0f)
                return;

            var nextY = body.position.y + input.MoveY * ladderSpeed * Time.fixedDeltaTime;
            if (nextY >= ladder.TopY && input.MoveY > 0.15f)
            {
                ladderAtTop = true;
                body.position = new Vector2(ladder.CenterX, ladder.TopY);
                body.velocity = Vector2.zero;
                return;
            }
            if (nextY < ladder.BottomY && input.MoveY < -0.15f)
            {
                ExitTraversal(Vector2.down * 0.2f);
                return;
            }

            body.position = new Vector2(ladder.CenterX, Mathf.Clamp(nextY, ladder.BottomY, ladder.TopY));
            body.velocity = Vector2.zero;
        }

        float GetLadderHorizontalInput()
        {
            if (!input.InputEnabled)
                return 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
                return -1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
                return 1f;
            return input.MoveX;
        }

        IEnumerator FinishLadder(float direction)
        {
            SetLadderAnimation(PlayerActionState.LadderClimbFinish);
            yield return new WaitForSeconds(ladderFinishTime);
            if (ladder)
                body.position = ladder.GetTopExitPosition(direction);
            climbRoutine = null;
            ExitTraversal(Vector2.zero);
        }

        IEnumerator JumpOffLadder()
        {
            SetLadderAnimation(PlayerActionState.LadderJumpPrepare);
            yield return new WaitForSeconds(0.12f);
            var direction = motor.FacingRight ? 1f : -1f;
            climbRoutine = null;
            ExitTraversal(new Vector2(ladderJumpVelocity.x * direction, ladderJumpVelocity.y));
        }

        void SetLadderAnimation(PlayerActionState state)
        {
            if (ladderAnimationState == state)
                return;
            if (ladderAnimationState != PlayerActionState.None)
                animationDriver.ClearForcedState(ladderAnimationState);
            ladderAnimationState = state;
            animationDriver.ForceState(state);
            animationDriver.PlayStateImmediately(state);
        }

        bool TryCatchClimbableWall()
        {
            if (!input.InputEnabled || motor.IsGrounded || stateMachine.IsInActionState ||
                motor.TraversalLocked)
                return false;

            RaycastHit2D hit;
            var preferredDirection = Mathf.Abs(input.MoveX) >= 0.15f
                ? Mathf.Sign(input.MoveX)
                : (motor.FacingRight ? 1f : -1f);
            var direction = preferredDirection;
            if (!TryGetClimbableWall(direction, out hit))
            {
                direction = -preferredDirection;
                if (!TryGetClimbableWall(direction, out hit))
                    return false;
            }

            if (HasGroundImmediatelyBelow())
                return false;

            wallDirection = direction;
            lockedRootPosition = body.position;
            mode = TraversalMode.WallCling;
            motor.CancelForcedVelocity();
            motor.TraversalLocked = true;
            motor.FaceDirection(wallDirection);
            body.velocity = Vector2.zero;
            animationDriver.ForceState(PlayerActionState.LedgeHang);
            animationDriver.PlayStateImmediately(PlayerActionState.LedgeHang);
            return true;
        }

        void UpdateWallCling()
        {
            RaycastHit2D hit;
            if (motor.IsGrounded || !TryGetClimbableWall(wallDirection, out hit) || HasGroundImmediatelyBelow())
            {
                ExitTraversal(Vector2.zero);
                return;
            }

            if (input.JumpPressed)
            {
                motor.FaceDirection(-wallDirection);
                ExitTraversal(new Vector2(-wallDirection * wallJumpVelocity.x, wallJumpVelocity.y));
                return;
            }

            if (input.MoveY < -0.5f)
                ExitTraversal(Vector2.down * wallSlideSpeed);
        }

        void SlideOnWall()
        {
            if (!body)
                return;

            lockedRootPosition.y -= wallSlideSpeed * Time.fixedDeltaTime;
            body.position = lockedRootPosition;
            body.velocity = Vector2.zero;
        }

        bool TryGetClimbableWall(float direction, out RaycastHit2D hit)
        {
            var bounds = bodyCollider.bounds;
            var origin = new Vector2(bounds.center.x, bounds.center.y);
            hit = Physics2D.Raycast(
                origin,
                Vector2.right * direction,
                bounds.extents.x + climbableWallCheckDistance,
                ledgeMask);
            if (!IsUsableHit(hit) || Mathf.Abs(hit.normal.x) < 0.8f)
                return false;

            return hit.collider.GetComponentInParent<ClimbableWall>() != null;
        }

        bool HasGroundImmediatelyBelow()
        {
            var bounds = bodyCollider.bounds;
            var inset = bounds.extents.x * 0.65f;
            var probeY = bounds.min.y + wallGroundClearance;
            for (var i = -1; i <= 1; i++)
            {
                var origin = new Vector2(bounds.center.x + inset * i, probeY);
                var hit = Physics2D.Raycast(origin, Vector2.down, wallGroundClearance, ledgeMask);
                if (IsUsableHit(hit) && hit.normal.y >= minTopNormalY)
                    return true;
            }
            return false;
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

            lockedRootPosition = new Vector2(
                wallHit.point.x - direction * hangHorizontalOffset,
                topHit.point.y - hangRootBelowLedge);
            climbStandPosition = standPosition;
            ledgeWallDirection = direction;
            canClimbLedge = HasStandingClearance(standPosition, bounds);
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
            var exitedWallCling = mode == TraversalMode.WallCling;
            if (climbRoutine != null)
            {
                StopCoroutine(climbRoutine);
                climbRoutine = null;
            }

            animationDriver.ClearForcedState(PlayerActionState.LedgeHang);
            animationDriver.ClearForcedState(PlayerActionState.LedgeClimb);
            animationDriver.ClearForcedState(PlayerActionState.MonkeyBarIdle);
            if (ladderAnimationState != PlayerActionState.None)
                animationDriver.ClearForcedState(ladderAnimationState);
            ladderAnimationState = PlayerActionState.None;
            SetTopSupportIgnored(false);
            ladderAtTop = false;
            mode = TraversalMode.None;
            monkeyBarAnchor = null;
            ladder = null;
            motor.TraversalLocked = false;
            canCatchAgainTime = Time.time + recatchDelay;
            if (exitedWallCling)
                canCatchWallAgainTime = Time.time + wallJumpRecatchDelay;

            if (releaseVelocity.sqrMagnitude > 0.0001f)
                motor.ApplyForcedVelocity(releaseVelocity, 0.08f);
        }

        void SetTopSupportIgnored(bool ignored)
        {
            if (ladder && ladder.TopSupportCollider && bodyCollider)
                Physics2D.IgnoreCollision(bodyCollider, ladder.TopSupportCollider, ignored);
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
