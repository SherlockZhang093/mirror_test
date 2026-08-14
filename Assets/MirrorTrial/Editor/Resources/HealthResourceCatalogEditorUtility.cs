using System.Linq;
using System.IO;
using MirrorTrial.HealthResources;
using UnityEditor;
using UnityEngine;

namespace MirrorTrial.Editor.HealthResources
{
    public static class HealthResourceCatalogEditorUtility
    {
        public const string CatalogPath = "Assets/MirrorTrial/Resources/HealthResourceCatalog.asset";

        public static HealthResourceCatalog GetOrCreate()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<HealthResourceCatalog>(CatalogPath);
            if (catalog) return catalog;

            if (File.Exists(CatalogPath))
            {
                AssetDatabase.ImportAsset(CatalogPath, ImportAssetOptions.ForceSynchronousImport);
                catalog = AssetDatabase.LoadAssetAtPath<HealthResourceCatalog>(CatalogPath);
                if (catalog) return catalog;
            }

            EnsureFolder("Assets/MirrorTrial", "Resources");
            catalog = ScriptableObject.CreateInstance<HealthResourceCatalog>();
            try
            {
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catch (UnityException)
            {
                Object.DestroyImmediate(catalog);
                AssetDatabase.ImportAsset(CatalogPath, ImportAssetOptions.ForceSynchronousImport);
                return AssetDatabase.LoadAssetAtPath<HealthResourceCatalog>(CatalogPath);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(CatalogPath, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<HealthResourceCatalog>(CatalogPath);
        }

        public static void Register(GameObject prefab)
        {
            if (!prefab || !prefab.GetComponent<HealthResourceNode>()) return;
            var catalog = GetOrCreate();
            var data = new SerializedObject(catalog);
            var entries = data.FindProperty("prefabs");
            for (var i = 0; i < entries.arraySize; i++)
                if (entries.GetArrayElementAtIndex(i).objectReferenceValue == prefab) return;

            entries.InsertArrayElementAtIndex(entries.arraySize);
            entries.GetArrayElementAtIndex(entries.arraySize - 1).objectReferenceValue = prefab;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        public static void Rebuild()
        {
            var catalog = GetOrCreate();
            var prefabs = AssetDatabase.FindAssets("t:Prefab")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .Where(p => p && p.GetComponent<HealthResourceNode>())
                .Distinct()
                .OrderBy(p => p.name)
                .ToArray();

            var data = new SerializedObject(catalog);
            var entries = data.FindProperty("prefabs");
            entries.arraySize = prefabs.Length;
            for (var i = 0; i < prefabs.Length; i++)
                entries.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        static void EnsureFolder(string parent, string child)
        {
            var path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
        }
    }
}
