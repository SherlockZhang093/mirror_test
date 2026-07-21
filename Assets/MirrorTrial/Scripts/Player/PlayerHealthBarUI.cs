using Platformer.Mechanics;
using UnityEngine;

namespace MirrorTrial.Player
{
    [RequireComponent(typeof(Health))]
    public sealed class PlayerHealthBarUI : MonoBehaviour
    {
        const string DefaultResourcePath = "UI/PlayerHealthBarUI";

        [SerializeField] PlayerHealthBarView viewPrefab;
        [SerializeField] string resourcePath = DefaultResourcePath;

        Health health;
        PlayerHealthBarView view;

        void Awake()
        {
            health = GetComponent<Health>();
            CreateView();
        }

        void Start()
        {
            Refresh(health.CurrentHP, health.maxHP);
        }

        void OnEnable()
        {
            if (health != null)
            {
                health.Changed += Refresh;
                Refresh(health.CurrentHP, health.maxHP);
            }
        }

        void OnDisable()
        {
            if (health != null)
                health.Changed -= Refresh;
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

            var prefab = viewPrefab;
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
            view.name = prefab.name;
        }

        void Refresh(int currentHP, int maxHP)
        {
            if (view == null)
                CreateView();

            if (view != null)
                view.SetHealth(currentHP, maxHP);
        }
    }
}