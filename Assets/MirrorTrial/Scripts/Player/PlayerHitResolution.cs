using MirrorTrial.Combat;

namespace MirrorTrial.Player
{
    public enum PlayerBodyState { Normal, HardBody, SuperArmor, DiamondBody, Invincible }
    public enum PlayerHitReaction
    {
        None,
        ArmorHit,
        LightHurt,
        HeavyHurt,
        Launch,
        Knockdown,
        Stunned,
        ShockLight,
        ShockHeavy,
        GetUp,
        Death
    }

    public struct PlayerHitResolution
    {
        public bool ApplyDamage;
        public bool PoiseBroken;
        public bool InterruptAction;
        public bool ApplyKnockback;
        public bool PlayFullBodyReaction;
        public PlayerHitReaction Reaction;

        public static PlayerHitResolution Ignored => new PlayerHitResolution { Reaction = PlayerHitReaction.None };

        public static PlayerHitReaction ConvertReaction(HitReactionType reaction)
        {
            switch (reaction)
            {
                case HitReactionType.HeavyHurt: return PlayerHitReaction.HeavyHurt;
                case HitReactionType.Launch: return PlayerHitReaction.Launch;
                case HitReactionType.Knockdown: return PlayerHitReaction.Knockdown;
                case HitReactionType.Stunned: return PlayerHitReaction.Stunned;
                case HitReactionType.ShockLight: return PlayerHitReaction.ShockLight;
                case HitReactionType.ShockHeavy: return PlayerHitReaction.ShockHeavy;
                case HitReactionType.None: return PlayerHitReaction.None;
                default: return PlayerHitReaction.LightHurt;
            }
        }
    }
}
