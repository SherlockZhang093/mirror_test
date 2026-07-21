using Cinemachine;
using UnityEngine;

namespace MirrorTrial.Level
{
    [ExecuteAlways]
    public sealed class CinemachineHorizontalConfiner : CinemachineExtension
    {
        [SerializeField] float minimumX;
        [SerializeField] float maximumX;
        [SerializeField] bool hasBounds;

        public void SetBounds(float minX, float maxX)
        {
            minimumX = Mathf.Min(minX, maxX);
            maximumX = Mathf.Max(minX, maxX);
            hasBounds = true;
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

            var halfWidth = state.Lens.Orthographic
                ? state.Lens.OrthographicSize * state.Lens.Aspect
                : 0f;
            var minCenter = minimumX + halfWidth;
            var maxCenter = maximumX - halfWidth;
            var currentX = state.CorrectedPosition.x;
            var desiredX = minCenter <= maxCenter
                ? Mathf.Clamp(currentX, minCenter, maxCenter)
                : (minimumX + maximumX) * 0.5f;

            state.PositionCorrection += new Vector3(desiredX - currentX, 0f, 0f);
        }
    }
}
