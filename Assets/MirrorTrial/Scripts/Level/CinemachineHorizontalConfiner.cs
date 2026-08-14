using Cinemachine;
using UnityEngine;

namespace MirrorTrial.Level
{
    [ExecuteAlways]
    public sealed class CinemachineHorizontalConfiner : CinemachineExtension
    {
        [SerializeField] Rect visibleArea;
        [SerializeField] bool hasBounds;

        public void SetVisibleArea(Rect area)
        {
            visibleArea = Rect.MinMaxRect(
                Mathf.Min(area.xMin, area.xMax), Mathf.Min(area.yMin, area.yMax),
                Mathf.Max(area.xMin, area.xMax), Mathf.Max(area.yMin, area.yMax));
            hasBounds = true;
        }

        public void SetBounds(float minX, float maxX)
        {
            SetVisibleArea(Rect.MinMaxRect(minX, -10000f, maxX, 10000f));
        }

        public void ClearBounds()
        {
            hasBounds = false;
        }

        protected override void PostPipelineStageCallback(
            CinemachineVirtualCameraBase vcam,
            CinemachineCore.Stage stage,
            ref CameraState state,
            float deltaTime)
        {
            if (!hasBounds || stage != CinemachineCore.Stage.Body) return;

            if (!state.Lens.Orthographic) return;
            var halfHeight = state.Lens.OrthographicSize;
            var halfWidth = halfHeight * state.Lens.Aspect;
            var minCenterX = visibleArea.xMin + halfWidth;
            var maxCenterX = visibleArea.xMax - halfWidth;
            var minCenterY = visibleArea.yMin + halfHeight;
            var maxCenterY = visibleArea.yMax - halfHeight;
            var current = state.CorrectedPosition;
            var desiredX = minCenterX <= maxCenterX ? Mathf.Clamp(current.x, minCenterX, maxCenterX) : visibleArea.center.x;
            var desiredY = minCenterY <= maxCenterY ? Mathf.Clamp(current.y, minCenterY, maxCenterY) : visibleArea.center.y;
            state.PositionCorrection += new Vector3(desiredX - current.x, desiredY - current.y, 0f);
        }
    }
}
