using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MirrorTrial.Editor.Level
{
    public static class ProximityInteractionHudPrefabBuilder
    {
        const string PrefabPath = "Assets/MirrorTrial/Resources/UI/Story/ProximityInteractionHud.prefab";
        const string FontPath = "Assets/MirrorTrial/Mod Assets/Mod Resources/Fonts/ZCOOLKuaiLe-Regular SDF.asset";

        [InitializeOnLoadMethod]
        static void BuildOnFirstImport()
        {
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath))
                EditorApplication.delayCall += Build;
        }

        [MenuItem("MirrorTrial/UI/Build Proximity Interaction HUD Prefab")]
        public static void Build()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var root = new GameObject("ProximityInteractionHud", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            rootRect.pivot = new Vector2(0.5f, 0f);
            rootRect.sizeDelta = new Vector2(300f, 92f);
            root.GetComponent<Image>().color = new Color(0.025f, 0.04f, 0.075f, 0.94f);
            var group = root.GetComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            CreateImage(root.transform, "Accent", new Color(0.23f, 0.93f, 1f, 1f), Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 7f), new Vector2(6f, -7f));
            CreateText(root.transform, "Title", "旋转镜子", 23f, FontStyles.Bold, font, new Vector2(0.1f, 0.48f), new Vector2(0.94f, 0.88f));
            var hint = CreateText(root.transform, "Hint", "E  交互", 19f, FontStyles.Normal, font, new Vector2(0.1f, 0.12f), new Vector2(0.94f, 0.5f));
            hint.color = new Color(0.7f, 0.84f, 0.92f, 1f);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Debug.Log($"Created proximity HUD prefab: {PrefabPath}");
        }

        static Image CreateImage(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            SetRect(image.rectTransform, anchorMin, anchorMax, offsetMin, offsetMax);
            return image;
        }

        static TextMeshProUGUI CreateText(Transform parent, string name, string value, float size, FontStyles style, TMP_FontAsset font, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            if (font) text.font = font;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Left;
            text.color = Color.white;
            text.raycastTarget = false;
            SetRect(text.rectTransform, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            return text;
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