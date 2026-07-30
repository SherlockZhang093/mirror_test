using Platformer.Mechanics;
using UnityEngine;

namespace MirrorTrial.Player
{
    [RequireComponent(typeof(Health), typeof(PlayerHealthReserve), typeof(PlayerRecoveryAbility))]
    public sealed class PlayerHealthBarUI : MonoBehaviour
    {
        const string DefaultResourcePath = "UI/PlayerHealthBarUI";

        static PlayerHealthBarView levelViewPrefab;

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
                reserve.Changed += OnReserveChanged;
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
                reserve.Changed -= OnReserveChanged;
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

            var prefab = levelViewPrefab ? levelViewPrefab : viewPrefab;
            if (prefab == null)
            {
                var prefabObject = Resources.Load<GameObject>(string.IsNullOrEmpty(resourcePath) ? DefaultResourcePath : resourcePath);
                if (prefabObject != null)
                    prefab = prefabObject.GetComponent<PlayerHealthBarView>();
            }

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
            if (!levelViewPrefab || instantiatedPrefab == levelViewPrefab)
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
            if (view == null)
                CreateView();

            if (view != null)
                view.SetHealth(currentHP, maxHP);
        }

        void OnReserveChanged(int current, int capacity)
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
                view.SetReserve(reserve != null ? reserve.Current : 0,
                    reserve != null ? reserve.Capacity : 0,
                    recovery != null && recovery.IsCasting);
        }
    }
}
