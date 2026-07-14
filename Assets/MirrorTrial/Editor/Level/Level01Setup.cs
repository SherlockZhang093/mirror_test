using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MirrorTrial.Combat;
using MirrorTrial.Editor;
using MirrorTrial.Level;
using MirrorTrial.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MirrorTrial.Editor.Level
{
    static class TransformComponentExtensions
    {
        public static T AddComponent<T>(this Transform transform) where T : Component
        {
            return transform.gameObject.AddComponent<T>();
        }
    }

    public static class Level01Setup
    {
        const string ScenePath = "Assets/MirrorTrial/Scenes/level_01.unity";
        const string PlayerPrefabPath = "Assets/MirrorTrial/Prefabs/Characters/Player_MirrorTrial.prefab";
        const string EnemyPrefabPath = "Assets/MirrorTrial/Prefabs/Characters/TrainingEnemy.prefab";
        const string WhiteSquarePath = "Assets/MirrorTrial/Sprites/WhiteSquare.png";

        [MenuItem("Tools/镜像试炼/关卡/生成第 1 关")]
        public static void Generate()
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(WhiteSquarePath);
            if (!playerPrefab || !sprite)
            {
                Debug.LogError("生成第 1 关需要 Player_MirrorTrial 和 WhiteSquare 资源。");
                return;
            }

            var enemyPrefab = CreateTrainingEnemyPrefab(sprite);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            scene.name = "level_01";
            RemoveDirectionalLight();

            var root = new GameObject("Level_Reality_01");
            var geometry = CreateChild(root.transform, "Geometry");
            BuildGeometry(geometry, sprite);

            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            player.name = "Player_MirrorTrial";
            player.transform.position = new Vector3(-12f, -0.95f, 0f);

            var bounds = CameraSetupUtil.ComputeSceneBounds(geometry);
            CameraSetupUtil.ApplyDefault(Camera.main, player.transform, boundsMinX: bounds.minX, boundsMaxX: bounds.maxX);

            var manager = root.AddComponent<LevelManager>();
            var gameplay = CreateChild(root.transform, "Gameplay");
            var runtime = CreateChild(root.transform, "Runtime");

            EnableTestGymAbilities(player);

            var segments = CreateChild(gameplay, "Segments");
            var combatRoot = CreateChild(gameplay, "Combat");
            var gateRoot = CreateChild(gameplay, "Gates");
            var mirrorRoot = CreateChild(gameplay, "MirrorGates");
            var spawnRoot = CreateChild(gameplay, "SpawnPoints");
            var triggerRoot = CreateChild(gameplay, "Triggers");

            var intro = CreateSegment(segments, "SEG_A_Intro", new Vector2(-9f, 0f), new Vector2(8f, 5f), true);
            var combatSegment = CreateSegment(segments, "SEG_B_Combat", new Vector2(0f, 0f), new Vector2(9f, 5f), true);
            var mirrorSegment = CreateSegment(segments, "SEG_C_Mirror", new Vector2(10f, 0f), new Vector2(4f, 5f), true);
            var exitSegment = CreateSegment(segments, "SEG_D_BladeRoute", new Vector2(17f, 0f), new Vector2(6f, 5f), false);

            var combatExitGate = CreateGate(gateRoot, sprite, "Gate_CombatExit", new Vector2(4.5f, 0f), true);
            var exitGate = CreateGate(gateRoot, sprite, "Gate_BladeExit", new Vector2(14.5f, 0f), false);

            var meleeSpawn = CreateSpawnPoint(spawnRoot, "SP_B01_Melee", new Vector2(1f, -1f), enemyPrefab, 180f);
            var flankSpawn = CreateSpawnPoint(spawnRoot, "SP_B02_Flank", new Vector2(3f, -1f), enemyPrefab, 180f);
            var bossSpawn = CreateSpawnPoint(spawnRoot, "SP_C01_Boss", new Vector2(11.5f, -1f), enemyPrefab, 180f);

            var combat = CreateEncounter(combatRoot, "Combat_B_01", new Vector2(-0.5f, 0f), new Vector2(8f, 4f),
                new[] { combatExitGate },
                new[]
                {
                    Wave("W01_Approach", (meleeSpawn, 2, 0f, 0.45f)),
                    Wave("W02_Crossfire", (meleeSpawn, 1, 0f, 0f), (flankSpawn, 2, 0.7f, 0.4f))
                });

            var boss = CreateEncounter(combatRoot, "Boss_Blade", new Vector2(11f, 0f), new Vector2(3.5f, 4f),
                Array.Empty<AreaGate>(),
                new[] { Wave("W01_MirrorSelf", (bossSpawn, 3, 0f, 0.5f)) });

            var bossEntry = CreateMarker(runtime, "BossEntry", new Vector2(10f, -0.95f));
            var returnPoint = CreateMarker(runtime, "MirrorReturn", new Vector2(6f, -0.95f));
            var mirrorGate = CreateMirrorGate(mirrorRoot, "MirrorGate_Blade", new Vector2(6.5f, -0.5f), bossEntry, returnPoint, boss, exitSegment);

            CreateEncounterClearTrigger(triggerRoot, "TR_CombatClear_ActivateMirror", combat, mirrorGate);
            CreateMirrorBrokenTrigger(triggerRoot, "TR_MirrorBroken_OpenExit", mirrorGate, exitGate);
            ConfigureManager(manager, player.transform, geometry, gameplay, runtime);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Selection.activeGameObject = root;
            Debug.Log("[镜像试炼] 第 1 关已生成：引导 -> 两波战斗 -> 镜门 -> Boss 波次 -> 刃路线。");
        }

        static GameObject CreateTrainingEnemyPrefab(Sprite sprite)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
            if (existing) return existing;

            var go = new GameObject("TrainingEnemy");
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(0.9f, 0.25f, 0.3f);
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.size = new Vector2(0.7f, 1.2f);
            var collider = go.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(0.8f, 1.3f);
            go.AddComponent<Hurtbox>();
            go.AddComponent<TrainingEnemy>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, EnemyPrefabPath);
            UnityEngine.Object.DestroyImmediate(go);
            return prefab;
        }

        static void BuildGeometry(Transform root, Sprite sprite)
        {
            CreateGround(root, sprite, "Ground_Main", new Vector2(2f, -2f), new Vector2(34f, 1f));
            CreateGround(root, sprite, "Platform_Combat", new Vector2(0f, 0.4f), new Vector2(3f, 0.4f));
            CreateGround(root, sprite, "Platform_Mirror", new Vector2(9.5f, 1.3f), new Vector2(2.5f, 0.4f));
            CreateGround(root, sprite, "Platform_BladeRoute", new Vector2(17f, 0.7f), new Vector2(4f, 0.4f));
            CreateGround(root, sprite, "Boundary_Left", new Vector2(-15f, 2f), new Vector2(0.5f, 8f));
            CreateGround(root, sprite, "Boundary_Right", new Vector2(20f, 2f), new Vector2(0.5f, 8f));
        }

        static void CreateGround(Transform parent, Sprite sprite, string name, Vector2 position, Vector2 size)
        {
            var go = CreateChild(parent, name);
            go.transform.position = position;
            var groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer >= 0) go.gameObject.layer = groundLayer;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = new Color(0.24f, 0.29f, 0.36f);
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.size = size;
            go.AddComponent<BoxCollider2D>().size = size;
        }

        static void RemoveDirectionalLight()
        {
            var light = GameObject.Find("Directional Light");
            if (light) UnityEngine.Object.DestroyImmediate(light);
        }

        static LevelSegment CreateSegment(Transform parent, string id, Vector2 center, Vector2 size, bool startEnabled)
        {
            var go = CreateChild(parent, id);
            go.transform.position = center;
            var collider = go.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = size;
            var segment = go.AddComponent<LevelSegment>();
            SetPrivate(segment, "segmentId", id);
            SetPrivate(segment, "displayName", id);
            SetPrivate(segment, "boundsCollider", collider);
            SetPrivate(segment, "startEnabled", startEnabled);
            return segment;
        }

        static SpawnPoint CreateSpawnPoint(Transform parent, string id, Vector2 position, GameObject prefab, float facing)
        {
            var go = CreateChild(parent, id);
            go.transform.position = position;
            var point = go.AddComponent<SpawnPoint>();
            SetPrivate(point, "spawnId", id);
            SetPrivate(point, "defaultEnemyPrefab", prefab);
            SetPrivate(point, "facingDegrees", facing);
            return point;
        }

        static AreaGate CreateGate(Transform parent, Sprite sprite, string id, Vector2 position, bool initialOpen)
        {
            var go = CreateChild(parent, id);
            go.transform.position = position;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = initialOpen ? new Color(0.2f, 1f, 0.3f, 0.85f) : new Color(1f, 0.25f, 0.25f, 0.9f);
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.size = new Vector2(0.25f, 2.4f);
            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.25f, 2.4f);
            var gate = go.AddComponent<AreaGate>();
            SetPrivate(gate, "gateId", id);
            SetPrivate(gate, "initialOpen", initialOpen);
            SetPrivate(gate, "gateCollider", collider);
            SetPrivate(gate, "visualObject", go.gameObject);
            return gate;
        }

        static CombatEncounter CreateEncounter(Transform parent, string id, Vector2 position, Vector2 size, AreaGate[] gates, WaveDefinition[] waves)
        {
            var go = CreateChild(parent, id);
            go.transform.position = position;
            var collider = go.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = size;
            var encounter = go.AddComponent<CombatEncounter>();
            SetPrivate(encounter, "encounterId", id);
            SetPrivate(encounter, "lockGates", gates);
            SetPrivate(encounter, "waves", waves);
            return encounter;
        }

        static WaveDefinition Wave(string id, params (SpawnPoint point, int count, float delay, float interval)[] entries)
        {
            return new WaveDefinition
            {
                waveId = id,
                clearCondition = CombatClearCondition.AllEnemiesDefeated,
                nextWaveDelay = 0.8f,
                spawnEntries = entries.Select(entry => new WaveSpawnEntry
                {
                    spawnPoint = entry.point,
                    count = entry.count,
                    delay = entry.delay,
                    interval = entry.interval
                }).ToArray()
            };
        }

        static MirrorGate CreateMirrorGate(Transform parent, string id, Vector2 position, Transform bossEntry, Transform returnPoint, CombatEncounter boss, LevelSegment nextSegment)
        {
            var go = CreateChild(parent, id);
            go.transform.position = position;
            var collider = go.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(1f, 2f);
            var gate = go.AddComponent<MirrorGate>();
            SetPrivate(gate, "gateId", id);
            SetPrivate(gate, "targetEncounter", boss.EncounterId);
            SetPrivate(gate, "encounterEntryPoint", bossEntry);
            SetPrivate(gate, "returnPoint", returnPoint);
            SetPrivate(gate, "rewardAbility", MirrorRewardAbility.MirrorBlade);
            SetPrivate(gate, "nextSegment", nextSegment);
            SetPrivate(gate, "bossEncounter", boss);
            return gate;
        }

        static void CreateEncounterClearTrigger(Transform parent, string id, CombatEncounter encounter, MirrorGate gate)
        {
            var trigger = CreateTrigger(parent, id, LevelTriggerWhen.OnEncounterClear);
            SetPrivate(trigger, "encounterTarget", encounter);
            SetPrivate(trigger, "actions", new[] { new LevelAction { type = LevelActionType.ActivateMirrorGate, mirrorGate = gate } });
        }

        static void CreateMirrorBrokenTrigger(Transform parent, string id, MirrorGate mirrorGate, AreaGate exitGate)
        {
            var trigger = CreateTrigger(parent, id, LevelTriggerWhen.OnMirrorBroken);
            SetPrivate(trigger, "mirrorGateTarget", mirrorGate);
            SetPrivate(trigger, "actions", new[] { new LevelAction { type = LevelActionType.OpenGate, gate = exitGate } });
        }

        static LevelTrigger CreateTrigger(Transform parent, string id, LevelTriggerWhen when)
        {
            var go = CreateChild(parent, id);
            var collider = go.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(1f, 1f);
            var trigger = go.AddComponent<LevelTrigger>();
            SetPrivate(trigger, "triggerId", id);
            SetPrivate(trigger, "when", when);
            return trigger;
        }

        static Transform CreateMarker(Transform parent, string name, Vector2 position)
        {
            var marker = CreateChild(parent, name);
            marker.transform.position = position;
            return marker.transform;
        }

        static Transform CreateChild(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            return go.transform;
        }

        static void ConfigureManager(LevelManager manager, Transform player, Transform geometry, Transform gameplay, Transform runtime)
        {
            SetPrivate(manager, "levelId", "Level_Reality_01");
            SetPrivate(manager, "levelDisplayName", "Reality Trial");
            SetPrivate(manager, "playerSpawn", player);
            SetPrivate(manager, "geometryRoot", geometry);
            SetPrivate(manager, "gameplayRoot", gameplay);
            SetPrivate(manager, "runtimeRoot", runtime);
            manager.CollectAll();
        }

        static void EnableTestGymAbilities(GameObject player)
        {
            var tuning = player.GetComponent<PlayerTuning>();
            if (!tuning) return;
            tuning.abilities.mirrorBladeUnlocked = true;
            tuning.abilities.echoDashUnlocked = true;
        }

        static void AddSceneToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.path != ScenePath).ToList();
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void SetPrivate(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new MissingFieldException(target.GetType().Name, fieldName);
            field.SetValue(target, value);
        }
    }
}
