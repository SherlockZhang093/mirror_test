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
        [SerializeField, Range(0f, 0.9999f)] float fractionalProgress;
        [SerializeField, Min(0)] int baseCapacity = 5;
        [SerializeField, Min(0)] int sessionCapacityBonus;
        [SerializeField, Min(0)] int temporaryCapacityBonus;

        Health health;

        public int Current => current;
        public float FractionalProgress => fractionalProgress;
        public float DisplayedCurrent => Mathf.Min(Capacity, current + fractionalProgress);
        public int BaseCapacity => baseCapacity;
        public int SessionCapacityBonus => sessionCapacityBonus;
        public int TemporaryCapacityBonus => temporaryCapacityBonus;
        public int Capacity => Mathf.Max(0, baseCapacity + sessionCapacityBonus + temporaryCapacityBonus);
        public event Action<int, int> Changed;
        public event Action<float, int> ProgressChanged;

        public void ConfigureBase(int capacity)
        {
            baseCapacity = Mathf.Max(0, capacity);
            ClampAndNotify();
        }

        public void SetCurrent(int amount)
        {
            current = Mathf.Clamp(amount, 0, Capacity);
            fractionalProgress = 0f;
            Changed?.Invoke(current, Capacity);
            ProgressChanged?.Invoke(DisplayedCurrent, Capacity);
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
            AddProgress(amount);
            Trace($"[生命能量][Add] 请求={amount}, 写入={current - before}, {before}->{current}/{Capacity}");
            return current - before;
        }

        public float AddProgress(float amount)
        {
            if (amount <= 0f || Capacity <= 0)
                return 0f;

            var beforeProgress = DisplayedCurrent;
            if (beforeProgress >= Capacity)
            {
                fractionalProgress = 0f;
                return 0f;
            }

            var accepted = Mathf.Min(amount, Capacity - beforeProgress);
            var accumulated = fractionalProgress + accepted;
            var wholeGain = Mathf.FloorToInt(accumulated + 0.0001f);
            fractionalProgress = Mathf.Clamp(accumulated - wholeGain, 0f, 0.9999f);

            var beforeWhole = current;
            current = Mathf.Clamp(current + wholeGain, 0, Capacity);
            if (current >= Capacity)
                fractionalProgress = 0f;

            if (current != beforeWhole)
                Changed?.Invoke(current, Capacity);
            ProgressChanged?.Invoke(DisplayedCurrent, Capacity);
            Trace($"[LifeEnergy][Progress] request={amount:0.###}, applied={DisplayedCurrent - beforeProgress:0.###}, " +
                  $"{beforeProgress:0.###}->{DisplayedCurrent:0.###}/{Capacity}");
            return DisplayedCurrent - beforeProgress;
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
            ProgressChanged?.Invoke(DisplayedCurrent, Capacity);
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
            if (current == 0 && fractionalProgress <= 0f)
            {
                Changed?.Invoke(0, Capacity);
                ProgressChanged?.Invoke(0f, Capacity);
                return;
            }

            current = 0;
            fractionalProgress = 0f;
            Changed?.Invoke(current, Capacity);
            ProgressChanged?.Invoke(DisplayedCurrent, Capacity);
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
            fractionalProgress = Mathf.Clamp(fractionalProgress, 0f, current < Capacity ? 0.9999f : 0f);
        }

        void ClampAndNotify()
        {
            current = Mathf.Clamp(current, 0, Capacity);
            fractionalProgress = Mathf.Clamp(fractionalProgress, 0f, current < Capacity ? 0.9999f : 0f);
            Changed?.Invoke(current, Capacity);
            ProgressChanged?.Invoke(DisplayedCurrent, Capacity);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        void Trace(string message)
        {
            Debug.Log("[HealthResourceTrace]" + message, this);
        }
    }
}
