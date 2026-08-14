using MirrorTrial.Player;
using UnityEngine;

namespace MirrorTrial.Level
{
    [DisallowMultipleComponent]
    public sealed class PulleyRopeHintVisual : MonoBehaviour
    {
        [Header("References")]
        [SerializeField, HideInInspector] int setupVersion;
        [SerializeField] PulleyLiftController controller;
        [SerializeField] Transform cutPoint;
        [SerializeField] GameObject hintRoot;
        [SerializeField] SpriteRenderer glowRenderer;
        [SerializeField] Transform promptRoot;
        [SerializeField] SpriteRenderer[] promptRenderers;
        [SerializeField] ParticleSystem ropeDust;
        [Header("靠近区域")]
        [SerializeField, Tooltip("在 Prefab 里编辑这个 BoxCollider2D，用来决定攻击提示的出现范围。")]
        BoxCollider2D promptProximityArea;

        [Header("距离提示")]
        [SerializeField, Min(0.1f)] float noticeDistance = 4.5f;
        [SerializeField, HideInInspector, Min(0.1f)] float promptDistance = 2.8f;
        [SerializeField, Min(0f)] float promptDelay = 1.2f;
        [SerializeField, Min(0.01f)] float promptFadeDuration = 0.2f;

        [Header("呼吸表现")]
        [SerializeField, Min(0.01f)] float pulseSpeed = 2f;
        [SerializeField, Range(0f, 1f)] float glowMinAlpha = 0.12f;
        [SerializeField, Range(0f, 1f)] float glowMaxAlpha = 0.32f;
        [SerializeField, Min(0f)] float promptFloatHeight = 0.04f;

        PlayerInputReader player;
        Color glowBaseColor = Color.white;
        Color[] promptBaseColors;
        Vector3 glowBaseScale = Vector3.one;
        Vector3 promptBasePosition;
        Vector3 promptBaseScale = Vector3.one;
        float promptTimer;
        float promptAlpha;
        float playerSearchTimer;
        bool cachedVisuals;

        public int SetupVersion => setupVersion;

        void Awake()
        {
            CacheVisuals();
            DisablePromptAreaPhysics();
        }

        void OnEnable()
        {
            CacheVisuals();
            DisablePromptAreaPhysics();
            promptTimer = 0f;
            promptAlpha = 0f;
            playerSearchTimer = 0f;
            SetPromptAlpha(0f);
            if (glowRenderer)
                SetRendererAlpha(glowRenderer, glowBaseColor, glowMinAlpha);
        }

        void OnDisable()
        {
            StopDust(true);
        }

        void Update()
        {
            if (!Application.isPlaying)
                return;

            if (!controller)
                controller = GetComponent<PulleyLiftController>();

            var isIntact = controller && controller.State == PulleyLiftController.PulleyState.Raised;
            if (hintRoot && hintRoot.activeSelf != isIntact)
                hintRoot.SetActive(isIntact);

            if (!isIntact)
            {
                promptTimer = 0f;
                promptAlpha = 0f;
                StopDust(true);
                return;
            }

            TryFindPlayer();
            var distance = player && cutPoint
                ? Vector2.Distance(player.transform.position, cutPoint.position)
                : float.PositiveInfinity;
            var isPromptRange = player && IsInsidePromptArea(player.transform.position);
            var isNoticed = isPromptRange || distance <= noticeDistance;

            if (isNoticed)
                PlayDust();
            else
                StopDust(false);

            if (isPromptRange)
                promptTimer += Time.deltaTime;
            else
                promptTimer = 0f;

            var targetPromptAlpha = isPromptRange && promptTimer >= promptDelay ? 1f : 0f;
            promptAlpha = Mathf.MoveTowards(
                promptAlpha,
                targetPromptAlpha,
                Time.deltaTime / promptFadeDuration);
            SetPromptAlpha(promptAlpha);

            var pulse = (Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
            var rangeBoost = isNoticed ? 1f : 0.62f;
            var glowAlpha = Mathf.Lerp(glowMinAlpha, glowMaxAlpha, pulse) * rangeBoost;
            if (glowRenderer)
            {
                SetRendererAlpha(glowRenderer, glowBaseColor, glowAlpha);
                glowRenderer.transform.localScale = glowBaseScale * Mathf.Lerp(0.96f, 1.04f, pulse);
            }

            if (promptRoot)
            {
                promptRoot.localPosition = promptBasePosition +
                    Vector3.up * (Mathf.Sin(Time.time * 2.4f) * promptFloatHeight);
                promptRoot.localScale = promptBaseScale * Mathf.Lerp(0.96f, 1.04f, pulse);
            }
        }

        void TryFindPlayer()
        {
            if (player)
                return;

            playerSearchTimer -= Time.deltaTime;
            if (playerSearchTimer > 0f)
                return;

            player = FindObjectOfType<PlayerInputReader>();
            playerSearchTimer = 1f;
        }

        void CacheVisuals()
        {
            if (cachedVisuals)
                return;

            if (!controller)
                controller = GetComponent<PulleyLiftController>();
            if (!promptProximityArea)
            {
                var area = transform.Find("RopeHitTarget/PromptProximityArea");
                if (area)
                    promptProximityArea = area.GetComponent<BoxCollider2D>();
            }
            if (glowRenderer)
            {
                glowBaseColor = glowRenderer.color;
                glowBaseScale = glowRenderer.transform.localScale;
            }

            if (promptRoot)
            {
                promptBasePosition = promptRoot.localPosition;
                promptBaseScale = promptRoot.localScale;
            }

            if (promptRenderers != null)
            {
                promptBaseColors = new Color[promptRenderers.Length];
                for (var i = 0; i < promptRenderers.Length; i++)
                    promptBaseColors[i] = promptRenderers[i] ? promptRenderers[i].color : Color.white;
            }

            cachedVisuals = true;
        }

        bool IsInsidePromptArea(Vector3 worldPosition)
        {
            if (!promptProximityArea)
            {
                return cutPoint &&
                    Vector2.Distance(worldPosition, cutPoint.position) <= promptDistance;
            }

            // Read the collider geometry directly so the area can stay out of the
            // physics simulation and cannot block attacks or projectiles.
            var localPoint = (Vector2)promptProximityArea.transform.InverseTransformPoint(worldPosition);
            var delta = localPoint - promptProximityArea.offset;
            var halfSize = promptProximityArea.size * 0.5f;
            return Mathf.Abs(delta.x) <= halfSize.x && Mathf.Abs(delta.y) <= halfSize.y;
        }

        void DisablePromptAreaPhysics()
        {
            if (Application.isPlaying && promptProximityArea)
                promptProximityArea.enabled = false;
        }

        void SetPromptAlpha(float alpha)
        {
            if (promptRenderers == null || promptBaseColors == null)
                return;

            for (var i = 0; i < promptRenderers.Length; i++)
            {
                if (promptRenderers[i])
                    SetRendererAlpha(promptRenderers[i], promptBaseColors[i], alpha);
            }
        }

        static void SetRendererAlpha(SpriteRenderer renderer, Color baseColor, float alpha)
        {
            baseColor.a *= Mathf.Clamp01(alpha);
            renderer.color = baseColor;
        }

        void PlayDust()
        {
            if (ropeDust && !ropeDust.isPlaying)
                ropeDust.Play();
        }

        void StopDust(bool clear)
        {
            if (!ropeDust || !ropeDust.isPlaying && !clear)
                return;

            ropeDust.Stop(
                true,
                clear
                    ? ParticleSystemStopBehavior.StopEmittingAndClear
                    : ParticleSystemStopBehavior.StopEmitting);
        }

        void OnValidate()
        {
            if (promptProximityArea)
                promptProximityArea.isTrigger = true;
            noticeDistance = Mathf.Max(0.1f, noticeDistance);
            promptDistance = Mathf.Clamp(promptDistance, 0.1f, noticeDistance);
            promptDelay = Mathf.Max(0f, promptDelay);
            promptFadeDuration = Mathf.Max(0.01f, promptFadeDuration);
            pulseSpeed = Mathf.Max(0.01f, pulseSpeed);
            glowMaxAlpha = Mathf.Max(glowMinAlpha, glowMaxAlpha);
        }
    }
}
