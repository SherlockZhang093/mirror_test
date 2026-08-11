using System;
using Platformer.Gameplay;
using UnityEngine;
using static Platformer.Core.Simulation;

namespace Platformer.Mechanics
{
    /// <summary>
    /// Represebts the current vital statistics of some game entity.
    /// </summary>
    public class Health : MonoBehaviour
    {
        /// <summary>
        /// The maximum hit points for the entity.
        /// </summary>
        public int maxHP = 1;

        public event Action<int, int> Changed;

        public int CurrentHP => currentHP;

        /// <summary>
        /// Indicates if the entity should be considered 'alive'.
        /// </summary>
        public bool IsAlive => currentHP > 0;

        public void SetMaxHealth(int value, bool healIncrease)
        {
            var previousMax = maxHP;
            maxHP = Mathf.Max(1, value);
            if (healIncrease && maxHP > previousMax && currentHP > 0)
                currentHP = Mathf.Min(maxHP, currentHP + maxHP - previousMax);
            else
                currentHP = Mathf.Clamp(currentHP, 0, maxHP);
            Changed?.Invoke(currentHP, maxHP);
        }

        public void SetCurrentHealth(int value)
        {
            currentHP = Mathf.Clamp(value, 0, Mathf.Max(1, maxHP));
            Changed?.Invoke(currentHP, maxHP);
        }

        int currentHP;

        /// <summary>
        /// Increment the HP of the entity.
        /// </summary>
        public void Increment()
        {
            Heal(1);
        }

        /// <summary>Restore up to <paramref name="amount"/> hit points and return the amount restored.</summary>
        public int Heal(int amount)
        {
            if (amount <= 0 || currentHP <= 0 || currentHP >= maxHP)
                return 0;

            var previous = currentHP;
            currentHP = Mathf.Clamp(currentHP + amount, 0, maxHP);
            Changed?.Invoke(currentHP, maxHP);
            return currentHP - previous;
        }

        /// <summary>Restore this entity to full health (for example, after respawning).</summary>
        public void RestoreFull()
        {
            currentHP = Mathf.Max(0, maxHP);
            Changed?.Invoke(currentHP, maxHP);
        }

        /// <summary>
        /// Decrement the HP of the entity. Will trigger a HealthIsZero event when
        /// current HP reaches 0.
        /// </summary>
        public void Decrement()
        {
            Damage(1);
        }

        public void Damage(int amount)
        {
            if (amount <= 0 || currentHP <= 0)
                return;

            currentHP = Mathf.Clamp(currentHP - amount, 0, maxHP);
            Changed?.Invoke(currentHP, maxHP);
            if (currentHP == 0)
            {
                var ev = Schedule<HealthIsZero>();
                ev.health = this;
            }
        }

        /// <summary>
        /// Decrement the HP of the entitiy until HP reaches 0.
        /// </summary>
        public void Die()
        {
            while (currentHP > 0) Decrement();
        }

        void Awake()
        {
            currentHP = maxHP;
        }
    }
}
