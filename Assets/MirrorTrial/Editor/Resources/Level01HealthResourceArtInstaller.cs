using System;
using System.IO;
using MirrorTrial.Combat;
using MirrorTrial.HealthResources;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MirrorTrial.Editor.HealthResources
{
    public static class Level01HealthResourceArtInstaller
    {
        const string ArtFolder = "Assets/MirrorTrial/Art/Level01_Ruins/LifeResources";
        const string PrefabFolder = "Assets/MirrorTrial/Prefabs/Level/Resources";
        const string PreviewFolder = "Assets/MirrorTrial/Art/Previews";
        const string SparkleTexturePath = ArtFolder + "/HealthResourceSparkle_v1.png";
        const string SparkleMaterialPath = ArtFolder + "/HealthResourceSparkle_v1.mat";

        [MenuItem("Tools/Mirror Trial/关卡/生成第一关生命资源与补充闪光")]
        public static void Install()
        {
            EnsureFolder("Assets/MirrorTrial/Art", "Level01_Ruins");
            EnsureFolder("Assets/MirrorTrial/Art/Level01_Ruins", "LifeResources");
            EnsureFolder("Assets/MirrorTrial/Art", "Previews");
            ConfigureTextures();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var sparkleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SparkleTexturePath);
            var sparkleMaterial = GetOrCreateSparkleMaterial(sparkleSprite);
            if (!sparkleSprite || !sparkleMaterial)
                throw new InvalidOperationException("生命资源闪光贴图或材质导入失败。");

            var crystal = CreateResourcePrefab(
                "HealthResource_RiftlightCrystal",
                "RiftlightCrystal",
                maxDurability: 3,
                lifeEssenceReward: 2,
                colliderSize: new Vector2(1.45f, 1.55f),
                colliderOffset: new Vector2(0f, 0.78f),
                glowPosition: new Vector3(0f, 0.95f, 0f),
                glowColor: new Color(0.4f, 0.86f, 1f, 1f),
                sparkleSprite,
                sparkleMaterial);

            var reliquary = CreateResourcePrefab(
                "HealthResource_MirrorDewReliquary",
                "MirrorDewReliquary",
                maxDurability: 4,
                lifeEssenceReward: 3,
                colliderSize: new Vector2(1.55f, 1.5f),
                colliderOffset: new Vector2(0f, 0.75f),
                glowPosition: new Vector3(0f, 1.15f, 0f),
                glowColor: new Color(0.55f, 0.92f, 1f, 1f),
                sparkleSprite,
                sparkleMaterial);

            var fern = AddSparklesToExistingPrefab(
                PrefabFolder + "/HealthResource_GlowSacFern.prefab",
                new Color(0.92f, 1f, 0.45f, 1f), sparkleSprite, sparkleMaterial);
            var orchid = AddSparklesToExistingPrefab(
                PrefabFolder + "/HealthResource_DewCupOrchid.prefab",
                new Color(1f, 0.93f, 0.48f, 1f), sparkleSprite, sparkleMaterial);

            HealthResourceCatalogEditorUtility.Rebuild();
            RenderPairPreview(
                new[] { crystal, reliquary },
                PreviewFolder + "/Level01_HealthResources_Preview_v1.png",
                new Color(0.025f, 0.055f, 0.105f, 1f));
            RenderPairPreview(
                new[] { fern, orchid },
                PreviewFolder + "/Existing_HealthResources_Sparkle_Preview_v1.png",
                new Color(0.025f, 0.075f, 0.085f, 1f));

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.SaveAssets();
            Debug.Log("[MirrorTrial] 第一关生命资源、现有资源闪光粒子与两张预览图已生成。", crystal);
        }

        static void ConfigureTextures()
        {
            var prefixes = new[] { "RiftlightCrystal", "MirrorDewReliquary" };
            var states = new[] { "Full", "Damaged1", "Damaged2", "Depleted" };
            foreach (var prefix in prefixes)
            foreach (var state in states)
                ConfigureSprite(ArtFolder + "/" + prefix + "_" + state + "_v1.png", 256f, new Vector2(0.5f, 0.0625f));

            ConfigureSprite(SparkleTexturePath, 16f, new Vector2(0.5f, 0.5f));
        }

        static void ConfigureSprite(string path, float pixelsPerUnit, Vector2 pivot)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) throw new FileNotFoundException("找不到生命资源贴图", path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.spritePivot = pivot;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        static Material GetOrCreateSparkleMaterial(Sprite sparkleSprite)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(SparkleMaterialPath);
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (!shader) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (!shader) shader = Shader.Find("Sprites/Default");
            if (!shader) return null;
            if (!material)
            {
                material = new Material(shader) { name = "HealthResourceSparkle_v1" };
                AssetDatabase.CreateAsset(material, SparkleMaterialPath);
            }
            else material.shader = shader;
            material.mainTexture = sparkleSprite.texture;
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", sparkleSprite.texture);
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 2f);
            BaseShaderGUI.SetupMaterialBlendMode(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        static GameObject CreateResourcePrefab(
            string prefabName,
            string artPrefix,
            int maxDurability,
            int lifeEssenceReward,
            Vector2 colliderSize,
            Vector2 colliderOffset,
            Vector3 glowPosition,
            Color glowColor,
            Sprite sparkleSprite,
            Material sparkleMaterial)
        {
            var sprites = new[]
            {
                LoadSprite(artPrefix, "Full"),
                LoadSprite(artPrefix, "Damaged1"),
                LoadSprite(artPrefix, "Damaged2"),
                LoadSprite(artPrefix, "Depleted")
            };
            var root = new GameObject(prefabName);
            try
            {
                var collider = root.AddComponent<BoxCollider2D>();
                collider.isTrigger = true;
                collider.size = colliderSize;
                collider.offset = colliderOffset;
                root.AddComponent<Hurtbox>();
                var node = root.AddComponent<HealthResourceNode>();

                var visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);
                var renderer = visual.AddComponent<SpriteRenderer>();
                renderer.sprite = sprites[0];
                renderer.sortingOrder = 15;
                var view = visual.AddComponent<HealthResourcePlantView>();
                SetObjectReference(view, "node", node);
                SetObjectReference(view, "targetRenderer", renderer);
                SetObjectReferenceArray(view, "durabilityStates", sprites);

                var particles = AddIdleSparkles(root.transform, glowPosition, glowColor, sparkleSprite, sparkleMaterial);
                var light = particles[0].GetComponent<Light2D>();
                var glow = particles[0].gameObject.AddComponent<HealthResourcePlantGlow>();
                SetObjectReference(glow, "node", node);
                SetObjectReference(glow, "glowLight", light);
                SetObjectReferenceArray(glow, "sparkleParticles", particles);
                SetFloat(glow, "baseIntensity", 0.78f);
                SetFloat(glow, "pulseSpeed", 1.85f);

                var nodeData = new SerializedObject(node);
                nodeData.FindProperty("maxDurability").intValue = maxDurability;
                nodeData.FindProperty("lifeEssenceReward").intValue = lifeEssenceReward;
                nodeData.FindProperty("countHitsInsteadOfDamage").boolValue = true;
                nodeData.FindProperty("rewardOrbSprite").objectReferenceValue = sparkleSprite;
                nodeData.FindProperty("rewardEffectOffset").vector3Value = glowPosition;
                nodeData.FindProperty("rewardOrbColor").colorValue = glowColor;
                nodeData.FindProperty("rewardTravelDuration").floatValue = 0.55f;
                nodeData.FindProperty("flashColor").colorValue = new Color(0.82f, 0.96f, 1f, 1f);
                nodeData.FindProperty("flashDuration").floatValue = 0.08f;
                nodeData.FindProperty("keepDepletedVisual").boolValue = false;
                nodeData.FindProperty("deactivateDelay").floatValue = 0.35f;
                var renderers = nodeData.FindProperty("flashRenderers");
                renderers.arraySize = 1;
                renderers.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
                nodeData.ApplyModifiedPropertiesWithoutUndo();

                var path = PrefabFolder + "/" + prefabName + ".prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                HealthResourceCatalogEditorUtility.Register(prefab);
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static GameObject AddSparklesToExistingPrefab(string prefabPath, Color color, Sprite sparkleSprite, Material sparkleMaterial)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                foreach (var old in root.GetComponentsInChildren<ParticleSystem>(true))
                    if (old.transform.name == "IdleSparkleParticles")
                        UnityEngine.Object.DestroyImmediate(old.gameObject);

                var glow = root.GetComponentInChildren<HealthResourcePlantGlow>(true);
                var parent = glow ? glow.transform : root.transform;
                var particles = AddIdleSparkles(parent, new Vector3(0f, 0.18f, 0f), color, sparkleSprite, sparkleMaterial);
                if (glow)
                {
                    SetObjectReferenceArray(glow, "sparkleParticles", particles);
                    SetFloat(glow, "baseIntensity", 0.78f);
                    SetFloat(glow, "pulseSpeed", 1.85f);
                }
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        static ParticleSystem[] AddIdleSparkles(Transform parent, Vector3 localPosition, Color color, Sprite sparkleSprite, Material material)
        {
            var go = new GameObject("IdleSparkleParticles");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;

            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = color;
            light.intensity = 0.78f;
            light.pointLightInnerRadius = 0.15f;
            light.pointLightOuterRadius = 1.08f;
            light.falloffIntensity = 0.65f;

            var ambient = go.AddComponent<ParticleSystem>();
            ambient.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ambient.useAutoRandomSeed = false;
            ambient.randomSeed = 71031;
            var main = ambient.main;
            main.loop = true;
            main.prewarm = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.22f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.12f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(color.r, color.g, color.b, 0.85f),
                new Color(0.9f, 0.98f, 1f, 1f));
            main.maxParticles = 36;

            var emission = ambient.emission;
            emission.rateOverTime = 8f;
            var shape = ambient.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.72f, 0.42f, 0.05f);

            var velocity = ambient.velocityOverLifetime;
            velocity.enabled = true;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var colorOverLifetime = ambient.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var alpha = new Gradient();
            alpha.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.18f),
                    new GradientAlphaKey(0.85f, 0.72f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = alpha;

            var size = ambient.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.25f, 1f, 1f));

            var particleRenderer = ambient.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.sortingOrder = 28;
            particleRenderer.sharedMaterial = material;
            ambient.Play(true);

            var burstObject = new GameObject("FocusFlashBurst");
            burstObject.transform.SetParent(go.transform, false);
            var burst = burstObject.AddComponent<ParticleSystem>();
            burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            burst.useAutoRandomSeed = false;
            burst.randomSeed = 97103;
            var burstMain = burst.main;
            burstMain.duration = 0.95f;
            burstMain.loop = true;
            burstMain.prewarm = true;
            burstMain.playOnAwake = true;
            burstMain.simulationSpace = ParticleSystemSimulationSpace.Local;
            burstMain.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.45f);
            burstMain.startSpeed = new ParticleSystem.MinMaxCurve(0.01f, 0.05f);
            burstMain.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.22f);
            burstMain.startColor = new ParticleSystem.MinMaxGradient(
                new Color(color.r, color.g, color.b, 0.95f),
                new Color(1f, 1f, 1f, 1f));
            burstMain.maxParticles = 12;

            var burstEmission = burst.emission;
            burstEmission.rateOverTime = 0f;
            burstEmission.SetBursts(new[] { new ParticleSystem.Burst(0.08f, 2, 3) });
            var burstShape = burst.shape;
            burstShape.shapeType = ParticleSystemShapeType.Box;
            burstShape.scale = new Vector3(0.46f, 0.28f, 0.03f);

            var burstColor = burst.colorOverLifetime;
            burstColor.enabled = true;
            var burstGradient = new Gradient();
            burstGradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.12f),
                    new GradientAlphaKey(0.92f, 0.55f),
                    new GradientAlphaKey(0f, 1f)
                });
            burstColor.color = burstGradient;

            var burstSize = burst.sizeOverLifetime;
            burstSize.enabled = true;
            burstSize.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.2f),
                new Keyframe(0.22f, 1f),
                new Keyframe(1f, 0.45f)));

            var burstRenderer = burst.GetComponent<ParticleSystemRenderer>();
            burstRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            burstRenderer.sortingOrder = 30;
            burstRenderer.sharedMaterial = material;
            burst.Play(true);
            return new[] { ambient, burst };
        }

        static void RenderPairPreview(GameObject[] prefabs, string path, Color background)
        {
            const int previewLayer = 31;
            const int width = 1280;
            const int height = 720;
            var cameraObject = new GameObject("HealthResourcePreviewCamera");
            var instances = new GameObject[prefabs.Length];
            var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var output = new Texture2D(width, height, TextureFormat.RGBA32, false);
            try
            {
                cameraObject.layer = previewLayer;
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 2.35f;
                camera.aspect = width / (float)height;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = background;
                camera.cullingMask = 1 << previewLayer;
                camera.transform.position = new Vector3(0f, 1.05f, -10f);
                camera.targetTexture = renderTexture;

                for (var i = 0; i < prefabs.Length; i++)
                {
                    if (!prefabs[i]) continue;
                    var instance = UnityEngine.Object.Instantiate(prefabs[i]);
                    instance.name = prefabs[i].name;
                    instance.transform.position = new Vector3(i == 0 ? -1.35f : 1.35f, 0f, 0f);
                    SetLayerRecursively(instance, previewLayer);
                    instances[i] = instance;
                    foreach (var particles in instance.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        particles.useAutoRandomSeed = false;
                        particles.randomSeed = (uint)(71031 + i * 97);
                        particles.Simulate(1.35f, true, true, true);
                    }
                }

                camera.Render();
                var previous = RenderTexture.active;
                RenderTexture.active = renderTexture;
                output.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                output.Apply();
                RenderTexture.active = previous;
                File.WriteAllBytes(path, output.EncodeToPNG());
            }
            finally
            {
                for (var i = 0; i < instances.Length; i++)
                    if (instances[i]) UnityEngine.Object.DestroyImmediate(instances[i]);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(output);
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
            }
        }

        static Sprite LoadSprite(string prefix, string state)
        {
            var path = ArtFolder + "/" + prefix + "_" + state + "_v1.png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (!sprite) throw new FileNotFoundException("生命资源状态贴图没有导入为 Sprite", path);
            return sprite;
        }

        static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform) SetLayerRecursively(child.gameObject, layer);
        }

        static void SetObjectReference(UnityEngine.Object target, string property, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(property).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetObjectReferenceArray(UnityEngine.Object target, string property, UnityEngine.Object[] values)
        {
            var serialized = new SerializedObject(target);
            var array = serialized.FindProperty(property);
            array.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetFloat(UnityEngine.Object target, string property, float value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(property).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void EnsureFolder(string parent, string child)
        {
            var path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
        }
    }
}
