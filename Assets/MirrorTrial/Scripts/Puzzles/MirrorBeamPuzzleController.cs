using System.Collections.Generic;
using UnityEngine;

namespace MirrorTrial.Puzzles
{
    [DisallowMultipleComponent]
    public sealed class MirrorBeamPuzzleController : MonoBehaviour
    {
        [SerializeField] Transform lightSource;
        [SerializeField] RotatablePuzzleMirror[] mirrors;
        [SerializeField] LightPuzzleReceiver receiver;
        [SerializeField] PuzzleLightBeamView beamPrefab;
        [SerializeField] Material beamMaterial;
        [SerializeField] Color beamColor = new Color(1f, 0.38f, 0.12f, 1f);
        [SerializeField, Min(0.01f)] float beamWidth = 0.055f;
        [SerializeField] Vector2 initialDirection = Vector2.down;
        [SerializeField, Min(1f)] float maxBeamDistance = 30f;
        [SerializeField, Min(1)] int maxReflections = 8;
        [SerializeField] LayerMask collisionMask = ~0;

        readonly List<LineRenderer> beams = new List<LineRenderer>();
        readonly List<PuzzleLightBeamView> beamViews = new List<PuzzleLightBeamView>();

        void Awake()
        {
            BuildBeams();
            Subscribe(true);
            RefreshPath();
        }

        void OnDestroy() => Subscribe(false);

        void Subscribe(bool value)
        {
            if (mirrors == null) return;
            foreach (var mirror in mirrors)
            {
                if (!mirror) continue;
                if (value) mirror.Changed += RefreshPath;
                else mirror.Changed -= RefreshPath;
            }
        }

        void BuildBeams()
        {
            var count = maxReflections + 1;
            for (var i = 0; i < count; i++)
            {
                if (beamPrefab)
                {
                    var view = Instantiate(beamPrefab, transform);
                    view.name = "Light Beam Segment " + (i + 1);
                    view.SetVisible(false);
                    beamViews.Add(view);
                    continue;
                }
                var child = new GameObject("Light Beam Segment " + (i + 1));
                child.transform.SetParent(transform, false);
                var line = child.AddComponent<LineRenderer>();
                line.positionCount = 2;
                line.useWorldSpace = true;
                line.startWidth = line.endWidth = beamWidth;
                line.startColor = line.endColor = beamColor;
                line.numCapVertices = 4;
                line.sortingOrder = 80;
                if (beamMaterial) line.material = beamMaterial;
                beams.Add(line);
            }
        }

        public void RefreshPath()
        {
            foreach (var beam in beams) beam.enabled = false;
            foreach (var view in beamViews) view.SetVisible(false);
            if (!lightSource) return;

            Physics2D.SyncTransforms();

            var from = (Vector2)lightSource.position;
            var direction = initialDirection.sqrMagnitude > 0.0001f
                ? initialDirection.normalized
                : Vector2.down;
            var connected = false;

            for (var segmentIndex = 0; segmentIndex <= maxReflections; segmentIndex++)
            {
                var blockerHit = FindFirstBlockingHit(from, direction);
                var blockerDistance = blockerHit.collider ? blockerHit.distance : maxBeamDistance;
                var hasMirrorHit = TryFindNearestMirror(from, direction, blockerDistance,
                    out var mirrorSurface, out var mirrorPoint, out var mirrorDistance);
                var pathDistance = hasMirrorHit ? mirrorDistance : blockerDistance;

                if (receiver && TryHitReceiver(from, direction, pathDistance, out var receiverPoint))
                {
                    Draw(segmentIndex, from, receiverPoint);
                    connected = true;
                    break;
                }

                if (hasMirrorHit)
                {
                    Draw(segmentIndex, from, mirrorPoint);
                    var normal = mirrorSurface.Normal;
                    if (Vector2.Dot(direction, normal) > 0f) normal = -normal;
                    direction = Vector2.Reflect(direction, normal).normalized;
                    from = mirrorPoint + direction * 0.01f;
                    continue;
                }

                Draw(segmentIndex, from, blockerHit.collider
                    ? blockerHit.point
                    : from + direction * maxBeamDistance);
                break;
            }

            if (receiver) receiver.SetLit(connected);
        }

        RaycastHit2D FindFirstBlockingHit(Vector2 origin, Vector2 direction)
        {
            var hits = Physics2D.RaycastAll(origin, direction, maxBeamDistance, collisionMask);
            foreach (var hit in hits)
            {
                if (!hit.collider || hit.distance <= 0.001f || hit.collider.isTrigger) continue;

                return hit;
            }
            return default;
        }

        bool TryFindNearestMirror(Vector2 origin, Vector2 direction, float maximumDistance,
            out MirrorSurfaceLine2D nearestSurface, out Vector2 nearestPoint, out float nearestDistance)
        {
            nearestSurface = null;
            nearestPoint = default;
            nearestDistance = maximumDistance;
            if (mirrors == null) return false;

            foreach (var mirror in mirrors)
            {
                var surface = mirror ? mirror.ReflectionSurface : null;
                if (!surface || !TryIntersectRaySegment(origin, direction,
                        surface.WorldPointA, surface.WorldPointB, out var point, out var distance))
                    continue;
                if (distance <= 0.005f || distance >= nearestDistance) continue;

                nearestSurface = surface;
                nearestPoint = point;
                nearestDistance = distance;
            }
            return nearestSurface;
        }

        static bool TryIntersectRaySegment(Vector2 origin, Vector2 rayDirection,
            Vector2 segmentA, Vector2 segmentB, out Vector2 point, out float distance)
        {
            point = default;
            distance = 0f;
            var segmentDirection = segmentB - segmentA;
            var denominator = Cross(rayDirection, segmentDirection);
            if (Mathf.Abs(denominator) < 0.00001f) return false;

            var offset = segmentA - origin;
            distance = Cross(offset, segmentDirection) / denominator;
            var segmentPosition = Cross(offset, rayDirection) / denominator;
            if (distance < 0f || segmentPosition < 0f || segmentPosition > 1f) return false;

            point = origin + rayDirection * distance;
            return true;
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        bool TryHitReceiver(Vector2 origin, Vector2 direction, float maximumDistance,
            out Vector2 receiverPoint)
        {
            return receiver.TryReceiveBeam(origin, direction, maximumDistance, out receiverPoint);
        }

        void Draw(int index, Vector3 from, Vector3 to)
        {
            if (index >= 0 && index < beamViews.Count)
            {
                beamViews[index].SetEndpoints(from, to);
                beamViews[index].SetVisible(true);
                return;
            }
            if (index < 0 || index >= beams.Count) return;
            beams[index].enabled = true;
            beams[index].SetPosition(0, from);
            beams[index].SetPosition(1, to);
        }
    }
}
