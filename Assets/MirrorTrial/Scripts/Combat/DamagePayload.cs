using UnityEngine;

namespace MirrorTrial.Combat
{
    public enum HitReactionType
    {
        None,
        LightHurt,
        HeavyHurt,
        Launch,
        Knockdown,
        Stunned,
        ShockLight,
        ShockHeavy
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

        public DamagePayload(GameObject source, int damage, Vector2 knockback, Vector2 direction, float hitStop,
            int interruptPower = 1, float poiseDamage = 1f,
            HitReactionType reaction = HitReactionType.LightHurt, bool breaksSuperArmor = false)
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
        }
    }
}
