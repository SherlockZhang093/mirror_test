using UnityEngine;
using UnityEngine.UI;

namespace MirrorTrial.Boss
{
    [RequireComponent(typeof(MirrorArcherTwoStageCoordinator))]
    public sealed class MirrorArcherTwoStageHud : MonoBehaviour
    {
        [SerializeField] MirrorArcherTwoStageCoordinator coordinator;
        [Header("Minimal Mirror Boss Bar")]
        [SerializeField] Sprite outerFrameSprite;
        [SerializeField] Sprite emptySprite;
        [SerializeField] Sprite fillSprite;
        [SerializeField] Sprite nameTickLeftSprite;
        [SerializeField] Sprite nameTickRightSprite;

        GameObject canvasObject;
        Image fill;
        Text healthValue;

        void Awake()
        {
            if (!coordinator) coordinator = GetComponent<MirrorArcherTwoStageCoordinator>();
        }

        void OnEnable()
        {
            if (!coordinator) coordinator = GetComponent<MirrorArcherTwoStageCoordinator>();
            if (!coordinator) return;
            coordinator.BossHealthChanged += OnBossHealthChanged;
            coordinator.BossHealthHidden += Hide;
        }

        void OnDisable()
        {
            if (!coordinator) return;
            coordinator.BossHealthChanged -= OnBossHealthChanged;
            coordinator.BossHealthHidden -= Hide;
        }

        void OnBossHealthChanged(MirrorArcherCombatStage stage, string displayName, int current, int maximum)
        {
            Ensure();
            if (canvasObject && !canvasObject.activeSelf) canvasObject.SetActive(true);
            current = Mathf.Clamp(current, 0, Mathf.Max(0, maximum));
            fill.fillAmount = maximum > 0 ? current / (float)maximum : 0f;
            healthValue.text = current + " / " + Mathf.Max(0, maximum);
        }

        void Hide()
        {
            if (canvasObject) canvasObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (canvasObject) Destroy(canvasObject);
        }

        void Ensure()
        {
            if (canvasObject) return;
            canvasObject = new GameObject("Mirror Archer Two-Stage HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var panel = new GameObject("BossHealthBar", typeof(RectTransform)).GetComponent<RectTransform>();
            panel.SetParent(canvasObject.transform, false);
            SetRect(panel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(760f, 92f), new Vector2(0f, 88f));

            var background = MakeImage("FillEmpty", panel, emptySprite);
            SetRect(background.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(720f, 24f), new Vector2(0f, 13f));
            fill = MakeImage("Fill", panel, fillSprite);
            SetRect(fill.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(720f, 24f), new Vector2(0f, 13f));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;

            var frame = MakeImage("OuterFrame", panel, outerFrameSprite);
            SetRect(frame.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(760f, 42f), new Vector2(0f, 13f));
            var leftTick = MakeImage("NameTick_Left", panel, nameTickLeftSprite);
            SetRect(leftTick.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(118f, 14f), new Vector2(-150f, -13f));
            var rightTick = MakeImage("NameTick_Right", panel, nameTickRightSprite);
            SetRect(rightTick.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(118f, 14f), new Vector2(150f, -13f));

            healthValue = MakeText("Health Value", panel, 18, Color.white);
            SetRect(healthValue.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(160f, 24f), new Vector2(0f, 13f));
        }

        static Image MakeImage(string name, Transform parent, Sprite sprite)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.sprite = sprite;
            image.color = Color.white;
            image.raycastTarget = false;
            return image;
        }

        static Text MakeText(string name, Transform parent, int fontSize, Color color)
        {
            var value = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            value.transform.SetParent(parent, false);
            value.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            value.fontSize = fontSize;
            value.alignment = TextAnchor.MiddleCenter;
            value.color = color;
            value.raycastTarget = false;
            return value;
        }

        static void SetRect(RectTransform rect, Vector2 min, Vector2 max, Vector2 size, Vector2 position)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }
    }
}
