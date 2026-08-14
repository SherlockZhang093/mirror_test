using UnityEngine;

namespace MirrorTrial.Level
{
    public class CameraFollow2D : MonoBehaviour
    {
        static CameraFollow2D main;

        [SerializeField, InspectorName("跟随目标")] Transform target;
        [SerializeField, InspectorName("构图偏移")] Vector2 offset = new Vector2(0f, 1f);
        [SerializeField, Min(0f), InspectorName("跟随平滑时间")] float smoothTime = 0.18f;
        [SerializeField, Range(0f, 4f), InspectorName("横向提前量")] float lookAheadDistance = 1.2f;
        [SerializeField, Min(0f), InspectorName("提前量平滑时间")] float lookAheadSmoothTime = 0.16f;
        [SerializeField, InspectorName("镜头死区")] Vector2 deadZone = new Vector2(0.35f, 1.1f);
        [SerializeField, InspectorName("跟随横向")] bool followX = true;
        [SerializeField, InspectorName("跟随纵向")] bool followY = true;
        [SerializeField, InspectorName("限制横向边界")] bool clampHorizontal = true;
        [SerializeField, InspectorName("相机 Z 轴位置")] float cameraZ = -10f;
        Vector3 velocity;
        Vector2 lookAhead;
        Vector2 lookAheadVelocity;
        Vector3 lastTargetPosition;
        Camera attachedCamera;
        bool hasHorizontalBounds;
        float horizontalMin;
        float horizontalMax;

        public static CameraFollow2D Main => main ? main : Camera.main ? Camera.main.GetComponent<CameraFollow2D>() : null;
        public Transform Target => target;

        void Awake()
        {
            attachedCamera = GetComponent<Camera>();
            if (!main || attachedCamera && attachedCamera.CompareTag("MainCamera"))
                main = this;
            if (target)
                lastTargetPosition = target.position;
        }

        void OnDestroy()
        {
            if (main == this)
                main = null;
        }

        public void SetTarget(Transform nextTarget, bool snap = true)
        {
            target = nextTarget;
            velocity = Vector3.zero;
            lookAhead = Vector2.zero;
            lookAheadVelocity = Vector2.zero;
            lastTargetPosition = target ? target.position : transform.position;
            if (snap)
                SnapToTarget();
        }

        public void SetHorizontalBounds(float minX, float maxX, bool snap = true)
        {
            horizontalMin = Mathf.Min(minX, maxX);
            horizontalMax = Mathf.Max(minX, maxX);
            hasHorizontalBounds = true;
            if (snap)
                SnapToTarget();
        }

        void LateUpdate()
        {
            if (!target) return;

            UpdateLookAhead();
            var desired = GetDesiredPosition();
            transform.position = smoothTime <= 0f
                ? desired
                : Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        }

        void SnapToTarget()
        {
            if (target)
                transform.position = GetDesiredPosition();
        }

        Vector3 GetDesiredPosition()
        {
            var current = transform.position;
            var targetPosition = target.position;
            var baseX = targetPosition.x + offset.x;
            var baseY = targetPosition.y + offset.y;
            var x = followX ? baseX + lookAhead.x : current.x;
            var y = followY ? baseY + lookAhead.y : current.y;

            if (followX && deadZone.x > 0f && Mathf.Abs(baseX - current.x) <= deadZone.x)
                x = current.x;
            if (followY && deadZone.y > 0f && Mathf.Abs(baseY - current.y) <= deadZone.y)
                y = current.y;

            if (clampHorizontal && hasHorizontalBounds)
                x = ClampCameraCenterX(x);

            return new Vector3(x, y, cameraZ);
        }

        void UpdateLookAhead()
        {
            var targetPosition = target.position;
            var delta = targetPosition - lastTargetPosition;
            var desiredLookAhead = Vector2.zero;
            if (Mathf.Abs(delta.x) > 0.0001f)
                desiredLookAhead.x = Mathf.Sign(delta.x) * lookAheadDistance;
            lookAhead = Vector2.SmoothDamp(lookAhead, desiredLookAhead, ref lookAheadVelocity, lookAheadSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
            lastTargetPosition = targetPosition;
        }

        float ClampCameraCenterX(float desiredX)
        {
            if (!attachedCamera)
                attachedCamera = GetComponent<Camera>();

            var halfWidth = attachedCamera && attachedCamera.orthographic
                ? attachedCamera.orthographicSize * attachedCamera.aspect
                : 0f;
            var minCenter = horizontalMin + halfWidth;
            var maxCenter = horizontalMax - halfWidth;

            return minCenter <= maxCenter
                ? Mathf.Clamp(desiredX, minCenter, maxCenter)
                : (horizontalMin + horizontalMax) * 0.5f;
        }
    }
}
