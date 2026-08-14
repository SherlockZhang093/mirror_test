using MirrorTrial.Combat;
using UnityEngine;

namespace MirrorTrial.Level
{
    [RequireComponent(typeof(Hurtbox))]
    public class TrainingEnemy : MonoBehaviour
    {
        [SerializeField, Min(1)] int hitPoints = 2;

        int remainingHitPoints;

        void Awake()
        {
            remainingHitPoints = hitPoints;
        }

        void OnDamagePayloadReceived(DamagePayload payload)
        {
            remainingHitPoints--;
            if (remainingHitPoints <= 0)
                Destroy(gameObject);
        }
    }
}
