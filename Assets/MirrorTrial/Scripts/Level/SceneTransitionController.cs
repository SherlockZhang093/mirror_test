using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MirrorTrial.Level
{
    /// <summary>
    /// Covers scene loading with a restrained black chapter card.
    /// </summary>
    public sealed class SceneTransitionController : MonoBehaviour
    {
        const float BlackFadeInDuration = 0.28f;
        const float TitleFadeInDuration = 0.42f;
        const float MinimumTitleHoldDuration = 1.45f;
        const float TitleFadeOutDuration = 0.3f;
        const float BlackFadeOutDuration = 0.48f;

        static SceneTransitionController instance;
        static Font transitionFont;

        Image blackBackground;
        CanvasGroup titleGroup;
        Text chapterText;
        bool transitioning;

        public static bool IsTransitioning => instance && instance.transitioning;

        public static bool LoadScene(string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName))
                return false;

            var controller = Ensure();
            if (controller.transitioning)
                return false;

            controller.StartCoroutine(controller.LoadSceneRoutine(sceneName));
            return true;
        }

        static SceneTransitionController Ensure()
        {
            if (instance) return instance;

            var root = new GameObject("SceneTransitionController");
            DontDestroyOnLoad(root);
            instance = root.AddComponent<SceneTransitionController>();
            instance.BuildPresentation();
            return instance;
        }

        void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        IEnumerator LoadSceneRoutine(string sceneName)
        {
            transitioning = true;
            ConfigureTitle(sceneName);
            SetBackgroundAlpha(0f);
            titleGroup.alpha = 0f;

            yield return FadeBackground(0f, 1f, BlackFadeInDuration);
            yield return FadeTitle(0f, 1f, TitleFadeInDuration);

            var loadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (loadOperation == null)
            {
                Debug.LogError("[SceneTransition] Unable to load scene: " + sceneName, this);
                transitioning = false;
                Destroy(gameObject);
                yield break;
            }

            loadOperation.allowSceneActivation = false;
            var elapsed = 0f;
            while (elapsed < MinimumTitleHoldDuration || loadOperation.progress < 0.9f)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            loadOperation.allowSceneActivation = true;
            while (!loadOperation.isDone)
                yield return null;

            // Keep the destination covered for its first initialization frame.
            yield return null;
            yield return FadeTitle(1f, 0f, TitleFadeOutDuration);
            yield return FadeBackground(1f, 0f, BlackFadeOutDuration);

            transitioning = false;
            Destroy(gameObject);
        }

        void ConfigureTitle(string sceneName)
        {
            if (sceneName.Equals(GameFlow.FirstRealitySceneName, StringComparison.OrdinalIgnoreCase))
                chapterText.text = "第一章：拔剑";
            else if (sceneName.Equals(GameFlow.SecondRealitySceneName, StringComparison.OrdinalIgnoreCase))
                chapterText.text = "第二章：引弓";
            else
                chapterText.text = string.Empty;
        }

        IEnumerator FadeTitle(float from, float to, float duration)
        {
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                titleGroup.alpha = Mathf.Lerp(from, to, Smooth01(elapsed / duration));
                yield return null;
            }

            titleGroup.alpha = to;
        }

        IEnumerator FadeBackground(float from, float to, float duration)
        {
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                SetBackgroundAlpha(Mathf.Lerp(from, to, Smooth01(elapsed / duration)));
                yield return null;
            }

            SetBackgroundAlpha(to);
        }

        void SetBackgroundAlpha(float alpha)
        {
            blackBackground.color = new Color(0f, 0f, 0f, alpha);
        }

        void BuildPresentation()
        {
            var canvasObject = new GameObject("TransitionCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var blocker = canvasObject.GetComponent<CanvasGroup>();
            blocker.blocksRaycasts = true;
            blocker.interactable = true;

            blackBackground = CreateImage("BlackBackground", canvasObject.transform, Color.black,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var titleObject = new GameObject("ChapterTitle", typeof(RectTransform), typeof(CanvasGroup));
            titleObject.transform.SetParent(canvasObject.transform, false);
            var titleRect = titleObject.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.18f, 0.35f);
            titleRect.anchorMax = new Vector2(0.82f, 0.65f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;
            titleGroup = titleObject.GetComponent<CanvasGroup>();

            chapterText = CreateText(titleObject.transform);
        }

        static Image CreateImage(string objectName, Transform parent, Color color, Vector2 anchorMin,
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
            return image;
        }

        static Text CreateText(Transform parent)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var text = go.GetComponent<Text>();
            text.font = ResolveFont();
            text.fontSize = 58;
            text.fontStyle = FontStyle.Normal;
            text.color = new Color(0.94f, 0.94f, 0.94f, 1f);
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        static Font ResolveFont()
        {
            if (transitionFont) return transitionFont;

            transitionFont = Font.CreateDynamicFontFromOSFont(
                new[] { "Noto Serif SC", "Source Han Serif SC", "SimSun", "STSong" }, 58);
            if (!transitionFont)
            {
                transitionFont = Font.CreateDynamicFontFromOSFont(
                    new[] { "Microsoft YaHei UI", "Microsoft YaHei", "Arial" }, 58);
            }

            return transitionFont;
        }

        static float Smooth01(float value)
        {
            var t = Mathf.Clamp01(value);
            return t * t * (3f - 2f * t);
        }
    }
}
