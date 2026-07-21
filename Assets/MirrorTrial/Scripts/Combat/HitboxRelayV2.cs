using MirrorTrial.Feedback;
using UnityEngine;

namespace MirrorTrial.Combat
{
    // A collision may report a child collider rather than the character root.
    // This relay keeps the existing Hitbox component untouched while making
    // damage resolution work for both player and mirror boss hierarchies.
    [RequireComponent(typeof(Collider2D))]
    public sealed class HitboxRelayV2 : MonoBehaviour
    {
        [SerializeField] Collider2D hitboxCollider;
        DamagePayload payload;

        void Awake()
        {
            if (!hitboxCollider) hitboxCollider = GetComponent<Collider2D>();
            if (hitboxCollider) hitboxCollider.isTrigger = true;
        }

        public void Configure(DamagePayload nextPayload) => payload = nextPayload;

        public void SetActive(bool active)
        {
            if (hitboxCollider) hitboxCollider.enabled = active;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (payload.source && other.transform.root.gameObject == payload.source) return;
            var hurtbox = other.GetComponent<Hurtbox>() ?? other.GetComponentInParent<Hurtbox>();
            if (!hurtbox) return;
            hurtbox.ReceiveHit(payload);
            HitStopService.Request(payload.hitStop);
            CameraFeedbackService.RequestHit(payload);
        }
    }
}
