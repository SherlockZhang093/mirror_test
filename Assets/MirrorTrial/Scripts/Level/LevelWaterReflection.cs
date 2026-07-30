using UnityEngine;

namespace MirrorTrial.Level
{
    [DisallowMultipleComponent]
    public sealed class LevelWaterReflection : MonoBehaviour
    {
        [SerializeField, Range(0.05f, 0.5f)] float resolutionScale = 0.2f;
        [SerializeField] LayerMask reflectedLayers = ~0;

        Camera sourceCamera;
        Camera reflectionCamera;
        RenderTexture reflectionTexture;
        MeshRenderer waterRenderer;
        int cachedWidth;
        int cachedHeight;

        void OnEnable()
        {
            waterRenderer = GetComponent<MeshRenderer>();
            EnsureCamera();
        }

        void LateUpdate()
        {
            sourceCamera = Camera.main;
            if (!sourceCamera || !waterRenderer || !waterRenderer.sharedMaterial) return;
            EnsureCamera();
            EnsureTexture();

            reflectionCamera.CopyFrom(sourceCamera);
            reflectionCamera.enabled = true;
            reflectionCamera.depth = sourceCamera.depth - 1f;
            reflectionCamera.targetTexture = reflectionTexture;
            reflectionCamera.clearFlags = CameraClearFlags.SolidColor;
            reflectionCamera.backgroundColor = Color.clear;
            int waterLayer = LayerMask.NameToLayer("Water");
            int waterMask = waterLayer >= 0 ? 1 << waterLayer : 0;
            reflectionCamera.cullingMask = reflectedLayers & ~waterMask & ~(1 << LayerMask.NameToLayer("UI"));
            reflectionCamera.transform.SetPositionAndRotation(sourceCamera.transform.position, sourceCamera.transform.rotation);

            var material = waterRenderer.sharedMaterial;
            material.SetTexture("_ReflectionTex", reflectionTexture);
            float waterTop = transform.position.y + transform.lossyScale.y * 0.5f;
            material.SetFloat("_ReflectionSurfaceV", sourceCamera.WorldToViewportPoint(new Vector3(transform.position.x, waterTop, 0f)).y);
        }

        void EnsureCamera()
        {
            if (reflectionCamera) return;
            var go = new GameObject("Water Reflection Camera") { hideFlags = HideFlags.HideAndDontSave };
            reflectionCamera = go.AddComponent<Camera>();
        }

        void EnsureTexture()
        {
            int width = Mathf.Max(160, Mathf.RoundToInt(Screen.width * resolutionScale));
            int height = Mathf.Max(90, Mathf.RoundToInt(Screen.height * resolutionScale));
            if (reflectionTexture && width == cachedWidth && height == cachedHeight) return;
            ReleaseTexture();
            cachedWidth = width;
            cachedHeight = height;
            reflectionTexture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32)
            {
                name = "Pixel Water Reflection",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            reflectionTexture.Create();
        }

        void OnDisable()
        {
            ReleaseTexture();
            if (reflectionCamera) Destroy(reflectionCamera.gameObject);
        }

        void ReleaseTexture()
        {
            if (!reflectionTexture) return;
            reflectionTexture.Release();
            Destroy(reflectionTexture);
            reflectionTexture = null;
        }
    }
}
