using MirrorTrial.Combat;
using MirrorTrial.Feedback;
using MirrorTrial.Player;
using UnityEngine;
using UnityEngine.Events;

namespace MirrorTrial.Level
{
    public sealed class PulleyLiftController : MonoBehaviour
    {
        public enum PulleyState
        {
            Raised,
            Moving,
            Lowered
        }

        [Header("Movement")]
        [SerializeField] Animator animator;
        [SerializeField] bool useAnimator = true;
        [SerializeField] Rigidbody2D platformBody;
        [SerializeField] Transform platformRaisedPoint;
        [SerializeField] Transform platformLoweredPoint;
        [SerializeField] Transform counterweight;
        [SerializeField] Transform weightRaisedPoint;
        [SerializeField] Transform weightLoweredPoint;
        [SerializeField, Min(0.05f)] float moveDuration = 1.25f;
        [SerializeField] AnimationCurve movementCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(1f, 1f, 0f, 0f));

        [Header("Counterweight Impact")]
        [SerializeField, Min(0.05f)] float weightFallDuration = 0.55f;
        [SerializeField] AnimationCurve weightFallCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(1f, 1f, 2f, 0f));
        [SerializeField, Range(0f, 1f)] float landingShakePower = 0.9f;
        [SerializeField, Min(0f)] float landingBounceHeight = 0.1f;
        [SerializeField, Min(0.01f)] float landingBounceDuration = 0.16f;

        [Header("Break Trigger")]
        [SerializeField, Min(1)] int requiredHits = 1;
        [SerializeField] bool requireHeavyAttack;
        [SerializeField] Collider2D ropeHitCollider;
        [SerializeField] GameObject intactRopeVisual;
        [SerializeField] GameObject brokenRopeVisual;
        [SerializeField] GameObject brokenLowerRopeVisual;

        [Header("Feedback")]
        [SerializeField] ParticleSystem breakParticles;
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip ropeBreakSound;
        [SerializeField] AudioClip landedSound;
        [SerializeField, Range(0f, 1f)] float soundVolume = 1f;
        [SerializeField] UnityEvent onTriggered;
        [SerializeField] UnityEvent onLowered;

        [Header("Runtime")]
        [SerializeField] PulleyState state = PulleyState.Raised;
        [SerializeField, Range(0f, 1f)] float progress;

        int currentHits;
        float weightProgress;
        float landingBounceRemaining;
        bool weightHasLanded;

        static readonly int TriggerDropHash = Animator.StringToHash("TriggerDrop");
        static readonly int RaisedStateHash = Animator.StringToHash("Raised");
        static readonly int LoweredStateHash = Animator.StringToHash("Lowered");

        public PulleyState State => state;
        public float Progress => progress;
        public bool IsLowered => state == PulleyState.Lowered;

        void OnEnable()
        {
            PlayerDamageReceiver.PlayerRespawned += ResetRaised;
        }

        void OnDisable()
        {
            PlayerDamageReceiver.PlayerRespawned -= ResetRaised;
        }

        void Awake()
        {
            if (platformBody)
            {
                platformBody.bodyType = RigidbodyType2D.Kinematic;
                platformBody.gravityScale = 0f;
                platformBody.freezeRotation = true;
                platformBody.interpolation = RigidbodyInterpolation2D.Interpolate;
            }

            if (!animator)
                animator = GetComponent<Animator>();

            progress = state == PulleyState.Lowered ? 1f : 0f;
            weightProgress = progress;
            weightHasLanded = state == PulleyState.Lowered;
            if (useAnimator && animator)
                animator.Play(state == PulleyState.Lowered ? LoweredStateHash : RaisedStateHash, 0, 0f);
            else
                ApplyPose(progress, weightProgress, true);
            ApplyRopeState(state != PulleyState.Raised);
        }

        void FixedUpdate()
        {
            if (state != PulleyState.Moving)
                return;
            if (useAnimator && animator)
                return;

            landingBounceRemaining = Mathf.Max(0f, landingBounceRemaining - Time.fixedDeltaTime);
            progress = Mathf.MoveTowards(progress, 1f, Time.fixedDeltaTime / moveDuration);
            weightProgress = Mathf.MoveTowards(weightProgress, 1f, Time.fixedDeltaTime / weightFallDuration);
            ApplyPose(progress, weightProgress, false);
            if (!weightHasLanded && weightProgress >= 1f)
                HandleWeightLanding();
            if (progress < 1f)
                return;

            state = PulleyState.Lowered;
            ApplyPose(1f, 1f, true);
            onLowered?.Invoke();
        }

        public void OnDamagePayloadReceived(DamagePayload payload)
        {
            if (state != PulleyState.Raised || payload.damage <= 0)
                return;
            if (requireHeavyAttack && payload.attackType != SkillAttackType.Heavy)
                return;

            currentHits++;
            if (currentHits >= requiredHits)
                Trigger();
        }

        [ContextMenu("Trigger Lift")]
        public void Trigger()
        {
            if (state != PulleyState.Raised)
                return;

            state = PulleyState.Moving;
            currentHits = requiredHits;
            weightProgress = 0f;
            weightHasLanded = false;
            landingBounceRemaining = 0f;
            if (useAnimator && animator)
                animator.SetTrigger(TriggerDropHash);
            ApplyRopeState(true);
            if (breakParticles)
                breakParticles.Play();
            PlayOneShot(ropeBreakSound);
            onTriggered?.Invoke();
        }

        [ContextMenu("Reset Raised")]
        public void ResetRaised()
        {
            state = PulleyState.Raised;
            progress = 0f;
            weightProgress = 0f;
            weightHasLanded = false;
            landingBounceRemaining = 0f;
            currentHits = 0;
            if (useAnimator && animator)
            {
                animator.ResetTrigger(TriggerDropHash);
                animator.Play(RaisedStateHash, 0, 0f);
                animator.Update(0f);
            }
            else
                ApplyPose(0f, 0f, true);
            ApplyRopeState(false);
        }

        [ContextMenu("Set Lowered")]
        public void SetLowered()
        {
            state = PulleyState.Lowered;
            progress = 1f;
            weightProgress = 1f;
            weightHasLanded = true;
            landingBounceRemaining = 0f;
            currentHits = requiredHits;
            if (useAnimator && animator)
                animator.Play(LoweredStateHash, 0, 0f);
            else
                ApplyPose(1f, 1f, true);
            ApplyRopeState(true);
        }

        // Called by the generated Drop animation at the exact contact frame.
        public void OnAnimatedWeightImpact()
        {
            if (!useAnimator || state != PulleyState.Moving || weightHasLanded)
                return;
            HandleWeightLanding();
        }

        // Called by the generated Drop animation after platform motion finishes.
        public void OnAnimatedDropFinished()
        {
            if (!useAnimator || state != PulleyState.Moving)
                return;
            state = PulleyState.Lowered;
            progress = 1f;
            weightProgress = 1f;
            onLowered?.Invoke();
        }

        void ApplyPose(float platformProgress, float counterweightProgress, bool teleport)
        {
            var eased = movementCurve == null
                ? platformProgress
                : movementCurve.Evaluate(platformProgress);

            if (platformBody && platformRaisedPoint && platformLoweredPoint)
            {
                var position = Vector2.Lerp(platformRaisedPoint.position, platformLoweredPoint.position, eased);
                if (teleport)
                    platformBody.position = position;
                else
                    platformBody.MovePosition(position);
            }

            if (counterweight && weightRaisedPoint && weightLoweredPoint)
            {
                var weightEased = weightFallCurve == null
                    ? counterweightProgress * counterweightProgress
                    : weightFallCurve.Evaluate(counterweightProgress);
                counterweight.position = Vector3.LerpUnclamped(
                    weightRaisedPoint.position, weightLoweredPoint.position, weightEased);
                if (weightHasLanded && landingBounceRemaining > 0f)
                {
                    var bounceProgress = 1f - landingBounceRemaining / landingBounceDuration;
                    counterweight.position += Vector3.up *
                        (Mathf.Sin(bounceProgress * Mathf.PI) * landingBounceHeight);
                }
            }
        }

        void HandleWeightLanding()
        {
            weightHasLanded = true;
            landingBounceRemaining = landingBounceDuration;
            CameraShakeService.Shake(Vector2.down, landingShakePower);
            PlayOneShot(landedSound);
        }

        void ApplyRopeState(bool broken)
        {
            if (intactRopeVisual)
                intactRopeVisual.SetActive(!broken);
            if (brokenRopeVisual)
                brokenRopeVisual.SetActive(broken);
            if (brokenLowerRopeVisual)
                brokenLowerRopeVisual.SetActive(broken);
            if (ropeHitCollider)
                ropeHitCollider.enabled = !broken;
        }

        void PlayOneShot(AudioClip clip)
        {
            if (!clip)
                return;
            if (audioSource)
                audioSource.PlayOneShot(clip, soundVolume);
            else
                AudioSource.PlayClipAtPoint(clip, transform.position, soundVolume);
        }

        void OnValidate()
        {
            requiredHits = Mathf.Max(1, requiredHits);
            moveDuration = Mathf.Max(0.05f, moveDuration);
            weightFallDuration = Mathf.Max(0.05f, weightFallDuration);
            landingBounceDuration = Mathf.Max(0.01f, landingBounceDuration);
        }

        void OnDrawGizmosSelected()
        {
            DrawPath(platformRaisedPoint, platformLoweredPoint, new Color(0.2f, 0.9f, 1f, 0.9f));
            DrawPath(weightRaisedPoint, weightLoweredPoint, new Color(1f, 0.65f, 0.2f, 0.9f));
        }

        static void DrawPath(Transform from, Transform to, Color color)
        {
            if (!from || !to)
                return;
            Gizmos.color = color;
            Gizmos.DrawLine(from.position, to.position);
            Gizmos.DrawWireSphere(from.position, 0.12f);
            Gizmos.DrawWireSphere(to.position, 0.12f);
        }
    }
}
