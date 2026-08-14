using System;
using System.Collections;
using System.Collections.Generic;
using MirrorTrial.Audio;
using MirrorTrial.Player;
using UnityEngine;

namespace MirrorTrial.Level
{
    [Serializable]
    public sealed class MirrorCrackStage
    {
        [Min(1)] public int showAfterHit = 1;
        public Sprite sprite;
    }

    public class MirrorShatterEffect : MonoBehaviour
    {
        [Header("Mirror Renderers")]
        [SerializeField] SpriteRenderer intactRenderer;
        [SerializeField] SpriteRenderer frameRenderer;
        [SerializeField] SpriteRenderer crackRenderer;

        [Header("Configurable Crack Stages")]
        [Tooltip("Show the assigned crack sprite after this many valid hits.")]
        [SerializeField] List<MirrorCrackStage> crackStages = new List<MirrorCrackStage>();
        [HideInInspector, SerializeField] Sprite[] crackStageSprites = new Sprite[0];

        [Header("Hit Feedback")]
        [SerializeField] float hitFlashDuration = 0.08f;
        [SerializeField] Color hitFlashColor = Color.white;
        [SerializeField] float hitShakeDuration = 0.12f;
        [SerializeField] float hitShakeStrength = 0.12f;
        [SerializeField] float finalShakeStrength = 0.22f;
        [SerializeField] float finalHitStop = 0.055f;

        [Header("Shard Source")]
        [SerializeField] Sprite[] shardSprites = new Sprite[0];
        [SerializeField] Color shardTint = Color.white;
        [SerializeField] int shardCount = 46;
        [SerializeField] Vector2 spawnArea = new Vector2(8.5f, 11f);
        [SerializeField] Vector2 spawnCenterOffset;
        [SerializeField] Vector2 perspectiveEdgeScale = new Vector2(1.08f, 0.9f);
        [Range(0f, 1f)] [SerializeField] float smallShardRatio = 0.35f;
        [Range(0f, 1f)] [SerializeField] float largeShardRatio = 0.2f;

        [Header("Shard Motion")]
        [SerializeField] Vector2 speedRange = new Vector2(2.5f, 6.5f);
        [SerializeField] float impactDirectionWeight = 0.65f;
        [SerializeField] float radialDirectionWeight = 0.8f;
        [SerializeField] float upwardBoost = 1.8f;
        [SerializeField] float gravityScale = 0.6f;
        [SerializeField] float linearDrag = 0.15f;
        [SerializeField] Vector2 angularSpeedRange = new Vector2(180f, 720f);
        [SerializeField] Vector2 shardScaleRange = new Vector2(0.65f, 1.1f);
        [SerializeField] bool collideWithWorld;

        [Header("Shard Lifetime")]
        [SerializeField] float visibleDuration = 0.55f;
        [SerializeField] float fadeDuration = 0.65f;

        [Header("Transition Timing")]
        [SerializeField] float burstDelayAfterHitStop = 0.02f;
        [SerializeField] float recommendedEnterDelay = 0.8f;

        [Header("Audio And Particles")]
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip hitClip;
        [SerializeField] AudioClip shatterClip;
        [SerializeField] GameObject hitParticlePrefab;
        [SerializeField] GameObject shatterParticlePrefab;

        readonly List<GameObject> liveShards = new List<GameObject>();
        Vector3 intactStartPosition;
        Vector3 crackStartPosition;
        Color intactBaseColor = Color.white;
        Color crackBaseColor = Color.white;
        Coroutine hitRoutine;
        Coroutine shatterRoutine;
        bool hasPlayed;
        bool ownsHitStop;
        float timeScaleBeforeHitStop = 1f;

        public float RecommendedEnterDelay => Mathf.Max(recommendedEnterDelay, finalHitStop + burstDelayAfterHitStop);

        void Awake()
        {
            CacheBaseState();
            BuildDefaultCrackStagesIfNeeded();
            if (crackRenderer) crackRenderer.enabled = false;
            if (frameRenderer) frameRenderer.enabled = true;
        }

        void OnValidate()
        {
            shardCount = Mathf.Max(0, shardCount);
            speedRange.x = Mathf.Max(0f, speedRange.x);
            speedRange.y = Mathf.Max(speedRange.x, speedRange.y);
            angularSpeedRange.x = Mathf.Max(0f, angularSpeedRange.x);
            angularSpeedRange.y = Mathf.Max(angularSpeedRange.x, angularSpeedRange.y);
            visibleDuration = Mathf.Max(0f, visibleDuration);
            fadeDuration = Mathf.Max(0.01f, fadeDuration);
            recommendedEnterDelay = Mathf.Max(0f, recommendedEnterDelay);
            finalHitStop = Mathf.Max(0f, finalHitStop);
        }

        public void SetHitProgress(int currentHits, int requiredHits)
        {
            if (!crackRenderer) return;
            BuildDefaultCrackStagesIfNeeded();

            MirrorCrackStage selected = null;
            var highestHit = int.MinValue;
            for (var i = 0; i < crackStages.Count; i++)
            {
                var stage = crackStages[i];
                if (stage == null || !stage.sprite || currentHits < Mathf.Max(1, stage.showAfterHit)) continue;
                if (stage.showAfterHit > highestHit)
                {
                    selected = stage;
                    highestHit = stage.showAfterHit;
                }
            }

            crackRenderer.sprite = selected != null ? selected.sprite : null;
            crackRenderer.enabled = selected != null;
        }


        public void PlayHit(Vector2 impactDirection)
        {
            if (hasPlayed) return;
            if (hitRoutine != null)
            {
                StopCoroutine(hitRoutine);
                RestoreGlassTransforms();
            }
            hitRoutine = StartCoroutine(HitFeedbackRoutine(hitShakeStrength));
            PlayClip(hitClip);
            SpawnParticle(hitParticlePrefab, impactDirection);
        }

        public void Play(Vector2 impactDirection)
        {
            if (hasPlayed) return;
            hasPlayed = true;
            if (hitRoutine != null)
            {
                StopCoroutine(hitRoutine);
                RestoreGlassTransforms();
            }
            shatterRoutine = StartCoroutine(ShatterRoutine(impactDirection.sqrMagnitude > 0.001f ? impactDirection.normalized : Vector2.right));
        }

        public void Play()
        {
            Play(Vector2.right);
        }

        public void ShowCompletedFrameOnly()
        {
            hasPlayed = true;
            RestoreTimeScale();
            StopVisualRoutines();
            ClearShards();
            if (intactRenderer) intactRenderer.enabled = false;
            if (crackRenderer) crackRenderer.enabled = false;
            if (frameRenderer) frameRenderer.enabled = true;
        }

        IEnumerator ShatterRoutine(Vector2 impactDirection)
        {
            PlayClip(shatterClip);
            SpawnParticle(shatterParticlePrefab, impactDirection);
            yield return HitFeedbackRoutine(finalShakeStrength, finalHitStop);

            if (burstDelayAfterHitStop > 0f)
                yield return new WaitForSecondsRealtime(burstDelayAfterHitStop);

            if (intactRenderer) intactRenderer.enabled = false;
            if (crackRenderer) crackRenderer.enabled = false;
            if (frameRenderer) frameRenderer.enabled = true;

            SpawnShardBurst(impactDirection);
            shatterRoutine = null;
        }

        IEnumerator HitFeedbackRoutine(float shakeStrength, float hitStopDuration = 0f)
        {
            CacheBaseState();
            var elapsed = 0f;
            if (hitStopDuration > 0f)
            {
                timeScaleBeforeHitStop = Time.timeScale > 0f ? Time.timeScale : 1f;
                ownsHitStop = true;
                Time.timeScale = 0f;
            }

            var duration = Mathf.Max(hitShakeDuration, hitFlashDuration, hitStopDuration);
            while (elapsed < duration)
            {
                var dt = Time.unscaledDeltaTime;
                elapsed += dt;
                var shakeT = hitShakeDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / hitShakeDuration);
                var strength = shakeStrength * (1f - shakeT);
                var offset = (Vector3)(UnityEngine.Random.insideUnitCircle * strength);
                if (intactRenderer) intactRenderer.transform.localPosition = intactStartPosition + offset;
                if (crackRenderer) crackRenderer.transform.localPosition = crackStartPosition + offset;

                var flashT = hitFlashDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / hitFlashDuration);
                if (intactRenderer) intactRenderer.color = Color.Lerp(hitFlashColor, intactBaseColor, flashT);
                if (crackRenderer) crackRenderer.color = Color.Lerp(hitFlashColor, crackBaseColor, flashT);

                if (ownsHitStop && elapsed >= hitStopDuration)
                    RestoreTimeScale();
                yield return null;
            }

            RestoreGlassTransforms();
            RestoreTimeScale();
            hitRoutine = null;
        }

        void SpawnShardBurst(Vector2 impactDirection)
        {
            ClearShards();
            if (shardSprites == null || shardSprites.Length == 0) return;

            for (var i = 0; i < shardCount; i++)
            {
                var normalizedIndex = shardCount <= 1 ? 0f : (float)i / (shardCount - 1);
                var sizeMultiplier = normalizedIndex < largeShardRatio
                    ? UnityEngine.Random.Range(1.05f, 1.35f)
                    : normalizedIndex > 1f - smallShardRatio
                        ? UnityEngine.Random.Range(0.35f, 0.7f)
                        : UnityEngine.Random.Range(0.75f, 1.05f);
                var sprite = i < shardSprites.Length ? shardSprites[i] : shardSprites[UnityEngine.Random.Range(0, shardSprites.Length)];
                SpawnShard(impactDirection, sizeMultiplier, sprite);
            }
        }

        void SpawnShard(Vector2 impactDirection, float sizeMultiplier, Sprite sprite)
        {
            var go = new GameObject("MirrorShard");
            liveShards.Add(go);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = shardTint;
            CopySorting(intactRenderer ? intactRenderer : frameRenderer, renderer);

            float normalizedX = UnityEngine.Random.Range(-0.5f, 0.5f);
            float normalizedY = UnityEngine.Random.Range(-0.5f, 0.5f);
            float sideScale = Mathf.Lerp(perspectiveEdgeScale.x, perspectiveEdgeScale.y, normalizedX + 0.5f);
            var localOffset = new Vector3(
                spawnCenterOffset.x + normalizedX * spawnArea.x,
                spawnCenterOffset.y + normalizedY * spawnArea.y * sideScale, 0f);
            go.transform.position = transform.TransformPoint(localOffset);
            go.transform.rotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));
            var scale = UnityEngine.Random.Range(shardScaleRange.x, shardScaleRange.y) * sizeMultiplier;
            var worldScale = transform.lossyScale;
            go.transform.localScale = new Vector3(worldScale.x * scale, worldScale.y * scale, 1f);

            var radial = new Vector2(
                localOffset.x - spawnCenterOffset.x,
                localOffset.y - spawnCenterOffset.y).normalized;
            if (radial.sqrMagnitude < 0.001f) radial = UnityEngine.Random.insideUnitCircle.normalized;
            var direction = (impactDirection * impactDirectionWeight + radial * radialDirectionWeight + UnityEngine.Random.insideUnitCircle * 0.35f).normalized;
            var speed = UnityEngine.Random.Range(speedRange.x, speedRange.y) * Mathf.Lerp(1.15f, 0.85f, sizeMultiplier / 1.35f);

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = gravityScale;
            body.drag = linearDrag;
            body.velocity = direction * speed + Vector2.up * UnityEngine.Random.Range(0.2f, upwardBoost);
            var angular = UnityEngine.Random.Range(angularSpeedRange.x, angularSpeedRange.y);
            body.angularVelocity = UnityEngine.Random.value < 0.5f ? -angular : angular;

            if (collideWithWorld)
            {
                var collider = go.AddComponent<PolygonCollider2D>();
                collider.isTrigger = false;
            }

            StartCoroutine(FadeAndDestroyShard(go, renderer));
        }

        IEnumerator FadeAndDestroyShard(GameObject shard, SpriteRenderer renderer)
        {
            if (visibleDuration > 0f) yield return new WaitForSeconds(visibleDuration);
            var startColor = renderer ? renderer.color : Color.white;
            var elapsed = 0f;
            while (shard && renderer && elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                var color = startColor;
                color.a = Mathf.Lerp(startColor.a, 0f, Mathf.Clamp01(elapsed / fadeDuration));
                renderer.color = color;
                yield return null;
            }
            liveShards.Remove(shard);
            if (shard) Destroy(shard);
        }

        void BuildDefaultCrackStagesIfNeeded()
        {
            if (crackStages != null && crackStages.Count > 0) return;
            crackStages = new List<MirrorCrackStage>();
            if (crackStageSprites == null || crackStageSprites.Length == 0) return;
            for (var i = 0; i < crackStageSprites.Length; i++)
            {
                crackStages.Add(new MirrorCrackStage
                {
                    showAfterHit = i + 1,
                    sprite = crackStageSprites[i]
                });
            }
        }

        void CacheBaseState()
        {
            if (intactRenderer)
            {
                intactStartPosition = intactRenderer.transform.localPosition;
                intactBaseColor = intactRenderer.color;
            }
            if (crackRenderer)
            {
                crackStartPosition = crackRenderer.transform.localPosition;
                crackBaseColor = crackRenderer.color;
            }
        }

        void RestoreGlassTransforms()
        {
            if (intactRenderer)
            {
                intactRenderer.transform.localPosition = intactStartPosition;
                intactRenderer.color = intactBaseColor;
            }
            if (crackRenderer)
            {
                crackRenderer.transform.localPosition = crackStartPosition;
                crackRenderer.color = crackBaseColor;
            }
        }

        void RestoreTimeScale()
        {
            if (!ownsHitStop) return;
            Time.timeScale = timeScaleBeforeHitStop;
            ownsHitStop = false;
        }

        void StopVisualRoutines()
        {
            if (hitRoutine != null) StopCoroutine(hitRoutine);
            if (shatterRoutine != null) StopCoroutine(shatterRoutine);
            hitRoutine = null;
            shatterRoutine = null;
            RestoreGlassTransforms();
        }

        void ClearShards()
        {
            for (var i = liveShards.Count - 1; i >= 0; i--)
                if (liveShards[i]) Destroy(liveShards[i]);
            liveShards.Clear();
        }

        void PlayClip(AudioClip clip)
        {
            if (!clip) return;
            var isShatter = clip == shatterClip;
            if (GlobalAudioFeedback.TryPlayMirrorImpact(isShatter) ||
                PlayerAudioFeedback.TryPlayMirrorImpact(isShatter))
                return;

            var palette = GameAudioPalette.LoadDefault();
            var audioVolume = palette
                ? isShatter ? palette.mirrorShatterVolume : palette.mirrorHitVolume
                : 1f;
            var volume = GameAudioPalette.ScaleDefaultVolume(1f, audioVolume);
            if (!audioSource) audioSource = GetComponent<AudioSource>();
            if (audioSource) audioSource.PlayOneShot(clip, volume);
            else AudioSource.PlayClipAtPoint(clip, transform.position, volume);
        }

        void SpawnParticle(GameObject prefab, Vector2 direction)
        {
            if (!prefab) return;
            var rotation = direction.sqrMagnitude > 0.001f
                ? Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg)
                : Quaternion.identity;
            var instance = Instantiate(prefab, transform.position, rotation);
            Destroy(instance, 5f);
        }

        void OnDisable()
        {
            RestoreTimeScale();
            ClearShards();
        }

        static void CopySorting(SpriteRenderer source, SpriteRenderer target)
        {
            if (!source || !target) return;
            target.sortingLayerID = source.sortingLayerID;
            target.sortingOrder = source.sortingOrder + 2;
        }
    }
}
