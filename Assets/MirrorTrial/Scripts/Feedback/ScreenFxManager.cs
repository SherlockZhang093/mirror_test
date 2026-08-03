using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MirrorTrial.Feedback
{
    [DefaultExecutionOrder(-900)]
    public sealed class ScreenFxManager : MonoBehaviour
    {
        const string OverlayResourcePath = "UI/ScreenFxOverlay";
        static ScreenFxManager instance;

        [Header("Overlay Prefab")]
        [SerializeField] ScreenFxOverlay overlayPrefab;
        [SerializeField] ScreenFxOverlay overlay;

        [Header("Shared Look")]
        [SerializeField] Color damageColor = new Color(0.48f, 0.005f, 0.008f, 1f);
        [SerializeField, Range(0f, 1f)] float lowHealthStrength = 0.22f;

        readonly Dictionary<ScreenFxType, HashSet<int>> stateSources = new Dictionary<ScreenFxType, HashSet<int>>();
        float bossTarget;
        float bossCurrent;
        float lowHealthTarget;
        float lowHealthCurrent;
        float damagePeak;
        float damageRemaining;
        float damageDuration;
        float phaseRemaining;
        float phaseDuration;
        Vector2 damageDirection;
        float flowTime;
        float bossFlickerTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (instance) return;
            var go = new GameObject("ScreenFxManager");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<ScreenFxManager>();
        }

        void Awake()
        {
            if (instance && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureOverlay();
        }

        void OnEnable()
        {
            ScreenFx.SignalSent += OnSignal;
            SceneManager.activeSceneChanged += OnSceneChanged;
        }

        void OnDisable()
        {
            ScreenFx.SignalSent -= OnSignal;
            SceneManager.activeSceneChanged -= OnSceneChanged;
            if (instance == this) instance = null;
        }

        void EnsureOverlay()
        {
            if (overlay) return;
            if (!overlayPrefab) overlayPrefab = Resources.Load<ScreenFxOverlay>(OverlayResourcePath);
            if (overlayPrefab) overlay = Instantiate(overlayPrefab, transform);
            else
            {
                var go = new GameObject("ScreenFxOverlay", typeof(RectTransform), typeof(Canvas),
                    typeof(CanvasGroup), typeof(ScreenFxOverlay));
                go.transform.SetParent(transform, false);
                overlay = go.GetComponent<ScreenFxOverlay>();
            }
            overlay.Initialize();
        }

        void Update()
        {
            EnsureOverlay();
            var dt = Time.unscaledDeltaTime;
            flowTime += dt;
            bossFlickerTime += dt;
            bossCurrent = Mathf.MoveTowards(bossCurrent, bossTarget,
                dt / Mathf.Max(0.01f, bossTarget > bossCurrent ? overlay.BossFadeIn : overlay.BossFadeOut));
            lowHealthCurrent = Mathf.MoveTowards(lowHealthCurrent, lowHealthTarget, dt * 2.5f);

            damageRemaining = Mathf.Max(0f, damageRemaining - dt);
            phaseRemaining = Mathf.Max(0f, phaseRemaining - dt);
            var damage = damageDuration > 0f
                ? damagePeak * DamageCurve(1f - damageRemaining / damageDuration)
                : 0f;
            var phase = phaseDuration > 0f
                ? Mathf.Sin(Mathf.Clamp01(phaseRemaining / phaseDuration) * Mathf.PI) * 0.55f
                : 0f;
            var lowPulse = lowHealthCurrent * (0.82f + Mathf.Sin(flowTime * 5.2f) * 0.18f);
            var bossFlicker = EvaluateBossFlicker(bossFlickerTime);
            overlay.Apply(bossCurrent * bossFlicker, damage, lowPulse, phase, flowTime,
                damageDirection, damageColor);
        }

        void OnSignal(ScreenFxSignal signal)
        {
            switch (signal.Mode)
            {
                case ScreenFxSignalMode.Play:
                    Play(signal);
                    break;
                case ScreenFxSignalMode.Begin:
                    SetState(signal, true);
                    break;
                case ScreenFxSignalMode.End:
                    SetState(signal, false);
                    break;
                case ScreenFxSignalMode.Clear:
                    ClearState(signal.Type);
                    break;
            }
        }

        void Play(ScreenFxSignal signal)
        {
            if (signal.Type == ScreenFxType.BossPhasePulse)
            {
                phaseDuration = signal.Duration > 0f ? signal.Duration : 0.85f;
                phaseRemaining = phaseDuration;
                return;
            }

            if (signal.Type != ScreenFxType.PlayerHit && signal.Type != ScreenFxType.Death) return;

            var death = signal.Type == ScreenFxType.Death;
            damagePeak = Mathf.Clamp01(signal.Intensity >= 0f ? signal.Intensity : death ? 0.55f : 0.42f);
            damageDuration = signal.Duration > 0f ? signal.Duration : death ? 0.55f : 0.32f;
            damageRemaining = damageDuration;
            damageDirection = signal.Direction.sqrMagnitude > 0.001f ? signal.Direction.normalized : Vector2.zero;
        }

        void SetState(ScreenFxSignal signal, bool enabled)
        {
            if (!stateSources.TryGetValue(signal.Type, out var sources))
            {
                sources = new HashSet<int>();
                stateSources.Add(signal.Type, sources);
            }
            var sourceId = signal.Source ? signal.Source.GetInstanceID() : 0;
            if (enabled) sources.Add(sourceId); else sources.Remove(sourceId);
            RefreshState(signal.Type, signal.Intensity);
        }

        void ClearState(ScreenFxType type)
        {
            if (stateSources.TryGetValue(type, out var sources)) sources.Clear();
            RefreshState(type, -1f);
        }

        void RefreshState(ScreenFxType type, float intensity)
        {
            var active = stateSources.TryGetValue(type, out var sources) && sources.Count > 0;
            if (type == ScreenFxType.BossBattle)
            {
                EnsureOverlay();
                if (active && bossTarget <= 0f) bossFlickerTime = 0f;
                bossTarget = active ? (intensity >= 0f ? intensity : overlay.BossStrength) : 0f;
            }
            else if (type == ScreenFxType.LowHealth)
                lowHealthTarget = active ? (intensity >= 0f ? intensity : lowHealthStrength) : 0f;
        }

        void OnSceneChanged(Scene previous, Scene next)
        {
            stateSources.Clear();
            bossTarget = lowHealthTarget = damageRemaining = phaseRemaining = 0f;
            bossFlickerTime = 0f;
        }

        float EvaluateBossFlicker(float time)
        {
            var phase = time * overlay.BossFlickerFrequency * Mathf.PI * 2f;
            var primary = Mathf.Cos(phase);
            var irregular = Mathf.Sin(phase * 2.17f + 0.9f);
            var pulse = Mathf.Clamp01(0.5f + primary * 0.34f + irregular * 0.16f);
            pulse = Mathf.SmoothStep(0f, 1f, pulse);
            return Mathf.Lerp(1f - overlay.BossFlickerAmount, 1f, pulse);
        }

        static float DamageCurve(float t)
        {
            t = Mathf.Clamp01(t);
            if (t < 0.16f) return Mathf.SmoothStep(0f, 1f, t / 0.16f);
            return 1f - Mathf.SmoothStep(0f, 1f, (t - 0.16f) / 0.84f);
        }
    }
}
