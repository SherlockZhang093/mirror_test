using MirrorTrial.Growth;
using MirrorTrial.Level;
using Platformer.Mechanics;
using UnityEngine;

namespace MirrorTrial.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerRuntimeStats : MonoBehaviour
    {
        Health health;
        PlayerHealthReserve reserve;
        LifeEnergyGainProcessor energyGain;
        PlayerTuning tuning;
        PlayerSessionGrowth growth;
        PlayerDamageReceiver damageReceiver;
        PlayerMotor motor;
        PlayerStateMachine stateMachine;
        PlayerWeaponController weapons;

        public Health Health => health;
        public PlayerHealthReserve Reserve => reserve;
        public LifeEnergyGainProcessor EnergyGain => energyGain;
        public PlayerTuning Tuning => tuning;
        public PlayerSessionGrowth Growth => growth;
        public PlayerDamageReceiver DamageReceiver => damageReceiver;
        public PlayerMotor Motor => motor;
        public PlayerStateMachine StateMachine => stateMachine;
        public PlayerWeaponController Weapons => weapons;
        public int LifeEssence => GameSessionProgress.Instance ? GameSessionProgress.Instance.LifeEssence : 0;
        public int ClaimedNodeCount => GameSessionProgress.Instance != null
            ? GameSessionProgress.Instance.ClaimedResourceNodeIds.Count
            : 0;
        public bool IsInMirror => MirrorTransitionBridge.Instance && MirrorTransitionBridge.Instance.IsInMirror;

        void Awake()
        {
            health = GetComponent<Health>();
            reserve = GetComponent<PlayerHealthReserve>();
            energyGain = GetComponent<LifeEnergyGainProcessor>();
            tuning = GetComponent<PlayerTuning>();
            growth = GetComponent<PlayerSessionGrowth>();
            damageReceiver = GetComponent<PlayerDamageReceiver>();
            motor = GetComponent<PlayerMotor>();
            stateMachine = GetComponent<PlayerStateMachine>();
            weapons = GetComponent<PlayerWeaponController>();
        }
    }
}
