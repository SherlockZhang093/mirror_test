using MirrorTrial.Player;
using Platformer.Mechanics;
using UnityEngine;
using UnityEngine.UI;

namespace MirrorTrial.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerHealthReserve), typeof(Health))]
    public sealed class HealthReserveUI : MonoBehaviour
    {
        PlayerHealthReserve reserve;
        PlayerRecoveryAbility recovery;
        Health health;
        GameObject uiRoot;
        Text valueText;
        Text hintText;

        void Awake()
        {
            reserve = GetComponent<PlayerHealthReserve>();
            recovery = GetComponent<PlayerRecoveryAbility>();
            health = GetComponent<Health>();
            BuildView();
            Refresh(reserve.DisplayedCurrent, reserve.Capacity);
        }

        void OnEnable()
        {
            if (reserve != null) reserve.ProgressChanged += Refresh;
            if (recovery != null) recovery.CastStateChanged += OnCastStateChanged;
        }

        void OnDisable()
        {
            if (reserve != null) reserve.ProgressChanged -= Refresh;
            if (recovery != null) recovery.CastStateChanged -= OnCastStateChanged;
        }

        void OnDestroy()
        {
            if (uiRoot) Destroy(uiRoot);
        }

        void Update()
        {
            if (!hintText || !health) return;
            var usable = health.IsAlive && health.CurrentHP < health.maxHP && reserve.Current > 0;
            hintText.color = usable || (recovery && recovery.IsCasting)
                ? Color.white
                : new Color(1f, 1f, 1f, 0.45f);
        }

        void BuildView()
        {
            if (uiRoot) return;
            uiRoot = new GameObject("Health Reserve UI", typeof(Canvas), typeof(CanvasScaler));
            DontDestroyOnLoad(uiRoot);
            var canvas = uiRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = uiRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(2560f, 1440f);

            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(uiRoot.transform, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0f, 1f);
            panelRect.pivot = new Vector2(0f, 1f);
            panelRect.anchoredPosition = new Vector2(24f, -155f);
            panelRect.sizeDelta = new Vector2(300f, 70f);
            panel.GetComponent<Image>().color = new Color(0.035f, 0.05f, 0.08f, 0.88f);

            valueText = CreateText("Value", panel.transform, new Vector2(12f, -7f), new Vector2(276f, 30f), 20, FontStyle.Bold);
            hintText = CreateText("Hint", panel.transform, new Vector2(12f, -37f), new Vector2(276f, 24f), 15, FontStyle.Normal);
        }

        static Text CreateText(string name, Transform parent, Vector2 position, Vector2 size, int fontSize, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = go.GetComponent<Text>();
            text.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        void Refresh(float current, int capacity)
        {
            if (!valueText) BuildView();
            if (valueText) valueText.text = "HEALTH RESERVE   " + current.ToString("0.0") + " / " + capacity;
            RefreshHint();
        }

        void OnCastStateChanged(bool casting)
        {
            RefreshHint();
        }

        void RefreshHint()
        {
            if (!hintText) return;
            hintText.text = recovery && recovery.IsCasting ? "RECOVERING..." : "[G]  RECOVER TO FULL";
        }
    }
}
