using System;
using UnityEngine;

namespace MirrorTrial.Combat
{
    public enum ChargeTelegraphStyle
    {
        RadialCore,
        Sword,
        Punch
    }

    public enum ChargeTelegraphEndReason
    {
        Released,
        Feinted,
        Interrupted,
        Cancelled
    }

    [Serializable]
    public sealed class ChargeTelegraphSettings
    {
        public ChargeTelegraphStyle style;
        public Color coreStartColor = new Color(0.22f, 0.58f, 1f, 0.55f);
        public Color coreFullColor = new Color(0.82f, 0.95f, 1f, 1f);
        public Color bodyGlowColor = new Color(0.18f, 0.62f, 1f, 0.42f);
        [Min(0.01f)] public float coreStartScale = 0.12f;
        [Min(0.01f)] public float coreFullScale = 0.32f;
        [Range(0f, 1f)] public float finalFlashStart = 0.9f;
        [Min(0f)] public float pulseSpeed = 18f;
        [Range(0f, 0.5f)] public float pulseAmount = 0.12f;
        public float rotationSpeed;
        [Range(0f, 2f)] public float particleIntensity = 1f;
        [Min(0.05f)] public float particleRadius = 0.9f;
        [Min(0.05f)] public float particleSpeed = 2.2f;
        [Min(0f)] public float bodyGlowMaxAlpha = 0.32f;
        [Min(1f)] public float bodyGlowScale = 1.035f;
        [Header("Charge stages")]
        [Range(0f, 1f)] public float readyChargeThreshold = 0.7f;
        public AudioClip readyChargeCue;
        [Header("Sword charge")]
        public Color swordOuterColor = new Color(0.2f, 1f, 0.45f, 0.42f);
        public Color swordCoreColor = new Color(0.82f, 1f, 0.86f, 0.9f);
        public float swordAngle;
        [Min(0.1f)] public float swordLength = 1.35f;
        [Min(0.01f)] public float swordWidth = 0.12f;
        [Range(0f, 1f)] public float swordJitterStart = 0.55f;
        [Min(0f)] public float swordJitterAmount = 0.03125f;
        public Vector2 leftFootOffset = new Vector2(-0.3f, 0.03f);
        public Vector2 rightFootOffset = new Vector2(0.3f, 0.03f);
        [Min(0f)] public float dustIntensity = 1f;
        [Header("Punch charge")]
        [Min(0.1f)] public float punchFieldRadius = 1.35f;
        [Min(0.01f)] public float punchCoreStartScale = 0.18f;
        [Min(0.01f)] public float punchCoreReadyScale = 0.72f;
        [Range(0f, 2f)] public float punchStreamIntensity = 1.25f;
        [Range(0f, 0.5f)] public float punchAfterimageAlpha = 0.16f;
        [Range(0f, 2f)] public float punchGroundPull = 1f;
        public AudioClip chargingLoop;
        public AudioClip fullChargeCue;
        public AudioClip releaseCue;
        public AudioClip feintCue;
        public AudioClip interruptedCue;
        [Range(0f, 1f)] public float audioVolume = 0.8f;
    }

    /// <summary>
    /// Shared presentation-only controller for boss windups and player charge attacks.
    /// It never owns combat timing, input, damage, or AI decisions.
    /// </summary>
    public sealed class ChargeTelegraphPresentation : MonoBehaviour
    {
        [SerializeField] ChargeTelegraphSettings defaultSettings = new ChargeTelegraphSettings();
        SpriteRenderer source;
        SpriteRenderer bodyGlow;
        SpriteRenderer coreGlow;
        Transform swordRoot;
        SpriteRenderer swordOuter;
        SpriteRenderer swordCore;
        SpriteRenderer swordAfterimage;
        SpriteRenderer swordTipFlash;
        SpriteRenderer punchAfterimage;
        ParticleSystem particles;
        ParticleSystem dustParticles;
        AudioSource audioSource;
        ChargeTelegraphSettings settings;
        Vector2 localChargeOffset;
        float localEffectAngle;
        float rotationSpeedOverride = float.NaN;
        bool facingRight = true;
        bool active;
        bool fullCuePlayed;
        bool readyCuePlayed;
        float progress;
        float readyThreshold;
        float particleTimer;
        float finishUntil;
        float dustTimer;
        float jitterTimer;
        float jitterOffset;
        float tipFlashUntil;
        float readyFlashUntil;
        float fullLockUntil;
        AudioClip chargingLoopOverride;
        AudioClip fullChargeCueOverride;
        bool loopChargingAudioOverride = true;

#if UNITY_EDITOR
        [NonSerialized] bool editorPreviewActive;
#endif

        static Sprite runtimeSwordSprite;

        public bool IsActive => active;
        public float Progress => progress;
        public ChargeTelegraphSettings DefaultSettings => defaultSettings;

        public static ChargeTelegraphPresentation Ensure(GameObject owner, SpriteRenderer sourceRenderer = null)
        {
            if (!owner) return null;
            var presentation = owner.GetComponent<ChargeTelegraphPresentation>();
            if (!presentation) presentation = owner.AddComponent<ChargeTelegraphPresentation>();
            presentation.Bind(sourceRenderer ? sourceRenderer : owner.GetComponentInChildren<SpriteRenderer>(true));
            return presentation;
        }

        public void Bind(SpriteRenderer sourceRenderer)
        {
            if (sourceRenderer && sourceRenderer != bodyGlow && sourceRenderer != coreGlow)
                source = sourceRenderer;
            EnsureVisuals();
        }

        public void Begin(ChargeTelegraphSettings visualSettings, Vector2 chargeOffset, bool pointsRight, Sprite coreSprite = null,
            float effectAngle = 0f, float readyThresholdOverride = -1f,
            AudioClip loopOverride = null, AudioClip fullCueOverride = null, bool loopChargingAudio = true,
            float effectRotationSpeedOverride = float.NaN)
        {
            settings = visualSettings ?? defaultSettings ?? new ChargeTelegraphSettings();
            chargingLoopOverride = loopOverride;
            fullChargeCueOverride = fullCueOverride;
            loopChargingAudioOverride = loopChargingAudio;
            localChargeOffset = chargeOffset;
            localEffectAngle = effectAngle;
            rotationSpeedOverride = effectRotationSpeedOverride;
            facingRight = pointsRight;
            active = true;
            fullCuePlayed = false;
            readyCuePlayed = false;
            progress = 0f;
            readyThreshold = readyThresholdOverride >= 0f
                ? Mathf.Clamp01(readyThresholdOverride)
                : Mathf.Clamp01(settings.readyChargeThreshold);
            finishUntil = 0f;
            particleTimer = 0f;
            dustTimer = 0f;
            jitterTimer = 0f;
            jitterOffset = 0f;
            tipFlashUntil = 0f;
            readyFlashUntil = 0f;
            fullLockUntil = 0f;
            EnsureVisuals();
            // Sword-style telegraphs intentionally do not create a radial core.
            // A presentation prefab may therefore have no coreGlow at all.
            if (coreGlow)
                coreGlow.sprite = settings.style == ChargeTelegraphStyle.Punch
                    ? Resources.Load<Sprite>("Effects/pixel_charge_star")
                    : coreSprite ? coreSprite : Resources.Load<Sprite>("Effects/pixel_charge_ring");
            SetVisible(true);
            var chargingLoop = chargingLoopOverride ? chargingLoopOverride : settings.chargingLoop;
            if (chargingLoop)
            {
                audioSource.clip = chargingLoop;
                audioSource.volume = settings.audioVolume;
                audioSource.pitch = loopChargingAudioOverride ? 0.82f : 1f;
                audioSource.loop = loopChargingAudioOverride;
                audioSource.Play();
            }
            ApplyVisuals();
        }

        public void SetProgress(float normalized, bool pointsRight)
        {
            if (!active) return;
            facingRight = pointsRight;
            progress = Mathf.Clamp01(normalized);
            if (audioSource && audioSource.isPlaying && loopChargingAudioOverride)
                audioSource.pitch = Mathf.Lerp(0.82f, 1.18f, progress);
            if (!readyCuePlayed && progress >= readyThreshold)
            {
                readyCuePlayed = true;
                readyFlashUntil = Time.unscaledTime + 0.16f;
                if (settings.style == ChargeTelegraphStyle.Punch)
                    EmitPunchLockBurst();
                PlayOneShot(settings.readyChargeCue);
            }
            if (!fullCuePlayed && progress >= 1f)
            {
                fullCuePlayed = true;
                if (settings.style == ChargeTelegraphStyle.Sword)
                    tipFlashUntil = Time.unscaledTime + 0.12f;
                else if (settings.style == ChargeTelegraphStyle.Punch)
                {
                    fullLockUntil = Time.unscaledTime + 0.2f;
                    EmitPunchFullBurst();
                }
                PlayOneShot(fullChargeCueOverride ? fullChargeCueOverride : settings.fullChargeCue);
            }
            ApplyVisuals();
        }

        public void End(ChargeTelegraphEndReason reason, bool pointsRight)
        {
            if (!active && finishUntil <= 0f) return;
            facingRight = pointsRight;
            active = false;
            StopLoop();
            switch (reason)
            {
                case ChargeTelegraphEndReason.Released:
                    PlayOneShot(settings?.releaseCue);
                    EmitBurst(false);
                    finishUntil = Time.unscaledTime + 0.1f;
                    break;
                case ChargeTelegraphEndReason.Feinted:
                    PlayOneShot(settings?.feintCue);
                    EmitBurst(true);
                    SetVisible(false);
                    finishUntil = Time.unscaledTime + 0.14f;
                    break;
                case ChargeTelegraphEndReason.Interrupted:
                    PlayOneShot(settings?.interruptedCue);
                    EmitBurst(true);
                    SetVisible(false);
                    finishUntil = Time.unscaledTime + 0.08f;
                    break;
                default:
                    finishUntil = 0f;
                    SetVisible(false);
                    break;
            }
        }

        void Awake()
        {
            if (!source) source = GetComponentInChildren<SpriteRenderer>(true);
            EnsureVisuals();
            SetVisible(false);
        }

        void Update()
        {
            if (active)
            {
                if (settings != null && settings.style == ChargeTelegraphStyle.Sword)
                {
                    EmitSwordGatheringParticles();
                    EmitFootDust();
                    UpdateSwordJitter();
                }
                else if (settings != null && settings.style == ChargeTelegraphStyle.Punch)
                {
                    EmitPunchGatheringParticles();
                    EmitPunchGroundPull();
                }
                else
                    EmitGatheringParticles();
                ApplyVisuals();
                return;
            }
            if (finishUntil > 0f && Time.unscaledTime >= finishUntil)
            {
                finishUntil = 0f;
                SetVisible(false);
            }
        }

        void LateUpdate()
        {
            if (settings != null && settings.style == ChargeTelegraphStyle.Sword)
            {
                UpdateSwordRoot();
                return;
            }
            if (!source || !bodyGlow) return;
            bodyGlow.sprite = source.sprite;
            bodyGlow.flipX = source.flipX;
            bodyGlow.flipY = source.flipY;
            bodyGlow.drawMode = source.drawMode;
            bodyGlow.size = source.size;
            bodyGlow.sortingLayerID = source.sortingLayerID;
            bodyGlow.sortingOrder = source.sortingOrder - 1;
            var sourceTransform = source.transform;
            var glowTransform = bodyGlow.transform;
            glowTransform.position = sourceTransform.position;
            glowTransform.rotation = sourceTransform.rotation;
            var ownerScale = transform.lossyScale;
            var sourceScale = sourceTransform.lossyScale;
            var glowScale = settings?.bodyGlowScale ?? 1.035f;
            glowTransform.localScale = new Vector3(
                sourceScale.x / Mathf.Max(0.0001f, Mathf.Abs(ownerScale.x)),
                sourceScale.y / Mathf.Max(0.0001f, Mathf.Abs(ownerScale.y)),
                sourceScale.z / Mathf.Max(0.0001f, Mathf.Abs(ownerScale.z))) * glowScale;
            UpdateChargePoint();
            if (settings != null && settings.style == ChargeTelegraphStyle.Punch)
                UpdatePunchPresentation(sourceTransform, sourceScale, ownerScale);
        }

        void EnsureVisuals()
        {
            if (Application.isPlaying && !audioSource)
            {
                audioSource = GetComponent<AudioSource>();
                if (!audioSource) audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;
            }
            if ((settings?.style ?? defaultSettings?.style) == ChargeTelegraphStyle.Sword)
            {
                EnsureSwordVisuals();
                return;
            }
            if (!bodyGlow)
            {
                var go = new GameObject("Charge Body Glow");
                MarkEditorPreviewObject(go);
                go.transform.SetParent(transform, false);
                bodyGlow = go.AddComponent<SpriteRenderer>();
            }
            if (!coreGlow)
            {
                var go = new GameObject("Charge Weapon Core");
                MarkEditorPreviewObject(go);
                go.transform.SetParent(transform, false);
                coreGlow = go.AddComponent<SpriteRenderer>();
                coreGlow.sprite = Resources.Load<Sprite>("Effects/pixel_charge_ring");
                coreGlow.sortingOrder = 120;
            }
            var punchStyle = (settings?.style ?? defaultSettings?.style) == ChargeTelegraphStyle.Punch;
            if (punchStyle && !punchAfterimage)
            {
                var go = new GameObject("Punch Charge Afterimage");
                MarkEditorPreviewObject(go);
                go.transform.SetParent(transform, false);
                punchAfterimage = go.AddComponent<SpriteRenderer>();
            }
            if (Application.isPlaying && !particles)
                particles = PixelParticleUtility.Create(transform, 121, !punchStyle);
            if (Application.isPlaying && punchStyle && !dustParticles)
                dustParticles = PixelParticleUtility.Create(transform, 119, false);
        }

        void EnsureSwordVisuals()
        {
            if (!swordRoot)
            {
                var root = new GameObject("Sword Charge Root");
                MarkEditorPreviewObject(root);
                root.transform.SetParent(transform, false);
                swordRoot = root.transform;
            }
            var sprite = GetRuntimeSwordSprite();
            swordOuter = EnsureSwordRenderer(swordOuter, "Sword Outer Glow", sprite, 121);
            swordCore = EnsureSwordRenderer(swordCore, "Sword Core Glow", sprite, 122);
            swordAfterimage = EnsureSwordRenderer(swordAfterimage, "Sword Afterimage", sprite, 120);
            if (!swordTipFlash)
            {
                var go = new GameObject("Sword Tip Flash");
                MarkEditorPreviewObject(go);
                go.transform.SetParent(swordRoot, false);
                swordTipFlash = go.AddComponent<SpriteRenderer>();
                swordTipFlash.sprite = Resources.Load<Sprite>("Effects/pixel_charge_star");
                swordTipFlash.sortingOrder = 123;
            }
            if (Application.isPlaying && !particles) particles = PixelParticleUtility.Create(transform, 121, true);
            if (Application.isPlaying && !dustParticles) dustParticles = PixelParticleUtility.Create(transform, 119, false);
        }

        SpriteRenderer EnsureSwordRenderer(SpriteRenderer renderer, string objectName, Sprite sprite, int order)
        {
            if (!renderer)
            {
                var go = new GameObject(objectName);
                MarkEditorPreviewObject(go);
                go.transform.SetParent(swordRoot, false);
                renderer = go.AddComponent<SpriteRenderer>();
            }
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            return renderer;
        }

        static void MarkEditorPreviewObject(GameObject go)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                go.hideFlags = HideFlags.HideAndDontSave;
#endif
        }

        static Sprite GetRuntimeSwordSprite()
        {
            if (runtimeSwordSprite) return runtimeSwordSprite;
            const int width = 3;
            const int height = 24;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Runtime Sword Charge Strip",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color[width * height];
            for (var i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            runtimeSwordSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0f), 32f);
            runtimeSwordSprite.name = "Runtime Sword Charge Strip";
            runtimeSwordSprite.hideFlags = HideFlags.HideAndDontSave;
            return runtimeSwordSprite;
        }

        void ApplyVisuals()
        {
            if (settings == null) return;
            if (settings.style == ChargeTelegraphStyle.Sword)
            {
                ApplySwordVisuals();
                return;
            }
            if (settings.style == ChargeTelegraphStyle.Punch)
            {
                ApplyPunchVisuals();
                return;
            }
            if (!coreGlow || !bodyGlow) return;
            var finalT = Mathf.InverseLerp(settings.finalFlashStart, 1f, progress);
            var pulse = 1f + Mathf.Sin(Time.unscaledTime * settings.pulseSpeed) * settings.pulseAmount * Mathf.Lerp(0.35f, 1f, progress);
            coreGlow.color = Color.Lerp(settings.coreStartColor, settings.coreFullColor, progress);
            var scale = Mathf.Lerp(settings.coreStartScale, settings.coreFullScale, progress) * pulse;
            coreGlow.transform.localScale = Vector3.one * scale;
            var mirroredAngle = facingRight ? localEffectAngle : -localEffectAngle;
            var rotationSpeed = float.IsNaN(rotationSpeedOverride) ? settings.rotationSpeed : rotationSpeedOverride;
            coreGlow.transform.localRotation = Quaternion.Euler(0f, 0f, mirroredAngle + Time.unscaledTime * rotationSpeed);
            var glowColor = settings.bodyGlowColor;
            glowColor.a = Mathf.Lerp(0.04f, settings.bodyGlowMaxAlpha, progress) * Mathf.Lerp(1f, 1.35f, finalT);
            bodyGlow.color = glowColor;
            UpdateChargePoint();
        }

        void ApplyPunchVisuals()
        {
            if (!coreGlow || !bodyGlow) return;
            UpdateChargePoint();
            var now = Time.unscaledTime;
            var ready = progress >= readyThreshold;
            var full = progress >= settings.finalFlashStart;
            var chargeT = Mathf.SmoothStep(0f, 1f, progress);
            var pulse = ready
                ? 1f + Mathf.Sin(now * settings.pulseSpeed * 0.45f) * settings.pulseAmount * 0.35f
                : 1f + Mathf.Sin(now * settings.pulseSpeed) * settings.pulseAmount * chargeT;
            var flashScale = readyFlashUntil > now ? 1.55f : fullLockUntil > now ? 1.35f : 1f;
            var scale = Mathf.Lerp(settings.punchCoreStartScale, settings.punchCoreReadyScale, chargeT) * pulse * flashScale;
            coreGlow.transform.localScale = Vector3.one * scale;
            coreGlow.transform.localRotation = Quaternion.Euler(0f, 0f,
                Mathf.Round(now * Mathf.Lerp(4f, 10f, chargeT)) * 12f);
            var coreColor = Color.Lerp(settings.coreStartColor, settings.coreFullColor, chargeT);
            coreColor.a = Mathf.Lerp(0.18f, 1f, chargeT);
            if (readyFlashUntil > now || fullLockUntil > now) coreColor = Color.white;
            coreGlow.color = coreColor;

            var glowColor = settings.bodyGlowColor;
            glowColor.a = Mathf.Lerp(0.025f, settings.bodyGlowMaxAlpha, chargeT) * (ready ? 1f : 0.65f);
            bodyGlow.color = glowColor;
            if (punchAfterimage)
            {
                var afterColor = settings.bodyGlowColor;
                afterColor.a = ready ? settings.punchAfterimageAlpha * (full ? 1f : 0.65f) : 0f;
                punchAfterimage.color = afterColor;
                punchAfterimage.enabled = afterColor.a > 0.001f;
            }
        }

        void UpdatePunchPresentation(Transform sourceTransform, Vector3 sourceScale, Vector3 ownerScale)
        {
            if (!punchAfterimage || !source) return;
            punchAfterimage.sprite = source.sprite;
            punchAfterimage.flipX = source.flipX;
            punchAfterimage.flipY = source.flipY;
            punchAfterimage.drawMode = source.drawMode;
            punchAfterimage.size = source.size;
            punchAfterimage.sortingLayerID = source.sortingLayerID;
            punchAfterimage.sortingOrder = source.sortingOrder - 2;
            var direction = facingRight ? -1f : 1f;
            punchAfterimage.transform.position = sourceTransform.position + Vector3.right * direction * 0.075f;
            punchAfterimage.transform.rotation = sourceTransform.rotation;
            punchAfterimage.transform.localScale = new Vector3(
                sourceScale.x / Mathf.Max(0.0001f, Mathf.Abs(ownerScale.x)),
                sourceScale.y / Mathf.Max(0.0001f, Mathf.Abs(ownerScale.y)),
                sourceScale.z / Mathf.Max(0.0001f, Mathf.Abs(ownerScale.z)));
            coreGlow.sortingLayerID = source.sortingLayerID;
            coreGlow.sortingOrder = source.sortingOrder + 4;
            if (particles)
            {
                var renderer = particles.GetComponent<ParticleSystemRenderer>();
                renderer.sortingLayerID = source.sortingLayerID;
                renderer.sortingOrder = source.sortingOrder + 3;
            }
            if (dustParticles)
            {
                var renderer = dustParticles.GetComponent<ParticleSystemRenderer>();
                renderer.sortingLayerID = source.sortingLayerID;
                renderer.sortingOrder = source.sortingOrder + 1;
            }
        }

        void ApplySwordVisuals()
        {
            if (!swordRoot || !swordOuter || !swordCore || !swordAfterimage || !swordTipFlash) return;
            UpdateSwordRoot();
            var chargeT = Mathf.SmoothStep(0f, 1f, progress);
            var outer = settings.swordOuterColor;
            outer.a *= Mathf.Lerp(0.22f, 1f, chargeT);
            swordOuter.color = outer;
            var core = settings.swordCoreColor;
            core.a *= Mathf.InverseLerp(0.15f, 1f, progress);
            swordCore.color = core;
            var after = settings.swordOuterColor;
            after.a = Mathf.Lerp(0f, 0.22f, Mathf.InverseLerp(settings.swordJitterStart, 1f, progress));
            swordAfterimage.color = after;

            const float spriteWidth = 3f / 32f;
            const float spriteHeight = 24f / 32f;
            swordOuter.transform.localScale = new Vector3(settings.swordWidth / spriteWidth, settings.swordLength / spriteHeight, 1f);
            swordCore.transform.localScale = new Vector3(settings.swordWidth * 0.42f / spriteWidth, settings.swordLength / spriteHeight, 1f);
            swordAfterimage.transform.localScale = swordOuter.transform.localScale;
            swordOuter.transform.localPosition = new Vector3(jitterOffset, 0f, 0f);
            swordCore.transform.localPosition = new Vector3(jitterOffset, 0f, 0f);
            swordAfterimage.transform.localPosition = new Vector3(-jitterOffset, 0f, 0f);

            swordTipFlash.transform.localPosition = new Vector3(jitterOffset, settings.swordLength, 0f);
            var flashT = tipFlashUntil > Time.unscaledTime
                ? 1f - (tipFlashUntil - Time.unscaledTime) / 0.12f
                : 1f;
            var flashVisible = tipFlashUntil > Time.unscaledTime;
#if UNITY_EDITOR
            if (!Application.isPlaying && editorPreviewActive)
                flashVisible = progress >= settings.finalFlashStart;
#endif
            swordTipFlash.enabled = flashVisible;
            if (flashVisible)
            {
                var alpha = 1f - flashT;
                swordTipFlash.color = new Color(0.88f, 1f, 0.9f, alpha);
                swordTipFlash.transform.localScale = Vector3.one * Mathf.Lerp(0.06f, 0.2f, Mathf.Sin(flashT * Mathf.PI));
            }
        }

        void UpdateSwordRoot()
        {
            if (!swordRoot || settings == null) return;
            var offset = localChargeOffset;
            offset.x *= facingRight ? 1f : -1f;
            swordRoot.localPosition = offset;
            var rotationSpeed = float.IsNaN(rotationSpeedOverride) ? settings.rotationSpeed : rotationSpeedOverride;
            var combinedAngle = settings.swordAngle + localEffectAngle + Time.unscaledTime * rotationSpeed;
            swordRoot.localRotation = Quaternion.Euler(0f, 0f, facingRight ? combinedAngle : -combinedAngle);
            if (!source) return;
            var layer = source.sortingLayerID;
            var order = source.sortingOrder;
            swordAfterimage.sortingLayerID = layer;
            swordOuter.sortingLayerID = layer;
            swordCore.sortingLayerID = layer;
            swordTipFlash.sortingLayerID = layer;
            swordAfterimage.sortingOrder = order + 1;
            swordOuter.sortingOrder = order + 2;
            swordCore.sortingOrder = order + 3;
            swordTipFlash.sortingOrder = order + 4;
            if (particles)
            {
                var renderer = particles.GetComponent<ParticleSystemRenderer>();
                renderer.sortingLayerID = layer;
                renderer.sortingOrder = order + 2;
            }
            if (dustParticles)
            {
                var renderer = dustParticles.GetComponent<ParticleSystemRenderer>();
                renderer.sortingLayerID = layer;
                renderer.sortingOrder = order + 1;
            }
        }

        void UpdateSwordJitter()
        {
            if (settings == null || progress < settings.swordJitterStart)
            {
                jitterOffset = 0f;
                return;
            }
            jitterTimer -= Time.unscaledDeltaTime;
            if (jitterTimer > 0f) return;
            var jitterT = Mathf.InverseLerp(settings.swordJitterStart, 1f, progress);
            jitterTimer = Mathf.Lerp(3f / 60f, 2f / 60f, jitterT);
            var direction = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            jitterOffset = direction * settings.swordJitterAmount * Mathf.Lerp(0.5f, 1f, jitterT);
        }

        void EmitSwordGatheringParticles()
        {
            if (settings == null || progress < 0.2f || settings.particleIntensity <= 0f || !particles || !swordRoot) return;
            particleTimer -= Time.unscaledDeltaTime;
            if (particleTimer > 0f) return;
            var gatherT = Mathf.InverseLerp(0.2f, 1f, progress);
            particleTimer = Mathf.Lerp(0.1f, 0.025f, gatherT) / settings.particleIntensity;
            var count = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(1f, 2f, gatherT) * settings.particleIntensity));
            for (var i = 0; i < count; i++)
            {
                var side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
                var localSpawn = new Vector2(side * UnityEngine.Random.Range(0.45f, 1.05f), UnityEngine.Random.Range(0.05f, 1.35f));
                localSpawn.x *= facingRight ? 1f : -1f;
                var spawn = transform.TransformPoint(localSpawn);
                var target = swordRoot.TransformPoint(new Vector3(0f, UnityEngine.Random.Range(0.15f, settings.swordLength), 0f));
                var delta = target - spawn;
                var speed = Mathf.Lerp(1.1f, 3.2f, gatherT);
                var colorRoll = UnityEngine.Random.value;
                var color = colorRoll < 0.5f ? new Color(0.42f, 1f, 0.55f, 0.9f)
                    : colorRoll < 0.8f ? Color.white
                    : new Color(0.65f, 1f, 0.9f, 0.9f);
                PixelParticleUtility.Emit(particles, spawn, delta.normalized * speed,
                    UnityEngine.Random.Range(0.025f, 0.055f), delta.magnitude / speed, color);
            }
        }

        void EmitFootDust()
        {
            if (settings == null || settings.dustIntensity <= 0f || !dustParticles) return;
            dustTimer -= Time.unscaledDeltaTime;
            if (dustTimer > 0f) return;
            dustTimer = Mathf.Lerp(0.2f, 0.11f, progress) / settings.dustIntensity;
            var foot = UnityEngine.Random.value < 0.5f ? settings.leftFootOffset : settings.rightFootOffset;
            foot.x *= facingRight ? 1f : -1f;
            var spawn = transform.TransformPoint(foot + UnityEngine.Random.insideUnitCircle * 0.04f);
            var count = progress > 0.8f && UnityEngine.Random.value < 0.45f ? 2 : 1;
            for (var i = 0; i < count; i++)
            {
                var velocity = new Vector2(UnityEngine.Random.Range(-0.35f, 0.35f), UnityEngine.Random.Range(0.25f, 0.7f));
                var color = Color.Lerp(new Color(0.36f, 0.34f, 0.3f, 0.75f), new Color(0.58f, 0.53f, 0.43f, 0.8f), UnityEngine.Random.value);
                PixelParticleUtility.Emit(dustParticles, spawn, velocity, UnityEngine.Random.Range(0.035f, 0.065f), UnityEngine.Random.Range(0.14f, 0.28f), color);
            }
        }

        void UpdateChargePoint()
        {
            if (!coreGlow) return;
            var offset = localChargeOffset;
            offset.x *= facingRight ? 1f : -1f;
            coreGlow.transform.localPosition = offset;
        }

        void EmitGatheringParticles()
        {
            if (settings == null || settings.particleIntensity <= 0f || !particles || !coreGlow) return;
            particleTimer -= Time.unscaledDeltaTime;
            if (particleTimer > 0f) return;
            particleTimer = Mathf.Lerp(0.075f, 0.02f, progress) / settings.particleIntensity;
            var count = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(1f, 3f, progress) * settings.particleIntensity));
            var center = coreGlow.transform.position;
            for (var i = 0; i < count; i++)
            {
                var angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                var radial = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var radius = settings.particleRadius * UnityEngine.Random.Range(0.75f, 1.15f);
                var position = center + (Vector3)(radial * radius);
                var velocity = -radial * settings.particleSpeed * Mathf.Lerp(0.7f, 1.35f, progress);
                var color = Color.Lerp(settings.coreStartColor, settings.coreFullColor, UnityEngine.Random.Range(0.2f, 0.9f));
                color.a = Mathf.Lerp(0.55f, 1f, progress);
                PixelParticleUtility.Emit(particles, position, velocity,
                    UnityEngine.Random.Range(0.035f, 0.075f),
                    radius / Mathf.Max(0.1f, velocity.magnitude), color);
            }
        }

        void EmitPunchGatheringParticles()
        {
            if (settings == null || settings.particleIntensity <= 0f || !particles || !coreGlow) return;
            particleTimer -= Time.unscaledDeltaTime;
            if (particleTimer > 0f) return;

            var ready = progress >= readyThreshold;
            var intensity = Mathf.Max(0.1f, settings.particleIntensity * settings.punchStreamIntensity);
            particleTimer = Mathf.Lerp(0.085f, ready ? 0.018f : 0.026f, progress) / intensity;
            var count = Mathf.Max(1, Mathf.RoundToInt((ready ? 2f : Mathf.Lerp(1f, 3f, progress)) * intensity));
            var target = coreGlow.transform.position;
            var fieldRadius = settings.punchFieldRadius;
            for (var i = 0; i < count; i++)
            {
                var localSpawn = new Vector2(
                    UnityEngine.Random.Range(-fieldRadius, fieldRadius),
                    UnityEngine.Random.Range(0.05f, fieldRadius * 1.15f));
                if (Mathf.Abs(localSpawn.x) < 0.4f)
                    localSpawn.x = Mathf.Sign(localSpawn.x == 0f ? UnityEngine.Random.Range(-1f, 1f) : localSpawn.x) * 0.4f;
                var spawn = transform.TransformPoint(localSpawn);
                var delta = (Vector2)(target - spawn);
                var speed = Mathf.Lerp(1.15f, ready ? 5.4f : 4.1f, progress);
                var tangent = new Vector2(-delta.y, delta.x).normalized * UnityEngine.Random.Range(-0.22f, 0.22f);
                var velocity = delta.normalized * speed + tangent;
                var color = Color.Lerp(settings.coreStartColor, settings.coreFullColor,
                    UnityEngine.Random.Range(0.25f, ready ? 1f : 0.78f));
                color.a = Mathf.Lerp(0.48f, 1f, progress);
                PixelParticleUtility.Emit(particles, spawn, velocity,
                    UnityEngine.Random.Range(0.035f, ready ? 0.065f : 0.085f),
                    delta.magnitude / Mathf.Max(0.1f, speed), color);
            }
        }

        void EmitPunchGroundPull()
        {
            if (settings == null || settings.punchGroundPull <= 0f || !dustParticles || !coreGlow) return;
            dustTimer -= Time.unscaledDeltaTime;
            if (dustTimer > 0f) return;
            dustTimer = Mathf.Lerp(0.16f, 0.055f, progress) / settings.punchGroundPull;

            var side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            var spawnLocal = new Vector2(side * UnityEngine.Random.Range(0.35f, settings.punchFieldRadius),
                UnityEngine.Random.Range(0.01f, 0.06f));
            var spawn = transform.TransformPoint(spawnLocal);
            var target = (Vector2)coreGlow.transform.position;
            var delta = target - (Vector2)spawn;
            var speed = Mathf.Lerp(0.7f, 1.75f, progress);
            var velocity = delta.normalized * speed;
            velocity.y = Mathf.Max(0.18f, velocity.y * 0.35f);
            var color = Color.Lerp(new Color(0.3f, 0.34f, 0.4f, 0.55f),
                new Color(0.5f, 0.68f, 0.78f, 0.75f), progress);
            PixelParticleUtility.Emit(dustParticles, spawn, velocity,
                UnityEngine.Random.Range(0.035f, 0.07f),
                delta.magnitude / Mathf.Max(0.1f, speed), color);
        }

        void EmitPunchLockBurst()
        {
            if (settings == null || !particles || !coreGlow) return;
            var target = (Vector2)coreGlow.transform.position;
            const int count = 14;
            for (var i = 0; i < count; i++)
            {
                var angle = Mathf.PI * 2f * i / count + UnityEngine.Random.Range(-0.1f, 0.1f);
                var radial = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var radius = settings.punchFieldRadius * UnityEngine.Random.Range(0.55f, 1f);
                var spawn = target + radial * radius;
                var speed = UnityEngine.Random.Range(4.5f, 6.2f);
                PixelParticleUtility.Emit(particles, spawn, -radial * speed,
                    UnityEngine.Random.Range(0.045f, 0.075f), radius / speed, settings.coreFullColor);
            }
        }

        void EmitPunchFullBurst()
        {
            if (settings == null || !particles || !coreGlow) return;
            var center = coreGlow.transform.position;
            const int count = 8;
            for (var i = 0; i < count; i++)
            {
                var angle = Mathf.PI * 2f * i / count;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                PixelParticleUtility.Emit(particles, center, direction * UnityEngine.Random.Range(0.8f, 1.35f),
                    UnityEngine.Random.Range(0.035f, 0.055f), 0.1f, Color.white);
            }
        }

        void EmitBurst(bool outward)
        {
            if (settings == null || !particles || !coreGlow) return;
            var center = coreGlow.transform.position;
            var count = outward ? 12 : 9;
            for (var i = 0; i < count; i++)
            {
                var angle = Mathf.PI * 2f * i / count + UnityEngine.Random.Range(-0.12f, 0.12f);
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                if (!outward) direction = Vector2.Lerp(direction, facingRight ? Vector2.right : Vector2.left, 0.65f).normalized;
                PixelParticleUtility.Emit(particles, center, direction * UnityEngine.Random.Range(2f, 4f),
                    UnityEngine.Random.Range(0.045f, 0.09f), UnityEngine.Random.Range(0.15f, 0.28f), settings.coreFullColor);
            }
        }

        void SetVisible(bool visible)
        {
            if (bodyGlow) bodyGlow.enabled = visible;
            if (coreGlow) coreGlow.enabled = visible;
            if (swordOuter) swordOuter.enabled = visible;
            if (swordCore) swordCore.enabled = visible;
            if (swordAfterimage) swordAfterimage.enabled = visible;
            if (swordTipFlash) swordTipFlash.enabled = visible && tipFlashUntil > Time.unscaledTime;
            if (punchAfterimage) punchAfterimage.enabled = visible && progress >= readyThreshold;
        }

        void StopLoop()
        {
            if (!audioSource) return;
            audioSource.Stop();
            audioSource.loop = false;
            audioSource.clip = null;
        }

        void PlayOneShot(AudioClip clip)
        {
            if (audioSource && clip) audioSource.PlayOneShot(clip, settings?.audioVolume ?? 0.8f);
        }

        void OnDisable()
        {
            active = false;
            finishUntil = 0f;
            StopLoop();
            SetVisible(false);
        }

#if UNITY_EDITOR
        public void EditorSetPreview(bool visible, float normalizedProgress, bool pointsRight)
        {
            EditorSetPreview(visible, normalizedProgress, pointsRight, Vector2.zero, 0f);
        }

        public void EditorSetPreview(bool visible, float normalizedProgress, bool pointsRight, Vector2 chargeOffset)
        {
            EditorSetPreview(visible, normalizedProgress, pointsRight, chargeOffset, 0f);
        }

        public void EditorSetPreview(bool visible, float normalizedProgress, bool pointsRight, Vector2 chargeOffset,
            float effectAngle, float effectRotationSpeedOverride = float.NaN)
        {
            editorPreviewActive = visible;
            if (!visible)
            {
                SetVisible(false);
                UnityEditor.SceneView.RepaintAll();
                return;
            }

            settings = defaultSettings ?? new ChargeTelegraphSettings();
            progress = Mathf.Clamp01(normalizedProgress);
            facingRight = pointsRight;
            localChargeOffset = chargeOffset;
            localEffectAngle = effectAngle;
            rotationSpeedOverride = effectRotationSpeedOverride;
            jitterOffset = progress >= settings.swordJitterStart ? settings.swordJitterAmount : 0f;
            readyThreshold = Mathf.Clamp01(settings.readyChargeThreshold);
            EnsureVisuals();
            SetVisible(true);
            ApplyVisuals();
            UnityEditor.SceneView.RepaintAll();
        }

        void OnDrawGizmosSelected()
        {
            if (!editorPreviewActive || settings == null) return;
            if (settings.style == ChargeTelegraphStyle.Sword)
            {
                var left = settings.leftFootOffset;
                var right = settings.rightFootOffset;
                left.x *= facingRight ? 1f : -1f;
                right.x *= facingRight ? 1f : -1f;
                Gizmos.color = new Color(0.65f, 0.55f, 0.4f, 0.9f);
                Gizmos.DrawWireSphere(transform.TransformPoint(left), 0.07f);
                Gizmos.DrawWireSphere(transform.TransformPoint(right), 0.07f);

                Gizmos.color = new Color(0.35f, 1f, 0.55f, 0.35f);
                var center = transform.position + Vector3.up * 0.7f;
                Gizmos.DrawWireCube(center, new Vector3(2.1f, 1.4f, 0f));
                if (swordRoot)
                {
                    var target = swordRoot.TransformPoint(Vector3.up * settings.swordLength * 0.6f);
                    Gizmos.DrawLine(center + Vector3.left, target);
                    Gizmos.DrawLine(center + Vector3.right, target);
                }
            }
            else if (settings.style == ChargeTelegraphStyle.Punch)
            {
                Gizmos.color = new Color(settings.coreFullColor.r, settings.coreFullColor.g,
                    settings.coreFullColor.b, 0.3f);
                var center = transform.TransformPoint(localChargeOffset);
                Gizmos.DrawWireSphere(center, settings.punchCoreReadyScale * 0.5f);
                Gizmos.DrawWireCube(transform.position + Vector3.up * settings.punchFieldRadius * 0.55f,
                    new Vector3(settings.punchFieldRadius * 2f, settings.punchFieldRadius * 1.15f, 0f));
            }
            else
            {
                Gizmos.color = new Color(settings.coreFullColor.r, settings.coreFullColor.g, settings.coreFullColor.b, 0.35f);
                Gizmos.DrawWireSphere(transform.TransformPoint(localChargeOffset), settings.particleRadius);
            }
        }
#endif
    }
}
