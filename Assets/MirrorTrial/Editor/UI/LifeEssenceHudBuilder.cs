using System;
using System.IO;
using MirrorTrial.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MirrorTrial.Editor.UI
{
    public static class LifeEssenceHudBuilder
    {
        const string HudPrefabPath = "Assets/MirrorTrial/Resources/UI/LifeEssenceHudUI.prefab";
        const string RequestRelativePath = "Temp/LifeEssenceHudBuilder.request";

        [InitializeOnLoadMethod]
        static void TryBuildRequested()
        {
            var requestPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", RequestRelativePath));
            if (!File.Exists(requestPath))
                return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += TryBuildRequested;
                return;
            }

            try
            {
                Rebuild();
                File.Delete(requestPath);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        [MenuItem("Tools/Mirror Trial/UI/重建生命精华计数器")]
        public static void Rebuild()
        {
            EnsureFolder("Assets/MirrorTrial", "Resources");
            EnsureFolder("Assets/MirrorTrial/Resources", "UI");

            if (!AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath))
            {
                var temp = new GameObject("LifeEssenceHudUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(LifeEssenceHudView));
                try
                {
                    PrefabUtility.SaveAsPrefabAsset(temp, HudPrefabPath);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(temp);
                }
            }

            var root = PrefabUtility.LoadPrefabContents(HudPrefabPath);
            try
            {
                for (var i = root.transform.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

                SetLayerRecursive(root, 5);
                var rootRect = GetOrAdd<RectTransform>(root);
                rootRect.anchorMin = Vector2.zero;
                rootRect.anchorMax = Vector2.one;
                rootRect.offsetMin = Vector2.zero;
                rootRect.offsetMax = Vector2.zero;
                rootRect.localScale = Vector3.one;

                var canvas = GetOrAdd<Canvas>(root);
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                var scaler = GetOrAdd<CanvasScaler>(root);
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0f;
                GetOrAdd<GraphicRaycaster>(root);
                var view = GetOrAdd<LifeEssenceHudView>(root);

                var counter = CreateRect("LifeEssenceCounter", root.transform,
                    new Vector2(-24f, -24f), new Vector2(168f, 38f), new Vector2(1f, 1f), new Vector2(1f, 1f));
                var background = counter.gameObject.AddComponent<Image>();
                background.color = new Color(0.04f, 0.08f, 0.12f, 0.72f);

                var icon = CreateText("Icon", counter, "◆", 19, FontStyle.Bold,
                    new Color(0.68f, 0.95f, 1f, 1f), TextAnchor.MiddleCenter);
                Stretch(icon.rectTransform, 10f, 118f, 6f, 6f);

                var value = CreateText("ValueText", counter, "0", 18, FontStyle.Bold,
                    new Color(0.82f, 0.96f, 1f, 1f), TextAnchor.MiddleLeft);
                Stretch(value.rectTransform, 40f, 12f, 6f, 6f);

                var gainAnchor = CreateRect("GainAnchor", counter,
                    new Vector2(-8f, -2f), new Vector2(96f, 28f), new Vector2(1f, 1f), new Vector2(1f, 1f));

                var serialized = new SerializedObject(view);
                serialized.FindProperty("counterRoot").objectReferenceValue = counter;
                serialized.FindProperty("iconText").objectReferenceValue = icon;
                serialized.FindProperty("valueText").objectReferenceValue = value;
                serialized.FindProperty("gainAnchor").objectReferenceValue = gainAnchor;
                serialized.FindProperty("background").objectReferenceValue = background;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[LifeEssenceHudBuilder] 已重建右上角生命精华 UI Prefab。", AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath));
        }

        static RectTransform CreateRect(string name, Transform parent, Vector2 anchoredPosition, Vector2 size, Vector2 anchor, Vector2 pivot)
        {
            var go = new GameObject(name, typeof(RectTransform));
            SetLayerRecursive(go, 5);
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            return rect;
        }

        static Text CreateText(string name, Transform parent, string value, int fontSize, FontStyle style, Color color, TextAnchor alignment)
        {
            var rect = CreateRect(name, parent, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            var text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        static void Stretch(RectTransform rect, float left, float right, float top, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            rect.localScale = Vector3.one;
        }

        static T GetOrAdd<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component ? component : target.AddComponent<T>();
        }

        static void EnsureFolder(string parent, string child)
        {
            var path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }

        static void SetLayerRecursive(GameObject root, int layer)
        {
            root.layer = layer;
            for (var i = 0; i < root.transform.childCount; i++)
                SetLayerRecursive(root.transform.GetChild(i).gameObject, layer);
        }
    }
}
