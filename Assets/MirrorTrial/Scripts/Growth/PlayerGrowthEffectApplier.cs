using MirrorTrial.Player;
using UnityEngine;

namespace MirrorTrial.Growth
{
    public enum GrowthEffectType
    {
        MaxHealth,
        AttackDamage,
        MoveSpeed
    }

    public static class PlayerGrowthEffectApplier
    {
        public static bool CanApply(GameObject player, GrowthEffectType effectType, float amount)
        {
            if (!player || amount <= 0f)
                return false;

            return player.GetComponentInChildren<PlayerSessionGrowth>(true);
        }

        public static bool TryApply(GameObject player, GrowthEffectType effectType, float amount)
        {
            if (!CanApply(player, effectType, amount))
                return false;

            var growth = player.GetComponentInChildren<PlayerSessionGrowth>(true);
            if (!growth)
                return false;
            switch (effectType)
            {
                case GrowthEffectType.MaxHealth:
                    return growth.AddMaxHealth(Mathf.Max(1, Mathf.RoundToInt(amount)));
                case GrowthEffectType.AttackDamage:
                    return growth.AddAttackDamage(Mathf.Max(1, Mathf.RoundToInt(amount)));
                case GrowthEffectType.MoveSpeed:
                    return growth.AddMoveSpeed(amount);
                default:
                    return false;
            }
        }

        public static bool TryRevert(GameObject player, GrowthEffectType effectType, float amount)
        {
            if (!player || amount <= 0f)
                return false;

            // Individual card rollback is intentionally unsupported now that growth is tracked cumulatively.
            return false;
        }

        public static string FormatEffect(GrowthEffectType effectType, float amount)
        {
            switch (effectType)
            {
                case GrowthEffectType.MaxHealth:
                    return "最大生命 +" + Mathf.Max(1, Mathf.RoundToInt(amount));
                case GrowthEffectType.AttackDamage:
                    return "攻击伤害 +" + Mathf.Max(1, Mathf.RoundToInt(amount));
                case GrowthEffectType.MoveSpeed:
                    return "移动速度 +" + amount.ToString("0.#");
                default:
                    return "未知提升";
            }
        }

        public static string FormatCardTitle(GrowthEffectType effectType)
        {
            switch (effectType)
            {
                case GrowthEffectType.MaxHealth:
                    return "生命增幅";
                case GrowthEffectType.AttackDamage:
                    return "攻击增幅";
                case GrowthEffectType.MoveSpeed:
                    return "速度增幅";
                default:
                    return "属性增幅";
            }
        }

        public static string FormatCardValue(float amount)
        {
            return "+" + amount.ToString("0.#");
        }

        public static string FormatCardAttribute(GrowthEffectType effectType)
        {
            switch (effectType)
            {
                case GrowthEffectType.MaxHealth:
                    return "最大生命";
                case GrowthEffectType.AttackDamage:
                    return "攻击伤害";
                case GrowthEffectType.MoveSpeed:
                    return "移动速度";
                default:
                    return "未知属性";
            }
        }
    }
}
