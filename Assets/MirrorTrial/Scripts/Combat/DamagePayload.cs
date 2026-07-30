using UnityEngine;

namespace MirrorTrial.Combat
{
    public enum SkillAttackType
    {
        [InspectorName("\u666e\u901a\u653b\u51fb")] Normal,
        [InspectorName("\u91cd\u51fb")] Heavy
    }

    public enum HitReactionType
    {
        [InspectorName("\u65e0\u53d7\u51fb\u53cd\u5e94")] None,
        [InspectorName("\u8f7b\u53d7\u51fb")] LightHurt,
        [InspectorName("\u91cd\u53d7\u51fb")] HeavyHurt,
        [InspectorName("\u51fb\u98de")] Launch,
        [InspectorName("\u51fb\u5012")] Knockdown,
        [InspectorName("\u7729\u6655")] Stunned,
        [InspectorName("\u8f7b\u611f\u7535")] ShockLight,
        [InspectorName("\u91cd\u611f\u7535")] ShockHeavy
    }

    public struct HitFeedbackRequest
    {
        public readonly bool playEffect;
        public readonly GameObject effectPrefab;
        public readonly Vector2 effectOffset;
        public readonly bool mirrorEffectByDirection;
        public readonly float effectLifetime;
        public readonly bool requestHitStop;
        public readonly float hitStopDuration;
        public readonly bool requestCamera;
        public readonly float cameraPower;
        public readonly bool playSound;
        public readonly AudioClip sound;
        public readonly float soundVolume;
        public readonly bool notifyAttackerReaction;

        public HitFeedbackRequest(bool playEffect, GameObject effectPrefab, Vector2 effectOffset,
            bool mirrorEffectByDirection, float effectLifetime, bool requestHitStop, float hitStopDuration,
            bool requestCamera, float cameraPower, bool playSound, AudioClip sound, float soundVolume,
            bool notifyAttackerReaction)
        {
            this.playEffect = playEffect;
            this.effectPrefab = effectPrefab;
            this.effectOffset = effectOffset;
            this.mirrorEffectByDirection = mirrorEffectByDirection;
            this.effectLifetime = Mathf.Max(0.01f, effectLifetime);
            this.requestHitStop = requestHitStop;
            this.hitStopDuration = Mathf.Max(0f, hitStopDuration);
            this.requestCamera = requestCamera;
            this.cameraPower = Mathf.Clamp01(cameraPower);
            this.playSound = playSound && sound;
            this.sound = sound;
            this.soundVolume = Mathf.Clamp01(soundVolume);
            this.notifyAttackerReaction = notifyAttackerReaction;
        }
    }

    public struct DamagePayload
    {
        public readonly GameObject source;
        public readonly int damage;
        public readonly Vector2 knockback;
        public readonly Vector2 direction;
        public readonly float hitStop;
        public readonly int interruptPower;
        public readonly float poiseDamage;
        public readonly HitReactionType reaction;
        public readonly bool breaksSuperArmor;
        public readonly SkillAttackType attackType;
        public readonly HitFeedbackRequest feedback;

        public DamagePayload(GameObject source, int damage, Vector2 knockback, Vector2 direction, float hitStop,
            int interruptPower = 1, float poiseDamage = 1f,
            HitReactionType reaction = HitReactionType.LightHurt, bool breaksSuperArmor = false,
            SkillAttackType attackType = SkillAttackType.Normal,
            HitFeedbackRequest feedback = default(HitFeedbackRequest))
        {
            this.source = source;
            this.damage = Mathf.Max(0, damage);
            this.knockback = knockback;
            this.direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.right;
            this.hitStop = Mathf.Max(0f, hitStop);
            this.interruptPower = Mathf.Max(0, interruptPower);
            this.poiseDamage = Mathf.Max(0f, poiseDamage);
            this.reaction = reaction;
            this.breaksSuperArmor = breaksSuperArmor;
            this.attackType = attackType;
            this.feedback = feedback;
        }
    }
}
