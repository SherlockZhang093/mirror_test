using MirrorTrial.Player;
using UnityEngine;

namespace MirrorTrial.Puzzles
{
    [DisallowMultipleComponent]
    public sealed class DoubleJumpAbilityPickup : MonoBehaviour
    {
        [SerializeField] ParticleSystem collectEffect;
        [SerializeField] AudioSource collectAudio;

        bool collected;

        void OnTriggerEnter2D(Collider2D other)
        {
            if (collected) return;
            var tuning = other.GetComponentInParent<PlayerTuning>();
            if (!tuning) return;

            collected = true;
            tuning.abilities.doubleJumpUnlocked = true;
            if (collectEffect)
            {
                collectEffect.transform.SetParent(null, true);
                collectEffect.Play();
            }
            if (collectAudio) collectAudio.Play();
            gameObject.SetActive(false);
        }
    }
}
