using MirrorTrial.Level;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MirrorTrial.UI
{
    /// <summary>
    /// Creates the menu presentation when the dedicated entry scene is loaded.
    /// The scene stays intentionally minimal and the hierarchy is runtime-owned.
    /// </summary>
    public static class MainMenuBootstrap
    {
        const string FirstTutorialSequenceId = "Level_Reality_01_StoryTutorial_v1";

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
                if (!Object.FindObjectOfType<MainMenuController>())
                    Debug.LogError("GameEntry scene is missing its prebuilt MainMenuController.");
                return;
            }

            if (scene.name == GameFlow.FirstRealitySceneName && !GameFlow.TutorialRequested)
                DisableFirstTutorial();
        }

        static void DisableFirstTutorial()
        {
            var triggers = Object.FindObjectsOfType<StorySequenceTrigger>(true);
            for (var i = 0; i < triggers.Length; i++)
            {
                var sequence = triggers[i].Sequence;
                if (sequence && sequence.SequenceId == FirstTutorialSequenceId)
                    triggers[i].enabled = false;
            }

            var sequences = Object.FindObjectsOfType<StoryTutorialSequence>(true);
            for (var i = 0; i < sequences.Length; i++)
            {
                if (sequences[i].SequenceId == FirstTutorialSequenceId)
                    sequences[i].enabled = false;
            }
        }
    }

    public class MainMenuController : MonoBehaviour
    {
        static readonly Color BackgroundColor = new Color(0.018f, 0.027f, 0.055f, 1f);
        Font font;
        [SerializeField] GameObject landingPanel;
        [SerializeField] Button startButton;
        [SerializeField] Toggle skipTutorialToggle;
        bool loading;

        void Awake()
        {
            font = CreateChineseFont();
            if (!landingPanel || !startButton || !skipTutorialToggle)
            {
                Debug.LogError("GameEntry UI is not prebuilt. Run Mirror Trial/Rebuild Game Entry Scene in the editor.");
                enabled = false;
                return;
            }

            Time.timeScale = 1f;
        }

        public void BuildSceneContent(Texture menuBackground = null)
        {
            font = CreateChineseFont();
            EnsureEntryCamera();
            EnsureEventSystem();
            BuildMenu(menuBackground);
        }

        void Update()
        {
            if (loading) return;

            if (landingPanel.activeSelf && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)))
                StartWithTutorialPreference();
        }

        void BuildMenu(Texture menuBackground)
        {
            var canvasObject = new GameObject("MainMenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (menuBackground)
                CreateRawImage("BackgroundArtwork", canvasObject.transform, menuBackground, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            else
                CreateImage("BackgroundArtwork", canvasObject.transform, BackgroundColor, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            landingPanel = CreatePanel("Landing", canvasObject.transform, Color.clear,
                new Vector2(0f, 0f), Vector2.one, Vector2.zero, Vector2.zero);

            CreateText("BrandMark", landingPanel.transform, "◯", 42, FontStyle.Normal, new Color(0.75f, 0.88f, 1f, 1f),
                TextAnchor.MiddleCenter, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(44f, -88f), new Vector2(92f, -36f));
            CreateText("BrandText", landingPanel.transform, "M I R R O R   T R I A L", 18, FontStyle.Normal, new Color(0.82f, 0.84f, 0.9f, 1f),
                TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(126f, -78f), new Vector2(480f, -42f));

            var settingsButton = CreateButton("SettingsButton", landingPanel.transform, "⚙  设置", false,
                new Vector2(1f, 1f), new Vector2(-200f, -86f), new Vector2(160f, 52f));
            settingsButton.GetComponent<Image>().color = new Color(0.02f, 0.04f, 0.08f, 0.18f);

            CreateText("Title", landingPanel.transform, "镜中试炼", 92, FontStyle.Normal, new Color(0.92f, 0.96f, 1f, 1f),
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-520f, 96f), new Vector2(1040f, 220f));
            CreateImage("TitleDividerLeft", landingPanel.transform, new Color(0.68f, 0.78f, 0.92f, 0.55f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-350f, 62f), new Vector2(-220f, 64f));
            CreateImage("TitleDividerRight", landingPanel.transform, new Color(0.68f, 0.78f, 0.92f, 0.55f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(220f, 62f), new Vector2(350f, 64f));
            CreateText("Subtitle", landingPanel.transform, "M I R R O R   T R I A L", 23, FontStyle.Normal, new Color(0.84f, 0.87f, 0.93f, 1f),
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-420f, 40f), new Vector2(840f, 82f));
            CreateText("Tagline", landingPanel.transform, "打 碎 镜 子，夺 回 失 落 的 自 己", 22, FontStyle.Normal, new Color(0.72f, 0.76f, 0.84f, 1f),
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-460f, -22f), new Vector2(920f, 22f));

            startButton = CreateButton("StartButton", landingPanel.transform, "开 始 试 炼", true,
                new Vector2(0.5f, 0.5f), new Vector2(-235f, -385f), new Vector2(470f, 76f));
            skipTutorialToggle = CreateToggle("SkipTutorialToggle", landingPanel.transform, "跳过新手教程",
                new Vector2(0.5f, 0.5f), new Vector2(-150f, -326f), new Vector2(300f, 38f));

            CreateText("InputHint", landingPanel.transform, "◉  滑动或点击开始", 18, FontStyle.Normal, new Color(0.7f, 0.75f, 0.82f, 1f),
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-300f, -442f), new Vector2(600f, 36f));
            CreateText("DownArrow", landingPanel.transform, "⌄", 42, FontStyle.Normal, new Color(0.75f, 0.82f, 0.92f, 1f),
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-50f, -492f), new Vector2(100f, 46f));

        }

        public void StartWithTutorialPreference()
        {
            StartGame(!skipTutorialToggle.isOn);
        }

        void StartGame(bool playTutorial)
        {
            if (loading) return;
            loading = true;

            var buttons = GetComponentsInChildren<Button>(true);
            for (var i = 0; i < buttons.Length; i++)
                buttons[i].interactable = false;
            skipTutorialToggle.interactable = false;

            GameFlow.StartNewGame(playTutorial);
        }

        Toggle CreateToggle(string objectName, Transform parent, string label, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var toggleObject = new GameObject(objectName, typeof(RectTransform), typeof(Toggle));
            toggleObject.transform.SetParent(parent, false);
            var rect = toggleObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = position + size * 0.5f;

            var box = CreateImage("Background", toggleObject.transform, new Color(0.03f, 0.08f, 0.15f, 0.95f),
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, -14f), new Vector2(28f, 14f));
            var checkmark = CreateText("Checkmark", box.transform, "✓", 24, FontStyle.Bold, new Color(0.55f, 0.9f, 1f, 1f),
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            checkmark.raycastTarget = false;
            var labelText = CreateText("Label", toggleObject.transform, label, 20, FontStyle.Normal, new Color(0.82f, 0.87f, 0.94f, 1f),
                TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, new Vector2(42f, 0f), Vector2.zero);
            labelText.raycastTarget = false;

            var toggle = toggleObject.GetComponent<Toggle>();
            toggle.targetGraphic = box.GetComponent<Image>();
            toggle.graphic = checkmark;
            toggle.isOn = false;
            return toggle;
        }

        Button CreateButton(string objectName, Transform parent, string label, bool primary, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var color = primary ? new Color(0.09f, 0.56f, 0.66f, 1f) : new Color(0.08f, 0.12f, 0.19f, 1f);
            var buttonObject = CreateImage(objectName, parent, color, anchor, anchor, position, position + size);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = position + size * 0.5f;

            var button = buttonObject.AddComponent<Button>();
            button.targetGraphic = buttonObject.GetComponent<Image>();
            var colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = primary ? new Color(0.13f, 0.72f, 0.82f, 1f) : new Color(0.13f, 0.2f, 0.29f, 1f);
            colors.pressedColor = new Color(0.04f, 0.38f, 0.47f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.12f, 0.14f, 0.17f, 0.75f);
            colors.fadeDuration = 0.12f;
            button.colors = colors;

            var text = CreateText("Label", buttonObject.transform, label, primary ? 26 : 22, FontStyle.Bold, Color.white,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            text.raycastTarget = false;
            return button;
        }

        GameObject CreatePanel(string objectName, Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            return CreateImage(objectName, parent, color, anchorMin, anchorMax, offsetMin, offsetMax);
        }

        GameObject CreateImage(string objectName, Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            go.GetComponent<Image>().color = color;
            return go;
        }

        GameObject CreateRawImage(string objectName, Transform parent, Texture texture, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
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
            image.color = Color.white;
            image.raycastTarget = false;
            return go;
        }

        Text CreateText(string objectName, Transform parent, string value, int size, FontStyle style, Color color,
            TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
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
            if (EventSystem.current) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }
    }
}
