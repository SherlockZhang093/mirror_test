using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MirrorTrial.Player
{
    public sealed class PlayerHealthBarView : MonoBehaviour
    {
        [Header("5 Life Blocks, left to right")]
        [SerializeField] Image[] fills = new Image[5];
        [SerializeField] Image[] hitFlashes = new Image[5];

        [Header("Optional sprites")]
        [SerializeField] Sprite normalFill;
        [SerializeField] Sprite lowFill;

        [Header("FX")]
        [SerializeField] float hitFlashDuration = 0.08f;
        [SerializeField] int lowHealthThreshold = 1;

        int previousHealth = -1;

        public void SetHealth(int currentHP, int maxHP)
        {
            var current = Mathf.Clamp(currentHP, 0, fills.Length);
            var wasInitialized = previousHealth >= 0;
            var oldHealth = wasInitialized ? previousHealth : current;
            previousHealth = current;

            for (var i = 0; i < fills.Length; i++)
            {
                var fill = fills[i];
                if (!fill) continue;

                var alive = i < current;
                fill.enabled = alive;

                if (alive && normalFill)
                    fill.sprite = current <= lowHealthThreshold && lowFill ? lowFill : normalFill;
            }

            if (wasInitialized && current < oldHealth)
                PlayLostHealthFlash(current, oldHealth);
        }

        void PlayLostHealthFlash(int current, int oldHealth)
        {
            for (var i = current; i < oldHealth && i < hitFlashes.Length; i++)
            {
                var flash = hitFlashes[i];
                if (flash != null)
                    StartCoroutine(PlayHitFlash(flash));
            }
        }

        IEnumerator PlayHitFlash(Image flash)
        {
            flash.gameObject.SetActive(true);
            yield return new WaitForSecondsRealtime(hitFlashDuration);
            if (flash != null)
                flash.gameObject.SetActive(false);
        }
    }
}