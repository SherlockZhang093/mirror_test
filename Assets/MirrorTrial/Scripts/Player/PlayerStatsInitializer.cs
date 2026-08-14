using Platformer.Mechanics;
using UnityEngine;

namespace MirrorTrial.Player
{
    [DefaultExecutionOrder(-300)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health), typeof(PlayerTuning), typeof(PlayerHealthReserve))]
    public sealed class PlayerStatsInitializer : MonoBehaviour
    {
        const string DefaultProfilePath = "Player/PlayerInitialStats";

        [SerializeField] PlayerInitialStats profile;
        [SerializeField] string fallbackResourcePath = DefaultProfilePath;

        Health health;
        PlayerTuning tuning;
        PlayerHealthReserve reserve;
        LifeEnergyGainProcessor energyGain;

        public PlayerInitialStats Profile => profile;

        void Awake()
        {
            ResolveComponents();
            if (!profile)
                profile = Resources.Load<PlayerInitialStats>(fallbackResourcePath);
            if (!profile)
            {
                Debug.LogError("[PlayerStatsInitializer] 找不到玩家初始属性配置。", this);
                return;
            }

            health.SetMaxHealth(profile.MaxHealth, false);
            tuning.combat.attackDamage = profile.AttackDamage;
            tuning.movement.moveSpeed = profile.MoveSpeed;
            reserve.ConfigureBase(profile.LifeEnergyCapacity);
            energyGain.Configure(profile.DamageConversionRate, profile.ConversionMultiplier);
        }

        void Start()
        {
            if (!profile)
                return;
            health.SetCurrentHealth(profile.InitialHealth);
            reserve.SetCurrent(profile.InitialLifeEnergy);
        }

        void ResolveComponents()
        {
            health = GetComponent<Health>();
            tuning = GetComponent<PlayerTuning>();
            reserve = GetComponent<PlayerHealthReserve>();
            energyGain = GetComponent<LifeEnergyGainProcessor>();
            if (!energyGain)
                energyGain = gameObject.AddComponent<LifeEnergyGainProcessor>();
        }
    }
}
