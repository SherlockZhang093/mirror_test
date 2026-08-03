using UnityEngine;
using UnityEngine.UI;

namespace MirrorTrial.Feedback
{
    [RequireComponent(typeof(Canvas), typeof(CanvasGroup))]
    public sealed class ScreenFxOverlay : MonoBehaviour
    {
        static readonly int BossIntensity = Shader.PropertyToID("_BossIntensity");
        static readonly int DamageIntensity = Shader.PropertyToID("_DamageIntensity");
        static readonly int LowHealthIntensity = Shader.PropertyToID("_LowHealthIntensity");
        static readonly int PhasePulse = Shader.PropertyToID("_PhasePulse");
        static readonly int FlowTime = Shader.PropertyToID("_FlowTime");
        static readonly int DamageDirection = Shader.PropertyToID("_DamageDirection");
        static readonly int BossColor = Shader.PropertyToID("_BossColor");
        static readonly int DamageColor = Shader.PropertyToID("_DamageColor");

        [SerializeField] RawImage mask;

        [Header("Boss Battle Effect")]
        [Tooltip("Color of the screen-edge effect during a boss battle.")]
        [SerializeField] Color bossColor = Color.black;
        [Tooltip("Maximum opacity/intensity of the boss edge effect.")]
        [SerializeField, Range(0f, 1f)] float bossStrength = 0.72f;
        [Tooltip("Seconds for the boss effect to reach full strength.")]
        [SerializeField, Min(0.01f)] float bossFadeIn = 0.18f;
        [Tooltip("Seconds for the boss effect to disappear after the fight.")]
        [SerializeField, Min(0.01f)] float bossFadeOut = 1.35f;
        [Tooltip("Base number of black-edge flashes per second.")]
        [SerializeField, Min(0.1f)] float bossFlickerFrequency = 0.8f;
        [Tooltip("Difference between the dimmest and brightest part of each flash.")]
        [SerializeField, Range(0f, 0.9f)] float bossFlickerAmount = 0.58f;

        Material runtimeMaterial;

        public float BossStrength => bossStrength;
        public float BossFadeIn => bossFadeIn;
        public float BossFadeOut => bossFadeOut;
        public float BossFlickerFrequency => bossFlickerFrequency;
        public float BossFlickerAmount => bossFlickerAmount;

        public void Initialize()
        {
            transform.localScale = Vector3.one;
            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 50;
            GetComponent<CanvasGroup>().blocksRaycasts = false;
            GetComponent<CanvasGroup>().interactable = false;

            if (!mask) mask = GetComponentInChildren<RawImage>(true);
            if (!mask) mask = CreateMask(transform);
            var maskRect = mask.rectTransform;
            maskRect.anchorMin = Vector2.zero;
            maskRect.anchorMax = Vector2.one;
            maskRect.offsetMin = Vector2.zero;
            maskRect.offsetMax = Vector2.zero;
            maskRect.localScale = Vector3.one;
            mask.raycastTarget = false;

            var source = mask.material;
            if (!source || source.shader.name != "MirrorTrial/UI/ScreenFxOverlay")
            {
                var shader = Shader.Find("MirrorTrial/UI/ScreenFxOverlay");
                if (shader) source = new Material(shader) { name = "ScreenFxOverlay Runtime Material" };
            }
            if (source)
            {
                runtimeMaterial = new Material(source) { name = source.name + " (Instance)" };
                mask.material = runtimeMaterial;
            }
        }

        public void Apply(float boss, float damage, float lowHealth, float phase, float time,
            Vector2 direction, Color damageColor)
        {
            if (!runtimeMaterial) return;
            runtimeMaterial.SetFloat(BossIntensity, boss);
            runtimeMaterial.SetFloat(DamageIntensity, damage);
            runtimeMaterial.SetFloat(LowHealthIntensity, lowHealth);
            runtimeMaterial.SetFloat(PhasePulse, phase);
            runtimeMaterial.SetFloat(FlowTime, time);
            runtimeMaterial.SetVector(DamageDirection, direction);
            runtimeMaterial.SetColor(BossColor, bossColor);
            runtimeMaterial.SetColor(DamageColor, damageColor);
            mask.enabled = boss + damage + lowHealth + phase > 0.001f;
        }

        static RawImage CreateMask(Transform parent)
        {
            var go = new GameObject("Mask", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return go.GetComponent<RawImage>();
        }

        void OnDestroy()
        {
            if (runtimeMaterial) Destroy(runtimeMaterial);
        }
    }
}
