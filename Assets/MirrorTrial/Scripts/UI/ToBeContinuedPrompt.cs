using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MirrorTrial.UI
{
    public sealed class ToBeContinuedPrompt : MonoBehaviour
    {
        [SerializeField] string message = "未完待续";
        [SerializeField, Min(0f)] float delay = 0.75f;
        [SerializeField, Min(0.01f)] float fadeDuration = 1.25f;

        IEnumerator Start()
        {
            var canvasObject = new GameObject("ToBeContinuedCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var group = canvasObject.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            var textObject = new GameObject("Message", typeof(RectTransform), typeof(Text), typeof(Outline));
            textObject.transform.SetParent(canvasObject.transform, false);

            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var label = textObject.GetComponent<Text>();
            label.text = message;
            label.font = CreateChineseFont();
            label.fontSize = 72;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;

            var outline = textObject.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(3f, -3f);

            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);

            var elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Clamp01(elapsed / fadeDuration);
                yield return null;
            }

            group.alpha = 1f;
        }

        static Font CreateChineseFont()
        {
            var candidates = new[] { "Microsoft YaHei", "Microsoft JhengHei", "SimHei", "Arial" };
            return Font.CreateDynamicFontFromOSFont(candidates, 72);
        }
    }
}
