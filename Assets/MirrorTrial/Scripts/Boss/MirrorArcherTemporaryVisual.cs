using UnityEngine;

namespace MirrorTrial.Boss
{
    /// <summary>
    /// Deliberately simple phase-one placeholder. Final art only needs to replace VisualRoot and keep
    /// RiderSocket, ProjectileSocket, MountRoot and ImpactSocket names plus the documented animator states.
    /// </summary>
    public sealed class MirrorArcherTemporaryVisual : MonoBehaviour
    {
        [SerializeField] Color mountColor = new Color(0.24f, 0.62f, 0.9f, 1f);
        [SerializeField] Color riderColor = new Color(0.88f, 0.72f, 0.3f, 1f);

        void Awake()
        {
            if (transform.Find("GeneratedPlaceholder")) return;
            var root = new GameObject("GeneratedPlaceholder").transform;
            root.SetParent(transform, false);
            CreateLoop(root, "Mount", new Vector2(0f, -0.15f), new Vector2(2.4f, 0.85f), mountColor);
            CreateLoop(root, "Rider", new Vector2(0.1f, 0.65f), new Vector2(0.65f, 1.15f), riderColor);
            CreateLine(root, "WingLeft", new[] { new Vector3(-0.55f, 0.15f), new Vector3(-1.65f, 0.65f), new Vector3(-1.1f, -0.05f) }, mountColor);
            CreateLine(root, "WingRight", new[] { new Vector3(0.55f, 0.15f), new Vector3(1.65f, 0.65f), new Vector3(1.1f, -0.05f) }, mountColor);
        }

        static void CreateLoop(Transform parent, string name, Vector2 center, Vector2 size, Color color)
        {
            const int count = 24;
            var points = new Vector3[count + 1];
            for (var i = 0; i <= count; i++)
            {
                var angle = i / (float)count * Mathf.PI * 2f;
                points[i] = center + new Vector2(Mathf.Cos(angle) * size.x * 0.5f, Mathf.Sin(angle) * size.y * 0.5f);
            }
            CreateLine(parent, name, points, color);
        }

        static void CreateLine(Transform parent, string name, Vector3[] points, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = points.Length;
            line.SetPositions(points);
            line.startWidth = 0.08f;
            line.endWidth = 0.08f;
            line.startColor = color;
            line.endColor = color;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.sortingOrder = 20;
        }
    }
}
