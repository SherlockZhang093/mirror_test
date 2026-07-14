using UnityEngine;

namespace MirrorTrial.Level
{
    public class CameraFollow2D : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Vector2 offset = new Vector2(0f, 1f);
        [SerializeField, Min(0f)] float smoothTime = 0.18f;
        [SerializeField] bool followX = true;
        [SerializeField] bool followY = true;
        [SerializeField] bool clampHorizontal = true;

        Vector3 velocity;
        Camera attachedCamera;
        bool hasHorizontalBounds;
        float horizontalMin;
        float horizontalMax;

        public Transform Target => target;

        void Awake()
        {
            attachedCamera = GetComponent<Camera>();
        }

        public void SetTarget(Transform nextTarget, bool snap = true)
        {
            target = nextTarget;
            velocity = Vector3.zero;
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

            var desired = GetDesiredPosition();
            transform.position = smoothTime <= 0f
                ? desired
                : Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
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
            var x = followX ? targetPosition.x + offset.x : current.x;
            if (clampHorizontal && hasHorizontalBounds)
                x = ClampCameraCenterX(x);

            return new Vector3(
                x,
                followY ? targetPosition.y + offset.y : current.y,
                current.z);
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
