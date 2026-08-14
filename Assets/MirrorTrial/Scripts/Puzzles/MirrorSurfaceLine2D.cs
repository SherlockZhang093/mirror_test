using UnityEngine;

namespace MirrorTrial.Puzzles
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LineRenderer))]
    public sealed class MirrorSurfaceLine2D : MonoBehaviour
    {
        [SerializeField] LineRenderer surfaceLine;
        [SerializeField] bool hideInPlayMode = true;

        public Vector2 WorldPointA => GetWorldPoint(0);
        public Vector2 WorldPointB => GetWorldPoint(1);
        public Vector2 Direction => (WorldPointB - WorldPointA).normalized;
        public Vector2 Normal => new Vector2(-Direction.y, Direction.x);

        void Awake()
        {
            EnsureLine();
            if (hideInPlayMode && surfaceLine) surfaceLine.enabled = false;
        }

        void OnValidate() => EnsureLine();

        Vector2 GetWorldPoint(int index)
        {
            EnsureLine();
            if (!surfaceLine || surfaceLine.positionCount < 2) return transform.position;
            return transform.TransformPoint(surfaceLine.GetPosition(index));
        }

        void EnsureLine()
        {
            if (!surfaceLine) surfaceLine = GetComponent<LineRenderer>();
            if (!surfaceLine) return;
            surfaceLine.useWorldSpace = false;
            if (surfaceLine.positionCount < 2)
            {
                surfaceLine.positionCount = 2;
                surfaceLine.SetPosition(0, new Vector3(-0.405f, 0f, 0f));
                surfaceLine.SetPosition(1, new Vector3(0.405f, 0f, 0f));
            }
        }
    }
}
