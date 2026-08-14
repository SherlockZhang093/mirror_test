using UnityEngine;

namespace MirrorTrial.Player
{
    public enum InitialHealthMode
    {
        Full,
        Fixed
    }

    [CreateAssetMenu(menuName = "Mirror Trial/玩家/初始属性", fileName = "PlayerInitialStats")]
    public sealed class PlayerInitialStats : ScriptableObject
    {
        [Header("生存")]
        [SerializeField, Min(1)] int maxHealth = 5;
        [SerializeField] InitialHealthMode initialHealthMode = InitialHealthMode.Full;
        [SerializeField, Min(1)] int fixedInitialHealth = 5;

        [Header("生命能量")]
        [SerializeField, Min(0)] int initialLifeEnergy;
        [SerializeField, Min(0)] int lifeEnergyCapacity = 5;
        [SerializeField, Range(0f, 1f)] float damageConversionRate = 0.2f;
        [SerializeField, Min(0f)] float conversionMultiplier = 1f;

        [Header("基础战斗与移动")]
        [SerializeField, Min(1)] int attackDamage = 8;
        [SerializeField, Min(0.1f)] float moveSpeed = 5f;

        public int MaxHealth => maxHealth;
        public int InitialHealth => initialHealthMode == InitialHealthMode.Full
            ? maxHealth
            : Mathf.Clamp(fixedInitialHealth, 1, maxHealth);
        public InitialHealthMode InitialHealthMode => initialHealthMode;
        public int InitialLifeEnergy => Mathf.Clamp(initialLifeEnergy, 0, lifeEnergyCapacity);
        public int LifeEnergyCapacity => lifeEnergyCapacity;
        public float DamageConversionRate => damageConversionRate;
        public float ConversionMultiplier => conversionMultiplier;
        public int AttackDamage => attackDamage;
        public float MoveSpeed => moveSpeed;

#if UNITY_EDITOR
        public void ConfigureDefaults(int health, int energy, int capacity, float rate, float multiplier,
            int damage, float speed)
        {
            maxHealth = Mathf.Max(1, health);
            initialHealthMode = InitialHealthMode.Full;
            fixedInitialHealth = maxHealth;
            lifeEnergyCapacity = Mathf.Max(0, capacity);
            initialLifeEnergy = Mathf.Clamp(energy, 0, lifeEnergyCapacity);
            damageConversionRate = Mathf.Clamp01(rate);
            conversionMultiplier = Mathf.Max(0f, multiplier);
            attackDamage = Mathf.Max(1, damage);
            moveSpeed = Mathf.Max(0.1f, speed);
        }
#endif

        void OnValidate()
        {
            maxHealth = Mathf.Max(1, maxHealth);
            fixedInitialHealth = Mathf.Clamp(fixedInitialHealth, 1, maxHealth);
            lifeEnergyCapacity = Mathf.Max(0, lifeEnergyCapacity);
            initialLifeEnergy = Mathf.Clamp(initialLifeEnergy, 0, lifeEnergyCapacity);
            attackDamage = Mathf.Max(1, attackDamage);
            moveSpeed = Mathf.Max(0.1f, moveSpeed);
            conversionMultiplier = Mathf.Max(0f, conversionMultiplier);
        }
    }
}
