#if UNITY_EDITOR
using System.IO;
using MirrorTrial.Feedback;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MirrorTrial.Editor.UI
{
    public static class ScreenFxOverlayBuilder
    {
        const string ResourceFolder = "Assets/MirrorTrial/Resources/UI";
        const string MaterialPath = ResourceFolder + "/ScreenFxOverlay.mat";
        const string PrefabPath = ResourceFolder + "/ScreenFxOverlay.prefab";
        static bool bossPreview;

        [InitializeOnLoadMethod]
        static void EnsureDefaultAssetExists()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)) return;
            EditorApplication.delayCall += CreateOrRepair;
        }

        [MenuItem("Mirror Trial/Screen FX/Create or Repair Default Overlay")]
        public static void CreateOrRepair()
        {
            if (!Directory.Exists(ResourceFolder)) Directory.CreateDirectory(ResourceFolder);
            var shader = Shader.Find("MirrorTrial/UI/ScreenFxOverlay");
            if (!shader)
            {
                Debug.LogError("[ScreenFx] ScreenFxOverlay shader was not found. Wait for shader import, then retry.");
                return;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (!material)
            {
                material = new Material(shader) { name = "ScreenFxOverlay" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else material.shader = shader;

            var root = new GameObject("ScreenFxOverlay", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(CanvasGroup), typeof(ScreenFxOverlay));
            root.transform.localScale = Vector3.one;
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 50;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var group = root.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            var maskObject = new GameObject("Mask", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            maskObject.transform.SetParent(root.transform, false);
            var rect = (RectTransform)maskObject.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = maskObject.GetComponent<RawImage>();
            image.material = material;
            image.raycastTarget = false;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Debug.Log("[ScreenFx] Default overlay prefab and material are ready.");
        }

        [MenuItem("Mirror Trial/Screen FX/Preview Player Hit _F8")]
        static void PreviewPlayerHit()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[ScreenFx] Enter Play Mode before previewing screen effects.");
                return;
            }
            ScreenFx.Play(ScreenFxType.PlayerHit, 0.42f, 0.32f);
        }

        [MenuItem("Mirror Trial/Screen FX/Toggle Boss Atmosphere _F9")]
        static void ToggleBossAtmosphere()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[ScreenFx] Enter Play Mode before previewing screen effects.");
                return;
            }
            bossPreview = !bossPreview;
            if (bossPreview) ScreenFx.Begin(ScreenFxType.BossBattle, null);
            else ScreenFx.End(ScreenFxType.BossBattle, null);
        }
    }
}
#endif
