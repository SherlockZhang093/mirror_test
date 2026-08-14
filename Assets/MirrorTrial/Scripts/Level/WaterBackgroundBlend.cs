using UnityEngine;

namespace MirrorTrial.Level
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class WaterBackgroundBlend : MonoBehaviour
    {
        MeshRenderer blendRenderer;
        Material runtimeMaterial;

        public void Configure(Rect waterArea, float height, Color color, float strength)
        {
            var filter = GetComponent<MeshFilter>();
            blendRenderer = GetComponent<MeshRenderer>();
            if (!filter.sharedMesh) filter.sharedMesh = CreateQuad();

            transform.position = new Vector3(waterArea.center.x, waterArea.yMax + height * 0.5f, 0f);
            transform.localScale = new Vector3(waterArea.width, height, 1f);
            blendRenderer.sortingOrder = -60;

            var shader = Shader.Find("MirrorTrial/Water Background Blend");
            if (!shader) return;
            if (!runtimeMaterial)
            {
                runtimeMaterial = new Material(shader)
                {
                    name = "Water Background Blend (Runtime)",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
            runtimeMaterial.SetColor("_BlendColor", new Color(color.r, color.g, color.b, strength));
            blendRenderer.sharedMaterial = runtimeMaterial;
        }

        void OnDestroy()
        {
            if (runtimeMaterial) Destroy(runtimeMaterial);
        }

        static Mesh CreateQuad()
        {
            var mesh = new Mesh { name = "Water Background Blend Quad" };
            mesh.vertices = new[] { new Vector3(-.5f,-.5f), new Vector3(.5f,-.5f), new Vector3(-.5f,.5f), new Vector3(.5f,.5f) };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
