using System;
using UnityEngine;

namespace MirrorTrial.Combat
{
    [Serializable]
    public sealed class SkillHitFeedbackSettings
    {
        public bool enableHitEffect = true;
        public GameObject hitEffectPrefab;
        public Vector2 hitEffectOffset;
        public bool mirrorHitEffectByDirection = true;
        [Min(0.01f)] public float hitEffectLifetime = 0.15f;

        public bool enableHitStop = true;
        public bool useAttackTypeDefaultHitStop = true;
        [Min(0f)] public float hitStopDuration = 0.035f;

        public bool enableCameraFeedback;
        [Range(0f, 1f)] public float cameraPower = 0.35f;

        public bool enableHitSound;
        public AudioClip hitSound;
        [Range(0f, 1f)] public float hitSoundVolume = 1f;

        public bool notifyPlayerReaction = true;

        public HitFeedbackRequest BuildRequest(SkillAttackType attackType)
        {
            var resolvedHitStop = useAttackTypeDefaultHitStop
                ? (attackType == SkillAttackType.Heavy ? 0.07f : 0.035f)
                : hitStopDuration;
            return new HitFeedbackRequest(enableHitEffect, hitEffectPrefab, hitEffectOffset,
                mirrorHitEffectByDirection, hitEffectLifetime, enableHitStop, resolvedHitStop,
                enableCameraFeedback, cameraPower, enableHitSound, hitSound, hitSoundVolume,
                notifyPlayerReaction);
        }
    }
}
