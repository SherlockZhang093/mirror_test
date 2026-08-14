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
        GameObject requiredTarget;

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

        public static BowArrowProjectile Create(GameObject projectilePrefab, Vector3 position)
        {
            if (!projectilePrefab) return Create(position);
            var instance = Instantiate(projectilePrefab, position, Quaternion.identity);
            var projectile = instance.GetComponent<BowArrowProjectile>();
            if (projectile) return projectile;
            Debug.LogError("[BowArrowProjectile] Projectile prefab is missing BowArrowProjectile: " +
                           projectilePrefab.name, projectilePrefab);
            Destroy(instance);
            return Create(position);
        }

        public void Launch(DamagePayload nextPayload, Vector2 nextDirection, float nextSpeed, float nextRange)
        {
            Launch(nextPayload, nextDirection, nextSpeed, nextRange, null);
        }

        public void Launch(DamagePayload nextPayload, Vector2 nextDirection, float nextSpeed, float nextRange,
            GameObject onlyDamageTarget)
        {
            payload = nextPayload;
            direction = nextDirection.normalized;
            speed = nextSpeed;
            range = nextRange;
            origin = transform.position;
            requiredTarget = onlyDamageTarget;
            // Arrow prefabs are authored pointing along local +X. Keep the visual aligned with
            // the actual launch vector so aerial, fan and rain arrows no longer fly sideways.
            if (direction.sqrMagnitude > 0f)
                transform.rotation = Quaternion.Euler(0f, 0f,
                    Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }

        void Update()
        {
            var currentPosition = (Vector2)transform.position;
            var nextPosition = currentPosition + direction * speed * Time.deltaTime;
            var groundHit = Physics2D.Linecast(currentPosition, nextPosition, LayerMask.GetMask("Ground"));
            if (groundHit.collider)
            {
                transform.position = groundHit.point;
                Destroy(gameObject);
                return;
            }

            transform.position = nextPosition;
            if (Vector3.Distance(origin, transform.position) >= range) Destroy(gameObject);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (payload.source && other.gameObject == payload.source) return;
            if (requiredTarget && other.gameObject != requiredTarget && !other.transform.IsChildOf(requiredTarget.transform))
                return;
            var hurtbox = other.GetComponent<Hurtbox>();
            if (!hurtbox) return;
            var allowsHitFeedback = hurtbox.ReceiveHit(payload);
            if (allowsHitFeedback)
            {
                MirrorTrial.Player.PlayerAudioFeedback.PlaySharedArrowHit();
                HitStopService.Request(payload.hitStop);
                CameraFeedbackService.RequestHit(payload);
            }
            Destroy(gameObject);
        }
    }
}

