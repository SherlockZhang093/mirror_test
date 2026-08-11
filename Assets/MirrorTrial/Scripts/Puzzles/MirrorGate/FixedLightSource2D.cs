using UnityEngine;

namespace MirrorTrial.Puzzles.MirrorGate
{
    [DisallowMultipleComponent]
    public sealed class FixedLightSource2D : MonoBehaviour
    {
        [SerializeField] Transform beamStartPoint;
        [SerializeField] RotatingMirror2D targetMirror;
        [SerializeField] LineRenderer incomingBeam;
        [SerializeField] LayerMask obstacleMask;
        [SerializeField] Color activeColor = new Color(0.2f, 0.95f, 1f, 0.9f);
        [SerializeField, Min(0.005f)] float beamWidth = 0.06f;
        [SerializeField] string sortingLayerName = "Default";
        [SerializeField] int sortingOrder = 20;

        readonly RaycastHit2D[] hits = new RaycastHit2D[12];

        public bool HasClearPath { get; private set; }
        public Transform BeamStartPoint => beamStartPoint ? beamStartPoint : transform;

        void Awake() => ConfigureLine();

        void LateUpdate() => RefreshBeam();

        public bool RefreshBeam()
        {
            if (!incomingBeam || !targetMirror)
            {
                if (incomingBeam) incomingBeam.enabled = false;
                HasClearPath = false;
                return false;
            }

            var start = (Vector2)BeamStartPoint.position;
            var target = (Vector2)targetMirror.BeamPoint.position;
            var delta = target - start;
            var end = target;
            HasClearPath = true;
            if (delta.sqrMagnitude > 0.0001f)
            {
                var count = Physics2D.RaycastNonAlloc(start, delta.normalized, hits, delta.magnitude, obstacleMask);
                var nearest = float.PositiveInfinity;
                for (var i = 0; i < count; i++)
                {
                    var hit = hits[i];
                    if (!hit.collider || hit.collider.transform.IsChildOf(targetMirror.transform)) continue;
                    if (hit.distance >= nearest) continue;
                    nearest = hit.distance;
                    end = hit.point;
                    HasClearPath = false;
                }
            }

            SetLine(start, end);
            incomingBeam.enabled = true;
            return HasClearPath;
        }

        void ConfigureLine()
        {
            if (!incomingBeam) return;
            incomingBeam.useWorldSpace = true;
            incomingBeam.positionCount = 2;
            incomingBeam.startWidth = incomingBeam.endWidth = beamWidth;
            incomingBeam.startColor = incomingBeam.endColor = activeColor;
            incomingBeam.sortingLayerName = sortingLayerName;
            incomingBeam.sortingOrder = sortingOrder;
        }

        void SetLine(Vector2 start, Vector2 end)
        {
            incomingBeam.SetPosition(0, start);
            incomingBeam.SetPosition(1, end);
        }
    }
}
