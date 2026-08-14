using MirrorTrial.Combat;

namespace MirrorTrial.Player
{
    public static class PlayerHitResolver
    {
        public static PlayerHitResolution Resolve(DamagePayload payload, PlayerBodyStateController bodyState)
        {
            var state = bodyState ? bodyState.CurrentState : PlayerBodyState.Normal;
            if (state == PlayerBodyState.Invincible) return PlayerHitResolution.Ignored;

            var requested = PlayerHitResolution.ConvertReaction(payload.reaction);
            var canInterrupt = payload.interruptPower > 0 && requested != PlayerHitReaction.None;
            var result = new PlayerHitResolution { ApplyDamage = payload.damage > 0, Reaction = PlayerHitReaction.ArmorHit };

            switch (state)
            {
                case PlayerBodyState.DiamondBody:
                    return result;
                case PlayerBodyState.SuperArmor:
                    if (!payload.breaksSuperArmor) return result;
                    break;
                case PlayerBodyState.HardBody:
                    result.PoiseBroken = bodyState && bodyState.ApplyPoiseDamage(payload.poiseDamage);
                    if (!result.PoiseBroken && !payload.breaksSuperArmor) return result;
                    break;
            }

            result.InterruptAction = canInterrupt;
            result.PlayFullBodyReaction = canInterrupt;
            result.ApplyKnockback = canInterrupt && payload.knockback.sqrMagnitude > 0.0001f;
            result.Reaction = canInterrupt ? requested : PlayerHitReaction.ArmorHit;
            return result;
        }
    }
}
