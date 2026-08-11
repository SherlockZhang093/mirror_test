using UnityEngine;

namespace MirrorTrial
{
    /// <summary>Keeps presentation timing consistent across displays and quality levels.</summary>
    public static class FrameRateBootstrap
    {
        public const int TargetFrameRate = 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ApplyFrameRateLimit()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFrameRate;
        }
    }
}
