using System.Collections.Generic;
using UnityEngine;

namespace MirrorTrial.Level
{
    [ExecuteAlways]
    [RequireComponent(typeof(LineRenderer))]
    public sealed class PulleyRopeLineVisual : MonoBehaviour
    {
        public enum RopePath
        {
            OverPulley,
            Straight
        }

        [SerializeField] RopePath path = RopePath.OverPulley;
        [SerializeField] Transform startPoint;
        [SerializeField] Transform endPoint;
        [SerializeField] Transform pulleyCenter;
        [SerializeField, Min(0.01f)] float pulleyRadius = 0.68f;
        [SerializeField, Range(4, 32)] int arcSegments = 14;
        [SerializeField, Min(0.01f)] float width = 0.12f;
        [SerializeField, Min(0.05f)] float textureWorldLength = 0.85f;

        readonly List<Vector3> points = new List<Vector3>(24);
        LineRenderer line;

        public void Configure(RopePath ropePath, Transform start, Transform end, Transform center,
            Material material, float radius = 0.68f, float ropeWidth = 0.12f)
        {
            path = ropePath;
            startPoint = start;
            endPoint = end;
            pulleyCenter = center;
            pulleyRadius = Mathf.Max(0.01f, radius);
            width = Mathf.Max(0.01f, ropeWidth);
            line = GetComponent<LineRenderer>();
            line.sharedMaterial = material;
            SetupRenderer();
            Refresh();
        }

        void Awake()
        {
            line = GetComponent<LineRenderer>();
            SetupRenderer();
        }

        void LateUpdate() => Refresh();

        void OnValidate()
        {
            line = GetComponent<LineRenderer>();
            SetupRenderer();
            Refresh();
        }

        void SetupRenderer()
        {
            if (!line)
                return;
            line.useWorldSpace = true;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Tile;
            line.widthMultiplier = width;
            line.numCornerVertices = 3;
            line.numCapVertices = 2;
            line.sortingOrder = 2;
            line.generateLightingData = false;
        }

        public void Refresh()
        {
            if (!line)
                line = GetComponent<LineRenderer>();
            if (!line || !startPoint || !endPoint)
                return;

            points.Clear();
            points.Add(startPoint.position);
            if (path == RopePath.OverPulley && pulleyCenter)
            {
                var center = pulleyCenter.position;
                for (var i = 0; i <= arcSegments; i++)
                {
                    var angle = Mathf.Lerp(180f, 0f, i / (float)arcSegments) * Mathf.Deg2Rad;
                    points.Add(center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * pulleyRadius);
                }
            }
            points.Add(endPoint.position);

            line.positionCount = points.Count;
            line.SetPositions(points.ToArray());
            line.widthMultiplier = width;

            var length = 0f;
            for (var i = 1; i < points.Count; i++)
                length += Vector3.Distance(points[i - 1], points[i]);
            line.textureScale = new Vector2(Mathf.Max(1f, length / textureWorldLength), 1f);
        }
    }
}
