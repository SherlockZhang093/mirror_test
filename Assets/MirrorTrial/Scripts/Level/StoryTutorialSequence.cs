using System;
using System.Collections;
using System.Collections.Generic;
using MirrorTrial.Audio;
using MirrorTrial.HealthResources;
using MirrorTrial.Player;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using AtlasPopulationMode = TMPro.AtlasPopulationMode;

namespace MirrorTrial.Level
{
    public enum StoryTutorialStepType
    {
        TitleCard,
        CameraShot,
        Dialogue,
        MoveObjective,
        JumpObjective,
        PrimaryAttackObjective,
        DodgeObjective,
        Wait,
        HealthResourceObjective,
        SystemMessage
    }

    [Serializable]
    public sealed class StoryTutorialStep
    {
        public StoryTutorialStepType type;
        public string speaker;
        [TextArea(2, 5)] public string text;
        public string hint;
        [Min(0f)] public float duration = 1.5f;
        [Min(0.1f)] public float requiredAmount = 2f;
        public Transform cameraTarget;
        [Min(1f)] public float cameraSize = 5.5f;
        [Min(0f)] public float blendIn = 0.6f;
        [Min(0f)] public float blendOut = 0.5f;
    }

    /// <summary>
    /// A lightweight, scene-authored story sequence that also validates tutorial actions.
    /// It intentionally builds its own presentation layer so level designers only author steps.
    /// </summary>
    [DefaultExecutionOrder(80)]
    public sealed class StoryTutorialSequence : MonoBehaviour
    {
        [Header("播放")]
        [SerializeField] string displayName = "新剧情段落";
        [SerializeField] string sequenceId = "Level_Reality_01_Intro";
        [SerializeField] bool playOnStart = true;
        [SerializeField] bool rememberCompletion = true;
        [SerializeField] bool replayInEditor = true;
        [SerializeField, Min(0.3f)] float skipHoldDuration = 1.2f;

        [Header("内容")]
        [SerializeField] List<StoryTutorialStep> steps = new List<StoryTutorialStep>();

        [Header("表现")]
        [SerializeField] TMP_FontAsset chineseFont;
        [SerializeField] Color accentColor = new Color(0.23f, 0.93f, 1f, 1f);
        [SerializeField] Color panelColor = new Color(0.025f, 0.04f, 0.075f, 0.94f);
        [SerializeField, Range(0.005f, 0.08f)] float typewriterInterval = 0.022f;
        [SerializeField, Min(0f)] float objectiveSuccessHold = 0.7f;

        [Header("事件")]
        [SerializeField] UnityEvent onSequenceStarted;
        [SerializeField] UnityEvent onSequenceCompleted;

        static readonly HashSet<string> CompletedThisSession = new HashSet<string>();
        static TMP_FontAsset runtimeChineseFont;

        PlayerInputReader playerInput;
        PlayerMotor playerMotor;
        Transform player;
        Coroutine routine;
        bool inputLocked;
        float skipHeld;

        CanvasGroup rootGroup;
        Image fadeImage;
        RectTransform topBar;
        RectTransform bottomBar;
        GameObject dialoguePanel;
        TMP_Text speakerText;
        TMP_Text dialogueText;
        TMP_Text advanceText;
        GameObject objectivePanel;
        TMP_Text objectiveTitle;
        TMP_Text objectiveHint;
        Image objectiveMarker;
        TMP_Text titleText;
        TMP_Text skipText;

        public bool IsPlaying => routine != null;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string SequenceId => sequenceId;
        public IReadOnlyList<StoryTutorialStep> Steps => steps;
        public event Action SequenceStarted;
        public event Action SequenceCompleted;

        void Start()
        {
            if (playOnStart && ShouldPlay())
                Play();
        }

        void Update()
        {
            if (!IsPlaying) return;

            if (inputLocked)
                ApplyInputLock(true);

            if (Input.GetKey(KeyCode.Escape))
            {
                skipHeld += Time.unscaledDeltaTime;
                if (skipText)
                    skipText.text = $"长按 ESC 跳过  {Mathf.Clamp01(skipHeld / skipHoldDuration):P0}";
                if (skipHeld >= skipHoldDuration)
                    Skip();
            }
            else
            {
                skipHeld = 0f;
                if (skipText) skipText.text = "长按 ESC 跳过";
            }
        }

        void OnDisable()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }
            CameraDirector.Instance?.StopShot();
            ApplyInputLock(false);
        }

        public void Play()
        {
            if (routine != null || steps.Count == 0) return;
            routine = StartCoroutine(PlayRoutine());
        }

        public void Skip()
        {
            if (routine == null) return;
            GlobalAudioFeedback.PlayCancel();
            StopCoroutine(routine);
            routine = null;
            CameraDirector.Instance?.StopShot();
            CompleteSequence();
        }

        public void Replay()
        {
            if (routine != null) return;
            Play();
        }

        public void Configure(string id, TMP_FontAsset font, List<StoryTutorialStep> authoredSteps, string title = null)
        {
            sequenceId = id;
            if (!string.IsNullOrWhiteSpace(title)) displayName = title;
            chineseFont = font;
            steps = authoredSteps ?? new List<StoryTutorialStep>();
        }

        public void ClearCompletionRecord()
        {
            if (string.IsNullOrWhiteSpace(sequenceId)) return;
            CompletedThisSession.Remove(sequenceId);
            PlayerPrefs.DeleteKey(CompletionKey());
            PlayerPrefs.Save();
        }

        bool ShouldPlay()
        {
            if (string.IsNullOrWhiteSpace(sequenceId)) return true;
            if (CompletedThisSession.Contains(sequenceId)) return false;
#if UNITY_EDITOR
            if (replayInEditor) return true;
#endif
            return !rememberCompletion || PlayerPrefs.GetInt(CompletionKey(), 0) == 0;
        }

        IEnumerator PlayRoutine()
        {
            yield return null;
            ResolvePlayer();
            BuildUi();
            SetAllPresentationVisible(false);
            rootGroup.alpha = 1f;
            onSequenceStarted?.Invoke();
            SequenceStarted?.Invoke();

            for (var i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                if (step == null) continue;
                yield return PlayStep(step);
            }

            routine = null;
            CompleteSequence();
        }

        IEnumerator PlayStep(StoryTutorialStep step)
        {
            switch (step.type)
            {
                case StoryTutorialStepType.TitleCard:
                    yield return PlayTitle(step);
                    break;
                case StoryTutorialStepType.CameraShot:
                    yield return PlayCameraShot(step);
                    break;
                case StoryTutorialStepType.Dialogue:
                case StoryTutorialStepType.SystemMessage:
                    yield return PlayDialogue(step);
                    break;
                case StoryTutorialStepType.MoveObjective:
                case StoryTutorialStepType.JumpObjective:
                case StoryTutorialStepType.PrimaryAttackObjective:
                case StoryTutorialStepType.DodgeObjective:
                case StoryTutorialStepType.HealthResourceObjective:
                    yield return PlayObjective(step);
                    break;
                case StoryTutorialStepType.Wait:
                    SetAllPresentationVisible(false);
                    yield return WaitUnscaled(step.duration);
                    break;
            }
        }

        IEnumerator PlayTitle(StoryTutorialStep step)
        {
            SetAllPresentationVisible(false);
            ApplyInputLock(true);
            fadeImage.gameObject.SetActive(true);
            titleText.gameObject.SetActive(true);
            titleText.text = step.text;
            fadeImage.color = new Color(0.005f, 0.012f, 0.025f, 0f);
            titleText.alpha = 0f;

            yield return Fade(0f, 0.9f, 0.35f, value =>
            {
                var color = fadeImage.color;
                color.a = value;
                fadeImage.color = color;
                titleText.alpha = value;
            });
            yield return WaitUnscaled(Mathf.Max(0.2f, step.duration));
            yield return Fade(0.9f, 0f, 0.35f, value =>
            {
                var color = fadeImage.color;
                color.a = value;
                fadeImage.color = color;
                titleText.alpha = value;
            });

            fadeImage.gameObject.SetActive(false);
            titleText.gameObject.SetActive(false);
        }

        IEnumerator PlayCameraShot(StoryTutorialStep step)
        {
            SetAllPresentationVisible(false);
            SetCinematicBars(true);
            ApplyInputLock(true);

            var target = step.cameraTarget ? step.cameraTarget : player;
            if (target)
            {
                var completed = false;
                var preset = ScriptableObject.CreateInstance<CameraShotPreset>();
                preset.blendIn = step.blendIn;
                preset.holdDuration = Mathf.Max(0.1f, step.duration);
                preset.blendOut = step.blendOut;
                preset.orthographicSize = step.cameraSize;
                preset.targetOffset = Vector3.zero;
                preset.lockPlayerInput = true;
                preset.useUnscaledTime = true;
                CameraDirector.Ensure().PlayShot(target, preset, () => completed = true);

                while (!completed && CameraDirector.Instance && CameraDirector.Instance.IsPlaying)
                    yield return null;
                Destroy(preset);
            }
            else
            {
                yield return WaitUnscaled(step.duration);
            }

            ApplyInputLock(false);
            SetCinematicBars(false);
        }

        IEnumerator PlayDialogue(StoryTutorialStep step)
        {
            SetAllPresentationVisible(false);
            SetCinematicBars(true);
            dialoguePanel.SetActive(true);
            ApplyInputLock(true);

            var hasSpeaker = !string.IsNullOrWhiteSpace(step.speaker);
            speakerText.gameObject.SetActive(hasSpeaker);
            speakerText.text = hasSpeaker ? step.speaker : string.Empty;
            SetRect(
                dialogueText.rectTransform,
                new Vector2(0.035f, 0.17f),
                new Vector2(0.96f, hasSpeaker ? 0.68f : 0.88f),
                Vector2.zero,
                Vector2.zero);
            dialogueText.text = string.Empty;
            advanceText.text = "空格 / 回车  继续";
            advanceText.alpha = 0.45f;

            var line = step.text ?? string.Empty;
            for (var i = 0; i < line.Length; i++)
            {
                dialogueText.text = line.Substring(0, i + 1);
                if (AdvancePressed())
                {
                    dialogueText.text = line;
                    break;
                }
                yield return WaitUnscaled(typewriterInterval);
            }

            yield return WaitUnscaled(0.15f);
            advanceText.alpha = 1f;
            while (!AdvancePressed())
                yield return null;

            dialoguePanel.SetActive(false);
            SetCinematicBars(false);
            ApplyInputLock(false);
            yield return WaitUnscaled(0.12f);
        }

        IEnumerator PlayObjective(StoryTutorialStep step)
        {
            SetAllPresentationVisible(false);
            ResolvePlayer();
            ApplyInputLock(false);
            objectivePanel.SetActive(true);
            objectiveMarker.color = accentColor;
            objectiveTitle.color = Color.white;
            objectiveTitle.text = string.IsNullOrWhiteSpace(step.text) ? DefaultObjectiveTitle(step.type) : step.text;
            objectiveHint.text = string.IsNullOrWhiteSpace(step.hint) ? DefaultObjectiveHint(step.type) : step.hint;

            var startPosition = player ? player.position : Vector3.zero;
            var healthResource = step.type == StoryTutorialStepType.HealthResourceObjective && step.cameraTarget
                ? step.cameraTarget.GetComponentInParent<HealthResourceNode>()
                : null;
            var completed = false;
            while (!completed)
            {
                if (!playerInput) ResolvePlayer();
                switch (step.type)
                {
                    case StoryTutorialStepType.MoveObjective:
                        completed = player && Mathf.Abs(player.position.x - startPosition.x) >= Mathf.Max(0.1f, step.requiredAmount);
                        break;
                    case StoryTutorialStepType.JumpObjective:
                        completed = playerInput && playerInput.JumpPressed;
                        break;
                    case StoryTutorialStepType.PrimaryAttackObjective:
                        completed = playerInput && playerInput.AttackPressed;
                        break;
                    case StoryTutorialStepType.DodgeObjective:
                        completed = playerInput && playerInput.DodgePressed;
                        break;
                    case StoryTutorialStepType.HealthResourceObjective:
                        completed = !healthResource || healthResource.IsDestroyed;
                        break;
                }
                yield return null;
            }

            objectiveMarker.color = new Color(0.35f, 1f, 0.62f, 1f);
            objectiveTitle.color = new Color(0.55f, 1f, 0.72f, 1f);
            objectiveHint.text = "已掌握";
            yield return WaitUnscaled(objectiveSuccessHold);
            objectivePanel.SetActive(false);
        }

        void CompleteSequence()
        {
            ApplyInputLock(false);
            SetAllPresentationVisible(false);
            if (rootGroup) rootGroup.alpha = 0f;

            if (!string.IsNullOrWhiteSpace(sequenceId))
            {
                CompletedThisSession.Add(sequenceId);
                if (rememberCompletion)
                {
                    PlayerPrefs.SetInt(CompletionKey(), 1);
                    PlayerPrefs.Save();
                }
            }
            onSequenceCompleted?.Invoke();
            SequenceCompleted?.Invoke();
        }

        string CompletionKey() => "MirrorTrial.StoryTutorial." + sequenceId;

        void ResolvePlayer()
        {
            var bridge = MirrorTransitionBridge.Instance;
            if (bridge && bridge.PersistentPlayer)
                player = bridge.PersistentPlayer.transform;
            if (!player)
            {
                playerInput = FindObjectOfType<PlayerInputReader>();
                if (playerInput) player = playerInput.transform;
            }
            if (player)
            {
                if (!playerInput) playerInput = player.GetComponent<PlayerInputReader>();
                if (!playerMotor) playerMotor = player.GetComponent<PlayerMotor>();
            }
        }

        void ApplyInputLock(bool locked)
        {
            inputLocked = locked;
            if (!playerInput) ResolvePlayer();
            if (playerInput) playerInput.InputEnabled = !locked;
            if (playerMotor)
            {
                playerMotor.MovementLocked = locked;
                if (locked) playerMotor.CancelForcedVelocity();
            }
        }

        bool AdvancePressed()
        {
            return Input.GetKeyDown(KeyCode.Space) ||
                   Input.GetKeyDown(KeyCode.Return) ||
                   Input.GetKeyDown(KeyCode.KeypadEnter) ||
                   Input.GetKeyDown(KeyCode.J) ||
                   Input.GetMouseButtonDown(0);
        }

        static string DefaultObjectiveTitle(StoryTutorialStepType type)
        {
            switch (type)
            {
                case StoryTutorialStepType.MoveObjective: return "向前探索";
                case StoryTutorialStepType.JumpObjective: return "越过断层";
                case StoryTutorialStepType.PrimaryAttackObjective: return "挥动武器";
                case StoryTutorialStepType.DodgeObjective: return "闪避危险";
                case StoryTutorialStepType.HealthResourceObjective: return "打碎生命能量";
                default: return "完成目标";
            }
        }

        static string DefaultObjectiveHint(StoryTutorialStepType type)
        {
            switch (type)
            {
                case StoryTutorialStepType.MoveObjective: return "A / D 或 ← / →  移动";
                case StoryTutorialStepType.JumpObjective: return "空格  跳跃";
                case StoryTutorialStepType.PrimaryAttackObjective: return "J 或鼠标左键  攻击";
                case StoryTutorialStepType.DodgeObjective: return "左 Shift  闪避";
                case StoryTutorialStepType.HealthResourceObjective: return "J 或鼠标左键  攻击";
                default: return string.Empty;
            }
        }

        IEnumerator WaitUnscaled(float duration)
        {
            var end = Time.unscaledTime + Mathf.Max(0f, duration);
            while (Time.unscaledTime < end)
                yield return null;
        }

        IEnumerator Fade(float from, float to, float duration, Action<float> setter)
        {
            if (duration <= 0f)
            {
                setter(to);
                yield break;
            }
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                setter(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / duration)));
                yield return null;
            }
            setter(to);
        }

        void SetAllPresentationVisible(bool visible)
        {
            if (!rootGroup) return;
            dialoguePanel.SetActive(visible);
            objectivePanel.SetActive(visible);
            fadeImage.gameObject.SetActive(visible);
            titleText.gameObject.SetActive(visible);
            SetCinematicBars(visible);
        }

        void SetCinematicBars(bool visible)
        {
            if (topBar) topBar.gameObject.SetActive(visible);
            if (bottomBar) bottomBar.gameObject.SetActive(visible);
            if (skipText) skipText.gameObject.SetActive(visible);
        }

        void BuildUi()
        {
            if (rootGroup) return;

            var root = new GameObject("StoryTutorialUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2000;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            rootGroup = root.GetComponent<CanvasGroup>();

            fadeImage = CreateImage(root.transform, "Fade", new Color(0.005f, 0.012f, 0.025f, 0.92f));
            Stretch(fadeImage.rectTransform);

            titleText = CreateText(root.transform, "Title", 54f, FontStyles.Bold, TextAlignmentOptions.Center);
            SetRect(titleText.rectTransform, new Vector2(0.15f, 0.35f), new Vector2(0.85f, 0.65f), Vector2.zero, Vector2.zero);

            topBar = CreateImage(root.transform, "TopBar", Color.black).rectTransform;
            SetRect(topBar, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -88f), Vector2.zero);
            bottomBar = CreateImage(root.transform, "BottomBar", Color.black).rectTransform;
            SetRect(bottomBar, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 88f));

            skipText = CreateText(root.transform, "Skip", 22f, FontStyles.Normal, TextAlignmentOptions.TopRight);
            skipText.color = new Color(0.75f, 0.83f, 0.9f, 0.72f);
            skipText.text = "长按 ESC 跳过";
            SetRect(skipText.rectTransform, new Vector2(0.72f, 0.9f), new Vector2(0.97f, 0.975f), Vector2.zero, Vector2.zero);

            dialoguePanel = CreatePanel(root.transform, "DialoguePanel", new Vector2(0.12f, 0.075f), new Vector2(0.88f, 0.27f));
            var dialogueBackground = dialoguePanel.GetComponent<Image>();
            dialogueBackground.color = panelColor;
            var accent = CreateImage(dialoguePanel.transform, "Accent", accentColor);
            SetRect(accent.rectTransform, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 10f), new Vector2(7f, -10f));

            speakerText = CreateText(dialoguePanel.transform, "Speaker", 26f, FontStyles.Bold, TextAlignmentOptions.Left);
            speakerText.color = accentColor;
            SetRect(speakerText.rectTransform, new Vector2(0.035f, 0.68f), new Vector2(0.38f, 0.92f), Vector2.zero, Vector2.zero);
            dialogueText = CreateText(dialoguePanel.transform, "Line", 32f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            dialogueText.enableWordWrapping = true;
            SetRect(dialogueText.rectTransform, new Vector2(0.035f, 0.17f), new Vector2(0.96f, 0.68f), Vector2.zero, Vector2.zero);
            advanceText = CreateText(dialoguePanel.transform, "Advance", 19f, FontStyles.Normal, TextAlignmentOptions.BottomRight);
            advanceText.color = new Color(0.7f, 0.82f, 0.9f, 0.85f);
            SetRect(advanceText.rectTransform, new Vector2(0.66f, 0.035f), new Vector2(0.96f, 0.2f), Vector2.zero, Vector2.zero);

            objectivePanel = CreatePanel(root.transform, "ObjectivePanel", new Vector2(0.035f, 0.76f), new Vector2(0.36f, 0.92f));
            objectivePanel.GetComponent<Image>().color = new Color(panelColor.r, panelColor.g, panelColor.b, 0.88f);
            objectiveMarker = CreateImage(objectivePanel.transform, "Marker", accentColor);
            SetRect(objectiveMarker.rectTransform, new Vector2(0.035f, 0.25f), new Vector2(0.052f, 0.78f), Vector2.zero, Vector2.zero);
            objectiveTitle = CreateText(objectivePanel.transform, "Objective", 28f, FontStyles.Bold, TextAlignmentOptions.Left);
            SetRect(objectiveTitle.rectTransform, new Vector2(0.085f, 0.48f), new Vector2(0.94f, 0.88f), Vector2.zero, Vector2.zero);
            objectiveHint = CreateText(objectivePanel.transform, "Hint", 21f, FontStyles.Normal, TextAlignmentOptions.Left);
            objectiveHint.color = new Color(0.7f, 0.84f, 0.92f, 1f);
            SetRect(objectiveHint.rectTransform, new Vector2(0.085f, 0.12f), new Vector2(0.94f, 0.48f), Vector2.zero, Vector2.zero);
        }

        GameObject CreatePanel(Transform parent, string objectName, Vector2 anchorMin, Vector2 anchorMax)
        {
            var image = CreateImage(parent, objectName, panelColor);
            SetRect(image.rectTransform, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            return image.gameObject;
        }

        Image CreateImage(Transform parent, string objectName, Color color)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        TMP_Text CreateText(Transform parent, string objectName, float size, FontStyles style, TextAlignmentOptions alignment)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            var presentationFont = ResolvePresentationFont();
            if (presentationFont) text.font = presentationFont;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        TMP_FontAsset ResolvePresentationFont()
        {
            if (runtimeChineseFont) return runtimeChineseFont;

            var sourceFont = Resources.Load<Font>("Fonts/ZCOOLKuaiLe-Regular");
            if (sourceFont)
            {
                runtimeChineseFont = TMP_FontAsset.CreateFontAsset(sourceFont);
                runtimeChineseFont.name = "MirrorTrial_RuntimeChinese";
                runtimeChineseFont.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                runtimeChineseFont.isMultiAtlasTexturesEnabled = true;
                runtimeChineseFont.hideFlags = HideFlags.HideAndDontSave;
                return runtimeChineseFont;
            }
            return chineseFont;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
