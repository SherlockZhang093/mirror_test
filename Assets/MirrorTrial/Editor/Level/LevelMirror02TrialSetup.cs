#if UNITY_EDITOR
using System.Linq;
using Cinemachine;
using MirrorTrial.Boss;
using MirrorTrial.Combat;
using MirrorTrial.Level;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MirrorTrial.Editor.Level
{
    /// <summary>
    /// Explicit, deterministic builder for the second mirror trial.
    /// It is intentionally menu-driven: importing or recompiling scripts must never rewrite a scene.
    /// </summary>
    public static class LevelMirror02TrialSetup
    {
        const string MirrorScenePath = "Assets/MirrorTrial/Scenes/Level_Mirror_02.unity";
        const string RealityScenePath = "Assets/MirrorTrial/Scenes/Level_Reality_02.unity";
        const string PlayerPrefabPath = "Assets/MirrorTrial/Prefabs/Characters/Player_MirrorTrial.prefab";
        const string BattleAreaPrefabPath = "Assets/MirrorTrial/Prefabs/Boss/MirrorArcherBossBattleArea.prefab";
        const string MirrorVisualPrefabPath = Level02JungleMirrorGateBuilder.PrefabPath;
        const string GateId = "MirrorGate_02";

        [MenuItem("Mirror Trial/Level/Build Level Mirror 02 Trial")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[LevelMirror02TrialSetup] 请退出播放模式后再生成场景。");
                return;
            }

            AssetDatabase.SaveAssets();
            BuildMirrorScene();
            ConnectRealityGate();
            EnsureBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[LevelMirror02TrialSetup] Level_Mirror_02、弓手 Boss 战斗区、现实第二关入口与 Build Settings 已完成。");
        }

        static void BuildMirrorScene()
        {
            var scene = OpenAdditive(MirrorScenePath, out var openedByBuilder);
            if (!scene.IsValid()) return;

            foreach (var root in scene.GetRootGameObjects())
                Object.DestroyImmediate(root);

            var mainCamera = CreateRoot(scene, "MainCamera");
            mainCamera.tag = "MainCamera";
            mainCamera.transform.position = new Vector3(0f, 0f, -10f);
            var camera = mainCamera.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.11f, 0.18f, 1f);
            mainCamera.AddComponent<CinemachineBrain>();
            mainCamera.AddComponent<AudioListener>();

            var managerObject = CreateRoot(scene, "LevelManager");
            var manager = managerObject.AddComponent<LevelManager>();
            var geometryRoot = CreateChild(managerObject.transform, "地形");
            var gameplayRoot = CreateChild(managerObject.transform, "玩法");
            var runtimeRoot = CreateChild(managerObject.transform, "运行时");
            var playerSpawn = CreateChild(managerObject.transform, "PlayerSpawn");
            playerSpawn.position = new Vector3(-7.2f, -3.05f, 0f);

            ConfigureLevelManager(manager, playerSpawn, geometryRoot, gameplayRoot, runtimeRoot);
            BuildArena(geometryRoot);

            var battleAreaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BattleAreaPrefabPath);
            if (!battleAreaPrefab)
            {
                Debug.LogError("[LevelMirror02TrialSetup] 缺少弓手 Boss 战斗区预制体：" + BattleAreaPrefabPath);
            }
            else
            {
                var battleAreaObject = PrefabUtility.InstantiatePrefab(battleAreaPrefab, scene) as GameObject;
                battleAreaObject.name = "MirrorArcherBossBattleArea";
                battleAreaObject.transform.SetParent(gameplayRoot, false);
                battleAreaObject.transform.localPosition = new Vector3(0f, -0.65f, 0f);

                var encounter = battleAreaObject.GetComponent<CombatEncounter>();
                ConfigureEncounter(encounter);

                var returnObject = new GameObject("MirrorReturnOnClear");
                SceneManager.MoveGameObjectToScene(returnObject, scene);
                returnObject.transform.SetParent(gameplayRoot, false);
                var returnOnClear = returnObject.AddComponent<MirrorReturnOnClear>();
                SetObjectReference(returnOnClear, "encounter", encounter);
            }

            var visualObject = CreateRoot(scene, "MirrorBattleVisuals_Level02");
            visualObject.transform.position = new Vector3(0f, 0f, 8f);
            visualObject.AddComponent<MirrorBattleVisuals>();

            manager.CollectAll();
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, MirrorScenePath);
            ValidateMirrorScene(scene);
            CloseIfNeeded(scene, openedByBuilder);
        }

        static void BuildArena(Transform geometryRoot)
        {
            var groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer < 0) groundLayer = 0;

            var collisionRoot = CreateChild(geometryRoot, "碰撞");
            CreateSolid(collisionRoot, "ArenaFloorCollision", new Vector2(0f, -4.25f), new Vector2(20.6f, 1f), groundLayer);
            CreateSolid(collisionRoot, "ArenaLeftBoundary", new Vector2(-10.55f, 0f), new Vector2(0.5f, 9.5f), groundLayer);
            CreateSolid(collisionRoot, "ArenaRightBoundary", new Vector2(10.55f, 0f), new Vector2(0.5f, 9.5f), groundLayer);

            var visualRoot = CreateChild(geometryRoot, "镜面平台");
            var centers = new[] { -8f, -4f, 0f, 4f, 8f };
            var sprites = new[] { 2, 4, 6, 8, 10 };
            for (var i = 0; i < centers.Length; i++)
            {
                var platform = new GameObject($"Platform_02_{i + 1:00}");
                platform.layer = groundLayer;
                platform.transform.SetParent(visualRoot, false);
                platform.transform.localPosition = new Vector3(centers[i], -4.25f, 0f);
                var sizingCollider = platform.AddComponent<BoxCollider2D>();
                sizingCollider.size = new Vector2(4.12f, 1f);
                sizingCollider.enabled = false;
                ApplyPlatformVisualWithoutUndo(platform, sprites[i]);
            }

            CreateMirrorPillar(visualRoot, "左侧镜柱", new Vector3(-9.8f, -1.2f, 0.2f), new Vector2(0.35f, 5.4f));
            CreateMirrorPillar(visualRoot, "右侧镜柱", new Vector3(9.8f, -1.2f, 0.2f), new Vector2(0.35f, 5.4f));
        }

        static void CreateMirrorPillar(Transform parent, string name, Vector3 position, Vector2 size)
        {
            var pillar = new GameObject(name);
            pillar.transform.SetParent(parent, false);
            pillar.transform.localPosition = position;
            var renderer = pillar.AddComponent<SpriteRenderer>();
            renderer.sprite = LevelPlatformVisualUtility.GetPlatformSprite(12);
            renderer.color = new Color(0.34f, 0.78f, 0.86f, 0.28f);
            renderer.sortingOrder = -5;
            if (renderer.sprite)
            {
                var spriteSize = renderer.sprite.bounds.size;
                pillar.transform.localScale = new Vector3(size.x / spriteSize.x, size.y / spriteSize.y, 1f);
            }
        }

        static void ApplyPlatformVisualWithoutUndo(GameObject platform, int spriteIndex)
        {
            var sprite = LevelPlatformVisualUtility.GetPlatformSprite(spriteIndex);
            if (!sprite) return;

            var visual = new GameObject("PlatformVisual");
            visual.transform.SetParent(platform.transform, false);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = Color.white;
            renderer.sortingOrder = 1;
            renderer.drawMode = SpriteDrawMode.Simple;

            var collider = platform.GetComponent<BoxCollider2D>();
            var size = collider ? collider.size : Vector2.one;
            var spriteSize = sprite.bounds.size;
            if (spriteSize.x <= 0f || spriteSize.y <= 0f) return;

            var scale = size.x / spriteSize.x;
            visual.transform.localScale = new Vector3(scale, scale, 1f);
            var visualHeight = spriteSize.y * scale;
            visual.transform.localPosition = new Vector3(0f, size.y * 0.5f - visualHeight * 0.5f, -0.01f);
        }

        static void ConfigureLevelManager(LevelManager manager, Transform playerSpawn, Transform geometryRoot,
            Transform gameplayRoot, Transform runtimeRoot)
        {
            var data = new SerializedObject(manager);
            data.FindProperty("levelId").stringValue = "Level_Mirror_02";
            data.FindProperty("levelDisplayName").stringValue = "第二关·镜中试炼";
            data.FindProperty("playerSpawn").objectReferenceValue = playerSpawn;
            data.FindProperty("playerPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            data.FindProperty("geometryRoot").objectReferenceValue = geometryRoot;
            data.FindProperty("gameplayRoot").objectReferenceValue = gameplayRoot;
            data.FindProperty("runtimeRoot").objectReferenceValue = runtimeRoot;
            data.FindProperty("AutoCollectOnAwake").boolValue = true;
            data.FindProperty("setupDefaultCameraFollow").boolValue = true;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ConfigureEncounter(CombatEncounter encounter)
        {
            if (!encounter) return;
            var data = new SerializedObject(encounter);
            data.FindProperty("encounterId").stringValue = "Mirror_02_ArcherBoss";
            data.FindProperty("startOnPlayerEnter").boolValue = false;
            data.FindProperty("clearCondition").enumValueIndex = (int)CombatClearCondition.AllEnemiesDefeated;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ConnectRealityGate()
        {
            var scene = OpenAdditive(RealityScenePath, out var openedByBuilder);
            if (!scene.IsValid()) return;

            var manager = FindInScene<LevelManager>(scene);
            if (!manager)
            {
                Debug.LogError("[LevelMirror02TrialSetup] Level_Reality_02 中没有 LevelManager，无法接入镜中试炼入口。");
                CloseIfNeeded(scene, openedByBuilder);
                return;
            }

            RemoveLegacyTempleArch(scene);

            var allMirrorGates = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MirrorGate>(true))
                .ToArray();
            var gate = allMirrorGates.FirstOrDefault(candidate => candidate.GateId == GateId || candidate.name == GateId);

            foreach (var duplicate in allMirrorGates.Where(candidate => candidate != gate && candidate.GateId == "MirrorGate_Archer"))
                Object.DestroyImmediate(duplicate.gameObject);

            if (!gate)
            {
                var gateObject = new GameObject(GateId);
                SceneManager.MoveGameObjectToScene(gateObject, scene);
                gateObject.transform.SetParent(manager.GameplayRoot, false);
                var collider = gateObject.AddComponent<BoxCollider2D>();
                collider.isTrigger = true;
                collider.size = new Vector2(1.2f, 2f);
                gateObject.AddComponent<Hurtbox>();
                gate = gateObject.AddComponent<MirrorGate>();
            }

            gate.name = GateId;
            gate.transform.position = FindRealityGatePosition(scene);
            var data = new SerializedObject(gate);
            data.FindProperty("gateId").stringValue = GateId;
            data.FindProperty("mirrorSceneName").stringValue = "Level_Mirror_02";
            data.FindProperty("requiredHits").intValue = 3;
            data.FindProperty("rewardAbility").enumValueIndex = (int)MirrorRewardAbility.None;
            data.FindProperty("nextSegment").objectReferenceValue = null;
            data.FindProperty("mirrorPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(MirrorVisualPrefabPath);
            data.FindProperty("enterMirrorDelay").floatValue = -1f;
            data.ApplyModifiedPropertiesWithoutUndo();

            manager.CollectAll();
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, RealityScenePath);
            Debug.Log($"[LevelMirror02TrialSetup] 现实入口位置：{gate.transform.position}", gate);
            CloseIfNeeded(scene, openedByBuilder);
        }

        static void RemoveLegacyTempleArch(Scene scene)
        {
            var legacyArches = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(candidate => candidate.name == "temple_arch_v1" || candidate.name == "神庙入口框" || candidate.name == "神庙入口拱")
                .Select(candidate => candidate.gameObject)
                .Distinct()
                .ToArray();
            foreach (var legacyArch in legacyArches)
                Object.DestroyImmediate(legacyArch);
        }

        static Vector3 FindRealityGatePosition(Scene scene)
        {
            var platformMarker = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(candidate => candidate.name == "09_Boss右侧台_表面_1");
            if (platformMarker)
            {
                var platformRenderer = platformMarker.GetComponent<SpriteRenderer>();
                var surfaceY = platformRenderer ? platformRenderer.bounds.max.y : platformMarker.position.y;
                return new Vector3(platformMarker.position.x, surfaceY + 1f, 0f);
            }

            const float targetX = 140.59f;
            Physics2D.SyncTransforms();
            var hit = Physics2D.RaycastAll(new Vector2(targetX, 8f), Vector2.down, 20f)
                .FirstOrDefault(candidate => candidate.collider &&
                    candidate.collider.gameObject.scene == scene && !candidate.collider.isTrigger);
            return hit.collider
                ? new Vector3(targetX, hit.point.y + 1f, 0f)
                : new Vector3(targetX, -1.55f, 0f);
        }

        static void ValidateMirrorScene(Scene scene)
        {
            var manager = FindInScene<LevelManager>(scene);
            var battleArea = FindInScene<MirrorArcherTwoStageCoordinator>(scene);
            var encounter = FindInScene<CombatEncounter>(scene);
            var returnOnClear = FindInScene<MirrorReturnOnClear>(scene);
            var floor = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<BoxCollider2D>(true))
                .FirstOrDefault(collider => collider.name == "ArenaFloorCollision" && collider.enabled && !collider.isTrigger);

            if (!manager || !battleArea || !encounter || !returnOnClear || returnOnClear.Encounter != encounter || !floor)
                Debug.LogError("[LevelMirror02TrialSetup] Level_Mirror_02 验证失败，请检查 LevelManager、战斗区、清场返回引用和连续地面。");
            else
                Debug.Log("[LevelMirror02TrialSetup] Level_Mirror_02 验证通过：出生点、连续地面、弓手 Boss、清场返回均已接通。");
        }

        static void EnsureBuildSettings()
        {
            var required = new[] { RealityScenePath, MirrorScenePath };
            var scenes = EditorBuildSettings.scenes.ToList();
            foreach (var path in required)
            {
                var index = scenes.FindIndex(scene => scene.path == path);
                if (index < 0)
                    scenes.Add(new EditorBuildSettingsScene(path, true));
                else if (!scenes[index].enabled)
                    scenes[index] = new EditorBuildSettingsScene(path, true);
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static Scene OpenAdditive(string path, out bool openedByBuilder)
        {
            var scene = SceneManager.GetSceneByPath(path);
            openedByBuilder = !scene.IsValid() || !scene.isLoaded;
            return openedByBuilder ? EditorSceneManager.OpenScene(path, OpenSceneMode.Additive) : scene;
        }

        static void CloseIfNeeded(Scene scene, bool openedByBuilder)
        {
            if (openedByBuilder && scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }

        static GameObject CreateRoot(Scene scene, string name)
        {
            var gameObject = new GameObject(name);
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            return gameObject;
        }

        static Transform CreateChild(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        static void CreateSolid(Transform parent, string name, Vector2 position, Vector2 size, int layer)
        {
            var solid = new GameObject(name);
            solid.layer = layer;
            solid.transform.SetParent(parent, false);
            solid.transform.localPosition = position;
            var collider = solid.AddComponent<BoxCollider2D>();
            collider.size = size;
        }

        static T FindInScene<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .FirstOrDefault();
        }

        static void SetObjectReference(Object target, string propertyName, Object value)
        {
            var data = new SerializedObject(target);
            data.FindProperty(propertyName).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
