using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MirrorTrial.Enemies
{
    public sealed class EnemyDamageVisual : MonoBehaviour
    {
        static readonly int FlashColor = Shader.PropertyToID("_FlashColor");
        static readonly int FlashAmount = Shader.PropertyToID("_FlashAmount");

        [SerializeField] Color lowDamageColor = new Color(1f, 0.2f, 0.16f, 1f);
        [SerializeField] Color highDamageColor = new Color(1f, 0.92f, 0.62f, 1f);
        [SerializeField] AnimationCurve intensityByHealthRatio = new AnimationCurve(
            new Keyframe(0f, 0.25f), new Keyframe(0.05f, 0.45f),
            new Keyframe(0.15f, 0.75f), new Keyframe(0.3f, 1f));
        [SerializeField, Min(0.01f)] float minimumDuration = 0.035f;
        [SerializeField, Min(0.01f)] float maximumDuration = 0.09f;

        readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
        MaterialPropertyBlock properties;
        Coroutine routine;

        void Awake()
        {
            properties = new MaterialPropertyBlock();
            GetComponentsInChildren(true, renderers);
            var shader = Shader.Find("MirrorTrial/SpriteHitFlash");
            if (!shader) return;
            foreach (var item in renderers)
                if (item)
                    item.material = new Material(shader);
        }

        public void PlayDamage(int actualDamage, int maxHealth)
        {
            if (actualDamage <= 0 || maxHealth <= 0) return;
            var ratio = Mathf.Clamp01(actualDamage / (float)maxHealth);
            var intensity = Mathf.Clamp01(intensityByHealthRatio.Evaluate(ratio));
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(FlashRoutine(intensity));
        }

        IEnumerator FlashRoutine(float intensity)
        {
            SetFlash(Color.Lerp(lowDamageColor, highDamageColor, intensity), intensity);
            yield return new WaitForSecondsRealtime(Mathf.Lerp(minimumDuration, maximumDuration, intensity));
            SetFlash(Color.white, 0f);
            routine = null;
        }

        void SetFlash(Color color, float amount)
        {
            if (properties == null) properties = new MaterialPropertyBlock();
            foreach (var item in renderers)
            {
                if (!item) continue;
                item.GetPropertyBlock(properties);
                properties.SetColor(FlashColor, color);
                properties.SetFloat(FlashAmount, amount);
                item.SetPropertyBlock(properties);
            }
        }
    }
}
