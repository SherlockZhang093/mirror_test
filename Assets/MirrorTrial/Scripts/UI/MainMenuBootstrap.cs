using System;
using System.Collections;
using MirrorTrial.Audio;
using MirrorTrial.Level;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MirrorTrial.UI
{
    public static class MainMenuBootstrap
    {
        const string FirstTutorialSequenceId = "PoYing_R01_Intro_v1";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == GameFlow.EntrySceneName)
            {
                if (!UnityEngine.Object.FindObjectOfType<MainMenuController>())
                    Debug.LogError("GameEntry scene is missing its prebuilt MainMenuController.");
                return;
            }

            if (scene.name == GameFlow.FirstRealitySceneName && !GameFlow.TutorialRequested)
                DisableFirstTutorial();
        }

        static void DisableFirstTutorial()
        {
            var triggers = UnityEngine.Object.FindObjectsOfType<StorySequenceTrigger>(true);
            for (var i = 0; i < triggers.Length; i++)
            {
                var sequence = triggers[i].Sequence;
                if (sequence && sequence.SequenceId == FirstTutorialSequenceId)
                    triggers[i].enabled = false;
            }

            var sequences = UnityEngine.Object.FindObjectsOfType<StoryTutorialSequence>(true);
            for (var i = 0; i < sequences.Length; i++)
            {
                if (sequences[i].SequenceId == FirstTutorialSequenceId)
                    sequences[i].enabled = false;
            }
        }
    }

    public class MainMenuController : MonoBehaviour
    {
        const string PresentationMarker = "LayeredPresentation_v1";
        const string ResourceRoot = "UI/GameEntry/";
        const string BackgroundResource = ResourceRoot + "GameEntry_BackgroundPlate_v1";
        const string MirrorResource = ResourceRoot + "GameEntry_MirrorPortal_v1";
        const string BlueFragmentResource = ResourceRoot + "GameEntry_Fragment_BlueRuins_v1";
        const string JungleFragmentResource = ResourceRoot + "GameEntry_Fragment_Jungle_v1";
        const string VioletFragmentResource = ResourceRoot + "GameEntry_Fragment_VioletRuins_v1";
        const string ShardsResource = ResourceRoot + "GameEntry_FloatingShardsSheet_v1";

        static readonly Color BackgroundColor = new Color(0.018f, 0.027f, 0.055f, 1f);
        static readonly Color TransitionInk = new Color(0.012f, 0.021f, 0.044f, 1f);

        Font font;

        [Header("Generated Entry Artwork")]
        [SerializeField] Texture backgroundTexture;
        [SerializeField] Texture mirrorTexture;
        [SerializeField] Texture blueRuinsTexture;
        [SerializeField] Texture jungleTexture;
        [SerializeField] Texture violetRuinsTexture;
        [SerializeField] Texture floatingShardsTexture;

        [Header("Player Prefab Animation")]
        [SerializeField] Sprite[] protagonistIdleFrames;
        [SerializeField] Sprite[] protagonistWalkFrames;

        [SerializeField] GameObject landingPanel;
        [SerializeField] CanvasGroup landingGroup;
        [SerializeField] Button startButton;
        [SerializeField] RectTransform mirrorRoot;
        [SerializeField] RectTransform protagonistRoot;
        [SerializeField] RawImage mirrorImage;
        [SerializeField] Image protagonistImage;
        [SerializeField] Image transitionFade;
        [SerializeField] GameEntryLayerMotion mirrorMotion;
        [SerializeField] GameEntryLayerMotion protagonistMotion;
        [SerializeField] GameEntrySpriteSequence protagonistSequence;

        bool loading;
        bool runtimeListenerAdded;

        void OnValidate()
        {
            if (!Application.isPlaying)
                BindGeneratedArtwork();
        }

        void Awake()
        {
            Time.timeScale = 1f;
            font = CreateChineseFont();

            var canvas = transform.Find("MainMenuCanvas");
            var marker = canvas ? canvas.Find(PresentationMarker) : null;
            if (!canvas || !marker || !landingPanel || !landingGroup || !startButton ||
                !mirrorRoot || !protagonistRoot || !mirrorImage || !protagonistImage ||
                !transitionFade || !mirrorMotion || !protagonistMotion || !protagonistSequence)
            {
                Debug.LogError(
                    "GameEntry is missing its serialized layered presentation. " +
                    "Run Mirror Trial/Rebuild Game Entry Scene in the editor.", this);
                enabled = false;
                return;
            }

            if (startButton.onClick.GetPersistentEventCount() == 0)
            {
                startButton.onClick.AddListener(StartWithTutorialPreference);
                runtimeListenerAdded = true;
            }
        }

        void OnDestroy()
        {
            if (runtimeListenerAdded && startButton)
                startButton.onClick.RemoveListener(StartWithTutorialPreference);
        }

        public void BuildSceneContent(Texture fallbackBackground = null)
        {
            font = CreateChineseFont();
            EnsureEntryCamera();
            EnsureEventSystem();
            BindGeneratedArtwork(fallbackBackground);
            BuildMenu(
                backgroundTexture,
                mirrorTexture,
                blueRuinsTexture,
                jungleTexture,
                violetRuinsTexture,
                floatingShardsTexture);
        }

        public void ConfigureProtagonistAnimation(Sprite[] idleFrames, Sprite[] walkFrames)
        {
            protagonistIdleFrames = idleFrames;
            protagonistWalkFrames = walkFrames;
        }

        void BindGeneratedArtwork(Texture fallbackBackground = null)
        {
            // Always resolve the generated artwork by its canonical Resources paths.
            // This repairs stale or accidentally swapped serialized bindings after a reimport.
            backgroundTexture = LoadTexture(BackgroundResource, fallbackBackground);
            mirrorTexture = LoadTexture(MirrorResource);
            blueRuinsTexture = LoadTexture(BlueFragmentResource);
            jungleTexture = LoadTexture(JungleFragmentResource);
            violetRuinsTexture = LoadTexture(VioletFragmentResource);
            floatingShardsTexture = LoadTexture(ShardsResource);
        }

        void Update()
        {
            if (loading || !landingPanel || !landingPanel.activeSelf) return;
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
                StartWithTutorialPreference();
        }

        void BuildMenu(Texture background, Texture mirror, Texture blueFragment, Texture jungleFragment,
            Texture violetFragment, Texture shards)
        {
            var canvasObject = new GameObject("MainMenuCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (background)
                CreateRawImage("BackgroundPlate", canvasObject.transform, background, Vector2.zero, Vector2.one,
                    Vector2.zero, Vector2.zero, new Rect(0f, 0f, 1f, 1f), Color.white);
            else
                CreateImage("BackgroundPlate", canvasObject.transform, BackgroundColor, Vector2.zero, Vector2.one,
                    Vector2.zero, Vector2.zero);

            var world = CreatePanel(PresentationMarker, canvasObject.transform, Color.clear,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            BuildStars(world.transform);

            CreateMovingLayer("BlueRuinsFragment", world.transform, blueFragment,
                new Vector2(-610f, 140f), new Vector2(550f, 644f),
                new Rect(0.149963f, 0f, 0.687637f, 0.95656f),
                new Vector2(7f, 10f), 0.48f, 0.4f, 0.28f, 0.012f, out _, out _);

            CreateMovingLayer("JungleFragment", world.transform, jungleFragment,
                new Vector2(575f, 205f), new Vector2(500f, 526f),
                new Rect(0.256579f, 0.025505f, 0.513158f, 0.959617f),
                new Vector2(6f, 9f), 0.41f, 2.1f, -0.22f, 0.01f, out _, out _);

            CreateMovingLayer("VioletRuinsFragment", world.transform, violetFragment,
                new Vector2(515f, -315f), new Vector2(375f, 398f),
                new Rect(0.279306f, 0f, 0.509569f, 0.959617f),
                new Vector2(5f, 11f), 0.55f, 4.2f, 0.36f, 0.014f, out _, out _);

            CreateMovingLayer("MainMirror", world.transform, mirror,
                new Vector2(-5f, -42f), new Vector2(478f, 770f),
                new Rect(0.131676f, 0.082182f, 0.729282f, 0.880525f),
                new Vector2(2f, 3f), 0.36f, 1.2f, 0.08f, 0.008f,
                out mirrorRoot, out mirrorImage, 0.13f);
            mirrorMotion = mirrorRoot ? mirrorRoot.GetComponent<GameEntryLayerMotion>() : null;

            // Keep the small shards above the large world pieces and mirror so their motion remains readable.
            BuildFloatingShards(world.transform, shards);

            BuildProtagonist(world.transform);

            landingPanel = CreatePanel("Landing", canvasObject.transform, Color.clear,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            landingGroup = landingPanel.AddComponent<CanvasGroup>();

            CreateText("Title", landingPanel.transform, "破映", 96, FontStyle.Normal,
                new Color(0.9f, 0.97f, 1f, 1f), TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-300f, -170f), new Vector2(300f, -48f));
            CreateImage("TitleLineLeft", landingPanel.transform, new Color(0.55f, 0.84f, 1f, 0.38f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-245f, -178f), new Vector2(-88f, -176f));
            CreateImage("TitleLineRight", landingPanel.transform, new Color(0.55f, 0.84f, 1f, 0.38f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(88f, -178f), new Vector2(245f, -176f));

            startButton = CreateButton("StartButton", landingPanel.transform, "踏入镜隙",
                new Vector2(1f, 0.5f), new Vector2(-365f, -44f), new Vector2(300f, 72f));

            transitionFade = CreateImage("TransitionFade", canvasObject.transform,
                new Color(0.72f, 0.91f, 1f, 0f), Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero).GetComponent<Image>();
            transitionFade.raycastTarget = false;
        }

        void BuildStars(Transform parent)
        {
            var rng = new System.Random(731);
            for (var i = 0; i < 18; i++)
            {
                var x = (float)(rng.NextDouble() * 1740.0 - 870.0);
                var y = (float)(rng.NextDouble() * 820.0 - 250.0);
                var size = (float)(rng.NextDouble() * 2.2 + 1.2);
                var alpha = (float)(rng.NextDouble() * 0.35 + 0.25);
                var star = CreateImage("Star_" + (i + 1).ToString("00"), parent,
                    new Color(0.68f, 0.9f, 1f, alpha), new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(x + size, y + size));
                var motion = star.AddComponent<GameEntryLayerMotion>();
                motion.Configure(Vector2.zero, 0.35f + i * 0.021f, i * 0.73f, 0f, 0.35f, 0.35f);
            }
        }

        void BuildFloatingShards(Transform parent, Texture shards)
        {
            if (!shards) return;
            var positions = new[]
            {
                new Vector2(-350f, 310f), new Vector2(-270f, 145f), new Vector2(305f, 300f),
                new Vector2(360f, 85f), new Vector2(-330f, -120f), new Vector2(-185f, -340f),
                new Vector2(275f, -300f), new Vector2(395f, -180f), new Vector2(-760f, -90f),
                new Vector2(785f, 55f), new Vector2(650f, -390f), new Vector2(-590f, 400f)
            };

            for (var i = 0; i < positions.Length; i++)
            {
                var col = i % 4;
                var row = i / 4;
                var uv = new Rect(col * 0.25f, (2 - row) / 3f, 0.25f, 1f / 3f);
                var size = 64f + (i % 3) * 14f;
                var root = CreateCenteredRoot("FloatingShard_" + (i + 1).ToString("00"), parent,
                    positions[i], new Vector2(size, size * 1.15f));
                CreateRawImage("Shard", root.transform, shards, Vector2.zero, Vector2.one,
                    Vector2.zero, Vector2.zero, uv, new Color(1f, 1f, 1f, 0.72f));
                var motion = root.AddComponent<GameEntryLayerMotion>();
                motion.Configure(new Vector2(3f + i % 3, 7f + i % 4),
                    0.32f + i * 0.027f, i * 0.82f, 1.2f + i % 3, 0.012f, 0.08f);
            }
        }

        void CreateMovingLayer(string objectName, Transform parent, Texture texture, Vector2 position,
            Vector2 size, Rect uvRect, Vector2 drift, float speed, float phase, float rotation,
            float scalePulse, out RectTransform rootRect, out RawImage mainImage, float glowAlpha = 0.08f)
        {
            rootRect = null;
            mainImage = null;
            if (!texture) return;

            var root = CreateCenteredRoot(objectName, parent, position, size);
            rootRect = root.GetComponent<RectTransform>();

            CreateRawImage("Glow", root.transform, texture, Vector2.zero, Vector2.one,
                new Vector2(-8f, -8f), new Vector2(8f, 8f), uvRect,
                new Color(0.55f, 0.88f, 1f, glowAlpha));
            mainImage = CreateRawImage("Artwork", root.transform, texture, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, uvRect, Color.white).GetComponent<RawImage>();

            var motion = root.AddComponent<GameEntryLayerMotion>();
            motion.Configure(drift, speed, phase, rotation, scalePulse, 0f);
        }

        void BuildProtagonist(Transform parent)
        {
            var initialSprite = protagonistIdleFrames != null && protagonistIdleFrames.Length > 0
                ? protagonistIdleFrames[0]
                : protagonistWalkFrames != null && protagonistWalkFrames.Length > 0
                    ? protagonistWalkFrames[0]
                    : null;
            if (!initialSprite)
            {
                Debug.LogError("GameEntry could not find the Player_MirrorTrial animation frames.", this);
                return;
            }

            var root = CreateCenteredRoot("Protagonist", parent,
                new Vector2(-405f, -245f), new Vector2(216f, 189f));
            protagonistRoot = root.GetComponent<RectTransform>();

            var imageObject = CreateImage("PlayerSprite", root.transform, Color.white,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            protagonistImage = imageObject.GetComponent<Image>();
            protagonistImage.sprite = initialSprite;
            protagonistImage.preserveAspect = true;

            protagonistSequence = root.AddComponent<GameEntrySpriteSequence>();
            protagonistSequence.Configure(protagonistImage, protagonistIdleFrames, protagonistWalkFrames,
                10f, 9f);

            protagonistMotion = root.AddComponent<GameEntryLayerMotion>();
            protagonistMotion.Configure(new Vector2(0.6f, 1.6f), 0.72f, 0.7f, 0.06f, 0.004f, 0f);
        }

        public void StartWithTutorialPreference()
        {
            if (!loading)
            {
                GlobalAudioFeedback.PlaySelect();
                StartCoroutine(StartGameRoutine(true));
            }
        }

        IEnumerator StartGameRoutine(bool playTutorial)
        {
            loading = true;
            startButton.interactable = false;
            landingGroup.blocksRaycasts = false;
            landingGroup.interactable = false;
            if (mirrorMotion) mirrorMotion.enabled = false;
            if (protagonistMotion) protagonistMotion.enabled = false;
            if (protagonistSequence) protagonistSequence.SetWalking(true);

            var mirrorStartScale = mirrorRoot ? mirrorRoot.localScale : Vector3.one;
            var protagonistStart = protagonistRoot ? protagonistRoot.anchoredPosition : Vector2.zero;
            var protagonistTarget = new Vector2(-105f, -185f);
            const float duration = 1.35f;
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var smooth = t * t * (3f - 2f * t);

                landingGroup.alpha = 1f - Mathf.Clamp01(t / 0.22f);
                if (protagonistRoot)
                    protagonistRoot.anchoredPosition = Vector2.Lerp(protagonistStart, protagonistTarget,
                        Mathf.Clamp01(smooth / 0.72f));
                if (protagonistImage)
                {
                    var color = protagonistImage.color;
                    color.a = 1f - Mathf.Clamp01((t - 0.48f) / 0.28f);
                    protagonistImage.color = color;
                }
                if (mirrorRoot)
                    mirrorRoot.localScale = mirrorStartScale * Mathf.Lerp(1f, 1.075f, smooth);

                var fade = Mathf.Clamp01((t - 0.43f) / 0.52f);
                if (transitionFade)
                    transitionFade.color = new Color(TransitionInk.r, TransitionInk.g, TransitionInk.b, fade);
                yield return null;
            }

            GameFlow.StartNewGame(playTutorial);
        }

        Button CreateButton(string objectName, Transform parent, string label, Vector2 anchor,
            Vector2 position, Vector2 size)
        {
            var buttonObject = CreateImage(objectName, parent, new Color(0.025f, 0.09f, 0.14f, 0.72f),
                anchor, anchor, position, position + size);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = position + size * 0.5f;

            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = buttonObject.GetComponent<Image>();
            button.targetGraphic.raycastTarget = true;
            var colors = button.colors;
            colors.normalColor = new Color(0.025f, 0.09f, 0.14f, 0.72f);
            colors.highlightedColor = new Color(0.08f, 0.42f, 0.52f, 0.9f);
            colors.pressedColor = new Color(0.12f, 0.62f, 0.72f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.04f, 0.07f, 0.09f, 0.4f);
            colors.fadeDuration = 0.12f;
            button.colors = colors;

            CreateImage("TopLine", buttonObject.transform, new Color(0.52f, 0.9f, 1f, 0.8f),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(18f, -2f), new Vector2(-18f, 0f));
            var text = CreateText("Label", buttonObject.transform, label, 26, FontStyle.Normal, Color.white,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            text.raycastTarget = false;
            return button;
        }

        GameObject CreateCenteredRoot(string objectName, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return go;
        }

        GameObject CreatePanel(string objectName, Transform parent, Color color, Vector2 anchorMin,
            Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            return CreateImage(objectName, parent, color, anchorMin, anchorMax, offsetMin, offsetMax);
        }

        GameObject CreateImage(string objectName, Transform parent, Color color, Vector2 anchorMin,
            Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return go;
        }

        GameObject CreateRawImage(string objectName, Transform parent, Texture texture, Vector2 anchorMin,
            Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Rect uvRect, Color color)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            var image = go.GetComponent<RawImage>();
            image.texture = texture;
            image.uvRect = uvRect;
            image.color = color;
            image.raycastTarget = false;
            return go;
        }

        Text CreateText(string objectName, Transform parent, string value, int size, FontStyle style,
            Color color, TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            var text = go.GetComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        static Texture LoadTexture(string resourcePath, Texture fallback = null)
        {
            var texture = Resources.Load<Texture2D>(resourcePath);
            if (!texture)
                Debug.LogWarning("GameEntry texture was not found in Resources: " + resourcePath);
            return texture ? texture : fallback;
        }

        static Font CreateChineseFont()
        {
            var names = new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial" };
            return Font.CreateDynamicFontFromOSFont(names, 32);
        }

        static void EnsureEntryCamera()
        {
            if (Camera.main) return;
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            var entryCamera = cameraObject.GetComponent<Camera>();
            entryCamera.clearFlags = CameraClearFlags.SolidColor;
            entryCamera.backgroundColor = BackgroundColor;
            entryCamera.orthographic = true;
            entryCamera.cullingMask = 0;
            entryCamera.depth = -100f;
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current || UnityEngine.Object.FindObjectOfType<EventSystem>(true)) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }
    }

}
