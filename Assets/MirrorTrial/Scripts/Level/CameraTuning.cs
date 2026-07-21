using UnityEngine;

namespace MirrorTrial.Level
{
    [CreateAssetMenu(menuName = "Mirror Trial/Camera Tuning", fileName = "CameraTuning")]
    public sealed class CameraTuning : ScriptableObject
    {
        [Header("Hit Shake")]
        public float lightHitStrength = 0.07f;
        public float heavyHitStrength = 0.3f;
        public float impulseGain = 0.7f;
        public float impulseDuration = 0.11f;

        [Header("Boss Intro")]
        public float bossBlendIn = 0.55f;
        public float bossHold = 1.15f;
        public float bossBlendOut = 0.5f;
        public float bossOrthographicSize = 3.25f;
        public Vector3 bossTargetOffset = new Vector3(0f, 1f, 0f);
    }
}
