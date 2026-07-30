using MirrorTrial.HealthResources;
using UnityEngine;
using UnityEngine.Events;

namespace MirrorTrial.Combat
{
    [RequireComponent(typeof(Collider2D))]
    public class Hurtbox : MonoBehaviour
    {
        public UnityEvent onHit;

        public void ReceiveHit(DamagePayload payload)
        {
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
            onHit.Invoke();
        }
    }
}
