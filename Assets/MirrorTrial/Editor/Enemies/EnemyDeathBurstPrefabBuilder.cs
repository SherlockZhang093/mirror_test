using System.IO;
using MirrorTrial.Enemies;
using UnityEditor;
using UnityEngine;

namespace MirrorTrial.Editor.Enemies
{
    public static class EnemyDeathBurstPrefabBuilder
    {
        const string PrefabFolder = "Assets/MirrorTrial/Resources/Effects";
        const string PrefabPath = PrefabFolder + "/EnemyDeathBurst.prefab";
        const string BurstSpritePath = "Assets/MirrorTrial/Resources/Effects/pixel_hit_burst.png";

        [InitializeOnLoadMethod]
        static void BuildWhenMissing()
        {
            EditorApplication.delayCall += () =>
            {
                if (!AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath))
                    Build();
            };
        }

        [MenuItem("Mirror Trial/Build/Rebuild Enemy Death Burst Prefab")]
        public static void Build()
        {
            EnsureFolder(PrefabFolder);
            AssetDatabase.ImportAsset(BurstSpritePath, ImportAssetOptions.ForceSynchronousImport);
            var burstSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BurstSpritePath);
            if (!burstSprite)
            {
                Debug.LogError("Enemy death burst build failed: impact sprite is missing.");
                return;
            }

            var root = new GameObject("EnemyDeathBurst");
            var meshFilter = root.AddComponent<MeshFilter>();
            var meshRenderer = root.AddComponent<MeshRenderer>();
            meshRenderer.sortingOrder = 122;
            var effect = root.AddComponent<EnemyDeathBurstEffect>();

            var flashObject = new GameObject("Split Flash");
            flashObject.transform.SetParent(root.transform, false);
            var flash = flashObject.AddComponent<SpriteRenderer>();
            flash.sprite = burstSprite;
            flash.sortingOrder = 124;

            effect.Configure(meshFilter, meshRenderer, flash,
                7, 5, EnemyDeathBurstEffect.DefaultLifetime,
                1.5f, new Vector2(100f, 310f), 0.14f, 0.03f);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Enemy death sprite-fragment prefab rebuilt: {PrefabPath}");
        }

        static void EnsureFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
