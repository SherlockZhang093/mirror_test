using UnityEngine;

namespace MirrorTrial.Level
{
    /// <summary>
    /// Keeps Level 02's surviving jungle background plates framed to the gameplay camera.
    /// World-space architecture, water and vegetation are deliberately left untouched.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Level02JungleBackground : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField, Range(1f, 1.5f)] private float skyOverscan = 1.08f;
        [SerializeField, Range(1f, 1.5f)] private float ruinsOverscan = 1.18f;
        [SerializeField, Range(0f, 0.3f)] private float ruinsParallax = 0.12f;
        [SerializeField, Range(0f, 0.3f)] private float verticalParallax = 0.04f;

        private Transform skyLayer;
        private Transform ruinsLayer;
        private SpriteRenderer skyRenderer;
        private SpriteRenderer ruinsRenderer;
        private Vector3 cameraOrigin;
        private Vector3 skyLayerOrigin;
        private Vector3 ruinsLayerOrigin;

        private void Awake()
        {
            ResolveReferences();
            CaptureOrigins();
            DisableDuplicatePlates(skyLayer, skyRenderer);
            DisableDuplicatePlates(ruinsLayer, ruinsRenderer);
            Refresh();
        }

        private void LateUpdate()
        {
            Refresh();
        }

        private void ResolveReferences()
        {
            if (targetCamera == null) targetCamera = Camera.main;
            skyLayer = transform.Find("远景天空");
            ruinsLayer = transform.Find("中景遗迹");
            if (skyLayer != null)
                skyRenderer = skyLayer.GetComponentInChildren<SpriteRenderer>(true);
            if (ruinsLayer != null)
                ruinsRenderer = ruinsLayer.GetComponentInChildren<SpriteRenderer>(true);
        }

        private void CaptureOrigins()
        {
            if (targetCamera != null) cameraOrigin = targetCamera.transform.position;
            if (skyLayer != null) skyLayerOrigin = skyLayer.position;
            if (ruinsLayer != null) ruinsLayerOrigin = ruinsLayer.position;
        }

        private void Refresh()
        {
            if (targetCamera == null || !targetCamera.orthographic) return;

            Vector3 cameraPosition = targetCamera.transform.position;
            Vector3 cameraDelta = cameraPosition - cameraOrigin;

            if (skyLayer != null)
            {
                skyLayer.position = new Vector3(cameraPosition.x, cameraPosition.y, skyLayerOrigin.z);
                if (skyRenderer != null) skyRenderer.transform.localPosition = Vector3.zero;
            }

            if (ruinsLayer != null)
            {
                ruinsLayer.position = new Vector3(cameraPosition.x, cameraPosition.y, ruinsLayerOrigin.z);
                if (ruinsRenderer != null)
                {
                    ruinsRenderer.transform.localPosition = new Vector3(
                        -cameraDelta.x * ruinsParallax,
                        -1.25f - cameraDelta.y * verticalParallax,
                        ruinsRenderer.transform.localPosition.z);
                }
            }

            FitToCamera(skyRenderer, skyOverscan, -100);
            FitToCamera(ruinsRenderer, ruinsOverscan, -82);
        }

        private void FitToCamera(SpriteRenderer renderer, float overscan, int sortingOrder)
        {
            if (renderer == null || renderer.sprite == null) return;

            float cameraHeight = targetCamera.orthographicSize * 2f;
            float cameraWidth = cameraHeight * targetCamera.aspect;
            Vector2 spriteSize = renderer.sprite.bounds.size;
            if (spriteSize.x <= 0f || spriteSize.y <= 0f) return;

            float scale = Mathf.Max(cameraWidth / spriteSize.x, cameraHeight / spriteSize.y) * overscan;
            renderer.transform.localScale = new Vector3(scale, scale, 1f);
            renderer.sortingOrder = sortingOrder;
        }

        private static void DisableDuplicatePlates(Transform layer, SpriteRenderer retained)
        {
            if (layer == null || retained == null) return;
            SpriteRenderer[] renderers = layer.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (SpriteRenderer renderer in renderers)
            {
                if (renderer != retained) renderer.gameObject.SetActive(false);
            }
        }
    }
}
