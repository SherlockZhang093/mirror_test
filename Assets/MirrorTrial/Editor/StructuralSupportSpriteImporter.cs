#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MirrorTrial.Editor
{
    public static class StructuralSupportSpriteImporter
    {
        private static readonly string[] AssetPaths =
        {
            "Assets/MirrorTrial/Art/Background2D/StructuralSupports/support_left_tower_cliff_v1.png",
            "Assets/MirrorTrial/Art/Background2D/StructuralSupports/support_single_main_arch_v1.png",
            "Assets/MirrorTrial/Art/Background2D/StructuralSupports/support_massive_lower_foundation_v1.png",
            "Assets/MirrorTrial/Art/Background2D/StructuralSupports/support_broken_arch_v1.png",
            "Assets/MirrorTrial/Art/Background2D/StructuralSupports/support_fortress_foundation_v1.png",
            "Assets/MirrorTrial/Art/Background2D/StructuralSupports/support_upper_wall_connector_v1.png"
        };

        [InitializeOnLoadMethod]
        private static void ScheduleConfiguration()
        {
            EditorApplication.delayCall += ConfigureAssets;
        }

        private static void ConfigureAssets()
        {
            foreach (string path in AssetPaths)
            {
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                bool changed = importer.textureType != TextureImporterType.Sprite ||
                               importer.spriteImportMode != SpriteImportMode.Single ||
                               !importer.alphaIsTransparency ||
                               importer.mipmapEnabled;

                if (!changed) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }
    }
}
#endif