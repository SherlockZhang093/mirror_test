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
        [SerializeField, Min(0)] int current;

        Health health;

        public int Current => current;
        public int Capacity => health ? Mathf.Max(0, health.maxHP) : 0;
        public event Action<int, int> Changed;

        void Awake()
        {
            health = GetComponent<Health>();
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
                Trace($"[储备系统][Add忽略] 请求={amount}");
                return 0;
            }
            var before = current;
            current = Mathf.Clamp(current + amount, 0, Capacity);
            if (current != before) Changed?.Invoke(current, Capacity);
            Trace($"[储备系统][Add] 请求={amount}, 写入={current - before}, {before}->{current}/{Capacity}");
            return current - before;
        }

        public bool TryReserveForFullHeal(out int reservedAmount)
        {
            reservedAmount = 0;
            if (!health || !health.IsAlive || current <= 0)
            {
                Trace(
                    $"[储备系统][G拒绝] Health={(health ? "存在" : "缺失")}, " +
                    $"Alive={(health && health.IsAlive)}, Current={current}");
                return false;
            }
            var missing = Mathf.Max(0, health.maxHP - health.CurrentHP);
            reservedAmount = Mathf.Min(missing, current);
            if (reservedAmount <= 0)
            {
                Trace($"[储备系统][G拒绝] 玩家未缺血，HP={health.CurrentHP}/{health.maxHP}");
                return false;
            }
            current -= reservedAmount;
            Changed?.Invoke(current, Capacity);
            Trace($"[储备系统][G预扣] 数量={reservedAmount}, 剩余={current}/{Capacity}");
            return true;
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
            if (health) health.RestoreFull();
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name.IndexOf("Reality", StringComparison.OrdinalIgnoreCase) < 0) return;
            var bridge = MirrorTransitionBridge.Instance;
            if (bridge != null && bridge.IsInMirror) return;
            Clear();
        }

        void OnValidate()
        {
            if (!health) health = GetComponent<Health>();
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
