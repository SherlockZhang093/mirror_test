using System.Collections;
using UnityEngine;

namespace MirrorTrial.Puzzles.MirrorGate
{
    [DisallowMultipleComponent]
    public sealed class StoneGate2D : MonoBehaviour
    {
        [SerializeField] Transform gateBody;
        [SerializeField] Collider2D gateCollider;
        [SerializeField] Vector3 closedLocalPosition;
        [SerializeField] Vector3 openLocalPosition = new Vector3(3.2f, 0f, 0f);
        [SerializeField, Min(0.01f)] float openDuration = 0.6f;
        [SerializeField] AnimationCurve openCurve = null;
        [SerializeField, Range(0f, 1f)] float disableColliderProgress = 0.9f;
        [SerializeField, Min(0f)] float preOpenShakeDuration = 0.16f;
        [SerializeField, Min(0f)] float preOpenShakeDistance = 0.05f;
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip openSound;
        [SerializeField, Range(0f, 1f)] float openVolume = 1f;
        [SerializeField] ParticleSystem dustEffect;

        bool opened;
        bool opening;

        public bool IsOpen => opened;
        public bool IsOpening => opening;

        void Awake()
        {
            if (!gateBody) gateBody = transform;
            if (!gateCollider) gateCollider = gateBody.GetComponent<Collider2D>();
            if (openCurve == null || openCurve.length == 0)
                openCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            gateBody.localPosition = closedLocalPosition;
            if (gateCollider) gateCollider.enabled = true;
        }

        public bool Open()
        {
            if (opened || opening) return false;
            StartCoroutine(OpenRoutine());
            return true;
        }

        IEnumerator OpenRoutine()
        {
            opening = true;
            if (audioSource && openSound) audioSource.PlayOneShot(openSound, openVolume);
            if (dustEffect) dustEffect.Play();

            var basePosition = closedLocalPosition;
            var shakeElapsed = 0f;
            while (shakeElapsed < preOpenShakeDuration)
            {
                shakeElapsed += Time.unscaledDeltaTime;
                var fade = 1f - Mathf.Clamp01(shakeElapsed / Mathf.Max(0.01f, preOpenShakeDuration));
                gateBody.localPosition = basePosition + Vector3.right *
                    (Mathf.Sin(shakeElapsed * 90f) * preOpenShakeDistance * fade);
                yield return null;
            }

            var elapsed = 0f;
            while (elapsed < openDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / openDuration);
                gateBody.localPosition = Vector3.LerpUnclamped(closedLocalPosition, openLocalPosition,
                    openCurve.Evaluate(t));
                if (gateCollider && t >= disableColliderProgress) gateCollider.enabled = false;
                yield return null;
            }

            gateBody.localPosition = openLocalPosition;
            if (gateCollider) gateCollider.enabled = false;
            opening = false;
            opened = true;
        }
    }
}
