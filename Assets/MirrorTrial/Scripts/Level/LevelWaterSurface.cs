using MirrorTrial.Player;
using UnityEngine;

namespace MirrorTrial.Level
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider2D))]
    public sealed class LevelWaterSurface : MonoBehaviour
    {
        [SerializeField] Rect waterArea = new Rect(-5f, -8f, 20f, 3f);
        [SerializeField, Range(0f, 1f)] float deathDepth = 0.35f;
        [SerializeField] Color shallowColor = new Color(0.13f, 0.36f, 0.39f, 0.78f);
        [SerializeField] Color deepColor = new Color(0.035f, 0.16f, 0.19f, 0.92f);
        [SerializeField] Color highlightColor = new Color(0.55f, 0.78f, 0.75f, 0.82f);
        [SerializeField, Range(0f, 3f)] float flowSpeed = 0.16f;
        [SerializeField, Range(0.1f, 4f)] float waveScale = 0.8f;
        [SerializeField, Range(1f, 24f)] float pixelDensity = 14f;
        [Header("Background Blend")]
        [SerializeField, Min(0.1f)] float backgroundBlendHeight = 2.1f;
        [SerializeField, Range(0f, 0.6f)] float backgroundBlendStrength = 0.24f;
        [SerializeField] Color backgroundBlendColor = new Color(0.08f, 0.39f, 0.43f, 1f);

        MeshFilter meshFilter;
        MeshRenderer meshRenderer;
        BoxCollider2D deathTrigger;

        public Rect WaterArea => waterArea;

        void OnEnable() { Refresh(); }
        void OnValidate() { Refresh(); }

        public void Refresh()
        {
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            deathTrigger = GetComponent<BoxCollider2D>();
            int waterLayer = LayerMask.NameToLayer("Water");
            if (waterLayer >= 0) gameObject.layer = waterLayer;
            if (!meshFilter.sharedMesh) meshFilter.sharedMesh = CreateQuad();

            transform.position = new Vector3(waterArea.center.x, waterArea.center.y, 0f);
            transform.localScale = new Vector3(Mathf.Max(0.01f, waterArea.width), Mathf.Max(0.01f, waterArea.height), 1f);
            deathTrigger.isTrigger = true;
            deathTrigger.size = new Vector2(1f, Mathf.Max(0.01f, 1f - deathDepth / Mathf.Max(0.01f, waterArea.height)));
            deathTrigger.offset = new Vector2(0f, -deathDepth / Mathf.Max(0.01f, waterArea.height) * 0.5f);
            meshRenderer.sortingOrder = -10;

            if (Application.isPlaying && !GetComponent<LevelWaterReflection>())
                gameObject.AddComponent<LevelWaterReflection>();
            if (Application.isPlaying)
            {
                var blend = transform.parent
                    ? transform.parent.GetComponentInChildren<WaterBackgroundBlend>(true)
                    : FindObjectOfType<WaterBackgroundBlend>(true);
                if (!blend)
                {
                    var blendObject = new GameObject("水面远景融合", typeof(MeshFilter), typeof(MeshRenderer), typeof(WaterBackgroundBlend));
                    blendObject.transform.SetParent(transform.parent, false);
                    blend = blendObject.GetComponent<WaterBackgroundBlend>();
                }
                blend.Configure(waterArea, backgroundBlendHeight, backgroundBlendColor, backgroundBlendStrength);
            }

            var material = meshRenderer.sharedMaterial;
            if (!material) return;
            material.SetColor("_ShallowColor", shallowColor);
            material.SetColor("_DeepColor", deepColor);
            material.SetColor("_HighlightColor", highlightColor);
            material.SetFloat("_FlowSpeed", flowSpeed);
            material.SetFloat("_WaveScale", waveScale);
            material.SetFloat("_PixelDensity", pixelDensity);
            material.SetFloat("_WaterTop", waterArea.yMax);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!Application.isPlaying) return;
            var receiver = other.GetComponentInParent<PlayerDamageReceiver>();
            if (receiver) receiver.KillAndRespawn();
        }

        static Mesh CreateQuad()
        {
            var mesh = new Mesh { name = "Level Water Quad" };
            mesh.vertices = new[] { new Vector3(-.5f,-.5f), new Vector3(.5f,-.5f), new Vector3(-.5f,.5f), new Vector3(.5f,.5f) };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
