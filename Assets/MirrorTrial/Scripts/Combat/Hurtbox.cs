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
            SendMessageUpwards("OnDamagePayloadReceived", payload, SendMessageOptions.DontRequireReceiver);
            onHit.Invoke();
        }
    }
}
