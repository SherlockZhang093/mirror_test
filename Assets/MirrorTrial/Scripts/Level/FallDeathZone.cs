using MirrorTrial.Player;
using UnityEngine;

namespace MirrorTrial.Level
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class FallDeathZone : MonoBehaviour
    {
        BoxCollider2D deathTrigger;

        void Reset()
        {
            ConfigureTrigger();
        }

        void OnValidate()
        {
            ConfigureTrigger();
        }

        void Awake()
        {
            ConfigureTrigger();
        }

        void ConfigureTrigger()
        {
            deathTrigger = GetComponent<BoxCollider2D>();
            if (deathTrigger)
                deathTrigger.isTrigger = true;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            var receiver = other.GetComponentInParent<PlayerDamageReceiver>();
            if (receiver)
                receiver.KillAndRespawn();
        }
    }
}
