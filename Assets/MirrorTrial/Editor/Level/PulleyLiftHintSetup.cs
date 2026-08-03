using MirrorTrial.Level;
using UnityEditor;
using UnityEngine;

namespace MirrorTrial.Editor.Level
{
    [InitializeOnLoad]
    public static class PulleyLiftHintSetup
    {
        const string PrefabPath = "Assets/MirrorTrial/Prefabs/Level/Mechanisms/PulleyLift.prefab";
        const string ArtRoot = "Assets/MirrorTrial/Art/Level02_Jungle/Mechanisms/PulleyLift";
        const string FrayedPath = ArtRoot + "/PulleyFrayedFibers.png";
        const string IconPath = ArtRoot + "/PulleyAttackIcon.png";
        const string GlowPath = ArtRoot + "/PulleyHintGlow.png";
        const string FiberPath = ArtRoot + "/PulleyRopeFiber.png";
        const string RopeTexturePath = ArtRoot + "/PulleyBraidedRopeLine.png";
        const string RopeMaterialPath = ArtRoot + "/PulleyBraidedRopeLine.mat";
        const string FiberMaterialPath = ArtRoot + "/PulleyRopeFiber.mat";
        const int CurrentSetupVersion = 4;

        // RopeHitTarget is centered on the hit box, while the slanted LineRenderer
        // crosses the cut height about 0.10 units to its left.
        static readonly Vector3 CutVisualLocalPosition = new Vector3(-0.10f, -0.3036f, 0f);

        static PulleyLiftHintSetup()
        {
            EditorApplication.delayCall += AutoInstall;
        }

        [MenuItem("Mirror Trial/Level/Install Pulley Lift Hint")]
        public static void InstallFromMenu()
        {
            Install(true);
        }

        static void AutoInstall()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                return;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var existingHint = prefab ? prefab.GetComponent<PulleyRopeHintVisual>() : null;
            if (!prefab || existingHint && existingHint.SetupVersion >= CurrentSetupVersion)
                return;

            Install(true);
        }

        static void Install(bool logResult)
        {
            ConfigureSprite(FrayedPath, 512f);
            ConfigureSprite(IconPath, 256f);
            ConfigureSprite(GlowPath, 256f);
            ConfigureSprite(FiberPath, 64f);
            ConfigureRopeTexture(RopeTexturePath);

            var frayedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FrayedPath);
            var iconSprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
            var glowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(GlowPath);
            var fiberSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FiberPath);
            var ropeTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(RopeTexturePath);
            if (!frayedSprite || !iconSprite || !glowSprite || !fiberSprite || !ropeTexture)
            {
                Debug.LogError("[PulleyLiftHintSetup] 提示素材导入失败，未修改 Prefab。");
                return;
            }

            var particleMaterial = GetOrCreateParticleMaterial(fiberSprite.texture);
            var ropeMaterial = GetOrCreateRopeMaterial(ropeTexture);
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (!root)
                return;

            try
            {
                var controller = root.GetComponent<PulleyLiftController>();
                var hitTarget = FindChild(root.transform, "RopeHitTarget");
                if (!controller || !hitTarget)
                {
                    Debug.LogError("[PulleyLiftHintSetup] PulleyLift 缺少 Controller 或 RopeHitTarget。");
                    return;
                }

                ApplyRopeMaterial(root, ropeMaterial);

                var oldHint = FindDirectChild(hitTarget, "CutHintRoot");
                if (oldHint)
                    Object.DestroyImmediate(oldHint.gameObject);
                var oldBreak = FindDirectChild(hitTarget, "RopeBreakParticles");
                if (oldBreak)
                    Object.DestroyImmediate(oldBreak.gameObject);

                var hintRoot = CreateChild(hitTarget, "CutHintRoot", CutVisualLocalPosition);
                var glow = CreateSprite(
                    hintRoot,
                    "SoftGlow",
                    glowSprite,
                    new Color(1f, 0.86f, 0.5f, 1f),
                    38,
                    Vector3.one * 1.15f);
                CreateSprite(
                    hintRoot,
                    "FrayedFibers",
                    frayedSprite,
                    Color.white,
                    40,
                    Vector3.one);

                var promptRoot = CreateChild(hintRoot, "AttackPrompt", new Vector3(0.55f, 0.03f, 0f));
                var icon = CreateSprite(
                    promptRoot,
                    "SwordIcon",
                    iconSprite,
                    new Color(1f, 0.92f, 0.7f, 1f),
                    42,
                    Vector3.one * 0.52f);
                var dust = CreateRopeDust(hintRoot, particleMaterial);
                var breakParticles = CreateBreakParticles(hitTarget, particleMaterial);

                var hint = root.GetComponent<PulleyRopeHintVisual>();
                if (!hint)
                    hint = root.AddComponent<PulleyRopeHintVisual>();
                var hintSerialized = new SerializedObject(hint);
                hintSerialized.FindProperty("setupVersion").intValue = CurrentSetupVersion;
                hintSerialized.FindProperty("controller").objectReferenceValue = controller;
                hintSerialized.FindProperty("cutPoint").objectReferenceValue = hintRoot;
                hintSerialized.FindProperty("hintRoot").objectReferenceValue = hintRoot.gameObject;
                hintSerialized.FindProperty("glowRenderer").objectReferenceValue = glow;
                hintSerialized.FindProperty("promptRoot").objectReferenceValue = promptRoot;
                var promptRenderers = hintSerialized.FindProperty("promptRenderers");
                promptRenderers.arraySize = 1;
                promptRenderers.GetArrayElementAtIndex(0).objectReferenceValue = icon;
                hintSerialized.FindProperty("ropeDust").objectReferenceValue = dust;
                hintSerialized.ApplyModifiedPropertiesWithoutUndo();

                var controllerSerialized = new SerializedObject(controller);
                var breakProperty = controllerSerialized.FindProperty("breakParticles");
                if (!breakProperty.objectReferenceValue)
                    breakProperty.objectReferenceValue = breakParticles;
                controllerSerialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                if (logResult)
                    Debug.Log("[PulleyLiftHintSetup] 已安装磨损绳索、距离提示、绳屑和断绳粒子。");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void ConfigureSprite(string path, float pixelsPerUnit)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        static void ConfigureRopeTexture(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                return;

            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        static Material GetOrCreateRopeMaterial(Texture texture)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(RopeMaterialPath);
            if (!material)
            {
                var shader = Shader.Find("Sprites/Default");
                material = new Material(shader) { name = "PulleyBraidedRopeLine" };
                AssetDatabase.CreateAsset(material, RopeMaterialPath);
            }

            material.mainTexture = texture;
            material.mainTextureScale = Vector2.one;
            material.color = Color.white;
            EditorUtility.SetDirty(material);
            return material;
        }

        static void ApplyRopeMaterial(GameObject root, Material material)
        {
            var renderers = root.GetComponentsInChildren<LineRenderer>(true);
            foreach (var rope in renderers)
            {
                rope.sharedMaterial = material;
                rope.textureMode = LineTextureMode.Tile;
                rope.widthMultiplier = 0.22f;
            }
        }

        static Material GetOrCreateParticleMaterial(Texture texture)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(FiberMaterialPath);
            if (!material)
            {
                var shader = Shader.Find("Sprites/Default");
                material = new Material(shader) { name = "PulleyRopeFiber" };
                AssetDatabase.CreateAsset(material, FiberMaterialPath);
            }

            material.mainTexture = texture;
            EditorUtility.SetDirty(material);
            return material;
        }

        static ParticleSystem CreateRopeDust(Transform parent, Material material)
        {
            var go = new GameObject("RopeDust", typeof(ParticleSystem));
            go.transform.SetParent(parent, false);
            var particles = go.GetComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.duration = 2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.07f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.72f, 0.49f, 0.2f, 0.75f),
                new Color(1f, 0.82f, 0.45f, 0.95f));
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 12;

            var emission = particles.emission;
            emission.rateOverTime = 2f;

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.22f, 0.35f, 0.01f);

            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);
            velocity.y = new ParticleSystem.MinMaxCurve(-0.3f, -0.16f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = 41;
            return particles;
        }

        static ParticleSystem CreateBreakParticles(Transform parent, Material material)
        {
            var go = new GameObject("RopeBreakParticles", typeof(ParticleSystem));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = CutVisualLocalPosition;
            var particles = go.GetComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 0.8f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.38f, 0.72f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.095f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.64f, 0.39f, 0.14f, 0.9f),
                new Color(1f, 0.8f, 0.38f, 1f));
            main.gravityModifier = 0.55f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 24;

            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, 14, 18)
            });

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.12f, 0.24f, 0.01f);

            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.85f, 0.85f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.15f, 0.75f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = 43;
            return particles;
        }

        static SpriteRenderer CreateSprite(
            Transform parent,
            string name,
            Sprite sprite,
            Color color,
            int sortingOrder,
            Vector3 scale)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localScale = scale;
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        static Transform CreateChild(Transform parent, string name, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go.transform;
        }

        static Transform FindChild(Transform root, string name)
        {
            if (root.name == name)
                return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var result = FindChild(root.GetChild(i), name);
                if (result)
                    return result;
            }
            return null;
        }

        static Transform FindDirectChild(Transform parent, string name)
        {
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == name)
                    return child;
            }
            return null;
        }
    }
}
