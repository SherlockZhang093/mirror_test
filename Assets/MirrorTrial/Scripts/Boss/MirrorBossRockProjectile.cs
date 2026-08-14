using System.Collections;
using MirrorTrial.Combat;
using MirrorTrial.Feedback;
using UnityEngine;

namespace MirrorTrial.Boss
{
    /// <summary>
    /// Ground-phase rockfall impact. Refactored to be sprite-driven with clearly separated
    /// concerns:
    ///   1. Telegraph  -> a<see cref="MirrorRockfallTelegraph"/> child (copied archer ripple)
    ///                ramps up over the windup and defines WHEN the hit happens.
    ///   2. Rock visual -> a falling <see cref="rockSprite"/> that descends from above onto the
    ///                     locked ground point over the same windup.
    ///   3. Hit judgment -> fires exactly when the telegraph completes, then a short shatter
    ///                      sprite animation plays before the object self-destroys.
    /// The old LineRenderer wireframe ring/box has been removed entirely.
    /// </summary>
    public sealed class MirrorBossRockProjectile : MonoBehaviour
    {
        [Header("Timing")]
        [SerializeField, Min(0.05f)] float defaultTelegraphDuration = 0.65f;
        [SerializeField, Min(0.1f)] float fallHeight = 6f;
        [Tooltip("Extra time the shatter sprites stay on screen after the hit lands.")]
        [SerializeField, Min(0.05f)] float shatterDuration = 0.34f;

        [Header("Sprites (assigned on the prefab)")]
        [SerializeField] Sprite rockSprite;
        [SerializeField] Sprite[] shatterFrames;
        [Tooltip("Uniform scale applied to the falling rock visual. 1 = native sprite size (2 world units at 32 PPU).")]
        [SerializeField, Min(0.05f)] float rockScale = 0.5f;
        [Tooltip("Uniform scale applied to the shatter effect.")]
        [SerializeField, Min(0.05f)] float shatterScale = 0.6f;
        [SerializeField, Min(1)] int rockSortingOrder = 90;
        [SerializeField, Min(1)] int shatterSortingOrder = 91;
        [Tooltip("Extra spin applied to the falling rock, degrees per second.")]
        [SerializeField] float fallSpin = 220f;

        [Header("Audio")]
        [Tooltip("轰隆隆下落声，在石头开始下落时播放。")]
        [SerializeField] AudioClip fallingRumbleSound;
        [SerializeField, Range(0f, 1f)] float fallingRumbleVolume = 0.62f;
        [Tooltip("石头砸地声，在命中判定的同一帧播放。")]
        [SerializeField] AudioClip impactSound;
        [SerializeField, Range(0f, 1f)] float impactVolume = 0.9f;

        [Header("Camera shake")]
        [Tooltip("石头从空中开始下落直到砸地期间的持续震动强度。0 表示关闭。")]
        [SerializeField, Range(0f, 1f)] float fallingShakePower = 0.08f;
        [Tooltip("持续震动脉冲的间隔（秒）。数值越小，震动越连续。")]
        [SerializeField, Min(0.03f)] float fallingShakeInterval = 0.12f;
        [Tooltip("石头砸地时的镜头震动强度。0 表示关闭。")]
        [SerializeField, Range(0f, 1f)] float impactShakePower = 0.16f;

        GameObject source;
        GameObject target;
        int damage;
        float radius;
        Vector2 knockback;

        SpriteRenderer rockRenderer;
        MirrorRockfallTelegraph telegraph;
        Hurtbox targetHurtbox;
        Collider2D targetCollider;
        AudioSource fallingRumbleSource;
        AudioLowPassFilter fallingRumbleFilter;
        float runtimeRumbleVolume;
        float runtimeRumblePitch;
        float runtimeRumbleLowPass;
        float runtimeImpactVolume;
        float runtimeImpactPitch;

        /// <summary>
        /// Spawns a single rockfall impact at <paramref name="groundPoint"/>. The caller is
        /// responsible for having already resolved the ground point (e.g. via a downward
        /// raycast), so every impact in a wave shares the same ground plane.
        /// </summary>
        public static MirrorBossRockProjectile Spawn(GameObject prefab, Vector2 groundPoint, GameObject source,
            GameObject target, int damage, float radius, Vector2 knockback, GameObject impactPrefabIgnored = null,
            float telegraphDuration = -1f, Vector2? authoredStartPoint = null,
            float preFallWarningDuration = 0f, float rumbleVolume = 0.72f,
            float rumblePitch = 0.65f, float rumbleLowPass = 900f,
            float hitVolume = 1f, float hitPitch = 0.9f)
        {
            GameObject instance;
            if (prefab)
                instance = Instantiate(prefab, groundPoint, Quaternion.identity);
            else
            {
                instance = new GameObject("MirrorBossRockImpact");
                instance.transform.position = groundPoint;
                instance.AddComponent<MirrorBossRockProjectile>();
            }
            instance.transform.position = groundPoint;

            var projectile = instance.GetComponent<MirrorBossRockProjectile>();
            if (!projectile)
            {
                Debug.LogError("[MirrorBossRockProjectile] Impact prefab lacks MirrorBossRockProjectile.", prefab);
                Destroy(instance);
                return null;
            }
            projectile.Configure(source, target, damage, radius, knockback, telegraphDuration,
                authoredStartPoint, preFallWarningDuration, rumbleVolume, rumblePitch,
                rumbleLowPass, hitVolume, hitPitch);
            return projectile;
        }

        void Configure(GameObject nextSource, GameObject nextTarget, int nextDamage, float nextRadius,
            Vector2 nextKnockback, float telegraphDuration, Vector2? authoredStartPoint,
            float preFallWarningDuration, float rumbleVolume, float rumblePitch,
            float rumbleLowPass, float hitVolume, float hitPitch)
        {
            source = nextSource;
            target = nextTarget;
            targetHurtbox = target ? target.GetComponentInChildren<Hurtbox>() : null;
            targetCollider = targetHurtbox
                ? targetHurtbox.GetComponent<Collider2D>() ?? targetHurtbox.GetComponentInParent<Collider2D>()
                : null;
            damage = Mathf.Max(0, nextDamage);
            radius = Mathf.Max(0.1f, nextRadius);
            knockback = nextKnockback;
            runtimeRumbleVolume = Mathf.Clamp01(rumbleVolume);
            runtimeRumblePitch = Mathf.Clamp(rumblePitch, 0.25f, 2f);
            runtimeRumbleLowPass = Mathf.Clamp(rumbleLowPass, 200f, 5000f);
            runtimeImpactVolume = Mathf.Clamp01(hitVolume);
            runtimeImpactPitch = Mathf.Clamp(hitPitch, 0.25f, 2f);
            StartCoroutine(Run(telegraphDuration > 0f ? telegraphDuration : defaultTelegraphDuration,
                authoredStartPoint, Mathf.Max(0f, preFallWarningDuration)));
        }

        IEnumerator Run(float duration, Vector2? authoredStartPoint, float preFallWarningDuration)
        {
            var groundPoint = (Vector2)transform.position;

            // --- concern 1: telegraph owns the danger-zone read and the hit timing ---
            telegraph = MirrorRockfallTelegraph.Create(groundPoint, radius);

            // --- concern 2: falling rock visual + falling rumble, purely presentation ---
            rockRenderer = BuildRockRenderer();
            var startPosition = authoredStartPoint ?? groundPoint + Vector2.up * fallHeight;
            if (rockRenderer) rockRenderer.transform.position = startPosition;

            var totalDuration = preFallWarningDuration + duration;
            var warningElapsed = 0f;
            while (warningElapsed < preFallWarningDuration)
            {
                warningElapsed += Time.deltaTime;
                if (telegraph) telegraph.SetProgress(Mathf.Clamp01(warningElapsed / totalDuration));
                yield return null;
            }

            StartFallingRumble();

            var elapsed = 0f;
            var nextFallingShakeAt = 0f;
            var hitTargetDuringFall = false;
            var impactPoint = groundPoint;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var progress = Mathf.Clamp01(elapsed / duration);
                if (telegraph)
                    telegraph.SetProgress(Mathf.Clamp01((preFallWarningDuration + elapsed) / totalDuration));

                if (fallingShakePower > 0f && elapsed >= nextFallingShakeAt)
                {
                    CameraShakeService.Shake(Vector2.down, fallingShakePower);
                    nextFallingShakeAt = elapsed + Mathf.Max(0.03f, fallingShakeInterval);
                }

                if (rockRenderer)
                {
                    // Ease-in so the rock accelerates as it falls.
                    var fallT = progress * progress;
                    rockRenderer.transform.position = Vector2.Lerp(startPosition, groundPoint, fallT);
                    rockRenderer.transform.Rotate(0f, 0f, fallSpin * Time.deltaTime);
                }

                // A player standing on the raised platform meets the rock before it reaches
                // the authored ground point. Resolve that contact immediately so the damage
                // and the complete impact presentation happen at the visible collision.
                if (TryApplyElevatedTargetImpact(groundPoint, out impactPoint))
                {
                    hitTargetDuringFall = true;
                    break;
                }

                yield return null;
            }

            // --- concern 3: a target contact or ground landing owns the full impact feedback ---
            if (!hitTargetDuringFall && TryApplyGroundColumnImpact(groundPoint, out var targetImpactPoint))
                impactPoint = targetImpactPoint;

            StopFallingRumble();
            PlaySound2D(impactSound, impactPoint, runtimeImpactVolume, runtimeImpactPitch);
            if (impactShakePower > 0f)
                CameraShakeService.Shake(Vector2.down, impactShakePower);

            if (telegraph) telegraph.Cancel();
            if (rockRenderer) rockRenderer.enabled = false;

            yield return PlayShatter(impactPoint);
            Destroy(gameObject);
        }

        bool TryApplyElevatedTargetImpact(Vector2 groundPoint, out Vector2 impactPoint)
        {
            impactPoint = groundPoint;
            if (!targetHurtbox || !targetCollider || !rockRenderer) return false;
            if (Mathf.Abs(targetHurtbox.transform.position.x - groundPoint.x) > radius) return false;

            var targetBounds = targetCollider.bounds;
            if (targetBounds.min.y <= groundPoint.y + 0.1f) return false;

            var rockBounds = rockRenderer.bounds;
            if (rockBounds.min.y > targetBounds.max.y || rockBounds.max.y < targetBounds.min.y)
                return false;

            impactPoint = new Vector2(targetHurtbox.transform.position.x, targetBounds.max.y);
            ApplyImpactToTarget(groundPoint);
            return true;
        }

        bool TryApplyGroundColumnImpact(Vector2 groundPoint, out Vector2 impactPoint)
        {
            impactPoint = groundPoint;
            if (!targetHurtbox) return false;

            // The telegraph marks the footprint of a rock falling from above. Treat the hit
            // area as a vertical column so standing on the raised platform (or jumping) does
            // not turn the same horizontal landing spot into a safe zone.
            if (Mathf.Abs(targetHurtbox.transform.position.x - groundPoint.x) > radius) return false;

            impactPoint = targetCollider
                ? new Vector2(targetHurtbox.transform.position.x, targetCollider.bounds.min.y)
                : (Vector2)targetHurtbox.transform.position;
            ApplyImpactToTarget(groundPoint);
            return true;
        }

        void ApplyImpactToTarget(Vector2 groundPoint)
        {
            var direction = ((Vector2)targetHurtbox.transform.position - groundPoint).normalized;
            if (direction.sqrMagnitude < 0.01f) direction = Vector2.up;
            targetHurtbox.ReceiveHit(new DamagePayload(source, damage,
                new Vector2(direction.x * Mathf.Abs(knockback.x), Mathf.Abs(knockback.y)), direction, 0.06f));
        }

        IEnumerator PlayShatter(Vector2 impactPoint)
        {
            if (shatterFrames == null || shatterFrames.Length == 0)
                yield break;

            var go = new GameObject("Shatter");
            go.transform.SetParent(transform, false);
            go.transform.position = impactPoint;
            go.transform.localScale = Vector3.one * shatterScale;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = shatterSortingOrder;
            renderer.sprite = shatterFrames[0];

            var frameTime = shatterDuration / shatterFrames.Length;
            for (var i = 0; i < shatterFrames.Length; i++)
            {
                renderer.sprite = shatterFrames[i];
                yield return new WaitForSeconds(frameTime);
            }
        }

        void StartFallingRumble()
        {
            if (!fallingRumbleSound) return;
            fallingRumbleSource = gameObject.AddComponent<AudioSource>();
            fallingRumbleSource.clip = fallingRumbleSound;
            fallingRumbleSource.volume = runtimeRumbleVolume;
            fallingRumbleSource.pitch = runtimeRumblePitch;
            fallingRumbleSource.loop = true;
            fallingRumbleSource.playOnAwake = false;
            fallingRumbleSource.spatialBlend = 0f;
            fallingRumbleFilter = gameObject.AddComponent<AudioLowPassFilter>();
            fallingRumbleFilter.cutoffFrequency = runtimeRumbleLowPass;
            fallingRumbleSource.Play();
        }

        void StopFallingRumble()
        {
            if (fallingRumbleSource) fallingRumbleSource.Stop();
            if (fallingRumbleFilter) Destroy(fallingRumbleFilter);
            if (fallingRumbleSource) Destroy(fallingRumbleSource);
            fallingRumbleFilter = null;
            fallingRumbleSource = null;
        }

        static void PlaySound2D(AudioClip clip, Vector2 worldPoint, float volume, float pitch)
        {
            if (!clip) return;
            var soundObject = new GameObject("MirrorRockfallImpactAudio");
            soundObject.transform.position = worldPoint;
            var source = soundObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume);
            source.pitch = Mathf.Clamp(pitch, 0.25f, 2f);
            source.spatialBlend = 0f;
            source.Play();
            Destroy(soundObject, clip.length / Mathf.Abs(source.pitch) + 0.1f);
        }

        SpriteRenderer BuildRockRenderer()
        {
            var go = new GameObject("RockVisual");
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * rockScale;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = rockSprite;
            renderer.sortingOrder = rockSortingOrder;
            return renderer;
        }

    }
}
