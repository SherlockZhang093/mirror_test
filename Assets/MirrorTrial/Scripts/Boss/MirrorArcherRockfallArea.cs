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
        [SerializeField, Min(1)] int impactsPerWave = 5;
        [SerializeField, Min(0f)] float waveInterval = 0.45f;
        [SerializeField, Min(0f)] float perImpactStagger = 0.18f;
        [SerializeField, Min(0.1f)] float impactRadius = 0.8f;
        [SerializeField, Min(0.1f)] float impactSpacingMultiplier = 2.25f;
        [SerializeField, Min(0.1f)] float minimumImpactSpacing = 1.2f;

        [Header("Damage")]
        [SerializeField, Min(0)] int damage = 1;
        [SerializeField] Vector2 knockback = new Vector2(1.5f, 0.3f);
        [SerializeField, Min(0f)] float lockMoment = 0.25f;
        [SerializeField, Min(0f)] float releaseMoment = 0.8f;
        [SerializeField, Min(0.05f)] float minimumFallDuration = 0.15f;

        [Header("Recovery")]
        [SerializeField, Min(0f)] float recovery = 1.15f;

        [Header("Boss platform ward")]
        [SerializeField, Min(0.1f)] float platformLiftHeight = 2.2f;
        [SerializeField, Min(1f)] float platformFramesPerSecond = 12f;
        [SerializeField, Min(1f)] float platformPixelsPerUnit = 100f;
        [SerializeField] float platformSurfaceOffset = 0.2f;

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
        public float FallDuration => Mathf.Max(minimumFallDuration, Mathf.Max(lockMoment, releaseMoment) - lockMoment);
        public float Recovery => recovery;
        public float PlatformLiftHeight => platformLiftHeight;
        public float PlatformFramesPerSecond => platformFramesPerSecond;
        public float PlatformPixelsPerUnit => platformPixelsPerUnit;
        public float PlatformSurfaceOffset => platformSurfaceOffset;
        public float PlatformAnimationDuration => 24f / Mathf.Max(1f, platformFramesPerSecond);
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
            releaseMoment = Mathf.Max(lockMoment, releaseMoment);
            if (!box) box = GetComponent<BoxCollider2D>();
            if (box) box.isTrigger = true;
        }

        public Vector2 GetStartPoint(float worldX)
        {
            var bounds = WorldBounds;
            return new Vector2(Mathf.Clamp(worldX, bounds.min.x, bounds.max.x), bounds.max.y);
        }

        public Vector2 GetLandingPoint(float worldX)
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
