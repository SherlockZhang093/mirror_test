using System;
using System.IO;
using MirrorTrial.Puzzles;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace MirrorTrial.Editor.Level
{
    public static class PuzzleChargePresentationPrefabBuilder
    {
        const string PrefabPath = "Assets/MirrorTrial/Prefabs/VFX/PuzzleChargePresentation.prefab";
        const string TransferPrefabPath = "Assets/MirrorTrial/Prefabs/VFX/AbilityTransferEffect.prefab";
        const string PuzzlePrefabPath = "Assets/MirrorTrial/Prefabs/Puzzles/DoubleJumpMirrorLightPuzzle.prefab";
        const string AssetFolder = "Assets/MirrorTrial/Art/Effects/PuzzleCharge";
        const string AudioFolder = "Assets/MirrorTrial/Audio/SFX/Puzzles/ChargePresentation";

        [InitializeOnLoadMethod]
        static void ScheduleBuild()
        {
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) ||
                !AssetDatabase.LoadAssetAtPath<GameObject>(TransferPrefabPath))
                EditorApplication.delayCall += Build;
        }

        [MenuItem("Tools/Mirror Trial/Level 02/Build Reusable Charge Presentation")]
        public static void Build()
        {
            EnsureFolders();
            CreateVisualAssets();
            CreateAudioAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureTextureImporters();

            var prefab = CreatePrefab();
            var transferPrefab = CreateAbilityTransferPrefab();
            BindToPuzzle(prefab, transferPrefab);
            AssetDatabase.SaveAssets();
            Debug.Log("Built reusable charge presentation prefab: " + PrefabPath, prefab);
        }

        static void EnsureFolders()
        {
            Directory.CreateDirectory(AssetFolder);
            Directory.CreateDirectory(AudioFolder);
            Directory.CreateDirectory("Assets/MirrorTrial/Prefabs/VFX");
        }

        static void CreateVisualAssets()
        {
            WriteTexture(AssetFolder + "/ChargeCore.png", 48, 48, (x, y, w, h) =>
            {
                var dx = x + 0.5f - w * 0.5f;
                var dy = y + 0.5f - h * 0.5f;
                var distance = Mathf.Sqrt(dx * dx + dy * dy);
                if (distance > 21f) return Color.clear;
                if (distance > 17f) return new Color32(72, 35, 10, 235);
                if (distance > 13f) return new Color32(194, 100, 12, 245);
                var light = (byte)Mathf.Lerp(120f, 255f, 1f - distance / 13f);
                return new Color32(255, light, 28, 255);
            });

            WriteTexture(AssetFolder + "/ChargeGlow.png", 64, 64, (x, y, w, h) =>
            {
                var dx = x + 0.5f - w * 0.5f;
                var dy = y + 0.5f - h * 0.5f;
                var normalized = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / 31f);
                var alpha = (byte)(Mathf.Pow(1f - normalized, 2.2f) * 210f);
                return new Color32(255, 145, 24, alpha);
            });

            WriteTexture(AssetFolder + "/ChargeSegment.png", 14, 28, (x, y, w, h) =>
            {
                if (x < 2 || x >= w - 2 || y < 3 || y >= h - 3) return Color.clear;
                var edge = x == 2 || x == w - 3 || y == 3 || y == h - 4;
                var rune = (y > 9 && y < 18 && (x == 5 || x == 8));
                return edge || rune
                    ? new Color32(255, 205, 76, 255)
                    : new Color32(151, 68, 8, 255);
            });

            WriteTexture(AssetFolder + "/ChargeFullFlash.png", 64, 64, (x, y, w, h) =>
            {
                var dx = Mathf.Abs(x + 0.5f - w * 0.5f);
                var dy = Mathf.Abs(y + 0.5f - h * 0.5f);
                var cross = Mathf.Min(dx * 0.22f + dy, dy * 0.22f + dx);
                if (cross > 8f) return Color.clear;
                var alpha = (byte)(Mathf.Clamp01(1f - cross / 8f) * 245f);
                return new Color32(255, 242, 170, alpha);
            });
        }

        static void WriteTexture(string path, int width, int height,
            Func<int, int, int, int, Color> pixel)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                    texture.SetPixel(x, y, pixel(x, y, width, height));
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        static void ConfigureTextureImporters()
        {
            foreach (var path in new[]
                     {
                         AssetFolder + "/ChargeCore.png", AssetFolder + "/ChargeGlow.png",
                         AssetFolder + "/ChargeSegment.png", AssetFolder + "/ChargeFullFlash.png"
                     })
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (!importer) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = 64f;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
        }

        static void CreateAudioAssets()
        {
            const int sampleRate = 44100;
            WriteWav(AudioFolder + "/Charge_CrystalTick.wav", sampleRate, 0.18f, (time, progress) =>
            {
                var envelope = Mathf.Exp(-time * 24f) * Mathf.Min(1f, time * 180f);
                return (Mathf.Sin(Mathf.PI * 2f * 880f * time) * 0.62f +
                        Mathf.Sin(Mathf.PI * 2f * 1320f * time) * 0.25f +
                        Mathf.Sin(Mathf.PI * 2f * 1760f * time) * 0.13f) * envelope * 0.45f;
            });
            WriteWav(AudioFolder + "/Charge_CompleteChime.wav", sampleRate, 1.15f, (time, progress) =>
            {
                var envelope = Mathf.Min(1f, time * 45f) * Mathf.Exp(-time * 3.1f);
                return (Mathf.Sin(Mathf.PI * 2f * 220f * time) * 0.38f +
                        Mathf.Sin(Mathf.PI * 2f * 660f * time) * 0.34f +
                        Mathf.Sin(Mathf.PI * 2f * 990f * time) * 0.18f +
                        Mathf.Sin(Mathf.PI * 2f * (330f + time * 210f) * time) * 0.2f) * envelope * 0.55f;
            });

            var random = new System.Random(7241);
            var filteredNoise = 0f;
            WriteWav(AudioFolder + "/Charge_StoneDoor.wav", sampleRate, 1.65f, (time, progress) =>
            {
                var noise = (float)(random.NextDouble() * 2.0 - 1.0);
                filteredNoise = Mathf.Lerp(filteredNoise, noise, 0.035f);
                var fade = Mathf.Sin(progress * Mathf.PI);
                var rumble = Mathf.Sin(Mathf.PI * 2f * 48f * time) * 0.32f +
                             Mathf.Sin(Mathf.PI * 2f * 73f * time) * 0.16f;
                return (filteredNoise * 0.72f + rumble +
                        Mathf.Sin(Mathf.PI * 2f * (7f + progress * 4f) * time) * 0.08f) * fade * 0.72f;
            });
            WriteWav(AudioFolder + "/AbilityTransfer_Launch.wav", sampleRate, 0.42f, (time, progress) =>
            {
                var envelope = Mathf.Sin(progress * Mathf.PI);
                var frequency = Mathf.Lerp(260f, 940f, progress * progress);
                return (Mathf.Sin(Mathf.PI * 2f * frequency * time) * 0.44f +
                        Mathf.Sin(Mathf.PI * 2f * frequency * 1.5f * time) * 0.16f) * envelope;
            });
            WriteWav(AudioFolder + "/AbilityTransfer_Arrive.wav", sampleRate, 0.48f, (time, progress) =>
            {
                var envelope = Mathf.Exp(-time * 7f) * Mathf.Min(1f, time * 90f);
                return (Mathf.Sin(Mathf.PI * 2f * 740f * time) * 0.42f +
                        Mathf.Sin(Mathf.PI * 2f * 1110f * time) * 0.24f +
                        Mathf.Sin(Mathf.PI * 2f * 1480f * time) * 0.12f) * envelope;
            });
        }

        static void WriteWav(string path, int sampleRate, float duration,
            Func<float, float, float> sampleFunction)
        {
            var sampleCount = Mathf.CeilToInt(sampleRate * duration);
            using (var stream = File.Create(path))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + sampleCount * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(sampleRate);
                writer.Write(sampleRate * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                writer.Write(sampleCount * 2);
                for (var i = 0; i < sampleCount; i++)
                {
                    var time = i / (float)sampleRate;
                    var value = Mathf.Clamp(sampleFunction(time, i / (float)(sampleCount - 1)), -1f, 1f);
                    writer.Write((short)Mathf.RoundToInt(value * short.MaxValue));
                }
            }
        }

        static GameObject CreatePrefab()
        {
            var root = new GameObject("PuzzleChargePresentation");
            try
            {
                var presentation = root.AddComponent<PuzzleChargePresentation>();
                var loopSource = AddAudioSource(root, true);
                var feedbackSource = AddAudioSource(root, false);
                var doorSource = AddAudioSource(root, false);

                var glow = AddSprite("Glow", root.transform, "ChargeGlow.png", 81);
                glow.transform.localScale = Vector3.one * 1.15f;
                var core = AddSprite("Core", root.transform, "ChargeCore.png", 82);
                var flash = AddSprite("FullFlash", root.transform, "ChargeFullFlash.png", 85);
                flash.enabled = false;

                var segments = new SpriteRenderer[8];
                for (var i = 0; i < segments.Length; i++)
                {
                    var angle = Mathf.PI * 2f * i / segments.Length;
                    var segment = AddSprite("RuneSegment_" + (i + 1), root.transform,
                        "ChargeSegment.png", 84);
                    segment.transform.localPosition = new Vector3(Mathf.Cos(angle) * 0.72f,
                        Mathf.Sin(angle) * 0.72f, 0f);
                    segment.transform.localRotation = Quaternion.Euler(0f, 0f,
                        angle * Mathf.Rad2Deg - 90f);
                    segments[i] = segment;
                }

                var serialized = new SerializedObject(presentation);
                serialized.FindProperty("core").objectReferenceValue = core;
                serialized.FindProperty("glow").objectReferenceValue = glow;
                serialized.FindProperty("fullFlash").objectReferenceValue = flash;
                var segmentProperty = serialized.FindProperty("segments");
                segmentProperty.arraySize = segments.Length;
                for (var i = 0; i < segments.Length; i++)
                    segmentProperty.GetArrayElementAtIndex(i).objectReferenceValue = segments[i];
                serialized.FindProperty("chargeLoopSource").objectReferenceValue = loopSource;
                serialized.FindProperty("feedbackSource").objectReferenceValue = feedbackSource;
                serialized.FindProperty("doorSource").objectReferenceValue = doorSource;
                serialized.FindProperty("chargeLoopClip").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/MirrorTrial/Audio/SFX/Boss/Boss_Charge.wav");
                serialized.FindProperty("chargeTickClip").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "/Charge_CrystalTick.wav");
                serialized.FindProperty("completedClip").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "/Charge_CompleteChime.wav");
                serialized.FindProperty("doorOpeningClip").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "/Charge_StoneDoor.wav");
                serialized.ApplyModifiedPropertiesWithoutUndo();

                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static AudioSource AddAudioSource(GameObject owner, bool loop)
        {
            var source = owner.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            return source;
        }

        static GameObject CreateAbilityTransferPrefab()
        {
            var root = new GameObject("AbilityTransferEffect");
            try
            {
                var effect = root.AddComponent<AbilityTransferEffect>();
                var source = AddAudioSource(root, false);
                var light = root.AddComponent<Light2D>();
                light.lightType = Light2D.LightType.Point;
                light.color = new Color(0.18f, 0.88f, 1f, 1f);
                light.intensity = 1.35f;
                light.pointLightInnerRadius = 0.06f;
                light.pointLightOuterRadius = 0.48f;
                light.falloffIntensity = 0.7f;

                var main = AddSprite("AbilityCore", root.transform, "ChargeCore.png", 92);
                main.transform.localScale = Vector3.one * 0.62f;
                main.color = new Color(1f, 0.96f, 0.72f, 1f);
                var flash = AddSprite("ArrivalFlash", root.transform, "ChargeFullFlash.png", 95);
                flash.enabled = false;
                var beamGlow = CreateTransferBeam("TransferBeamGlow", root.transform, 90,
                    0.14f, new Color(0.18f, 0.88f, 1f, 0.1f), new Color(0.18f, 0.88f, 1f, 0.38f));
                var beamCore = CreateTransferBeam("TransferBeamCore", root.transform, 93,
                    0.045f, new Color(1f, 0.9f, 0.55f, 0.7f), new Color(1f, 0.98f, 0.82f, 1f));
                var gatherBurst = CreateTransferParticles("GatherBurst", root.transform,
                    new Color(0.35f, 0.9f, 1f, 0.82f), 18, 0.28f, 0.32f);
                var arrivalBurst = CreateTransferParticles("ArrivalBurst", root.transform,
                    new Color(1f, 0.96f, 0.72f, 0.92f), 26, 0.34f, 0.52f);
                arrivalBurst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                var echoes = new SpriteRenderer[2];
                for (var i = 0; i < echoes.Length; i++)
                {
                    var echo = AddSprite("TrailEcho_" + (i + 1), root.transform,
                        "ChargeCore.png", 91 - i);
                    echo.transform.localScale = Vector3.one * (0.78f - i * 0.18f);
                    echo.color = new Color(0.18f, 0.88f, 1f, i == 0 ? 0.26f : 0.12f);
                    echoes[i] = echo;
                }

                var sparks = new SpriteRenderer[3];
                for (var i = 0; i < sparks.Length; i++)
                {
                    var spark = AddSprite("OrbitSpark_" + (i + 1), root.transform,
                        "ChargeFullFlash.png", 94);
                    spark.transform.localScale = Vector3.one * (0.2f - i * 0.025f);
                    spark.color = i == sparks.Length - 1
                        ? new Color(0.72f, 0.48f, 1f, 0.72f)
                        : new Color(0.45f, 0.92f, 1f, 0.86f);
                    sparks[i] = spark;
                }

                var serialized = new SerializedObject(effect);
                serialized.FindProperty("mainRenderer").objectReferenceValue = main;
                SetRendererArray(serialized.FindProperty("echoes"), echoes);
                SetRendererArray(serialized.FindProperty("sparks"), sparks);
                serialized.FindProperty("arrivalFlash").objectReferenceValue = flash;
                serialized.FindProperty("beamCore").objectReferenceValue = beamCore;
                serialized.FindProperty("beamGlow").objectReferenceValue = beamGlow;
                serialized.FindProperty("gatherBurst").objectReferenceValue = gatherBurst;
                serialized.FindProperty("arrivalBurst").objectReferenceValue = arrivalBurst;
                serialized.FindProperty("orbLight").objectReferenceValue = light;
                serialized.FindProperty("audioSource").objectReferenceValue = source;
                serialized.FindProperty("launchSound").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "/AbilityTransfer_Launch.wav");
                serialized.FindProperty("arrivalSound").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "/AbilityTransfer_Arrive.wav");
                serialized.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(root, TransferPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static void SetRendererArray(SerializedProperty property, SpriteRenderer[] renderers)
        {
            property.arraySize = renderers.Length;
            for (var i = 0; i < renderers.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
        }

        static LineRenderer CreateTransferBeam(string name, Transform parent, int sortingOrder,
            float width, Color edgeColor, Color centerColor)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            var line = child.AddComponent<LineRenderer>();
            line.enabled = false;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, Vector3.zero);
            line.SetPosition(1, Vector3.right);
            line.startWidth = line.endWidth = width;
            line.numCapVertices = 6;
            line.numCornerVertices = 4;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.sortingOrder = sortingOrder;
            line.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(edgeColor, 0f), new GradientColorKey(centerColor, 0.5f), new GradientColorKey(edgeColor, 1f) },
                new[] { new GradientAlphaKey(edgeColor.a, 0f), new GradientAlphaKey(centerColor.a, 0.5f), new GradientAlphaKey(edgeColor.a, 1f) });
            line.colorGradient = gradient;
            return line;
        }

        static ParticleSystem CreateTransferParticles(string name, Transform parent, Color color,
            int burstCount, float lifetime, float speed)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            var system = child.AddComponent<ParticleSystem>();
            var main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.18f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.65f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.45f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.09f);
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burstCount) });

            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.18f;
            shape.radiusThickness = 0.15f;

            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = 96;
            renderer.material = new Material(Shader.Find("Sprites/Default"));
            return system;
        }

        static SpriteRenderer AddSprite(string objectName, Transform parent, string assetName,
            int sortingOrder)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(parent, false);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetFolder + "/" + assetName);
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        static void BindToPuzzle(GameObject presentationPrefab, GameObject transferPrefab)
        {
            var root = PrefabUtility.LoadPrefabContents(PuzzlePrefabPath);
            try
            {
                var receiver = root.GetComponentInChildren<LightPuzzleReceiver>(true);
                if (!receiver) return;
                var anchor = receiver.transform.Find("LeftReceivePoint");
                if (!anchor) return;

                var existing = anchor.GetComponentInChildren<PuzzleChargePresentation>(true);
                if (existing) Object.DestroyImmediate(existing.gameObject);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(presentationPrefab, anchor);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;

                var serialized = new SerializedObject(receiver);
                serialized.FindProperty("chargePresentation").objectReferenceValue =
                    instance.GetComponent<PuzzleChargePresentation>();
                serialized.FindProperty("abilityTransferPrefab").objectReferenceValue =
                    transferPrefab.GetComponent<AbilityTransferEffect>();
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PuzzlePrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
