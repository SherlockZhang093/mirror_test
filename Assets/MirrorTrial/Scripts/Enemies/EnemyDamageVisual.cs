using System.Collections;
using System.Collections.Generic;
using MirrorTrial.Combat;
using UnityEngine;

namespace MirrorTrial.Enemies
{
    public sealed class EnemyDamageVisual : MonoBehaviour
    {
        const string HitFlashMaterialResourcePath = "Materials/SpriteHitFlash";

        static readonly int FlashColor = Shader.PropertyToID("_FlashColor");
        static readonly int FlashAmount = Shader.PropertyToID("_FlashAmount");
        static Material sharedFlashTemplate;

        [SerializeField] Color whiteFlashColor = Color.white;
        [SerializeField] Color redFlashColor = new Color(1f, 0.12f, 0.08f, 1f);
        [SerializeField, Min(0.01f)] float whiteFlashDuration = 0.05f;
        [SerializeField, Min(0.01f)] float redFlashDuration = 0.08f;
        [SerializeField, Range(0f, 1f)] float flashAmount = 1f;

        readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
        MaterialPropertyBlock properties;
        Coroutine routine;
        bool materialsReady;
        bool materialLoadFailureLogged;

        void Awake()
        {
            properties = new MaterialPropertyBlock();
            GetComponentsInChildren(true, renderers);
        }

        public void Bind(params SpriteRenderer[] targets)
        {
            renderers.Clear();
            if (targets != null)
            {
                foreach (var target in targets)
                    if (target && !renderers.Contains(target))
                        renderers.Add(target);
            }
            materialsReady = false;
        }

        bool EnsureMaterials()
        {
            if (materialsReady) return true;

            if (!sharedFlashTemplate)
                sharedFlashTemplate = Resources.Load<Material>(HitFlashMaterialResourcePath);

            if (!sharedFlashTemplate)
            {
                var fallbackShader = Shader.Find("MirrorTrial/SpriteHitFlash");
                if (fallbackShader)
                {
                    sharedFlashTemplate = new Material(fallbackShader)
                    {
                        name = "SpriteHitFlash_RuntimeFallback"
                    };
                }
                else
                {
                    if (!materialLoadFailureLogged)
                    {
                        Debug.LogError(
                            $"Unable to load hit flash material at Resources/{HitFlashMaterialResourcePath}.",
                            this);
                        materialLoadFailureLogged = true;
                    }
                    return false;
                }
            }

            var assignedAny = false;
            foreach (var item in renderers)
            {
                if (item)
                {
                    item.material = new Material(sharedFlashTemplate)
                    {
                        name = $"{item.name}_HitFlash"
                    };
                    assignedAny = true;
                }
            }

            materialsReady = assignedAny;
            return materialsReady;
        }

        public void PlayDamage(HitFlashType flashType, int actualDamage)
        {
            if (actualDamage <= 0) return;
            if (!EnsureMaterials()) return;
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(FlashRoutine(flashType));
        }

        IEnumerator FlashRoutine(HitFlashType flashType)
        {
            var red = flashType == HitFlashType.Red;
            SetFlash(red ? redFlashColor : whiteFlashColor, flashAmount);
            yield return new WaitForSecondsRealtime(red ? redFlashDuration : whiteFlashDuration);
            SetFlash(Color.white, 0f);
            routine = null;
        }

        void OnDisable()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }
            SetFlash(Color.white, 0f);
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
