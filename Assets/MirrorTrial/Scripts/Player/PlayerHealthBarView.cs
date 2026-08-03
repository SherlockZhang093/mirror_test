using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MirrorTrial.Player
{
    public sealed class PlayerHealthBarView : MonoBehaviour
    {
        [Header("5 Life Blocks, left to right")]
        [SerializeField] Image[] fills = new Image[5];
        [SerializeField] Image[] hitFlashes = new Image[5];

        [Header("Optional sprites")]
        [SerializeField] Sprite normalFill;
        [SerializeField] Sprite lowFill;

        [Header("FX")]
        [SerializeField] float hitFlashDuration = 0.08f;
        [SerializeField] Color hitFlashColor = new Color(1f, 0.92f, 0.82f, 1f);
        [SerializeField] Color hitDamageColor = new Color(1f, 0.16f, 0.12f, 0.95f);
        [SerializeField] Color damageTrailColor = new Color(0.48f, 0.025f, 0.02f, 0.8f);
        [SerializeField, Min(0f)] float damageTrailHold = 0.07f;
        [SerializeField, Min(0f)] float damageTrailFade = 0.22f;

        [Header("Damage Shake")]
        [SerializeField, Min(0f)] float normalShakeDistance = 4f;
        [SerializeField, Min(0f)] float heavyShakeDistance = 7f;
        [SerializeField, Range(0f, 1f)] float heavyDamageRatio = 0.4f;
        [SerializeField, Min(0.01f)] float shakeDuration = 0.15f;

        [Header("Low Health")]
        [SerializeField, Range(0f, 1f)] float lowHealthRatio = 0.3f;
        [SerializeField] Color lowHealthTint = new Color(1f, 0.14f, 0.1f, 1f);
        [SerializeField, Range(0f, 1f)] float lowHealthTintStrength = 0.28f;
        [SerializeField, Range(0f, 1f)] float lowHealthMinAlpha = 0.62f;
        [SerializeField, Min(0.1f)] float lowHealthPulsePeriod = 1.05f;

        [Header("Death")]
        [SerializeField, Range(0f, 1f)] float deathAlpha = 0.5f;
        [SerializeField, Min(0f)] float deathFadeDuration = 0.18f;

        [Header("Health Reserve")]
        [SerializeField] Image reserveFill;
        [SerializeField] CanvasGroup reserveGroup;
        [SerializeField] Text recoverKeyText;

        [Header("Reserve Colors")]
        [SerializeField] Color reserveReadyColor = new Color(0.16f, 0.95f, 0.86f, 1f);
        [SerializeField] Color reserveEmptyColor = new Color(0.16f, 0.95f, 0.86f, 0.28f);
        [SerializeField] Color reserveCastingColor = new Color(0.72f, 1f, 0.86f, 1f);

        static readonly Vector2[] ShakePattern =
        {
            new Vector2(-1f, 0.25f),
            new Vector2(0.8f, -0.2f),
            new Vector2(-0.55f, 0.1f),
            new Vector2(0.35f, -0.08f),
            Vector2.zero
        };

        int previousHealth = -1;
        Color[] baseFillColors;
        RectTransform feedbackRoot;
        CanvasGroup feedbackGroup;
        Vector2 feedbackRootPosition;
        Coroutine damageRoutine;
        Coroutine shakeRoutine;
        Coroutine lowHealthRoutine;
        Coroutine deathRoutine;

        void Awake()
        {
            feedbackRoot = transform as RectTransform;
            if (feedbackRoot)
                feedbackRootPosition = feedbackRoot.anchoredPosition;

            feedbackGroup = GetComponent<CanvasGroup>();
            if (!feedbackGroup)
                feedbackGroup = gameObject.AddComponent<CanvasGroup>();

            baseFillColors = new Color[fills.Length];
            for (var i = 0; i < fills.Length; i++)
            {
                if (fills[i])
                    baseFillColors[i] = fills[i].color;
            }

            ClearDamageOverlays();
        }

        public void SetHealth(int currentHP, int maxHP)
        {
            var current = Mathf.Clamp(currentHP, 0, fills.Length);
            var wasInitialized = previousHealth >= 0;
            var oldHealth = wasInitialized ? previousHealth : current;
            previousHealth = current;

            for (var i = 0; i < fills.Length; i++)
            {
                var fill = fills[i];
                if (!fill) continue;

                var alive = i < current;
                fill.enabled = alive;

                if (alive && normalFill)
                    fill.sprite = IsLowHealth(currentHP, maxHP) && lowFill ? lowFill : normalFill;
            }

            if (wasInitialized && current < oldHealth)
                PlayLostHealthFeedback(current, oldHealth, maxHP);
            else if (current > oldHealth)
            {
                StopDamageFeedback();
                StopDeathFeedback();
            }

            UpdateLowHealthFeedback(currentHP, maxHP);

            if (current <= 0)
                BeginDeathFeedback(wasInitialized && current < oldHealth
                    ? hitFlashDuration + damageTrailHold + damageTrailFade
                    : 0f);
            else
                StopDeathFeedback();
        }

        public void SetReserve(int current, int capacity, bool isCasting)
        {
            var normalized = capacity > 0
                ? Mathf.Clamp01((float)current / capacity)
                : 0f;

            if (reserveFill)
            {
                reserveFill.fillAmount = normalized;
                reserveFill.color = isCasting
                    ? reserveCastingColor
                    : current > 0 ? reserveReadyColor : reserveEmptyColor;
            }

            if (reserveGroup)
                reserveGroup.alpha = current > 0 || isCasting ? 1f : 0.62f;

            if (recoverKeyText)
            {
                recoverKeyText.color = isCasting
                    ? reserveCastingColor
                    : current > 0 ? reserveReadyColor : reserveEmptyColor;
            }
        }

        bool IsLowHealth(int current, int maximum)
        {
            return current > 0 && maximum > 0 && current / (float)maximum <= lowHealthRatio;
        }

        void PlayLostHealthFeedback(int current, int oldHealth, int maximum)
        {
            StopDamageFeedback();

            damageRoutine = StartCoroutine(PlayDamageFlashAndTrail(current, oldHealth));

            if (shakeRoutine != null)
            {
                StopCoroutine(shakeRoutine);
                shakeRoutine = null;
            }

            if (feedbackRoot)
                feedbackRoot.anchoredPosition = feedbackRootPosition;

            var lostHealth = Mathf.Max(0, oldHealth - current);
            var ratio = maximum > 0 ? lostHealth / (float)maximum : 0f;
            var distance = ratio >= heavyDamageRatio ? heavyShakeDistance : normalShakeDistance;
            shakeRoutine = StartCoroutine(PlayShake(distance));
        }

        IEnumerator PlayDamageFlashAndTrail(int current, int oldHealth)
        {
            SetDamageOverlayRange(current, oldHealth, hitFlashColor, true);

            var elapsed = 0f;
            while (elapsed < hitFlashDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = hitFlashDuration > 0f ? Mathf.Clamp01(elapsed / hitFlashDuration) : 1f;
                SetDamageOverlayRange(current, oldHealth, Color.Lerp(hitFlashColor, hitDamageColor, t), true);
                yield return null;
            }

            SetDamageOverlayRange(current, oldHealth, damageTrailColor, true);
            if (damageTrailHold > 0f)
                yield return new WaitForSecondsRealtime(damageTrailHold);

            elapsed = 0f;
            while (elapsed < damageTrailFade)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = damageTrailFade > 0f ? Mathf.Clamp01(elapsed / damageTrailFade) : 1f;
                var color = damageTrailColor;
                color.a *= 1f - t;
                SetDamageOverlayRange(current, oldHealth, color, true);
                yield return null;
            }

            ClearDamageOverlays();
            damageRoutine = null;
        }

        void SetDamageOverlayRange(int current, int oldHealth, Color color, bool active)
        {
            for (var i = current; i < oldHealth && i < hitFlashes.Length; i++)
            {
                var flash = hitFlashes[i];
                if (!flash) continue;
                flash.color = color;
                flash.gameObject.SetActive(active);
            }
        }

        IEnumerator PlayShake(float distance)
        {
            if (!feedbackRoot || distance <= 0f)
            {
                shakeRoutine = null;
                yield break;
            }

            var elapsed = 0f;
            while (elapsed < shakeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var progress = Mathf.Clamp01(elapsed / shakeDuration);
                var patternPosition = progress * (ShakePattern.Length - 1);
                var index = Mathf.Min(Mathf.FloorToInt(patternPosition), ShakePattern.Length - 2);
                var offset = Vector2.Lerp(ShakePattern[index], ShakePattern[index + 1], patternPosition - index);
                feedbackRoot.anchoredPosition = feedbackRootPosition + offset * distance * (1f - progress);
                yield return null;
            }

            feedbackRoot.anchoredPosition = feedbackRootPosition;
            shakeRoutine = null;
        }

        void UpdateLowHealthFeedback(int current, int maximum)
        {
            if (IsLowHealth(current, maximum))
            {
                if (lowHealthRoutine == null)
                    lowHealthRoutine = StartCoroutine(PlayLowHealthPulse());
                return;
            }

            StopLowHealthFeedback();
        }

        IEnumerator PlayLowHealthPulse()
        {
            var elapsed = 0f;
            while (true)
            {
                elapsed += Time.unscaledDeltaTime;
                var wave = (Mathf.Sin(elapsed / lowHealthPulsePeriod * Mathf.PI * 2f - Mathf.PI * 0.5f) + 1f) * 0.5f;
                var tintAmount = wave * lowHealthTintStrength;

                for (var i = 0; i < fills.Length; i++)
                {
                    var fill = fills[i];
                    if (!fill || !fill.enabled) continue;

                    var color = Color.Lerp(baseFillColors[i], lowHealthTint, tintAmount);
                    color.a = baseFillColors[i].a * Mathf.Lerp(1f, lowHealthMinAlpha, wave);
                    fill.color = color;
                }

                yield return null;
            }
        }

        void BeginDeathFeedback(float delay)
        {
            if (deathRoutine != null)
                StopCoroutine(deathRoutine);
            deathRoutine = StartCoroutine(PlayDeathFade(delay));
        }

        IEnumerator PlayDeathFade(float delay)
        {
            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);

            if (!feedbackGroup)
            {
                deathRoutine = null;
                yield break;
            }

            var startAlpha = feedbackGroup.alpha;
            var elapsed = 0f;
            while (elapsed < deathFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = deathFadeDuration > 0f ? Mathf.Clamp01(elapsed / deathFadeDuration) : 1f;
                feedbackGroup.alpha = Mathf.Lerp(startAlpha, deathAlpha, t);
                yield return null;
            }

            feedbackGroup.alpha = deathAlpha;
            deathRoutine = null;
        }

        void StopDamageFeedback()
        {
            if (damageRoutine != null)
            {
                StopCoroutine(damageRoutine);
                damageRoutine = null;
            }
            ClearDamageOverlays();
        }

        void ClearDamageOverlays()
        {
            for (var i = 0; i < hitFlashes.Length; i++)
            {
                if (hitFlashes[i])
                    hitFlashes[i].gameObject.SetActive(false);
            }
        }

        void StopLowHealthFeedback()
        {
            if (lowHealthRoutine != null)
            {
                StopCoroutine(lowHealthRoutine);
                lowHealthRoutine = null;
            }

            if (baseFillColors == null) return;
            for (var i = 0; i < fills.Length && i < baseFillColors.Length; i++)
            {
                if (fills[i])
                    fills[i].color = baseFillColors[i];
            }
        }

        void StopDeathFeedback()
        {
            if (deathRoutine != null)
            {
                StopCoroutine(deathRoutine);
                deathRoutine = null;
            }
            if (feedbackGroup)
                feedbackGroup.alpha = 1f;
        }

        void OnDisable()
        {
            StopDamageFeedback();
            StopLowHealthFeedback();
            StopDeathFeedback();

            if (shakeRoutine != null)
            {
                StopCoroutine(shakeRoutine);
                shakeRoutine = null;
            }
            if (feedbackRoot)
                feedbackRoot.anchoredPosition = feedbackRootPosition;
        }
    }
}
