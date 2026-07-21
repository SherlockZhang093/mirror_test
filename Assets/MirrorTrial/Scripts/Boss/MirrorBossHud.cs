using UnityEngine;
using UnityEngine.UI;

namespace MirrorTrial.Boss
{
    [RequireComponent(typeof(MirrorBossActor))]
    public sealed class MirrorBossHud : MonoBehaviour
    {
        MirrorBossActor boss;
        GameObject canvasObject;
        Image fill;
        Text title;

        void Awake()
        {
            boss = GetComponent<MirrorBossActor>();
        }

        void OnEnable()
        {
            boss.Activated += OnActivated;
            boss.HealthChanged += OnHealthChanged;
            boss.PhaseChanged += OnPhaseChanged;
            boss.Defeated += OnDefeated;
        }

        void OnDisable()
        {
            boss.Activated -= OnActivated;
            boss.HealthChanged -= OnHealthChanged;
            boss.PhaseChanged -= OnPhaseChanged;
            boss.Defeated -= OnDefeated;
        }

        void OnActivated(MirrorBossActor actor)
        {
            EnsureHud();
            canvasObject.SetActive(true);
            title.text = actor.DisplayName;
            SetRatio(1f);
        }

        void OnHealthChanged(MirrorBossActor actor, int current, int maximum)
        {
            EnsureHud();
            SetRatio(maximum > 0 ? current / (float)maximum : 0f);
        }

        void OnPhaseChanged(MirrorBossActor actor, int phase)
        {
            EnsureHud();
            title.text = actor.DisplayName + "  ·  " + ToRoman(phase);
        }

        void OnDefeated(MirrorBossActor actor)
        {
            SetRatio(0f);
            if (canvasObject) Destroy(canvasObject, 1.5f);
        }

        void EnsureHud()
        {
            if (canvasObject) return;
            canvasObject = new GameObject("Mirror Boss HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            DontDestroyOnLoad(canvasObject);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var panel = CreateImage("Panel", canvasObject.transform, new Color(0.015f, 0.025f, 0.07f, 0.88f));
            SetRect(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(720f, 54f), new Vector2(0f, 60f));
            var background = CreateImage("Health Background", panel.transform, new Color(0.08f, 0.1f, 0.18f, 1f));
            SetRect(background.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 15f), new Vector2(0f, 10f));
            fill = CreateImage("Health Fill", background.transform, new Color(0.38f, 0.58f, 1f, 1f));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            SetStretch(fill.rectTransform);

            var titleObject = new GameObject("Boss Name", typeof(RectTransform), typeof(Text));
            titleObject.transform.SetParent(panel.transform, false);
            title = titleObject.GetComponent<Text>();
            title.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            title.fontSize = 23;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = new Color(0.82f, 0.9f, 1f, 1f);
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-24f, 28f), new Vector2(0f, -15f));
        }

        static Image CreateImage(string name, Transform parent, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            var image = obj.GetComponent<Image>();
            image.color = color;
            return image;
        }

        static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 size, Vector2 position)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        static void SetStretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        void SetRatio(float ratio)
        {
            if (fill) fill.fillAmount = Mathf.Clamp01(ratio);
        }

        static string ToRoman(int value) => value == 2 ? "II" : value == 3 ? "III" : "I";
    }
}
