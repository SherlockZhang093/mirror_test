using System.IO;
using MirrorTrial.Level;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MirrorTrial.Editor
{
    internal static class GeneratedBackground2DSetup
    {
        private const string ScenePath = "Assets/MirrorTrial/Scenes/Level_Reality_01.unity";
        private const string SkyPath = "Assets/MirrorTrial/Art/Background2D/void_ruins_sky_v1.png";
        private const string RuinsPath = "Assets/MirrorTrial/Art/Background2D/distant_ruins_sheet_v1.png";
        private const string MidgroundPath = "Assets/MirrorTrial/Art/Background2D/midground_ruins_v1.png";
        private const string ForegroundPath = "Assets/MirrorTrial/Art/Background2D/foreground_frame_v1.png";
        private const string FogPath = "Assets/MirrorTrial/Art/Background2D/procedural_fog_v1.png";
        private const string RootName = "Generated2DBackground";
        private static void Install()
        {
            CreateFogTexture();
            ConfigureTexture(SkyPath);
            ConfigureTexture(RuinsPath);
            ConfigureTexture(MidgroundPath);
            ConfigureTexture(ForegroundPath);
            ConfigureTexture(FogPath);

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Camera camera = Camera.main;
            if (camera == null)
            {
                Debug.LogError("Generated background setup could not find the MainCamera.");
                return;
            }

            DisableLegacyBackground("FarBackground_GlbIslands");
            DisableLegacyBackground("Background_FloatingTemple_Hunyuan");
            DisableLegacyBackground("Hunyuan_Background_KeyLight");

            GameObject oldRoot = GameObject.Find(RootName);
            if (oldRoot != null) Object.DestroyImmediate(oldRoot);

            GameObject root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.position = new Vector3(camera.transform.position.x, camera.transform.position.y, 8f);

            SpriteRenderer sky = CreateLayer(root.transform, "Sky", SkyPath, -100, Color.white, Vector3.zero);
            SpriteRenderer ruins = CreateLayer(root.transform, "FarRuins", RuinsPath, -90,
                new Color(0.47f, 0.61f, 0.76f, 0.22f), new Vector3(0f, -0.1f, -0.1f));
            SpriteRenderer farFog = CreateLayer(root.transform, "FarFog", FogPath, -82,
                new Color(0.28f, 0.45f, 0.61f, 0.13f), new Vector3(0f, -1.5f, -0.2f));
            SpriteRenderer midground = CreateLayer(root.transform, "MidgroundRuins", MidgroundPath, -72,
                new Color(0.34f, 0.46f, 0.59f, 0.30f), new Vector3(0f, -1.25f, -0.3f));
            SpriteRenderer nearFog = CreateLayer(root.transform, "NearFog", FogPath, -60,
                new Color(0.2f, 0.34f, 0.47f, 0.17f), new Vector3(0.6f, -2.15f, -0.4f));
            nearFog.flipX = true;
            SpriteRenderer foreground = CreateLayer(root.transform, "ForegroundFrame", ForegroundPath, 40,
                new Color(0.16f, 0.23f, 0.32f, 0.52f), new Vector3(0f, -1.15f, -0.5f));

            GeneratedBackground2D controller = root.AddComponent<GeneratedBackground2D>();
            controller.Configure(camera, sky, ruins, farFog, midground, nearFog, foreground);

            EditorUtility.SetDirty(root);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = root;
            Debug.Log("Installed layered 2D background with far, midground, fog, and foreground depth.");
        }

        private static SpriteRenderer CreateLayer(Transform parent, string name, string assetPath,
            int sortingOrder, Color color, Vector3 localPosition)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null) throw new MissingReferenceException($"Could not load background sprite at {assetPath}");
            GameObject layer = new GameObject(name);
            layer.transform.SetParent(parent, false);
            layer.transform.localPosition = localPosition;
            SpriteRenderer renderer = layer.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            renderer.color = color;
            return renderer;
        }

        private static void CreateFogTexture()
        {
            const int width = 1024;
            const int height = 256;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float vertical = Mathf.Sin((y / (height - 1f)) * Mathf.PI);
                for (int x = 0; x < width; x++)
                {
                    float broad = Mathf.PerlinNoise(x * 0.006f, y * 0.014f);
                    float fine = Mathf.PerlinNoise(20f + x * 0.018f, 40f + y * 0.028f);
                    float alpha = Mathf.Clamp01((broad * 0.72f + fine * 0.28f - 0.35f) * vertical);
                    pixels[y * width + x] = new Color(1f, 1f, 1f, alpha * 0.72f);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(FogPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(FogPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        private static void ConfigureTexture(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        private static void DisableLegacyBackground(string objectName)
        {
            GameObject legacy = GameObject.Find(objectName);
            if (legacy == null) return;
            legacy.SetActive(false);
            EditorUtility.SetDirty(legacy);
        }
    }
}