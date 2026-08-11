using System.IO;
using MirrorTrial.Puzzles;
using UnityEditor;
using UnityEngine;

namespace MirrorTrial.Editor.Level
{
    public static class UpdraftWindVfxPrefabBuilder
    {
        const string ArtFolder = "Assets/MirrorTrial/Art/VFX/UpdraftWind";
        const string MaterialFolder = "Assets/MirrorTrial/Materials/VFX";
        const string PrefabFolder = "Assets/MirrorTrial/Prefabs/VFX";
        const string PrefabPath = PrefabFolder + "/UpdraftWindVFX.prefab";

        [MenuItem("Tools/Mirror Trial/VFX/Rebuild Updraft Wind Prefab")]
        public static void Rebuild()
        {
            EnsureFolder("Assets/MirrorTrial/Art", "VFX");
            EnsureFolder("Assets/MirrorTrial/Art/VFX", "UpdraftWind");
            EnsureFolder("Assets/MirrorTrial/Materials", "VFX");
            EnsureFolder("Assets/MirrorTrial/Prefabs", "VFX");

            var lineTexture = CreateOrReplaceTexture(ArtFolder + "/UpdraftWindLine.png", 8, 32, DrawWindLine);
            var leafTexture = CreateOrReplaceTexture(ArtFolder + "/UpdraftLeaf.png", 8, 6, DrawLeaf);
            var pixelTexture = CreateOrReplaceTexture(ArtFolder + "/UpdraftPixel.png", 5, 5, DrawPixel);

            var lineMaterial = CreateOrUpdateMaterial(
                MaterialFolder + "/UpdraftWindLine.mat", lineTexture, "Updraft Wind Line");
            var leafMaterial = CreateOrUpdateMaterial(
                MaterialFolder + "/UpdraftLeaf.mat", leafTexture, "Updraft Leaf");
            var pixelMaterial = CreateOrUpdateMaterial(
                MaterialFolder + "/UpdraftPixel.mat", pixelTexture, "Updraft Pixel");

            var root = new GameObject("UpdraftWindVFX");
            try
            {
                var controller = root.AddComponent<UpdraftWindVfx>();
                var primary = CreatePrimaryLines(root.transform, lineMaterial);
                var wisps = CreateShortWisps(root.transform, lineMaterial);
                var leaves = CreateLeaves(root.transform, leafMaterial);
                var motes = CreateMotes(root.transform, pixelMaterial);
                var dust = CreateBaseDust(root.transform, pixelMaterial);
                controller.AssignLayers(primary, wisps, leaves, motes, dust);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
                Debug.Log("Reusable updraft wind VFX prefab rebuilt at " + PrefabPath, prefab);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static ParticleSystem CreatePrimaryLines(Transform parent, Material material)
        {
            var system = CreateSystem("Primary Wind Lines", parent, material, 70);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.72f, 1.05f);
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(0.045f, 0.075f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(0.48f, 0.82f);
            main.startSizeZ = 0.01f;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.92f, 0.97f, 1f, 0.86f),
                new Color(1f, 1f, 1f, 0.98f));
            main.maxParticles = 36;

            SetEmission(system, 8.5f);
            SetBaseShape(system, 1.05f);
            SetUpwardVelocity(system, 3.6f, 4.8f, -0.1f, 0.1f);
            SetSoftNoise(system, 0.08f, 0.45f, 0.32f);
            SetFade(system, 0f, 0.85f, 0.7f, 0f);
            return system;
        }

        static ParticleSystem CreateShortWisps(Transform parent, Material material)
        {
            var system = CreateSystem("Short Wind Wisps", parent, material, 71);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.34f, 0.62f);
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(0.025f, 0.045f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(0.2f, 0.42f);
            main.startSizeZ = 0.01f;
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.88f, 0.95f, 1f, 0.72f),
                new Color(1f, 1f, 1f, 0.9f));
            main.maxParticles = 42;

            SetEmission(system, 11f);
            SetBaseShape(system, 1.2f);
            SetUpwardVelocity(system, 4.5f, 5.8f, -0.14f, 0.14f);
            SetSoftNoise(system, 0.11f, 0.65f, 0.45f);
            SetFade(system, 0f, 0.72f, 0.58f, 0f);
            return system;
        }

        static ParticleSystem CreateLeaves(Transform parent, Material material)
        {
            var system = CreateSystem("Lifted Leaves", parent, material, 73);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.35f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.2f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-3.14f, 3.14f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.48f, 0.6f, 0.18f, 0.68f),
                new Color(0.86f, 0.7f, 0.22f, 0.88f));
            main.maxParticles = 24;

            SetEmission(system, 3.6f);
            SetBaseShape(system, 0.95f);
            SetUpwardVelocity(system, 2.6f, 3.8f, -0.28f, 0.28f);
            SetSoftNoise(system, 0.2f, 0.5f, 0.7f);
            SetFade(system, 0f, 1f, 0.82f, 0f);
            var rotation = system.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-2.2f, 2.2f);
            return system;
        }

        static ParticleSystem CreateMotes(Transform parent, Material material)
        {
            var system = CreateSystem("Floating Motes", parent, material, 72);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.75f, 1.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.052f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.68f, 0.96f, 1f, 0.3f),
                new Color(1f, 0.78f, 0.28f, 0.66f));
            main.maxParticles = 18;

            SetEmission(system, 4.2f);
            SetBaseShape(system, 1.05f);
            SetUpwardVelocity(system, 2.1f, 3.45f, -0.2f, 0.2f);
            SetSoftNoise(system, 0.14f, 0.7f, 0.42f);
            SetFade(system, 0f, 0.9f, 0.72f, 0f);
            return system;
        }

        static ParticleSystem CreateBaseDust(Transform parent, Material material)
        {
            var system = CreateSystem("Base Dust", parent, material, 69);
            var main = system.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.38f, 0.66f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.1f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.62f, 0.82f, 0.72f, 0.1f),
                new Color(0.88f, 0.92f, 0.72f, 0.24f));
            main.maxParticles = 12;

            SetEmission(system, 3.2f);
            SetBaseShape(system, 0.82f);
            SetUpwardVelocity(system, 0.38f, 0.78f, -0.42f, 0.42f);
            SetSoftNoise(system, 0.1f, 0.6f, 0.3f);
            SetFade(system, 0f, 0.7f, 0.46f, 0f);
            return system;
        }

        static ParticleSystem CreateSystem(string name, Transform parent, Material material, int sortingOrder)
        {
            var gameObject = new GameObject(name, typeof(ParticleSystem));
            gameObject.transform.SetParent(parent, false);
            var system = gameObject.GetComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 2f;
            main.startSpeed = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            var renderer = gameObject.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortMode = ParticleSystemSortMode.YoungestInFront;
            renderer.sortingOrder = sortingOrder;
            return system;
        }

        static void SetEmission(ParticleSystem system, float rate)
        {
            var emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = rate;
        }

        static void SetBaseShape(ParticleSystem system, float width)
        {
            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Rectangle;
            shape.scale = new Vector3(width, 0.025f, 0f);
            shape.rotation = new Vector3(-90f, 0f, 0f);
        }

        static void SetUpwardVelocity(
            ParticleSystem system,
            float minY,
            float maxY,
            float minX,
            float maxX)
        {
            var velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(minX, maxX);
            velocity.y = new ParticleSystem.MinMaxCurve(minY, maxY);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        }

        static void SetSoftNoise(
            ParticleSystem system,
            float strength,
            float frequency,
            float scrollSpeed)
        {
            var noise = system.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.Low;
            noise.strength = strength;
            noise.frequency = frequency;
            noise.scrollSpeed = scrollSpeed;
        }

        static void SetFade(
            ParticleSystem system,
            float startAlpha,
            float peakAlpha,
            float lateAlpha,
            float endAlpha)
        {
            var color = system.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(startAlpha, 0f),
                    new GradientAlphaKey(peakAlpha, 0.12f),
                    new GradientAlphaKey(lateAlpha, 0.72f),
                    new GradientAlphaKey(endAlpha, 1f)
                });
            color.color = gradient;
        }

        delegate Color PixelDrawer(int x, int y, int width, int height);

        static Texture2D CreateOrReplaceTexture(string path, int width, int height, PixelDrawer draw)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                texture.SetPixel(x, y, draw(x, y, width, height));
            texture.Apply(false, false);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.spritePixelsPerUnit = 32f;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Color DrawWindLine(int x, int y, int width, int height)
        {
            var normalizedY = y / (float)(height - 1);
            var center = width / 2 + ((y / 7) % 2 == 0 ? 0 : 1);
            var distance = Mathf.Abs(x - center);
            var taper = Mathf.Sin(normalizedY * Mathf.PI);
            if (distance == 0 && taper > 0.08f) return new Color(1f, 1f, 1f, Mathf.Lerp(0.35f, 1f, taper));
            if (distance == 1 && taper > 0.28f) return new Color(0.82f, 0.98f, 1f, 0.34f * taper);
            return Color.clear;
        }

        static Color DrawLeaf(int x, int y, int width, int height)
        {
            var shape = (x == 1 && y == 2) || (x == 2 && (y == 1 || y == 2 || y == 3)) ||
                        (x == 3 && (y == 1 || y == 2 || y == 3 || y == 4)) ||
                        (x == 4 && (y == 2 || y == 3 || y == 4)) || (x == 5 && y == 3);
            if (!shape) return Color.clear;
            return (x + y) % 3 == 0 ? new Color(0.82f, 0.72f, 0.28f, 1f) : Color.white;
        }

        static Color DrawPixel(int x, int y, int width, int height)
        {
            var dx = x - width / 2;
            var dy = y - height / 2;
            if (Mathf.Abs(dx) + Mathf.Abs(dy) > 2) return Color.clear;
            return Mathf.Abs(dx) + Mathf.Abs(dy) == 2
                ? new Color(1f, 1f, 1f, 0.45f)
                : Color.white;
        }

        static Material CreateOrUpdateMaterial(string path, Texture texture, string name)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                var shader = Shader.Find("Sprites/Default");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.mainTexture = texture;
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            EditorUtility.SetDirty(material);
            return material;
        }

        static void EnsureFolder(string parent, string child)
        {
            var path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
        }
    }
}
