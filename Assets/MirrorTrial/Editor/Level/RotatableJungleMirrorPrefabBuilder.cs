using MirrorTrial.Puzzles;
using UnityEditor;
using UnityEngine;

namespace MirrorTrial.Editor.Level
{
    [InitializeOnLoad]
    public static class RotatableJungleMirrorPrefabBuilder
    {
        const string BaseSpritePath = "Assets/MirrorTrial/Art/Level02_Jungle/Puzzle_DoubleJump/Sprites/Puzzle_Mirror_Left_FixedBaseAxle_v3.png";
        const string RotatorSpritePath = "Assets/MirrorTrial/Art/Level02_Jungle/Puzzle_DoubleJump/Sprites/Puzzle_Mirror_Left_RotatorRearSupport_v3.png";
        const string PrefabFolder = "Assets/MirrorTrial/Prefabs/Puzzles";
        const string PrefabPath = PrefabFolder + "/Puzzle_RotatableMirror_Left.prefab";

        // Pixel coordinates are measured from each image's bottom-left corner.
        // Both points describe the same physical axle after assembly.
        static readonly Vector2 BaseAxlePixels = new Vector2(694f, 755f);
        static readonly Vector2 RotatorAxlePixels = new Vector2(573f, 194f);
        static readonly Vector2 MirrorFacePixels = new Vector2(420f, 785f);

        static RotatableJungleMirrorPrefabBuilder()
        {
            EditorApplication.delayCall += BuildOnceWhenAssetsAreReady;
        }

        [MenuItem("Tools/Mirror Trial/Level 02/Rebuild Rotatable Jungle Mirror Prefab")]
        public static void Rebuild()
        {
            AssetDatabase.ImportAsset(BaseSpritePath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(RotatorSpritePath, ImportAssetOptions.ForceUpdate);

            var baseSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BaseSpritePath);
            var rotatorSprite = AssetDatabase.LoadAssetAtPath<Sprite>(RotatorSpritePath);
            if (!baseSprite || !rotatorSprite)
            {
                Debug.LogWarning("Rotatable mirror prefab was not built because its sprites are not ready yet.");
                return;
            }

            EnsureFolder("Assets/MirrorTrial/Prefabs");
            EnsureFolder(PrefabFolder);

            var root = new GameObject("Puzzle_RotatableMirror_Left");
            try
            {
                var controller = root.AddComponent<RotatablePuzzleMirror>();
                var trigger = root.AddComponent<BoxCollider2D>();
                trigger.isTrigger = true;
                trigger.offset = new Vector2(0f, 0.62f);
                trigger.size = new Vector2(2.25f, 2.15f);

                const float assembledWidth = 1.65f;
                var baseScale = assembledWidth / (baseSprite.rect.width / baseSprite.pixelsPerUnit);
                var rotatorScale = (assembledWidth * 0.78f) / (rotatorSprite.rect.width / rotatorSprite.pixelsPerUnit);

                var fixedBase = CreateSpriteChild("Fixed_Base_And_Axle", root.transform, baseSprite, 65);
                fixedBase.transform.localScale = Vector3.one * baseScale;
                fixedBase.transform.localPosition = -PixelPointFromPivot(baseSprite, BaseAxlePixels) * baseScale;

                var pivot = new GameObject("Mirror_Rotation_Pivot");
                pivot.transform.SetParent(root.transform, false);

                var rotatingAssembly = CreateSpriteChild("Mirror_And_Rear_Support", pivot.transform, rotatorSprite, 66);
                rotatingAssembly.transform.localScale = Vector3.one * rotatorScale;
                rotatingAssembly.transform.localPosition = -PixelPointFromPivot(rotatorSprite, RotatorAxlePixels) * rotatorScale;

                var beamPoint = new GameObject("Beam_Point");
                beamPoint.transform.SetParent(pivot.transform, false);
                beamPoint.transform.localPosition = (PixelPointFromPivot(rotatorSprite, MirrorFacePixels) -
                                                     PixelPointFromPivot(rotatorSprite, RotatorAxlePixels)) * rotatorScale;

                var serialized = new SerializedObject(controller);
                serialized.FindProperty("rotatingVisual").objectReferenceValue = pivot.transform;
                serialized.FindProperty("beamPoint").objectReferenceValue = beamPoint.transform;
                serialized.FindProperty("angleSteps").arraySize = 3;
                serialized.FindProperty("angleSteps").GetArrayElementAtIndex(0).floatValue = -28f;
                serialized.FindProperty("angleSteps").GetArrayElementAtIndex(1).floatValue = 0f;
                serialized.FindProperty("angleSteps").GetArrayElementAtIndex(2).floatValue = 28f;
                serialized.FindProperty("correctStep").intValue = 1;
                serialized.FindProperty("currentStep").intValue = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("Rotatable jungle mirror prefab rebuilt: " + PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static void BuildOnceWhenAssetsAreReady()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)) return;
            Rebuild();
        }

        static GameObject CreateSpriteChild(string name, Transform parent, Sprite sprite, int sortingOrder)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            return child;
        }

        static Vector3 PixelPointFromPivot(Sprite sprite, Vector2 pixelPoint)
        {
            var local = (pixelPoint - sprite.pivot) / sprite.pixelsPerUnit;
            return new Vector3(local.x, local.y, 0f);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var slash = path.LastIndexOf('/');
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
