using MirrorTrial.Puzzles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MirrorTrial.Editor.Level
{
    public static class PuzzleLightBeamPrefabBuilder
    {
        const string PrefabPath = "Assets/MirrorTrial/Prefabs/VFX/PuzzleLightBeam.prefab";
        const string CoreMaterialPath = "Assets/MirrorTrial/Materials/PuzzleLightBeam_Core.mat";
        const string GlowMaterialPath = "Assets/MirrorTrial/Materials/PuzzleLightBeam_Glow.mat";

        [MenuItem("Tools/Mirror Trial/Level 02/Create Light Beam Prefab")]
        public static void Create()
        {
            var shader = Shader.Find("Sprites/Default");
            if (!shader) { Debug.LogError("Could not find Sprites/Default shader."); return; }

            var coreMaterial = GetOrCreateMaterial(CoreMaterialPath, shader, new Color(1f, 0.52f, 0.12f, 1f));
            var glowMaterial = GetOrCreateMaterial(GlowMaterialPath, shader, new Color(1f, 0.22f, 0.035f, 0.22f));
            var root = new GameObject("PuzzleLightBeam");
            try
            {
                var view = root.AddComponent<PuzzleLightBeamView>();
                var glow = CreateLine("Glow", root.transform, glowMaterial, 0.18f, 79,
                    new Color(1f, 0.18f, 0.025f, 0.08f), new Color(1f, 0.42f, 0.06f, 0.3f));
                var core = CreateLine("Core", root.transform, coreMaterial, 0.055f, 80,
                    new Color(1f, 0.36f, 0.055f, 1f), new Color(1f, 0.78f, 0.26f, 1f));

                var serialized = new SerializedObject(view);
                serialized.FindProperty("glow").objectReferenceValue = glow;
                serialized.FindProperty("core").objectReferenceValue = core;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                BindToOpenSceneControllers(prefab.GetComponent<PuzzleLightBeamView>());
                AssetDatabase.SaveAssets();
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
                Debug.Log("Created and selected light beam prefab: " + PrefabPath, prefab);
            }
            finally { Object.DestroyImmediate(root); }
        }

        static LineRenderer CreateLine(string name, Transform parent, Material material, float width,
            int sortingOrder, Color edgeColor, Color centerColor)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            var line = child.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, new Vector3(-1.5f, 0f, 0f));
            line.SetPosition(1, new Vector3(1.5f, 0f, 0f));
            line.startWidth = line.endWidth = width;
            line.numCapVertices = 6;
            line.numCornerVertices = 4;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.sortingOrder = sortingOrder;
            line.sharedMaterial = material;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(edgeColor, 0f), new GradientColorKey(centerColor, 0.5f), new GradientColorKey(edgeColor, 1f) },
                new[] { new GradientAlphaKey(edgeColor.a, 0f), new GradientAlphaKey(centerColor.a, 0.5f), new GradientAlphaKey(edgeColor.a, 1f) });
            line.colorGradient = gradient;
            return line;
        }

        static Material GetOrCreateMaterial(string path, Shader shader, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader;
            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        static void BindToOpenSceneControllers(PuzzleLightBeamView prefab)
        {
            var controllers = Object.FindObjectsOfType<MirrorBeamPuzzleController>(true);
            foreach (var controller in controllers)
            {
                var serialized = new SerializedObject(controller);
                var property = serialized.FindProperty("beamPrefab");
                if (property == null) continue;
                property.objectReferenceValue = prefab;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(controller);
            }
            if (controllers.Length > 0) EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
    }
}
