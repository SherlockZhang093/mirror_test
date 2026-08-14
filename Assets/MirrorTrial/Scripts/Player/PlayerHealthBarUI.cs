using Platformer.Mechanics;
using System.Collections.Generic;
using UnityEngine;

namespace MirrorTrial.Player
{
    [RequireComponent(typeof(Health), typeof(PlayerHealthReserve), typeof(PlayerRecoveryAbility))]
    public sealed class PlayerHealthBarUI : MonoBehaviour
    {
        const string DefaultResourcePath = "UI/PlayerHealthBarUI";
        const int DefaultSlotCount = 5;

        static PlayerHealthBarView levelViewPrefab;
        static readonly HashSet<string> MissingVariantWarnings = new HashSet<string>();

        [SerializeField] PlayerHealthBarView viewPrefab;
        [SerializeField] string resourcePath = DefaultResourcePath;

        Health health;
        PlayerHealthReserve reserve;
        PlayerRecoveryAbility recovery;
        PlayerHealthBarView view;
        PlayerHealthBarView instantiatedPrefab;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetLevelViewPrefab()
        {
            levelViewPrefab = null;
            MissingVariantWarnings.Clear();
        }

        public static void SetLevelViewPrefab(PlayerHealthBarView prefab)
        {
            if (!prefab || levelViewPrefab == prefab)
                return;

            levelViewPrefab = prefab;
            var instances = FindObjectsOfType<PlayerHealthBarUI>(true);
            foreach (var instance in instances)
                instance.ApplyLevelViewPrefab();
        }

        void Awake()
        {
            health = GetComponent<Health>();
            reserve = GetComponent<PlayerHealthReserve>();
            recovery = GetComponent<PlayerRecoveryAbility>();
            CreateView();
        }

        void Start()
        {
            Refresh(health.CurrentHP, health.maxHP);
            RefreshReserve();
        }

        void OnEnable()
        {
            if (health != null)
            {
                health.Changed += Refresh;
                Refresh(health.CurrentHP, health.maxHP);
            }

            if (reserve != null)
            {
                reserve.ProgressChanged += OnReserveProgressChanged;
                RefreshReserve();
            }

            if (recovery != null)
            {
                recovery.CastStateChanged += OnCastStateChanged;
                RefreshReserve();
            }
        }

        void OnDisable()
        {
            if (health != null)
                health.Changed -= Refresh;
            if (reserve != null)
                reserve.ProgressChanged -= OnReserveProgressChanged;
            if (recovery != null)
                recovery.CastStateChanged -= OnCastStateChanged;
        }

        void OnDestroy()
        {
            if (view != null)
                Destroy(view.gameObject);
        }

        void CreateView()
        {
            if (view != null)
                return;

            var prefab = ResolveViewPrefab(health ? health.maxHP : DefaultSlotCount);

            if (prefab == null)
            {
                Debug.LogWarning("[PlayerHealthBarUI] Missing PlayerHealthBarUI prefab in Resources/UI.", this);
                return;
            }

            view = Instantiate(prefab, transform, false);
            instantiatedPrefab = prefab;
            view.name = prefab.name;
        }

        void ApplyLevelViewPrefab()
        {
            var targetPrefab = ResolveViewPrefab(health ? health.maxHP : DefaultSlotCount);
            if (!targetPrefab || instantiatedPrefab == targetPrefab)
                return;

            if (view != null)
            {
                view.gameObject.SetActive(false);
                Destroy(view.gameObject);
            }

            view = null;
            instantiatedPrefab = null;
            CreateView();

            if (view == null)
                return;

            if (health != null)
                view.SetHealth(health.CurrentHP, health.maxHP);
            RefreshReserve();
        }

        void Refresh(int currentHP, int maxHP)
        {
            ApplyLevelViewPrefab();

            if (view != null)
                view.SetHealth(currentHP, maxHP);
        }

        PlayerHealthBarView ResolveViewPrefab(int maxHealth)
        {
            var standardPrefab = levelViewPrefab ? levelViewPrefab : viewPrefab;
            if (!standardPrefab)
            {
                var path = string.IsNullOrEmpty(resourcePath) ? DefaultResourcePath : resourcePath;
                var prefabObject = Resources.Load<GameObject>(path);
                if (prefabObject)
                    standardPrefab = prefabObject.GetComponent<PlayerHealthBarView>();
            }

            if (!standardPrefab || maxHealth <= DefaultSlotCount)
                return standardPrefab;

            var variantPath = $"UI/{standardPrefab.gameObject.name}_{maxHealth}Slot";
            var variantObject = Resources.Load<GameObject>(variantPath);
            var variant = variantObject ? variantObject.GetComponent<PlayerHealthBarView>() : null;
            if (variant)
                return variant;

            if (MissingVariantWarnings.Add(variantPath))
            {
                Debug.LogWarning(
                    $"[PlayerHealthBarUI] 玩家最大生命值为 {maxHealth}，但找不到 Resources/{variantPath}，继续使用标准生命 UI。",
                    this);
            }

            return standardPrefab;
        }

        void OnReserveProgressChanged(float current, int capacity)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log(
                $"[HealthResourceTrace][UI接收] 储备刷新={current}/{capacity}, " +
                $"View={(view ? view.name : "<null>")}",
                this);
#endif
            if (view == null) CreateView();
            if (view != null)
                view.SetReserve(current, capacity, recovery != null && recovery.IsCasting);
        }

        void OnCastStateChanged(bool isCasting)
        {
            RefreshReserve();
        }

        void RefreshReserve()
        {
            if (view == null) CreateView();
            if (view != null)
                view.SetReserve(reserve != null ? reserve.DisplayedCurrent : 0f,
                    reserve != null ? reserve.Capacity : 0,
                    recovery != null && recovery.IsCasting);
        }
    }
}
