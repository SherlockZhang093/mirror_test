using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BossHealthBarUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image fillImage;          // Use Image Type = Filled
    [SerializeField] private Image hitFlashImage;      // Optional overlay
    [SerializeField] private TMP_Text bossNameText;    // Optional
    [SerializeField] private GameObject root;          // Optional show/hide root

    [Header("Settings")]
    [SerializeField] private float hitFlashDuration = 0.08f;

    private float currentHp;
    private float maxHp = 1f;

    public void Initialize(string bossName, float maxHealth)
    {
        maxHp = Mathf.Max(1f, maxHealth);
        currentHp = maxHp;

        if (bossNameText != null)
            bossNameText.text = bossName;

        SetInstant(currentHp, maxHp);

        if (root != null)
            root.SetActive(true);
    }

    public void SetInstant(float hp, float maxHealth)
    {
        maxHp = Mathf.Max(1f, maxHealth);
        currentHp = Mathf.Clamp(hp, 0f, maxHp);

        if (fillImage != null)
            fillImage.fillAmount = currentHp / maxHp;
    }

    public void SetHealth(float hp)
    {
        float oldHp = currentHp;
        currentHp = Mathf.Clamp(hp, 0f, maxHp);

        if (fillImage != null)
            fillImage.fillAmount = currentHp / maxHp;

        if (currentHp < oldHp && hitFlashImage != null)
            StartCoroutine(PlayHitFlash());
    }

    public void HideBar()
    {
        if (root != null)
            root.SetActive(false);
    }

    private IEnumerator PlayHitFlash()
    {
        hitFlashImage.gameObject.SetActive(true);
        yield return new WaitForSecondsRealtime(hitFlashDuration);
        hitFlashImage.gameObject.SetActive(false);
    }
}
