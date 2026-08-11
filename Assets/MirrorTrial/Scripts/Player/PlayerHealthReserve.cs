using System;
using MirrorTrial.Level;
using Platformer.Mechanics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MirrorTrial.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public sealed class PlayerHealthReserve : MonoBehaviour
    {
        [Header("Current Life Energy")]
        [SerializeField, Min(0)] int current;
        [SerializeField, Min(0)] int baseCapacity = 5;
        [SerializeField, Min(0)] int sessionCapacityBonus;
        [SerializeField, Min(0)] int temporaryCapacityBonus;

        Health health;

        public int Current => current;
        public int BaseCapacity => baseCapacity;
        public int SessionCapacityBonus => sessionCapacityBonus;
        public int TemporaryCapacityBonus => temporaryCapacityBonus;
        public int Capacity => Mathf.Max(0, baseCapacity + sessionCapacityBonus + temporaryCapacityBonus);
        public event Action<int, int> Changed;

        public void ConfigureBase(int capacity)
        {
            baseCapacity = Mathf.Max(0, capacity);
            ClampAndNotify();
        }

        public void SetCurrent(int amount)
        {
            current = Mathf.Clamp(amount, 0, Capacity);
            Changed?.Invoke(current, Capacity);
        }

        void Awake()
        {
            health = GetComponent<Health>();
            if (!GetComponent<LifeEnergyGainProcessor>())
                gameObject.AddComponent<LifeEnergyGainProcessor>();
            ClampAndNotify();
        }

        void OnEnable()
        {
            MirrorTransitionBridge.OnReturnToRealityScene += ResetForReality;
            PlayerDamageReceiver.PlayerRespawned += Clear;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            MirrorTransitionBridge.OnReturnToRealityScene -= ResetForReality;
            PlayerDamageReceiver.PlayerRespawned -= Clear;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        public int Add(int amount)
        {
            if (amount <= 0)
            {
                Trace($"[生命能量][Add忽略] 请求={amount}");
                return 0;
            }

            var before = current;
            current = Mathf.Clamp(current + amount, 0, Capacity);
            if (current != before)
                Changed?.Invoke(current, Capacity);
            Trace($"[生命能量][Add] 请求={amount}, 写入={current - before}, {before}->{current}/{Capacity}");
            return current - before;
        }

        public bool TryReserveForFullHeal(out int reservedAmount)
        {
            reservedAmount = 0;
            if (!health || !health.IsAlive || current <= 0)
            {
                Trace(
                    $"[生命能量][G拒绝] Health={(health ? "存在" : "缺失")}, " +
                    $"Alive={(health && health.IsAlive)}, Current={current}");
                return false;
            }

            var missing = Mathf.Max(0, health.maxHP - health.CurrentHP);
            reservedAmount = Mathf.Min(missing, current);
            if (reservedAmount <= 0)
            {
                Trace($"[生命能量][G拒绝] 玩家未缺血，HP={health.CurrentHP}/{health.maxHP}");
                return false;
            }

            current -= reservedAmount;
            Changed?.Invoke(current, Capacity);
            Trace($"[生命能量][G预扣] 数量={reservedAmount}, 剩余={current}/{Capacity}");
            return true;
        }

        public void SetSessionCapacityBonus(int bonus)
        {
            sessionCapacityBonus = Mathf.Max(0, bonus);
            ClampAndNotify();
        }

        public void SetTemporaryCapacityBonus(int bonus)
        {
            temporaryCapacityBonus = Mathf.Max(0, bonus);
            ClampAndNotify();
        }

        public void Clear()
        {
            if (current == 0)
            {
                Changed?.Invoke(0, Capacity);
                return;
            }

            current = 0;
            Changed?.Invoke(current, Capacity);
        }

        void ResetForReality()
        {
            Clear();
            if (health)
                health.RestoreFull();
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name.IndexOf("Reality", StringComparison.OrdinalIgnoreCase) < 0)
                return;

            var bridge = MirrorTransitionBridge.Instance;
            if (bridge != null && bridge.IsInMirror)
                return;

            Clear();
        }

        void OnValidate()
        {
            if (!health)
                health = GetComponent<Health>();
            baseCapacity = Mathf.Max(0, baseCapacity);
            sessionCapacityBonus = Mathf.Max(0, sessionCapacityBonus);
            temporaryCapacityBonus = Mathf.Max(0, temporaryCapacityBonus);
            current = Mathf.Clamp(current, 0, Capacity);
        }

        void ClampAndNotify()
        {
            current = Mathf.Clamp(current, 0, Capacity);
            Changed?.Invoke(current, Capacity);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        void Trace(string message)
        {
            Debug.Log("[HealthResourceTrace]" + message, this);
        }
    }
}
