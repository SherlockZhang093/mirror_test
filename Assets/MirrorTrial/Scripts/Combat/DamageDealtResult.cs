using System;
using UnityEngine;

namespace MirrorTrial.Combat
{
    public readonly struct DamageDealtResult
    {
        public readonly GameObject source;
        public readonly GameObject target;
        public readonly int requestedDamage;
        public readonly int actualDamage;
        public readonly bool allowLifeEnergyGain;

        public DamageDealtResult(GameObject source, GameObject target, int requestedDamage, int actualDamage,
            bool allowLifeEnergyGain = true)
        {
            this.source = source;
            this.target = target;
            this.requestedDamage = Mathf.Max(0, requestedDamage);
            this.actualDamage = Mathf.Max(0, actualDamage);
            this.allowLifeEnergyGain = allowLifeEnergyGain;
        }
    }

    public static class DamageDealtEvents
    {
        public static event Action<DamageDealtResult> PlayerDamageDealt;

        public static void RaisePlayerDamageDealt(in DamageDealtResult result)
        {
            if (result.actualDamage <= 0 || !result.allowLifeEnergyGain)
                return;
            PlayerDamageDealt?.Invoke(result);
        }
    }
}
