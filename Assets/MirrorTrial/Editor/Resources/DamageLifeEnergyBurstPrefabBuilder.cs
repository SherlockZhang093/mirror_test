using System.IO;
using MirrorTrial.HealthResources;
using UnityEditor;
using UnityEngine;

namespace MirrorTrial.Editor.HealthResources
{
    public static class DamageLifeEnergyBurstPrefabBuilder
    {
        const string PrefabFolder = "Assets/MirrorTrial/Resources/Effects";
        const string PrefabPath = PrefabFolder + "/DamageLifeEnergyBurst.prefab";

        [InitializeOnLoadMethod]
        static void BuildWhenMissing()
        {
            EditorApplication.delayCall += () =>
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (!prefab || !HasSerializedParticleSystem()) Build();
            };
        }

        static bool HasSerializedParticleSystem()
        {
            if (!File.Exists(PrefabPath)) return false;
            var yaml = File.ReadAllText(PrefabPath);
            return yaml.Contains("--- !u!198 ") && yaml.Contains("--- !u!199 ");
        }

        [MenuItem("Mirror Trial/Build/Rebuild Damage Life Energy Burst Prefab")]
        public static void Build()
        {
            EnsureFolder(PrefabFolder);
            var root = new GameObject("DamageLifeEnergyBurst");
            var particles = root.AddComponent<ParticleSystem>();
            var renderer = root.GetComponent<ParticleSystemRenderer>();
            var effect = root.AddComponent<DamageLifeEnergyBurstEffect>();
            effect.Configure(particles, renderer,
                0.09f, 0.08f, 0.34f, 0.65f,
                new Color(0.18f, 0.92f, 0.8f, 1f),
                new Color(0.72f, 1f, 0.9f, 1f), 123);

            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            root.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Damage life-energy burst prefab rebuilt: " + PrefabPath);
        }

        static void EnsureFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace(Path.DirectorySeparatorChar, '/');
            var name = Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
