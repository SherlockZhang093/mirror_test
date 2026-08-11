using UnityEngine;

namespace MirrorTrial.Boss
{
    /// <summary>
    /// Visual-only flight presentation. The combat actor still owns movement and
    /// facing; this component only pitches the mounted artwork along its travel
    /// direction so the bird and rider do not remain horizontally level in dives.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MirrorArcherMountedVisualOrientation : MonoBehaviour
    {
        [SerializeField, Min(0f)] float movementThreshold = 0.01f;
        [SerializeField, Range(0f, 85f)] float maximumDivePitch = 58f;
        [SerializeField, Range(0f, 85f)] float maximumRisePitch = 32f;
        [SerializeField, Min(1f)] float turnSpeed = 240f;
        [SerializeField, Range(-85f, 0f)] float fallStartPitch = -18f;
        [SerializeField, Range(-85f, 0f)] float fallLoopPitch = -64f;

        MirrorArcherMountedBoss actor;
        Animator animator;
        Vector3 previousPosition;
        float pitch;
        bool hasPreviousPosition;

        void Awake()
        {
            actor = GetComponentInParent<MirrorArcherMountedBoss>();
            animator = GetComponent<Animator>();
        }

        void OnEnable()
        {
            if (!actor) actor = GetComponentInParent<MirrorArcherMountedBoss>();
            if (!animator) animator = GetComponent<Animator>();
            previousPosition = actor ? actor.transform.position : transform.position;
            hasPreviousPosition = true;
            pitch = 0f;
            transform.localRotation = Quaternion.identity;
        }

        void LateUpdate()
        {
            if (!actor)
            {
                return;
            }

            var position = actor.transform.position;
            var travel = hasPreviousPosition ? position - previousPosition : Vector3.zero;
            previousPosition = position;
            hasPreviousPosition = true;

            float targetPitch = 0f;
            var animationState = animator ? animator.GetCurrentAnimatorStateInfo(0) : default;
            if (animator && animationState.IsName("MountedFall"))
                targetPitch = fallStartPitch;
            else if (animator && animationState.IsName("MountedFallLoop"))
                targetPitch = fallLoopPitch;
            else if (!(animator && animationState.IsName("MountedImpact")) &&
                actor.CurrentState == MirrorArcherMountedBoss.MountedState.Acting &&
                travel.sqrMagnitude >= movementThreshold * movementThreshold)
            {
                targetPitch = Mathf.Atan2(travel.y, Mathf.Abs(travel.x)) * Mathf.Rad2Deg;
                targetPitch = Mathf.Clamp(targetPitch, -maximumDivePitch, maximumRisePitch);
            }

            pitch = Mathf.MoveTowardsAngle(pitch, targetPitch, turnSpeed * Time.deltaTime);
            // VisualRoot performs the left/right mirror. Keeping the same local
            // pitch on this child produces the correct world-space angle on both sides.
            transform.localRotation = Quaternion.Euler(0f, 0f, pitch);

        }
    }
}
