using Platformer.Mechanics;
using UnityEngine;

namespace MirrorTrial.Player
{
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health), typeof(PlayerTuning), typeof(PlayerStatsInitializer))]
    public sealed class PlayerSessionGrowth : MonoBehaviour
    {
        [SerializeField, Min(0)] int maxHealthBonus;
        [SerializeField, Min(0)] int attackDamageBonus;
        [SerializeField, Min(0f)] float moveSpeedBonus;

        Health health;
        PlayerTuning tuning;

        public int MaxHealthBonus => maxHealthBonus;
        public int AttackDamageBonus => attackDamageBonus;
        public float MoveSpeedBonus => moveSpeedBonus;

        void Awake()
        {
            health = GetComponent<Health>();
            tuning = GetComponent<PlayerTuning>();
            ApplyStoredBonuses();
        }

        public bool AddMaxHealth(int amount)
        {
            if (amount <= 0 || !health) return false;
            maxHealthBonus += amount;
            health.SetMaxHealth(health.maxHP + amount, true);
            return true;
        }

        public bool AddAttackDamage(int amount)
        {
            if (amount <= 0 || !tuning) return false;
            attackDamageBonus += amount;
            tuning.combat.attackDamage += amount;
            return true;
        }

        public bool AddMoveSpeed(float amount)
        {
            if (amount <= 0f || !tuning) return false;
            moveSpeedBonus += amount;
            tuning.movement.moveSpeed += amount;
            return true;
        }

        public void ResetGrowth()
        {
            if (health && maxHealthBonus > 0)
                health.SetMaxHealth(health.maxHP - maxHealthBonus, false);
            if (tuning)
            {
                tuning.combat.attackDamage = Mathf.Max(1, tuning.combat.attackDamage - attackDamageBonus);
                tuning.movement.moveSpeed = Mathf.Max(0.1f, tuning.movement.moveSpeed - moveSpeedBonus);
            }
            maxHealthBonus = 0;
            attackDamageBonus = 0;
            moveSpeedBonus = 0f;
        }

        void ApplyStoredBonuses()
        {
            if (maxHealthBonus > 0) health.SetMaxHealth(health.maxHP + maxHealthBonus, false);
            if (attackDamageBonus > 0) tuning.combat.attackDamage += attackDamageBonus;
            if (moveSpeedBonus > 0f) tuning.movement.moveSpeed += moveSpeedBonus;
        }
    }
}
