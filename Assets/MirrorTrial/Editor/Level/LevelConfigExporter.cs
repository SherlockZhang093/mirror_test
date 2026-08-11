using System.Collections.Generic;
using System.Linq;
using MirrorTrial.Enemies;
using MirrorTrial.Level;
using UnityEditor;
using UnityEngine;

namespace MirrorTrial.Editor.Level
{
    public static class LevelConfigExporter
    {
        public const string DefaultConfigFolder = "Assets/MirrorTrial/LevelConfigs";

        public static LevelConfig ExportCurrentScene(string assetPath = null)
        {
            var manager = Object.FindObjectOfType<LevelManager>();
            if (!manager)
            {
                EditorUtility.DisplayDialog("导出关卡配置", "当前场景找不到 LevelManager。", "确定");
                return null;
            }

            return Export(manager, assetPath);
        }

        public static LevelConfig Export(LevelManager manager, string assetPath = null)
        {
            if (!manager)
            {
                Debug.LogError("[镜像试炼] 导出失败：LevelManager 为空。");
                return null;
            }

            manager.CollectAll();

            if (string.IsNullOrEmpty(assetPath))
            {
                var folder = DefaultConfigFolder;
                if (!AssetDatabase.IsValidFolder(folder))
                    AssetDatabase.CreateFolder("Assets/MirrorTrial", "LevelConfigs");
                assetPath = $"{folder}/{manager.LevelId}.asset";
            }

            var config = AssetDatabase.LoadAssetAtPath<LevelConfig>(assetPath);
            if (!config)
            {
                config = ScriptableObject.CreateInstance<LevelConfig>();
                AssetDatabase.CreateAsset(config, assetPath);
            }

            FillConfig(manager, config);

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(config);

            Debug.Log($"[镜像试炼] 已导出关卡配置：{assetPath}");
            return config;
        }

        static void FillConfig(LevelManager manager, LevelConfig config)
        {
            config.levelId = manager.LevelId;
            config.displayName = manager.LevelDisplayName;
            config.playerSpawn = manager.PlayerSpawn ? (Vector2)manager.PlayerSpawn.position : Vector2.zero;
            config.playerPrefab = manager.PlayerPrefab;
            config.playerHealthHudPrefab = manager.HealthHudPrefab;

            config.geometry.Clear();
            config.segments.Clear();
            config.spawnPoints.Clear();
            config.encounters.Clear();
            config.gates.Clear();
            config.mirrorGates.Clear();
            config.triggers.Clear();

            ExportGeometry(manager, config);
            ExportSegments(manager, config);
            ExportSpawnPoints(manager, config);
            ExportEncounters(manager, config);
            ExportGates(manager, config);
            ExportMirrorGates(manager, config);
            ExportTriggers(manager, config);

            config.isMirrorLevel = config.mirrorGates.Count == 0 && Object.FindObjectOfType<MirrorReturnOnClear>() != null;
        }

        static void ExportGeometry(LevelManager manager, LevelConfig config)
        {
            if (!manager.GeometryRoot) return;
            foreach (Transform child in manager.GeometryRoot)
            {
                var renderer = child.GetComponent<SpriteRenderer>();
                var collider = child.GetComponent<BoxCollider2D>();
                if (!renderer && !collider) continue;

                var size = collider ? collider.size : Vector2.one;
                var entry = new GeometryEntry
                {
                    name = child.name,
                    position = child.position,
                    size = size,
                    isBoundary = child.name.Contains("Boundary"),
                    isPlatform = child.name.StartsWith("Platform_") || child.Find("PlatformVisual") != null,
                    geometryType = ResolveGeometryType(child),
                    platformVisualIndex = ExtractPlatformVisualIndex(child),
                    color = renderer ? renderer.color : Color.white
                };
                config.geometry.Add(entry);
            }
        }

        static GeometryType ResolveGeometryType(Transform geometry)
        {
            if (geometry.GetComponent<ClimbableWall>()) return GeometryType.ClimbableWall;
            if (geometry.name.Contains("Boundary")) return GeometryType.Boundary;
            if (geometry.name.StartsWith("Platform_") || geometry.Find("PlatformVisual")) return GeometryType.Platform;
            return GeometryType.SolidBlock;
        }

        static int ExtractPlatformVisualIndex(Transform platform)
        {
            var visual = platform.Find("PlatformVisual");
            if (!visual) return 0;
            var renderer = visual.GetComponent<SpriteRenderer>();
            if (!renderer || !renderer.sprite) return 0;
            var path = AssetDatabase.GetAssetPath(renderer.sprite);
            var fileName = System.IO.Path.GetFileNameWithoutExtension(path);
            if (fileName.StartsWith("pingtai_") && int.TryParse(fileName.Substring("pingtai_".Length), out var index))
                return index;
            return 0;
        }

        static void ExportSegments(LevelManager manager, LevelConfig config)
        {
            foreach (var segment in manager.Segments)
            {
                if (!segment) continue;
                var col = segment.BoundsCollider;
                var size = col ? col.size : Vector2.one;

                var so = new SerializedObject(segment);
                config.segments.Add(new SegmentEntry
                {
                    segmentId = segment.SegmentId,
                    displayName = segment.DisplayName,
                    position = segment.transform.position,
                    size = size,
                    startEnabled = GetBool(so, "startEnabled")
                });
            }
        }

        static void ExportSpawnPoints(LevelManager manager, LevelConfig config)
        {
            foreach (var point in manager.SpawnPoints)
            {
                if (!point) continue;
                var so = new SerializedObject(point);
                config.spawnPoints.Add(new SpawnPointEntry
                {
                    spawnId = point.SpawnId,
                    position = point.transform.position,
                    role = point.Role,
                    enemyPrefab = point.DefaultEnemyPrefab,
                    aiProfile = point.AIProfile,
                    overrideHitPoints = point.OverrideHitPoints,
                    overrideMoveSpeed = point.OverrideMoveSpeed,
                    facingDegrees = GetFloat(so, "facingDegrees")
                });
            }
        }

        static void ExportEncounters(LevelManager manager, LevelConfig config)
        {
            foreach (var encounter in manager.Encounters)
            {
                if (!encounter) continue;
                var so = new SerializedObject(encounter);
                var col = encounter.GetComponent<BoxCollider2D>();
                var size = col ? col.size : Vector2.one;

                var entry = new EncounterEntry
                {
                    encounterId = encounter.EncounterId,
                    position = encounter.transform.position,
                    size = size,
                    startOnPlayerEnter = GetBool(so, "startOnPlayerEnter"),
                    clearCondition = encounter.ClearCondition,
                    lockGateIds = encounter.LockGates?.Where(g => g).Select(g => g.GateId).ToList() ?? new List<string>(),
                    waves = new List<WaveEntry>()
                };

                var wavesProp = so.FindProperty("waves");
                if (wavesProp != null)
                {
                    for (var i = 0; i < wavesProp.arraySize; i++)
                    {
                        entry.waves.Add(ExportWave(wavesProp.GetArrayElementAtIndex(i)));
                    }
                }

                config.encounters.Add(entry);
            }
        }

        static WaveEntry ExportWave(SerializedProperty waveProp)
        {
            var entry = new WaveEntry
            {
                waveId = waveProp.FindPropertyRelative("waveId")?.stringValue ?? "Wave_01",
                clearCondition = (CombatClearCondition)(waveProp.FindPropertyRelative("clearCondition")?.intValue ?? 0),
                nextWaveDelay = waveProp.FindPropertyRelative("nextWaveDelay")?.floatValue ?? 0.8f,
                spawnEntries = new List<WaveSpawnEntryData>()
            };

            var entriesProp = waveProp.FindPropertyRelative("spawnEntries");
            if (entriesProp != null)
            {
                for (var i = 0; i < entriesProp.arraySize; i++)
                {
                    var entryProp = entriesProp.GetArrayElementAtIndex(i);
                    var spawnPoint = entryProp.FindPropertyRelative("spawnPoint")?.objectReferenceValue as SpawnPoint;
                    entry.spawnEntries.Add(new WaveSpawnEntryData
                    {
                        spawnPointId = spawnPoint ? spawnPoint.SpawnId : string.Empty,
                        count = entryProp.FindPropertyRelative("count")?.intValue ?? 1,
                        delay = entryProp.FindPropertyRelative("delay")?.floatValue ?? 0f,
                        interval = entryProp.FindPropertyRelative("interval")?.floatValue ?? 0f,
                        enemyPrefab = entryProp.FindPropertyRelative("enemyPrefab")?.objectReferenceValue as GameObject
                    });
                }
            }

            return entry;
        }

        static void ExportGates(LevelManager manager, LevelConfig config)
        {
            foreach (var gate in manager.Gates)
            {
                if (!gate) continue;
                var so = new SerializedObject(gate);
                config.gates.Add(new GateEntry
                {
                    gateId = gate.GateId,
                    position = gate.transform.position,
                    initialOpen = GetBool(so, "initialOpen")
                });
            }
        }

        static void ExportMirrorGates(LevelManager manager, LevelConfig config)
        {
            foreach (var gate in manager.MirrorGates)
            {
                if (!gate) continue;
                var so = new SerializedObject(gate);
                var nextSegment = GetObject<LevelSegment>(so, "nextSegment");
                var visual = gate.MirrorPrefab;
                var visualIndex = 0;
                if (visual) visualIndex = ExtractPlatformVisualIndex(visual.transform);

                config.mirrorGates.Add(new MirrorGateEntry
                {
                    gateId = gate.GateId,
                    position = gate.transform.position,
                    mirrorSceneName = gate.MirrorSceneName,
                    hitPoints = gate.HitPoints,
                    returnPointPosition = gate.transform.position,
                    rewardAbility = gate.RewardAbility,
                    nextSegmentId = nextSegment ? nextSegment.SegmentId : string.Empty,
                    platformVisualIndex = visualIndex,
                    color = visual && visual.GetComponentInChildren<SpriteRenderer>() ? visual.GetComponentInChildren<SpriteRenderer>().color : new Color(0.6f, 0.85f, 1f, 0.75f)
                });
            }
        }

        static void ExportTriggers(LevelManager manager, LevelConfig config)
        {
            foreach (var trigger in manager.Triggers)
            {
                if (!trigger) continue;
                var so = new SerializedObject(trigger);
                var col = trigger.GetComponent<BoxCollider2D>();
                var boxSize = col ? col.size : Vector2.one;

                var entry = new TriggerEntry
                {
                    triggerId = trigger.TriggerId,
                    position = trigger.transform.position,
                    when = trigger.When,
                    shape = (LevelTriggerShape)(so.FindProperty("shape")?.intValue ?? 0),
                    boxSize = boxSize,
                    circleRadius = GetFloat(so, "circleRadius"),
                    enabledAtStart = GetBool(so, "enabledAtStart"),
                    oneShot = GetBool(so, "oneShot"),
                    gizmoColor = GetColor(so, "gizmoColor"),
                    conditions = ExportConditions(so.FindProperty("conditions")),
                    actions = ExportActions(so.FindProperty("actions"))
                };
                config.triggers.Add(entry);
            }
        }

        static List<ConditionEntry> ExportConditions(SerializedProperty conditionsProp)
        {
            var result = new List<ConditionEntry>();
            if (conditionsProp == null) return result;
            for (var i = 0; i < conditionsProp.arraySize; i++)
            {
                var prop = conditionsProp.GetArrayElementAtIndex(i);
                var type = (LevelConditionType)(prop.FindPropertyRelative("type")?.intValue ?? 0);
                var targetId = string.Empty;
                switch (type)
                {
                    case LevelConditionType.SegmentNotCompleted:
                        targetId = GetObjectId<LevelSegment>(prop, "segment", s => s.SegmentId);
                        break;
                    case LevelConditionType.EncounterNotStarted:
                    case LevelConditionType.EncounterCleared:
                        targetId = GetObjectId<CombatEncounter>(prop, "encounter", e => e.EncounterId);
                        break;
                    case LevelConditionType.MirrorGateIsSmashed:
                    case LevelConditionType.MirrorGateIsIntact:
                        targetId = GetObjectId<MirrorGate>(prop, "mirrorGate", g => g.GateId);
                        break;
                }

                result.Add(new ConditionEntry
                {
                    type = type,
                    targetId = targetId,
                    requiredAbility = (MirrorRewardAbility)(prop.FindPropertyRelative("requiredAbility")?.intValue ?? 0),
                    invert = prop.FindPropertyRelative("invert")?.boolValue ?? false
                });
            }
            return result;
        }

        static List<ActionEntry> ExportActions(SerializedProperty actionsProp)
        {
            var result = new List<ActionEntry>();
            if (actionsProp == null) return result;
            for (var i = 0; i < actionsProp.arraySize; i++)
            {
                var prop = actionsProp.GetArrayElementAtIndex(i);
                var type = (LevelActionType)(prop.FindPropertyRelative("type")?.intValue ?? 0);
                var targetId = string.Empty;
                Vector2 targetPos = Vector2.zero;

                switch (type)
                {
                    case LevelActionType.StartEncounter:
                    case LevelActionType.StartWave:
                        targetId = GetObjectId<CombatEncounter>(prop, "encounter", e => e.EncounterId);
                        break;
                    case LevelActionType.LockGate:
                    case LevelActionType.OpenGate:
                        targetId = GetObjectId<AreaGate>(prop, "gate", g => g.GateId);
                        break;
                    case LevelActionType.SmashMirrorGate:
                        targetId = GetObjectId<MirrorGate>(prop, "mirrorGate", g => g.GateId);
                        break;
                    case LevelActionType.EnableSegment:
                        targetId = GetObjectId<LevelSegment>(prop, "segment", s => s.SegmentId);
                        break;
                    case LevelActionType.TeleportPlayer:
                        var target = prop.FindPropertyRelative("teleportTarget")?.objectReferenceValue as Transform;
                        if (target)
                        {
                            targetId = target.name;
                            targetPos = target.position;
                        }
                        break;
                }

                result.Add(new ActionEntry
                {
                    type = type,
                    targetId = targetId,
                    targetPosition = targetPos,
                    waveIndex = prop.FindPropertyRelative("waveIndex")?.intValue ?? 0,
                    ability = (MirrorRewardAbility)(prop.FindPropertyRelative("ability")?.intValue ?? 0),
                    feedbackMessage = prop.FindPropertyRelative("feedbackMessage")?.stringValue ?? string.Empty,
                    delay = prop.FindPropertyRelative("delay")?.floatValue ?? 0f
                });
            }
            return result;
        }

        static string GetObjectId<T>(SerializedProperty prop, string fieldName, System.Func<T, string> getId) where T : Object
        {
            var obj = prop.FindPropertyRelative(fieldName)?.objectReferenceValue as T;
            return obj ? getId(obj) : string.Empty;
        }

        static T GetObject<T>(SerializedObject so, string name) where T : Object
        {
            return so.FindProperty(name)?.objectReferenceValue as T;
        }

        static bool GetBool(SerializedObject so, string name)
        {
            return so.FindProperty(name)?.boolValue ?? false;
        }

        static float GetFloat(SerializedObject so, string name)
        {
            return so.FindProperty(name)?.floatValue ?? 0f;
        }

        static Color GetColor(SerializedObject so, string name)
        {
            var prop = so.FindProperty(name);
            return prop != null ? prop.colorValue : Color.white;
        }
    }
}
