using System.Collections;
using System;
using MirrorTrial.Audio;
using MirrorTrial.Combat;
using MirrorTrial.Player;
using UnityEngine;

namespace MirrorTrial.HealthResources
{
    public enum ResourceHitMaterial
    {
        Plant,
        Stone
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D), typeof(Hurtbox))]
    public sealed class HealthResourceNode : MonoBehaviour
    {
        [Header("Resource")]
        [SerializeField, Min(1)] int maxDurability = 30;
        [SerializeField, Min(1)] int healthReward = 25;
        [SerializeField] bool countHitsInsteadOfDamage;

        [Header("Reward Presentation")]
        [SerializeField] Sprite rewardOrbSprite;
        [SerializeField] Vector3 rewardEffectOffset = new Vector3(0f, 0.8f, 0f);
        [SerializeField] Color rewardOrbColor = Color.white;
        [SerializeField, Min(0.1f)] float rewardTravelDuration = 0.55f;

        [Header("Hit Feedback")]
        [SerializeField] Animator animator;
        [SerializeField] string hitTrigger = "Hit";
        [SerializeField] SpriteRenderer[] flashRenderers;
        [SerializeField] Color flashColor = Color.white;
        [SerializeField, Min(0f)] float flashDuration = 0.08f;
        [SerializeField] AudioClip hitSound;
        [SerializeField] ResourceHitMaterial hitMaterial;
        [SerializeField] AudioClip depletedSound;
        [SerializeField, Range(0f, 1f)] float hitVolume = 1f;

        [Header("Depleted")]
        [SerializeField] bool keepDepletedVisual;
        [SerializeField, Min(0f)] float deactivateDelay = 0.65f;

        int currentDurability;
        bool destroyed;
        bool initialized;
        Coroutine flashRoutine;
        Coroutine deactivateRoutine;
        Color[] originalColors;

        public event Action<int, int> DurabilityChanged;
        public event Action Depleted;

        public int CurrentDurability => currentDurability;
        public int MaxDurability => maxDurability;
        public int HealthReward => healthReward;
        public bool IsDestroyed => destroyed;
        public bool IsInitialized => initialized;

        void Awake()
        {
            maxDurability = Mathf.Max(1, maxDurability);
            healthReward = Mathf.Max(1, healthReward);
            currentDurability = maxDurability;
            if (flashRenderers == null || flashRenderers.Length == 0)
                flashRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            originalColors = new Color[flashRenderers.Length];
            for (var i = 0; i < flashRenderers.Length; i++)
                originalColors[i] = flashRenderers[i] ? flashRenderers[i].color : Color.white;
            initialized = true;
            DurabilityChanged?.Invoke(currentDurability, maxDurability);
        }

        public void OnDamagePayloadReceived(DamagePayload payload)
        {
            var playerSource = IsPlayerSource(payload.source);
            Trace(
                $"[接收层] Node={name}, Source={(payload.source ? payload.source.name : "<null>")}, " +
                $"Damage={payload.damage}, IsPlayer={playerSource}, Destroyed={destroyed}, " +
                $"Durability={currentDurability}/{maxDurability}");

            if (destroyed)
            {
                Trace("[接收层][忽略] 节点已经耗尽。");
                return;
            }
            if (payload.damage <= 0)
            {
                Trace("[接收层][忽略] Damage <= 0。");
                return;
            }
            if (!playerSource)
            {
                Trace("[接收层][忽略] Source 没有 PlayerInputReader，不属于玩家攻击。");
                return;
            }

            var durabilityDamage = countHitsInsteadOfDamage ? 1 : payload.damage;
            currentDurability = Mathf.Max(0, currentDurability - durabilityDamage);
            Trace($"[耐久扣除] -{durabilityDamage}, 当前={currentDurability}/{maxDurability}");
            DurabilityChanged?.Invoke(currentDurability, maxDurability);
            PlayHitFeedback();
            if (currentDurability <= 0) DestroyNode(payload.source);
        }

        static bool IsPlayerSource(GameObject source)
        {
            return source && source.GetComponentInParent<PlayerInputReader>();
        }

        void PlayHitFeedback()
        {
            if (animator && !string.IsNullOrEmpty(hitTrigger)) animator.SetTrigger(hitTrigger);
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            if (flashDuration > 0f && flashRenderers.Length > 0) flashRoutine = StartCoroutine(FlashRoutine());
            var palette = GameAudioPalette.LoadDefault();
            var clip = hitSound;
            var audioVolume = 1f;
            if (palette)
            {
                if (!clip)
                    clip = hitMaterial == ResourceHitMaterial.Stone ? palette.stoneHit : palette.grassHit;
                audioVolume = hitMaterial == ResourceHitMaterial.Stone
                    ? palette.stoneHitVolume
                    : palette.grassHitVolume;
            }
            if (clip)
                AudioSource.PlayClipAtPoint(clip, transform.position,
                    palette ? palette.ScaleVolume(hitVolume, audioVolume) : hitVolume);
        }

        IEnumerator FlashRoutine()
        {
            SetFlashColors(flashColor);
            yield return new WaitForSecondsRealtime(flashDuration);
            RestoreColors();
            flashRoutine = null;
        }

        void SetFlashColors(Color color)
        {
            for (var i = 0; i < flashRenderers.Length; i++)
                if (flashRenderers[i]) flashRenderers[i].color = color;
        }

        void RestoreColors()
        {
            for (var i = 0; i < flashRenderers.Length; i++)
                if (flashRenderers[i]) flashRenderers[i].color = originalColors[i];
        }

        void DestroyNode(GameObject source)
        {
            if (destroyed) return;
            destroyed = true;
            var reserve = source ? source.GetComponentInParent<PlayerHealthReserve>() : null;
            if (!reserve) reserve = FindObjectOfType<PlayerHealthReserve>();
            if (reserve)
            {
                // Gameplay state is committed immediately. The travelling orb is presentation only,
                // so disabling the node, changing scenes, or losing the effect can never discard health.
                var added = reserve.Add(healthReward);
                Trace(
                    $"[储备写入] 请求={healthReward}, 实际写入={added}, " +
                    $"当前储备={reserve.Current}/{reserve.Capacity}, Player={reserve.name}");
                if (added > 0)
                    HealthResourceTransferEffect.TrySpawn(
                        transform.position + rewardEffectOffset,
                        reserve,
                        rewardOrbSprite,
                        rewardOrbColor,
                        rewardTravelDuration);
            }
            else Debug.LogWarning("[HealthResourceNode] PlayerHealthReserve was not found; reward was discarded.", this);

            var hurtbox = GetComponent<Hurtbox>();
            if (hurtbox) hurtbox.enabled = false;
            var collider = GetComponent<Collider2D>();
            if (collider) collider.enabled = false;
            Depleted?.Invoke();
            if (depletedSound)
            {
                var palette = GameAudioPalette.LoadDefault();
                var audioVolume = palette ? palette.resourcePickupVolume : 1f;
                AudioSource.PlayClipAtPoint(depletedSound, transform.position, GameAudioPalette.ScaleDefaultVolume(hitVolume, audioVolume));
            }

            if (keepDepletedVisual) return;
            if (deactivateDelay <= 0f)
            {
                gameObject.SetActive(false);
                return;
            }
            deactivateRoutine = StartCoroutine(DeactivateAfterDelay());
        }

        IEnumerator DeactivateAfterDelay()
        {
            yield return new WaitForSeconds(deactivateDelay);
            deactivateRoutine = null;
            gameObject.SetActive(false);
        }

        void OnDisable()
        {
            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
                flashRoutine = null;
            }
            if (deactivateRoutine != null)
            {
                StopCoroutine(deactivateRoutine);
                deactivateRoutine = null;
            }
            if (originalColors != null) RestoreColors();
        }

        void OnValidate()
        {
            maxDurability = Mathf.Max(1, maxDurability);
            healthReward = Mathf.Max(1, healthReward);
            deactivateDelay = Mathf.Max(0f, deactivateDelay);
            rewardTravelDuration = Mathf.Max(0.1f, rewardTravelDuration);
            if (!animator) animator = GetComponentInChildren<Animator>();
            if (flashRenderers == null || flashRenderers.Length == 0)
                flashRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        void Trace(string message)
        {
            Debug.Log("[HealthResourceTrace]" + message, this);
        }
    }
}
