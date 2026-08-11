using System.Collections;
using MirrorTrial.Growth;
using UnityEngine;
using UnityEngine.UI;

namespace MirrorTrial.UI
{
    [DisallowMultipleComponent]
    public sealed class LifeEssenceHudView : MonoBehaviour
    {
        static Font cachedFont;
        static LifeEssenceHudView active;

        [SerializeField] RectTransform counterRoot;
        [SerializeField] Text iconText;
        [SerializeField] Text valueText;
        [SerializeField] RectTransform gainAnchor;
        [SerializeField] Image background;
        [SerializeField] Color normalTextColor = new Color(0.82f, 0.96f, 1f, 1f);
        [SerializeField] Color warningTextColor = new Color(1f, 0.48f, 0.48f, 1f);

        Coroutine pulseRoutine;
        Coroutine warningRoutine;
        GameSessionProgress boundSession;

        public static LifeEssenceHudView Active => active;
        public RectTransform GainAnchor => gainAnchor ? gainAnchor : counterRoot;

        void Awake()
        {
            active = this;
            if (!counterRoot)
                counterRoot = transform as RectTransform;
            if (!gainAnchor)
                gainAnchor = counterRoot;
        }

        void OnDestroy()
        {
            if (active == this)
                active = null;
            Unbind(boundSession);
        }

        public void Bind(GameSessionProgress session)
        {
            if (ReferenceEquals(boundSession, session))
                return;

            Unbind(boundSession);
            boundSession = session;
            if (!session)
                return;

            session.LifeEssenceChanged += OnLifeEssenceChanged;
            session.LifeEssenceClaimed += OnLifeEssenceClaimed;
            session.LifeEssenceSpendFailed += OnLifeEssenceSpendFailed;
            RefreshImmediate(session.LifeEssence);
        }

        public void Unbind(GameSessionProgress session)
        {
            if (!session)
                return;

            session.LifeEssenceChanged -= OnLifeEssenceChanged;
            session.LifeEssenceClaimed -= OnLifeEssenceClaimed;
            session.LifeEssenceSpendFailed -= OnLifeEssenceSpendFailed;
            if (ReferenceEquals(boundSession, session))
                boundSession = null;
        }

        public void RefreshImmediate(int amount)
        {
            if (valueText)
                valueText.text = Mathf.Max(0, amount).ToString();
        }

        public static bool TryGetWorldTarget(Vector3 fromWorld, out Vector3 worldTarget)
        {
            if (!active || !active.GainAnchor)
            {
                worldTarget = fromWorld + Vector3.up * 1.5f;
                return false;
            }

            var camera = Camera.main;
            if (!camera)
            {
                worldTarget = fromWorld + Vector3.up * 1.5f;
                return false;
            }

            var screen = RectTransformUtility.WorldToScreenPoint(null, active.GainAnchor.position);
            var depth = Mathf.Abs(fromWorld.z - camera.transform.position.z);
            worldTarget = camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, depth));
            worldTarget.z = fromWorld.z;
            return true;
        }

        public static LifeEssenceHudView CreateRuntimeFallback()
        {
            var root = new GameObject("LifeEssenceHudUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(LifeEssenceHudView));
            SetLayerRecursive(root, 5);

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0f;

            var view = root.GetComponent<LifeEssenceHudView>();
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            rootRect.localScale = Vector3.one;

            var counter = CreateRect("LifeEssenceCounter", root.transform, new Vector2(-24f, -24f), new Vector2(168f, 38f), new Vector2(1f, 1f), new Vector2(1f, 1f));
            var background = counter.gameObject.AddComponent<Image>();
            background.color = new Color(0.04f, 0.08f, 0.12f, 0.72f);

            var icon = CreateText("Icon", counter, "◆", 19, FontStyle.Bold, new Color(0.68f, 0.95f, 1f, 1f), TextAnchor.MiddleCenter);
            Stretch(icon.rectTransform, 10f, 118f, 6f, 6f);

            var value = CreateText("ValueText", counter, "0", 18, FontStyle.Bold, new Color(0.82f, 0.96f, 1f, 1f), TextAnchor.MiddleLeft);
            Stretch(value.rectTransform, 40f, 12f, 6f, 6f);

            var gainAnchor = CreateRect("GainAnchor", counter, new Vector2(-8f, -2f), new Vector2(80f, 22f), new Vector2(1f, 1f), new Vector2(1f, 1f));

            view.counterRoot = counter;
            view.iconText = icon;
            view.valueText = value;
            view.gainAnchor = gainAnchor;
            view.background = background;
            return view;
        }

        void OnLifeEssenceChanged(int previous, int current)
        {
            RefreshImmediate(current);
            if (current < previous)
                StartPulse(0.16f, 1.08f);
        }

        void OnLifeEssenceClaimed(string nodeId, int amount)
        {
            if (amount <= 0)
                return;

            RefreshImmediate(boundSession ? boundSession.LifeEssence : amount);
            StartPulse(0.2f, 1.25f);
            SpawnGainText("+" + amount);
        }

        void OnLifeEssenceSpendFailed(int amount, string reasonId)
        {
            if (warningRoutine != null)
                StopCoroutine(warningRoutine);
            warningRoutine = StartCoroutine(WarningRoutine());
        }

        void StartPulse(float duration, float peakScale)
        {
            if (pulseRoutine != null)
                StopCoroutine(pulseRoutine);
            pulseRoutine = StartCoroutine(PulseRoutine(duration, peakScale));
        }

        void SpawnGainText(string text)
        {
            if (!gainAnchor)
                return;

            var go = new GameObject("LifeEssenceGainText", typeof(RectTransform), typeof(Text));
            SetLayerRecursive(go, 5);
            go.transform.SetParent(gainAnchor, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(96f, 28f);
            var textComponent = go.GetComponent<Text>();
            textComponent.font = GetFont();
            textComponent.text = text;
            textComponent.fontSize = 16;
            textComponent.fontStyle = FontStyle.Bold;
            textComponent.alignment = TextAnchor.MiddleRight;
            textComponent.color = normalTextColor;
            textComponent.raycastTarget = false;
            StartCoroutine(GainTextRoutine(rect, textComponent));
        }

        IEnumerator PulseRoutine(float duration, float peakScale)
        {
            if (!counterRoot)
            {
                pulseRoutine = null;
                yield break;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
                var scale = 1f + Mathf.Sin(t * Mathf.PI) * (peakScale - 1f);
                counterRoot.localScale = Vector3.one * scale;
                yield return null;
            }

            counterRoot.localScale = Vector3.one;
            pulseRoutine = null;
        }

        IEnumerator WarningRoutine()
        {
            if (!counterRoot)
            {
                warningRoutine = null;
                yield break;
            }

            var start = counterRoot.anchoredPosition;
            var duration = 0.3f;
            var elapsed = 0f;
            SetTextColor(warningTextColor);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var wave = Mathf.Sin(t * Mathf.PI * 6f) * (1f - t) * 4f;
                counterRoot.anchoredPosition = start + new Vector2(wave, 0f);
                yield return null;
            }
            counterRoot.anchoredPosition = start;
            SetTextColor(normalTextColor);
            warningRoutine = null;
        }

        IEnumerator GainTextRoutine(RectTransform rect, Text text)
        {
            var elapsed = 0f;
            var duration = 0.6f;
            var start = rect.anchoredPosition;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                rect.anchoredPosition = start + new Vector2(0f, Mathf.Lerp(0f, 24f, t));
                var color = text.color;
                color.a = 1f - t;
                text.color = color;
                yield return null;
            }
            Destroy(text.gameObject);
        }

        void SetTextColor(Color color)
        {
            if (iconText)
                iconText.color = color;
            if (valueText)
                valueText.color = color;
        }

        static RectTransform CreateRect(string name, Transform parent, Vector2 anchoredPosition, Vector2 size, Vector2 anchor, Vector2 pivot)
        {
            var go = new GameObject(name, typeof(RectTransform));
            SetLayerRecursive(go, 5);
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            return rect;
        }

        static Text CreateText(string name, Transform parent, string value, int fontSize, FontStyle style, Color color, TextAnchor anchor)
        {
            var rect = CreateRect(name, parent, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            var text = rect.gameObject.AddComponent<Text>();
            text.font = GetFont();
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = anchor;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        static void Stretch(RectTransform rect, float left, float right, float top, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            rect.localScale = Vector3.one;
        }

        static Font GetFont()
        {
            if (!cachedFont)
                cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return cachedFont;
        }

        static void SetLayerRecursive(GameObject root, int layer)
        {
            root.layer = layer;
            for (var i = 0; i < root.transform.childCount; i++)
                SetLayerRecursive(root.transform.GetChild(i).gameObject, layer);
        }
    }
}
