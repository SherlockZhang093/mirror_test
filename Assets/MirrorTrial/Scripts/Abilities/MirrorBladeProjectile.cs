using MirrorTrial.Combat;
using MirrorTrial.Feedback;
using UnityEngine;

namespace MirrorTrial.Abilities
{
    [RequireComponent(typeof(Collider2D))]
    public class MirrorBladeProjectile : MonoBehaviour
    {
        [SerializeField] Collider2D hitCollider;
        [SerializeField] SpriteRenderer spriteRenderer;

        DamagePayload payload;
        Vector2 direction = Vector2.right;
        float speed;
        float maxRange;
        Vector3 startPosition;
        bool active;

        void Awake()
        {
            if (!hitCollider)
                hitCollider = GetComponent<Collider2D>();
            if (!spriteRenderer)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            hitCollider.isTrigger = true;
        }

        public void Launch(DamagePayload nextPayload, Vector2 launchDirection, float projectileSpeed, float range)
        {
            payload = nextPayload;
            direction = launchDirection.sqrMagnitude > 0f ? launchDirection.normalized : Vector2.right;
            speed = projectileSpeed;
            maxRange = range;
            startPosition = transform.position;
            active = true;

            if (spriteRenderer)
                spriteRenderer.flipX = direction.x < 0f;
        }

        void Update()
        {
            if (!active)
                return;

            transform.position += (Vector3)(direction * speed * Time.deltaTime);

            if (Vector3.Distance(startPosition, transform.position) >= maxRange)
                Destroy(gameObject);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!active)
                return;
            if (payload.source && other.gameObject == payload.source)
                return;

            var hurtbox = other.GetComponent<Hurtbox>();
            if (!hurtbox)
                return;

            hurtbox.ReceiveHit(payload);
            if (payload.source)
                payload.source.SendMessage("OnMirrorBladeConnected", SendMessageOptions.DontRequireReceiver);
            HitStopService.Request(payload.hitStop);
            CameraFeedbackService.RequestHit(payload);
            Destroy(gameObject);
        }
    }
}


