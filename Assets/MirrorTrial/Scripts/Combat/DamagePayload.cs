using UnityEngine;

namespace MirrorTrial.Combat
{
    public struct DamagePayload
    {
        public readonly GameObject source;
        public readonly int damage;
        public readonly Vector2 knockback;
        public readonly Vector2 direction;
        public readonly float hitStop;

        public DamagePayload(GameObject source, int damage, Vector2 knockback, Vector2 direction, float hitStop)
        {
            this.source = source;
            this.damage = damage;
            this.knockback = knockback;
            this.direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.right;
            this.hitStop = hitStop;
        }
    }
}
