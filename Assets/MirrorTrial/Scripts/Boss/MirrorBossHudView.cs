using UnityEngine;
using UnityEngine.UI;

namespace MirrorTrial.Boss
{
    public sealed class MirrorBossHudView : MonoBehaviour
    {
        [SerializeField] Text title;
        [SerializeField] Image healthFill;

        public void Show(string bossName, int phase, int current, int maximum)
        {
            gameObject.SetActive(true);
            SetTitle(bossName, phase);
            SetHealth(current, maximum);
        }

        public void SetTitle(string bossName, int phase)
        {
            if (!title) return;
            title.text = string.IsNullOrEmpty(bossName) ? "Mirror Boss" : bossName;
            title.text += "  " + PhaseLabel(phase);
        }

        public void SetHealth(int current, int maximum)
        {
            if (!healthFill) return;
            healthFill.fillAmount = maximum > 0 ? Mathf.Clamp01(current / (float)maximum) : 0f;
        }

        static string PhaseLabel(int phase)
        {
            switch (phase)
            {
                case 2: return "II";
                case 3: return "III";
                default: return "I";
            }
        }
    }
}