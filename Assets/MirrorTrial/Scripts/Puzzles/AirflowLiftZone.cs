using MirrorTrial.Player;
using UnityEngine;

namespace MirrorTrial.Puzzles
{
    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class AirflowLiftZone : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] float liftSpeed = 7.2f;
        [SerializeField, Min(0f)] float horizontalDamping = 1.5f;
        [SerializeField] ParticleSystem airflowParticles;

        BoxCollider2D trigger;

        void Awake()
        {
            trigger = GetComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            if (!airflowParticles) airflowParticles = GetComponentInChildren<ParticleSystem>(true);
        }

        void OnEnable()
        {
            if (airflowParticles) airflowParticles.Play(true);
        }

        void OnTriggerStay2D(Collider2D other)
        {
            var body = other.attachedRigidbody;
            if (!body || !other.GetComponentInParent<PlayerMotor>()) return;
            var velocity = body.velocity;
            velocity.y = Mathf.Max(velocity.y, liftSpeed);
            velocity.x = Mathf.MoveTowards(velocity.x, 0f, horizontalDamping * Time.fixedDeltaTime);
            body.velocity = velocity;
        }

        void Reset()
        {
            trigger = GetComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(1.6f, 5.5f);
        }
    }
}
