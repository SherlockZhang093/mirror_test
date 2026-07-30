using UnityEngine;

namespace MirrorTrial.HealthResources
{
    [DisallowMultipleComponent]
    public sealed class HealthResourcePlantView : MonoBehaviour
    {
        [SerializeField] HealthResourceNode node;
        [SerializeField] SpriteRenderer targetRenderer;
        [SerializeField] Sprite[] durabilityStates = new Sprite[4];

        [Header("Motion")]
        [SerializeField, Range(0f, 0.08f)] float breatheAmount = 0.018f;
        [SerializeField, Min(0.1f)] float breatheSpeed = 1.35f;
        [SerializeField, Range(1f, 1.25f)] float hitPunchScale = 1.08f;
        [SerializeField, Min(0.01f)] float hitPunchDuration = 0.12f;

        Vector3 restScale;
        float punchRemaining;
        bool depleted;

        void Awake()
        {
            ResolveReferences();
            restScale = transform.localScale;
        }

        void OnEnable()
        {
            ResolveReferences();
            if (node)
            {
                node.DurabilityChanged += OnDurabilityChanged;
                node.Depleted += OnDepleted;
                if (node.IsInitialized)
                    ApplyDurability(node.CurrentDurability, node.MaxDurability);
                else
                    ApplyState(0);
            }
        }

        void OnDisable()
        {
            if (node)
            {
                node.DurabilityChanged -= OnDurabilityChanged;
                node.Depleted -= OnDepleted;
            }
            transform.localScale = restScale;
            punchRemaining = 0f;
        }

        void Update()
        {
            if (!targetRenderer) return;

            var breathe = depleted ? 1f : 1f + Mathf.Sin(Time.time * breatheSpeed) * breatheAmount;
            var punch = 1f;
            if (punchRemaining > 0f)
            {
                punchRemaining = Mathf.Max(0f, punchRemaining - Time.deltaTime);
                var progress = 1f - punchRemaining / hitPunchDuration;
                punch += Mathf.Sin(progress * Mathf.PI) * (hitPunchScale - 1f);
            }
            transform.localScale = restScale * (breathe * punch);
        }

        void OnDurabilityChanged(int current, int maximum)
        {
            ApplyDurability(current, maximum);
            punchRemaining = hitPunchDuration;
        }

        void OnDepleted()
        {
            depleted = true;
            ApplyState(3);
        }

        void ApplyDurability(int current, int maximum)
        {
            depleted = current <= 0;
            var state = maximum <= 0
                ? 3
                : Mathf.Clamp(Mathf.CeilToInt((maximum - current) * 3f / maximum), 0, 3);
            ApplyState(state);
        }

        void ApplyState(int state)
        {
            if (!targetRenderer || durabilityStates == null || durabilityStates.Length == 0) return;
            state = Mathf.Clamp(state, 0, durabilityStates.Length - 1);
            if (durabilityStates[state]) targetRenderer.sprite = durabilityStates[state];
        }

        void ResolveReferences()
        {
            if (!node) node = GetComponentInParent<HealthResourceNode>();
            if (!targetRenderer) targetRenderer = GetComponent<SpriteRenderer>();
        }
    }
}
