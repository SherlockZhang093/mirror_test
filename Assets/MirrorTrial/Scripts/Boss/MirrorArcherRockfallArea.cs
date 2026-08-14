using UnityEngine;

namespace MirrorTrial.Boss
{
    /// <summary>
    /// Scene-authored rectangle for the ground-phase rockfall. The top edge is the rock spawn
    /// line, the bottom edge is the impact line, and the horizontal bounds define valid Xs.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class MirrorArcherRockfallArea : MonoBehaviour
    {
        [Header("Schedule")]
        [SerializeField, Min(0f)] float firstDelay = 3f;
        [SerializeField, Min(0.5f)] float cooldown = 7f;

        [Header("Impact pattern")]
        [SerializeField, Min(1)] int waveCount = 3;
        [SerializeField, Min(1)] int impactsPerWave = 3;
        [SerializeField, Min(0f)] float waveInterval = 0.45f;
        [SerializeField, Min(0f)] float perImpactStagger = 0.18f;
        [SerializeField, Min(0.1f)] float impactRadius = 0.8f;
        [SerializeField, Min(0.1f)] float impactSpacingMultiplier = 2.25f;
        [SerializeField, Min(0.1f)] float minimumImpactSpacing = 1.2f;

        [Header("Placement")]
        [Tooltip("Random horizontal offset applied only to the rock's starting point.")]
        [SerializeField, Min(0f)] float startHorizontalRandomness = 1.5f;

        [Header("Damage")]
        [SerializeField, Min(0)] int damage = 1;
        [SerializeField] Vector2 knockback = new Vector2(1.5f, 0.3f);
        [Tooltip("Time taken by a rock to travel from its randomized start point to the landing point.")]
        [SerializeField, Min(0.05f)] float fallDuration = 1.25f;
        [Tooltip("Time between showing the landing warning and the rock actually beginning to fall.")]
        [SerializeField, Min(0f)] float preFallWarningDuration = 0.6f;

        [Header("Recovery")]
        [SerializeField, Min(0f)] float recovery = 1.15f;

        [Header("Rock audio")]
        [SerializeField, Range(0f, 1f)] float fallingRumbleVolume = 0.72f;
        [SerializeField, Range(0.25f, 2f)] float fallingRumblePitch = 0.65f;
        [SerializeField, Range(200f, 5000f)] float fallingRumbleLowPass = 900f;
        [SerializeField, Range(0f, 1f)] float impactVolume = 1f;
        [SerializeField, Range(0.25f, 2f)] float impactPitch = 0.9f;

        [Header("Boss platform ward")]
        [SerializeField, Min(0.1f)] float platformLiftHeight = 2.2f;
        [SerializeField, Min(1f)] float platformFramesPerSecond = 12f;
        [SerializeField, Min(1f)] float platformPixelsPerUnit = 100f;
        [Tooltip("Local Y of the central walkable surface measured from the sprite frame center, in world units.")]
        [SerializeField] float platformSurfaceAnchor = -0.3f;
        [Tooltip("Live adjustment for the platform top relative to the boss's feet. 0 makes them touch; positive values move the platform upward.")]
        [SerializeField] float platformAlignmentOffset;

        [Header("Platform descent")]
        [Tooltip("Warning time before a player on the platform is pushed away.")]
        [SerializeField, Min(0f)] float descentWarningDuration = 0.58f;
        [Tooltip("Horizontal speed used to push the player toward the nearest platform edge.")]
        [SerializeField, Min(0.1f)] float playerPushSpeed = 7.5f;
        [SerializeField, Min(0.02f)] float playerPushDuration = 0.16f;
        [Tooltip("Extra horizontal clearance required before the blocker is enabled.")]
        [SerializeField, Min(0f)] float playerExitPadding = 0.15f;

        [Header("Scene preview")]
        [SerializeField, Min(0f)] float safeRadius = 2.4f;

        BoxCollider2D box;

        public float FirstDelay => firstDelay;
        public float Cooldown => cooldown;
        public int WaveCount => waveCount;
        public int ImpactsPerWave => impactsPerWave;
        public float WaveInterval => waveInterval;
        public float PerImpactStagger => perImpactStagger;
        public float ImpactRadius => impactRadius;
        public float ImpactSpacing => Mathf.Max(impactRadius * impactSpacingMultiplier, minimumImpactSpacing);
        public int Damage => damage;
        public Vector2 Knockback => knockback;
        public float FallDuration => fallDuration;
        public float PreFallWarningDuration => preFallWarningDuration;
        public float Recovery => recovery;
        public float FallingRumbleVolume => fallingRumbleVolume;
        public float FallingRumblePitch => fallingRumblePitch;
        public float FallingRumbleLowPass => fallingRumbleLowPass;
        public float ImpactVolume => impactVolume;
        public float ImpactPitch => impactPitch;
        public float PlatformLiftHeight => platformLiftHeight;
        public float PlatformFramesPerSecond => platformFramesPerSecond;
        public float PlatformPixelsPerUnit => platformPixelsPerUnit;
        public float PlatformSurfaceAnchor => platformSurfaceAnchor;
        public float PlatformAlignmentOffset => platformAlignmentOffset;
        public float PlatformAnimationDuration => 24f / Mathf.Max(1f, platformFramesPerSecond);
        public float DescentWarningDuration => Mathf.Max(0f, descentWarningDuration);
        public float PlayerPushSpeed => Mathf.Max(0.1f, playerPushSpeed);
        public float PlayerPushDuration => Mathf.Max(0.02f, playerPushDuration);
        public float PlayerExitPadding => Mathf.Max(0f, playerExitPadding);
        public float SafeRadius => safeRadius;

        public Bounds WorldBounds
        {
            get
            {
                if (!box) box = GetComponent<BoxCollider2D>();
                return box.bounds;
            }
        }

        void Awake()
        {
            box = GetComponent<BoxCollider2D>();
            box.isTrigger = true;
        }

        void OnValidate()
        {
            if (!box) box = GetComponent<BoxCollider2D>();
            if (box) box.isTrigger = true;
        }

        public Vector2 GetStartPoint(float worldX)
        {
            var bounds = WorldBounds;
            return new Vector2(Mathf.Clamp(worldX, bounds.min.x, bounds.max.x), bounds.max.y);
        }

        public Vector2 GetRandomStartPoint(float landingWorldX)
        {
            var bounds = WorldBounds;
            var randomX = landingWorldX + Random.Range(-startHorizontalRandomness, startHorizontalRandomness);
            return new Vector2(Mathf.Clamp(randomX, bounds.min.x, bounds.max.x), bounds.max.y);
        }

        public Vector2 GetLandingPoint(float worldX)
        {
            var bounds = WorldBounds;
            return new Vector2(Mathf.Clamp(worldX, bounds.min.x, bounds.max.x), bounds.min.y);
        }

        public Vector2 GetBottomPoint(float worldX)
        {
            var bounds = WorldBounds;
            return new Vector2(Mathf.Clamp(worldX, bounds.min.x, bounds.max.x), bounds.min.y);
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            var bounds = WorldBounds;
            Gizmos.color = new Color(0.95f, 0.22f, 0.12f, 0.12f);
            Gizmos.DrawCube(bounds.center, bounds.size);
            Gizmos.color = new Color(1f, 0.75f, 0.12f, 0.95f);
            Gizmos.DrawLine(new Vector3(bounds.min.x, bounds.max.y, 0f),
                new Vector3(bounds.max.x, bounds.max.y, 0f));
            Gizmos.color = new Color(1f, 0.18f, 0.12f, 0.95f);
            Gizmos.DrawLine(new Vector3(bounds.min.x, bounds.min.y, 0f),
                new Vector3(bounds.max.x, bounds.min.y, 0f));
            Gizmos.DrawWireCube(bounds.center, bounds.size);
            Gizmos.color = new Color(0.2f, 1f, 0.45f, 0.9f);
            Gizmos.DrawWireSphere(GetLandingPoint(transform.position.x), safeRadius);
        }
#endif
    }
}
