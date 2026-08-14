using UnityEngine;

namespace MirrorTrial.Puzzles.MirrorGate
{
    [DisallowMultipleComponent]
    public sealed class MirrorLightReceiver2D : MonoBehaviour
    {
        [SerializeField] Transform receiverPoint;
        [SerializeField] SpriteRenderer receiverRenderer;
        [SerializeField] Color inactiveColor = new Color(0.18f, 0.28f, 0.32f, 1f);
        [SerializeField] Color activeColor = new Color(0.2f, 1f, 1f, 1f);
        [SerializeField] GameObject activeEffect;
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip activatedSound;
        [SerializeField, Range(0f, 1f)] float activatedVolume = 0.9f;

        public bool IsActive { get; private set; }
        public Transform ReceiverPoint => receiverPoint ? receiverPoint : transform;

        void Awake()
        {
            IsActive = false;
            ApplyVisual(false);
        }

        public void Activate()
        {
            if (IsActive) return;
            IsActive = true;
            ApplyVisual(true);
            if (audioSource && activatedSound) audioSource.PlayOneShot(activatedSound, activatedVolume);
        }

        public void Deactivate()
        {
            if (!IsActive) return;
            IsActive = false;
            ApplyVisual(false);
        }

        void ApplyVisual(bool active)
        {
            if (receiverRenderer) receiverRenderer.color = active ? activeColor : inactiveColor;
            if (activeEffect) activeEffect.SetActive(active);
        }
    }
}
