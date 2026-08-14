using UnityEngine;

namespace MirrorTrial.Boss
{
    [RequireComponent(typeof(MirrorBossActor))]
    public sealed class MirrorBossHudV2 : MonoBehaviour
    {
        const string DefaultResourcePath = "UI/MirrorBossHud";

        [SerializeField] MirrorBossHudView viewPrefab;
        [SerializeField] string resourcePath = DefaultResourcePath;
        [SerializeField] float destroyDelay = 1.5f;

        MirrorBossActor boss;
        MirrorBossHudView view;

        void Awake() => boss = GetComponent<MirrorBossActor>();

        void OnEnable()
        {
            boss.Activated += OnActivated;
            boss.HealthChanged += OnHealthChanged;
            boss.PhaseChanged += OnPhaseChanged;
            boss.Defeated += OnDefeated;
        }

        void OnDisable()
        {
            boss.Activated -= OnActivated;
            boss.HealthChanged -= OnHealthChanged;
            boss.PhaseChanged -= OnPhaseChanged;
            boss.Defeated -= OnDefeated;
        }

        void OnActivated(MirrorBossActor actor)
        {
            EnsureHud();
            if (view != null)
                view.Show(actor.DisplayName, actor.Phase, actor.CurrentHitPoints, actor.MaxHitPoints);
        }

        void OnHealthChanged(MirrorBossActor actor, int current, int maximum)
        {
            EnsureHud();
            if (view != null)
                view.SetHealth(current, maximum);
        }

        void OnPhaseChanged(MirrorBossActor actor, int phase)
        {
            EnsureHud();
            if (view != null)
                view.SetTitle(actor.DisplayName, phase);
        }

        void OnDefeated(MirrorBossActor actor)
        {
            if (view != null)
            {
                view.SetHealth(0, actor.MaxHitPoints);
                Destroy(view.gameObject, destroyDelay);
                view = null;
            }
        }

        void EnsureHud()
        {
            if (view != null)
                return;

            var prefab = viewPrefab;
            if (prefab == null)
            {
                var prefabObject = Resources.Load<GameObject>(string.IsNullOrEmpty(resourcePath) ? DefaultResourcePath : resourcePath);
                if (prefabObject != null)
                    prefab = prefabObject.GetComponent<MirrorBossHudView>();
            }

            if (prefab == null)
            {
                Debug.LogWarning("[MirrorBossHudV2] Missing MirrorBossHud prefab in Resources/UI.", this);
                return;
            }

            view = Instantiate(prefab);
            view.name = prefab.name;
            DontDestroyOnLoad(view.gameObject);
        }
    }
}