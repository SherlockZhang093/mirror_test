using MirrorTrial.HealthResources;
using UnityEngine;
using UnityEngine.Events;

namespace MirrorTrial.Combat
{
    public interface IHitFeedbackGate
    {
        bool AllowsHitFeedback(DamagePayload payload);
    }

    [RequireComponent(typeof(Collider2D))]
    public class Hurtbox : MonoBehaviour
    {
        public UnityEvent onHit;

        public bool ReceiveHit(DamagePayload payload)
        {
            var allowsHitFeedback = AllowsHitFeedback(payload);
            var healthResource = GetComponentInParent<HealthResourceNode>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (healthResource)
                Debug.Log(
                    $"[HealthResourceTrace][发送层] Hurtbox={name}, Node={healthResource.name}, " +
                    $"Source={(payload.source ? payload.source.name : "<null>")}, Damage={payload.damage}; " +
                    "即将 SendMessageUpwards(OnDamagePayloadReceived)。",
                    healthResource);
#endif
            SendMessageUpwards("OnDamagePayloadReceived", payload, SendMessageOptions.DontRequireReceiver);
            if (allowsHitFeedback)
                onHit.Invoke();
            return allowsHitFeedback;
        }

        bool AllowsHitFeedback(DamagePayload payload)
        {
            var behaviours = GetComponentsInParent<MonoBehaviour>(true);
            foreach (var behaviour in behaviours)
            {
                var gate = behaviour as IHitFeedbackGate;
                if (gate != null && !gate.AllowsHitFeedback(payload))
                    return false;
            }
            return true;
        }
    }
}
