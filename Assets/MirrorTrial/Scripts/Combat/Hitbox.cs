using MirrorTrial.Feedback;
using UnityEngine;

namespace MirrorTrial.Combat
{
    [RequireComponent(typeof(Collider2D))]
    public class Hitbox : MonoBehaviour
    {
        [SerializeField] Collider2D hitboxCollider;

        DamagePayload payload;

        void Awake()
        {
            if (!hitboxCollider)
                hitboxCollider = GetComponent<Collider2D>();

            hitboxCollider.isTrigger = true;
        }

        public void Configure(DamagePayload nextPayload)
        {
            payload = nextPayload;
        }

        public void SetActive(bool active)
        {
            if (hitboxCollider)
                hitboxCollider.enabled = active;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (payload.source && other.gameObject == payload.source)
                return;

            var hurtbox = other.GetComponent<Hurtbox>();
            if (!hurtbox)
                return;

            hurtbox.ReceiveHit(payload);
            HitStopService.Request(payload.hitStop);
            CameraFeedbackService.RequestHit(payload);
        }
    }
}

