using UnityEngine;

namespace MirrorTrial.Level
{
    [CreateAssetMenu(menuName = "Mirror Trial/Camera Tuning", fileName = "CameraTuning")]
    public sealed class CameraTuning : ScriptableObject
    {
        [Header("Player Follow")]
        public Vector3 playerTrackedOffset = new Vector3(0f, 1.25f, 0f);
        [Min(0f)] public float playerLookaheadTime = 0.12f;
        [Min(0f)] public float playerLookaheadSmoothing = 0.15f;
        [Min(0f)] public float playerXDamping = 0.22f;
        [Min(0f)] public float playerYDamping = 0.32f;
        [Range(0f, 1f)] public float playerScreenX = 0.5f;
        [Range(0f, 1f)] public float playerScreenY = 0.45f;
        [Range(0f, 2f)] public float playerDeadZoneWidth = 0.08f;
        [Range(0f, 2f)] public float playerDeadZoneHeight = 0.12f;
        [Range(0f, 2f)] public float playerSoftZoneWidth = 0.8f;
        [Range(0f, 2f)] public float playerSoftZoneHeight = 0.72f;

        [Header("Camera Shake")]
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
