using MirrorTrial.Combat;
using UnityEngine;

namespace MirrorTrial.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerInputReader), typeof(PlayerHealthReserve))]
    public sealed class LifeEnergyGainProcessor : MonoBehaviour
    {
        [Header("Damage Conversion")]
        [SerializeField, Range(0f, 1f)] float baseDamageConversionRate = 0.2f;
        [SerializeField, Min(0f)] float playerConversionMultiplier = 1f;

        PlayerInputReader input;
        PlayerHealthReserve reserve;
        float fractionalCarry;

        public float BaseDamageConversionRate => baseDamageConversionRate;
        public float PlayerConversionMultiplier => playerConversionMultiplier;
        public float FractionalCarry => fractionalCarry;

        public void Configure(float rate, float multiplier)
        {
            baseDamageConversionRate = Mathf.Clamp01(rate);
            playerConversionMultiplier = Mathf.Max(0f, multiplier);
        }

        void Awake()
        {
            input = GetComponent<PlayerInputReader>();
            reserve = GetComponent<PlayerHealthReserve>();
        }

        void OnEnable()
        {
            DamageDealtEvents.PlayerDamageDealt += OnPlayerDamageDealt;
        }

        void OnDisable()
        {
            DamageDealtEvents.PlayerDamageDealt -= OnPlayerDamageDealt;
        }

        void OnPlayerDamageDealt(DamageDealtResult result)
        {
            if (!reserve || !input || !IsOwnedByThisPlayer(result.source))
                return;

            var raw = result.actualDamage * baseDamageConversionRate * playerConversionMultiplier + fractionalCarry;
            var gain = Mathf.FloorToInt(raw);
            fractionalCarry = raw - gain;

            if (gain <= 0)
                return;

            reserve.Add(gain);
        }

        bool IsOwnedByThisPlayer(GameObject source)
        {
            if (!source)
                return false;

            var owner = source.GetComponentInParent<PlayerInputReader>();
            return owner && owner == input;
        }
    }
}
