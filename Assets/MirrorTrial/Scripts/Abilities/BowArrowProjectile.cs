using MirrorTrial.Combat;
using MirrorTrial.Feedback;
using UnityEngine;

namespace MirrorTrial.Abilities
{
    public sealed class BowArrowProjectile : MonoBehaviour
    {
        DamagePayload payload;
        Vector2 direction;
        float speed;
        float range;
        Vector3 origin;

        public static BowArrowProjectile Create(Vector3 position)
        {
            var arrow = new GameObject("BowArrow");
            arrow.transform.position = position;
            var collider = arrow.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(0.45f, 0.08f);
            var body = arrow.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            var line = arrow.AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.useWorldSpace = false;
            line.SetPosition(0, new Vector3(-0.22f, 0f, 0f));
            line.SetPosition(1, new Vector3(0.22f, 0f, 0f));
            line.startWidth = 0.045f;
            line.endWidth = 0.025f;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = new Color(0.9f, 0.75f, 0.35f, 1f);
            line.endColor = Color.white;
            return arrow.AddComponent<BowArrowProjectile>();
        }

        public void Launch(DamagePayload nextPayload, Vector2 nextDirection, float nextSpeed, float nextRange)
        {
            payload = nextPayload;
            direction = nextDirection.normalized;
            speed = nextSpeed;
            range = nextRange;
            origin = transform.position;
            if (direction.x < 0f) transform.localScale = new Vector3(-1f, 1f, 1f);
        }

        void Update()
        {
            transform.position += (Vector3)(direction * speed * Time.deltaTime);
            if (Vector3.Distance(origin, transform.position) >= range) Destroy(gameObject);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (payload.source && other.gameObject == payload.source) return;
            var hurtbox = other.GetComponent<Hurtbox>();
            if (!hurtbox) return;
            hurtbox.ReceiveHit(payload);
            HitStopService.Request(payload.hitStop);
            Destroy(gameObject);
        }
    }
}
