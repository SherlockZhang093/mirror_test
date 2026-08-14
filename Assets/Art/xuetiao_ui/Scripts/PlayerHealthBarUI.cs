using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHealthBarUI : MonoBehaviour
{
    [Header("5 Life Blocks, left to right")]
    [SerializeField] private Image[] fills = new Image[5];
    [SerializeField] private Image[] hitFlashes = new Image[5];

    [Header("Optional sprites")]
    [SerializeField] private Sprite normalFill;
    [SerializeField] private Sprite lowFill;

    [Header("FX")]
    [SerializeField] private float hitFlashDuration = 0.08f;
    [SerializeField] private int lowHealthThreshold = 1;

    private int currentHealth = 5;

    public void SetHealth(int value)
    {
        value = Mathf.Clamp(value, 0, 5);
        currentHealth = value;

        for (int i = 0; i < fills.Length; i++)
        {
            bool alive = i < currentHealth;
            fills[i].enabled = alive;

            if (alive && normalFill != null)
            {
                fills[i].sprite =
                    currentHealth <= lowHealthThreshold && lowFill != null
                    ? lowFill
                    : normalFill;
            }
        }
    }

    public void TakeDamage(int newHealth)
    {
        int lostIndex = Mathf.Clamp(currentHealth - 1, 0, 4);
        SetHealth(newHealth);

        if (hitFlashes != null &&
            lostIndex < hitFlashes.Length &&
            hitFlashes[lostIndex] != null)
        {
            StartCoroutine(PlayHitFlash(hitFlashes[lostIndex]));
        }
    }

    private IEnumerator PlayHitFlash(Image flash)
    {
        flash.gameObject.SetActive(true);
        yield return new WaitForSecondsRealtime(hitFlashDuration);
        flash.gameObject.SetActive(false);
    }
}
