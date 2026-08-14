using UnityEngine;
using UnityEngine.UI;

namespace MirrorTrial.Boss
{
    [RequireComponent(typeof(MirrorBossActorV2))]
    public sealed class MirrorBossHudV3 : MonoBehaviour
    {
        [Header("Minimal Mirror Boss Bar")]
        [SerializeField] Sprite outerFrameSprite;
        [SerializeField] Sprite emptySprite;
        [SerializeField] Sprite fillSprite;
        [SerializeField] Sprite nameTickLeftSprite;
        [SerializeField] Sprite nameTickRightSprite;

        MirrorBossActorV2 boss;
        GameObject canvasObject;
        Image fill;
        Text title;
        Text healthValue;

        void Awake() => boss = GetComponent<MirrorBossActorV2>();
        void OnEnable()
        {
            boss.Activated += Activated;
            boss.HealthChanged += HealthChanged;
            boss.PhaseChanged += PhaseChanged;
            boss.Defeated += Defeated;
        }
        void OnDisable()
        {
            boss.Activated -= Activated;
            boss.HealthChanged -= HealthChanged;
            boss.PhaseChanged -= PhaseChanged;
            boss.Defeated -= Defeated;
        }

        void Activated(MirrorBossActorV2 actor)
        {
            Ensure();
            title.text = actor.DisplayName;
            SetHealth(actor.CurrentHitPoints, actor.MaxHitPoints);
        }
        void HealthChanged(MirrorBossActorV2 actor, int hp, int max)
        {
            Ensure();
            SetHealth(hp, max);
        }
        void PhaseChanged(MirrorBossActorV2 actor, int phase) { Ensure(); title.text = actor.DisplayName + "  ·  " + (phase == 2 ? "II" : "III"); }
        void Defeated(MirrorBossActorV2 actor)
        {
            SetHealth(0, actor.MaxHitPoints);
            if (canvasObject) Destroy(canvasObject, 1.5f);
        }

        void OnDestroy()
        {
            if (canvasObject) Destroy(canvasObject);
        }

        void Ensure()
        {
            if (canvasObject) return;
            canvasObject = new GameObject("Mirror Boss HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 80;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var panel = new GameObject("BossHealthBar", typeof(RectTransform)).GetComponent<RectTransform>();
            panel.SetParent(canvasObject.transform, false);
            SetRect(panel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(760f, 92f), new Vector2(0f, 88f));
            var bg = MakeImage("FillEmpty", panel, emptySprite);
            SetRect(bg.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(720f, 24f), new Vector2(0f, 13f));
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

            var textObject = new GameObject("Boss Name", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(panel, false);
            title = textObject.GetComponent<Text>();
            title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            title.fontSize = 23;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = new Color(0.82f, 0.9f, 1f, 1f);
            SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(180f, 30f), new Vector2(0f, -13f));
            var healthObject = new GameObject("Health Value", typeof(RectTransform), typeof(Text));
            healthObject.transform.SetParent(panel, false);
            healthValue = healthObject.GetComponent<Text>();
            healthValue.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            healthValue.fontSize = 18;
            healthValue.alignment = TextAnchor.MiddleCenter;
            healthValue.color = Color.white;
            healthValue.raycastTarget = false;
            SetRect(healthValue.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(160f, 24f), new Vector2(0f, 13f));
        }

        void SetHealth(int current, int maximum)
        {
            current = Mathf.Clamp(current, 0, Mathf.Max(0, maximum));
            if (fill) fill.fillAmount = maximum > 0 ? current / (float)maximum : 0f;
            if (healthValue) healthValue.text = current + " / " + Mathf.Max(0, maximum);
        }
        static Image MakeImage(string name, Transform parent, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.raycastTarget = false;
            return image;
        }

        static void SetRect(RectTransform rect, Vector2 min, Vector2 max, Vector2 size, Vector2 pos)
        {
            rect.anchorMin = min; rect.anchorMax = max; rect.sizeDelta = size; rect.anchoredPosition = pos;
        }
    }
}
