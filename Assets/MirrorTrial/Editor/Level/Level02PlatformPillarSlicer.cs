using UnityEditor;
using UnityEngine;

namespace MirrorTrial.Editor.Level
{
    public static class Level02PlatformPillarSlicer
    {
        private const string AssetPath = "Assets/MirrorTrial/Art/Level02_Jungle/Platforms/platform_pillar_v1.png";

        [MenuItem("MirrorTrial/Level 02/Slice Platform Pillar")]
        public static void Slice()
        {
            var importer = AssetImporter.GetAtPath(AssetPath) as TextureImporter;
            if (!importer)
                throw new System.InvalidOperationException("找不到贴图导入器：" + AssetPath);

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;

            // Keep a full sprite for existing scene references, plus three reusable construction pieces.
            importer.spritesheet = new[]
            {
                Make("platform_pillar_v1", 0, 0, 384, 341),
                Make("platform_pillar_top", 0, 228, 384, 113),
                Make("platform_pillar_body", 0, 113, 384, 115),
                Make("platform_pillar_base", 0, 0, 384, 113)
            };

            importer.SaveAndReimport();
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(AssetPath);
            Debug.Log("[Level 02] platform_pillar_v1 is now Multiple with full/top/body/base sprites.");
        }

        private static SpriteMetaData Make(string name, float x, float y, float width, float height)
        {
            return new SpriteMetaData
            {
                name = name,
                rect = new Rect(x, y, width, height),
                alignment = (int)SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
                border = Vector4.zero
            };
        }
    }
}
