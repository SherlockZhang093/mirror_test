using UnityEngine;
using UnityEngine.UI;

namespace MirrorTrial.UI
{
    [DisallowMultipleComponent]
    public sealed class GameEntryLayerMotion : MonoBehaviour
    {
        [SerializeField] Vector2 driftAmplitude = new Vector2(4f, 7f);
        [SerializeField] float speed = 0.5f;
        [SerializeField] float phase;
        [SerializeField] float rotationAmplitude = 0.3f;
        [SerializeField] float scaleAmplitude = 0.01f;
        [SerializeField] float alphaAmplitude;

        RectTransform rect;
        Graphic graphic;
        Vector2 basePosition;
        Vector3 baseScale;
        float baseAlpha = 1f;

        public void Configure(Vector2 drift, float motionSpeed, float motionPhase,
            float rotation, float scale, float alpha)
        {
            driftAmplitude = drift;
            speed = Mathf.Max(0.01f, motionSpeed);
            phase = motionPhase;
            rotationAmplitude = rotation;
            scaleAmplitude = Mathf.Max(0f, scale);
            alphaAmplitude = Mathf.Max(0f, alpha);
            CaptureBaseState();
        }

        void Awake() => CaptureBaseState();
        void OnEnable() => CaptureBaseState();

        void CaptureBaseState()
        {
            rect = transform as RectTransform;
            if (!rect) return;
            graphic = GetComponentInChildren<Graphic>();
            basePosition = rect.anchoredPosition;
            baseScale = rect.localScale;
            if (graphic) baseAlpha = graphic.color.a;
        }

        void Update()
        {
            if (!rect) CaptureBaseState();
            if (!rect) return;

            var time = Time.unscaledTime * speed + phase;
            var horizontal = Mathf.Sin(time * 0.73f);
            var vertical = Mathf.Sin(time);
            rect.anchoredPosition = basePosition + new Vector2(horizontal * driftAmplitude.x,
                vertical * driftAmplitude.y);
            rect.localRotation = Quaternion.Euler(0f, 0f, vertical * rotationAmplitude);
            rect.localScale = baseScale * (1f + vertical * scaleAmplitude);

            if (!graphic || alphaAmplitude <= 0f) return;
            var color = graphic.color;
            color.a = Mathf.Clamp01(baseAlpha * (1f - alphaAmplitude +
                (vertical * 0.5f + 0.5f) * alphaAmplitude * 2f));
            graphic.color = color;
        }
    }
}
