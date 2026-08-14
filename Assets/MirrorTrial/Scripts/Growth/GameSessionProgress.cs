using System;
using System.Collections.Generic;
using MirrorTrial.UI;
using UnityEngine;

namespace MirrorTrial.Growth
{
    [DisallowMultipleComponent]
    public sealed class GameSessionProgress : MonoBehaviour, IGrowthEssenceWallet
    {
        const string HudResourcePath = "UI/LifeEssenceHudUI";

        static GameSessionProgress instance;
        static LifeEssenceHudView hudView;

        [SerializeField, Min(0)] int lifeEssence;

        readonly HashSet<string> claimedResourceNodeIds = new HashSet<string>(StringComparer.Ordinal);

        public static GameSessionProgress Instance => instance;
        public int LifeEssence => lifeEssence;
        public int Balance => lifeEssence;
        public IReadOnlyCollection<string> ClaimedResourceNodeIds => claimedResourceNodeIds;

        public event Action<int, int> LifeEssenceChanged;
        public event Action<string, int> LifeEssenceClaimed;
        public event Action<int, string> LifeEssenceSpendSucceeded;
        public event Action<int, string> LifeEssenceSpendFailed;
        public event Action<int> BalanceChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
            hudView = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            Ensure();
        }

        public static GameSessionProgress Ensure()
        {
            if (instance)
            {
                instance.EnsureHud();
                return instance;
            }

            var existing = FindObjectOfType<GameSessionProgress>(true);
            if (existing)
            {
                existing.InitializeAsSingleton();
                return existing;
            }

            var root = new GameObject("GameSessionProgress");
            return root.AddComponent<GameSessionProgress>();
        }

        void Awake()
        {
            InitializeAsSingleton();
        }

        public bool HasClaimed(string nodeId)
        {
            return !string.IsNullOrWhiteSpace(nodeId) && claimedResourceNodeIds.Contains(nodeId);
        }

        public bool TryClaimLifeEssence(string nodeId, int amount)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                Debug.LogError("[GameSessionProgress] 生命精华节点缺少稳定唯一 ID。", this);
                return false;
            }
            if (amount < 0)
            {
                Debug.LogError($"[GameSessionProgress] 非法生命精华奖励：{amount}。", this);
                return false;
            }
            if (!claimedResourceNodeIds.Add(nodeId))
                return false;

            if (amount <= 0)
                return true;

            SetLifeEssence(lifeEssence + amount);
            LifeEssenceClaimed?.Invoke(nodeId, amount);
            return true;
        }

        public bool CanSpendLifeEssence(int amount)
        {
            return amount >= 0 && lifeEssence >= amount;
        }

        public bool TrySpendLifeEssence(int amount, string reasonId)
        {
            if (amount < 0)
            {
                Debug.LogError($"[GameSessionProgress] 非法生命精华消费：{amount}。", this);
                LifeEssenceSpendFailed?.Invoke(amount, reasonId);
                return false;
            }
            if (amount == 0)
                return true;
            if (lifeEssence < amount)
            {
                LifeEssenceSpendFailed?.Invoke(amount, reasonId);
                return false;
            }

            SetLifeEssence(lifeEssence - amount);
            LifeEssenceSpendSucceeded?.Invoke(amount, reasonId);
            return true;
        }

        public bool TrySpend(int amount)
        {
            return TrySpendLifeEssence(amount, "GrowthCard");
        }

        public void ResetSession()
        {
            claimedResourceNodeIds.Clear();
            SetLifeEssence(0);
        }

        void InitializeAsSingleton()
        {
            if (instance && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            lifeEssence = Mathf.Max(0, lifeEssence);
            DontDestroyOnLoad(gameObject);
            GrowthEssenceWalletLocator.Register(this);
            EnsureHud();
        }

        void EnsureHud()
        {
            if (!hudView)
                hudView = FindObjectOfType<LifeEssenceHudView>(true);

            if (!hudView)
            {
                var prefab = Resources.Load<GameObject>(HudResourcePath);
                if (prefab)
                {
                    var instanceObject = Instantiate(prefab);
                    hudView = instanceObject.GetComponent<LifeEssenceHudView>();
                    if (!hudView)
                        hudView = instanceObject.GetComponentInChildren<LifeEssenceHudView>(true);
                }
                else
                {
                    hudView = LifeEssenceHudView.CreateRuntimeFallback();
                }
            }

            if (!hudView)
                return;

            DontDestroyOnLoad(hudView.gameObject);
            hudView.Bind(this);
            hudView.RefreshImmediate(lifeEssence);
        }

        void SetLifeEssence(int value)
        {
            var clamped = Mathf.Max(0, value);
            if (clamped == lifeEssence)
                return;

            var before = lifeEssence;
            lifeEssence = clamped;
            LifeEssenceChanged?.Invoke(before, lifeEssence);
            BalanceChanged?.Invoke(lifeEssence);
        }

        void OnDestroy()
        {
            if (instance != this)
                return;

            GrowthEssenceWalletLocator.Unregister(this);
            if (hudView)
                hudView.Unbind(this);
            instance = null;
        }
    }
}
