using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using MirrorTrial.Level;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MirrorTrial.Editor.Level
{
    public static class LevelConfigImporter
    {
        public const string SceneFolder = "Assets/MirrorTrial/Scenes";
        const string DefaultPlayerPrefabPath = "Assets/MirrorTrial/Prefabs/Characters/Player_MirrorTrial.prefab";
        const string MirrorGatePrefabPath = "Assets/MirrorTrial/Prefabs/Level/MirrorGate.prefab";

        public static void GenerateScene(LevelConfig config, string scenePath = null)
        {
            if (!config) { Debug.LogError("[Mirror Trial] LevelConfig is null; scene generation cancelled."); return; }

            if (string.IsNullOrEmpty(scenePath))
                scenePath = string.Format("{0}/{1}.unity", SceneFolder, config.levelId);

            Scene scene;
            if (!AssetDatabase.IsValidFolder(SceneFolder))
                AssetDatabase.CreateFolder("Assets/MirrorTrial", "Scenes");
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var mainCamera = EnsureMainCamera();

            var manager = Object.FindObjectOfType<LevelManager>();
            if (!manager)
            {
                var go = new GameObject("LevelManager");
                manager = go.AddComponent<LevelManager>();
            }

            var geometryRoot = GetOrCreateChild(manager.transform, "\u5730\u5f62");
            var gameplayRoot = GetOrCreateChild(manager.transform, "\u73a9\u6cd5");
            var runtimeRoot = GetOrCreateChild(manager.transform, "\u8fd0\u884c\u65f6");

            var mso = new SerializedObject(manager);
            mso.FindProperty("levelId").stringValue = config.levelId;
            mso.FindProperty("levelDisplayName").stringValue = config.displayName;
            mso.FindProperty("playerPrefab").objectReferenceValue = ResolvePlayerPrefab(config);
            mso.FindProperty("geometryRoot").objectReferenceValue = geometryRoot;
            mso.FindProperty("gameplayRoot").objectReferenceValue = gameplayRoot;
            mso.FindProperty("runtimeRoot").objectReferenceValue = runtimeRoot;
            mso.ApplyModifiedProperties();

            var segments = new Dictionary<string, LevelSegment>();
            var encounters = new Dictionary<string, CombatEncounter>();
            var gates = new Dictionary<string, AreaGate>();
            var mirrorGates = new Dictionary<string, MirrorGate>();
            var spawnPoints = new Dictionary<string, SpawnPoint>();

            BuildGeometry(geometryRoot, config);
            foreach (var e in config.segments) { var s = CreateSegment(manager, e); if (s) segments[e.segmentId] = s; }
            foreach (var e in config.spawnPoints) { var s = CreateSpawnPoint(manager, e); if (s) spawnPoints[e.spawnId] = s; }
            foreach (var e in config.encounters) { var c = CreateEncounter(manager, e, spawnPoints); if (c) encounters[e.encounterId] = c; }
            foreach (var e in config.gates) { var g = CreateGate(manager, e); if (g) gates[e.gateId] = g; }
            ResolveEncounterGateReferences(config, encounters, gates);
            foreach (var e in config.mirrorGates) { var mg = CreateMirrorGate(manager, e, segments); if (mg) mirrorGates[e.gateId] = mg; }
            foreach (var e in config.triggers) CreateTrigger(manager, e, segments, encounters, gates, mirrorGates);

            var ps = new GameObject("PlayerSpawn");
            ps.transform.SetParent(manager.transform);
            ps.transform.position = config.playerSpawn;
            var mso2 = new SerializedObject(manager);
            mso2.FindProperty("playerSpawn").objectReferenceValue = ps.transform;
            mso2.ApplyModifiedProperties();


            if (config.isMirrorLevel)
            {
                var ret = new GameObject("MirrorReturnOnClear");
                ret.transform.SetParent(gameplayRoot);
                var hook = ret.AddComponent<MirrorReturnOnClear>();
                BindMirrorReturnEncounter(hook, encounters.Values);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(manager);
            Debug.Log("[闀滃儚璇曠偧] 宸茬敓鎴愬満鏅細" + scenePath);
        }

        static void BindMirrorReturnEncounter(MirrorReturnOnClear hook, IEnumerable<CombatEncounter> sceneEncounters)
        {
            if (!hook || sceneEncounters == null) return;

            var candidates = sceneEncounters.Where(e => e).ToList();
            if (candidates.Count == 0) return;

            CombatEncounter target = null;
            if (candidates.Count == 1)
                target = candidates[0];
            else
                target = candidates.FirstOrDefault(e => e.HasWaves) ?? candidates[0];

            var so = new SerializedObject(hook);
            so.FindProperty("encounter").objectReferenceValue = target;
            so.ApplyModifiedProperties();
        }
        static Transform GetOrCreateChild(Transform parent, string name)
        {
            var existing = parent.Find(name);
            if (existing) return existing;
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.localPosition = Vector3.zero;
            return go.transform;
        }

        static void BuildGeometry(Transform root, LevelConfig config)
        {
            foreach (var g in config.geometry)
            {
                var go = new GameObject(string.IsNullOrEmpty(g.name) ? "Geo" : g.name);
                go.transform.SetParent(root);
                go.transform.position = g.position;
                var col = go.AddComponent<BoxCollider2D>();
                col.size = g.size;
                col.isTrigger = false;
                var sr = go.AddComponent<SpriteRenderer>();
                sr.color = g.color;
                sr.sortingOrder = 0;
                if (g.isPlatform && g.platformVisualIndex > 0)
                    LevelPlatformVisualUtility.ApplyPlatformVisual(go, g.platformVisualIndex);
            }
        }

        static LevelSegment CreateSegment(LevelManager m, SegmentEntry e)
        {
            var parent = m.GetCategoryRoot("\u6bb5\u843d");
            var go = new GameObject(string.IsNullOrEmpty(e.segmentId) ? "SEG" : e.segmentId);
            go.transform.SetParent(parent);
            go.transform.position = e.position;
            var seg = go.AddComponent<LevelSegment>();
            var col = go.AddComponent<BoxCollider2D>();
            col.size = e.size;
            col.isTrigger = true;
            var so = new SerializedObject(seg);
            so.FindProperty("segmentId").stringValue = e.segmentId;
            so.FindProperty("displayName").stringValue = e.displayName;
            so.FindProperty("startEnabled").boolValue = e.startEnabled;
            so.FindProperty("boundsCollider").objectReferenceValue = col;
            so.ApplyModifiedProperties();
            return seg;
        }

        static SpawnPoint CreateSpawnPoint(LevelManager m, SpawnPointEntry e)
        {
            var parent = m.GetCategoryRoot("\u5237\u602a\u70b9");
            var go = new GameObject(string.IsNullOrEmpty(e.spawnId) ? "SP" : e.spawnId);
            go.transform.SetParent(parent);
            go.transform.position = e.position;
            var sp = go.AddComponent<SpawnPoint>();
            var so = new SerializedObject(sp);
            so.FindProperty("spawnId").stringValue = e.spawnId;
            so.FindProperty("role").intValue = (int)e.role;
            so.FindProperty("defaultEnemyPrefab").objectReferenceValue = e.enemyPrefab;
            so.FindProperty("aiProfile").objectReferenceValue = e.aiProfile;
            so.FindProperty("overrideHitPoints").intValue = e.overrideHitPoints;
            so.FindProperty("overrideMoveSpeed").floatValue = e.overrideMoveSpeed;
            so.FindProperty("facingDegrees").floatValue = e.facingDegrees;
            so.ApplyModifiedProperties();
            return sp;
        }

        static CombatEncounter CreateEncounter(LevelManager m, EncounterEntry e, Dictionary<string, SpawnPoint> spawnPoints)
        {
            var parent = m.GetCategoryRoot("\u6218\u6597\u533a");
            var go = new GameObject(string.IsNullOrEmpty(e.encounterId) ? "Combat" : e.encounterId);
            go.transform.SetParent(parent);
            go.transform.position = e.position;
            var enc = go.AddComponent<CombatEncounter>();
            var col = go.AddComponent<BoxCollider2D>();
            col.size = e.size;
            col.isTrigger = true;
            var so = new SerializedObject(enc);
            so.FindProperty("encounterId").stringValue = e.encounterId;
            so.FindProperty("startOnPlayerEnter").boolValue = e.startOnPlayerEnter;
            so.FindProperty("clearCondition").intValue = (int)e.clearCondition;

            var wavesProp = so.FindProperty("waves");
            wavesProp.arraySize = e.waves.Count;
            for (var i = 0; i < e.waves.Count; i++)
            {
                var wp = wavesProp.GetArrayElementAtIndex(i);
                var w = e.waves[i];
                wp.FindPropertyRelative("waveId").stringValue = w.waveId;
                wp.FindPropertyRelative("clearCondition").intValue = (int)w.clearCondition;
                wp.FindPropertyRelative("nextWaveDelay").floatValue = w.nextWaveDelay;
                var sp2 = wp.FindPropertyRelative("spawnEntries");
                sp2.arraySize = w.spawnEntries.Count;
                for (var j = 0; j < w.spawnEntries.Count; j++)
                {
                    var sep = sp2.GetArrayElementAtIndex(j);
                    var se = w.spawnEntries[j];
                    sep.FindPropertyRelative("spawnPoint").objectReferenceValue = spawnPoints.TryGetValue(se.spawnPointId, out var sp) ? sp : null;
                    sep.FindPropertyRelative("count").intValue = se.count;
                    sep.FindPropertyRelative("delay").floatValue = se.delay;
                    sep.FindPropertyRelative("interval").floatValue = se.interval;
                    sep.FindPropertyRelative("enemyPrefab").objectReferenceValue = se.enemyPrefab;
                }
            }
            so.ApplyModifiedProperties();
            return enc;
        }

        static void ResolveEncounterGateReferences(LevelConfig config, Dictionary<string, CombatEncounter> encounters, Dictionary<string, AreaGate> gates)
        {
            foreach (var e in config.encounters)
            {
                if (!encounters.TryGetValue(e.encounterId, out var encounter) || !encounter)
                    continue;

                var so = new SerializedObject(encounter);
                var lockProp = so.FindProperty("lockGates");
                lockProp.arraySize = e.lockGateIds.Count;
                for (var i = 0; i < e.lockGateIds.Count; i++)
                    lockProp.GetArrayElementAtIndex(i).objectReferenceValue = gates.TryGetValue(e.lockGateIds[i], out var gate) ? gate : null;
                so.ApplyModifiedProperties();
            }
        }

        static AreaGate CreateGate(LevelManager m, GateEntry e)
        {
            var parent = m.GetCategoryRoot("\u95e8");
            var go = new GameObject(string.IsNullOrEmpty(e.gateId) ? "Gate" : e.gateId);
            go.transform.SetParent(parent);
            go.transform.position = e.position;
            var gate = go.AddComponent<AreaGate>();
            var col = go.AddComponent<BoxCollider2D>();
            var so = new SerializedObject(gate);
            so.FindProperty("gateId").stringValue = e.gateId;
            so.FindProperty("initialOpen").boolValue = e.initialOpen;
            so.FindProperty("gateCollider").objectReferenceValue = col;
            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform);
            var vsr = visual.AddComponent<SpriteRenderer>();
            vsr.color = e.initialOpen ? new Color(0.2f, 1f, 0.3f, 0.85f) : new Color(1f, 0.25f, 0.25f, 0.9f);
            so.FindProperty("visualObject").objectReferenceValue = visual;
            so.ApplyModifiedProperties();
            return gate;
        }

        static MirrorGate CreateMirrorGate(LevelManager m, MirrorGateEntry e, Dictionary<string, LevelSegment> segments)
        {
            var mirrorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MirrorGatePrefabPath);
            if (!mirrorPrefab)
            {
                Debug.LogError($"[Mirror Trial] Missing mirror prefab: {MirrorGatePrefabPath}");
                return null;
            }

            var parent = m.GetCategoryRoot("镜子门");
            var go = new GameObject(string.IsNullOrEmpty(e.gateId) ? "MirrorGate" : e.gateId);
            go.transform.SetParent(parent);
            go.transform.position = e.position;
            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1.7f, 2f);
            collider.isTrigger = true;
            var mg = go.AddComponent<MirrorGate>();


            var so = new SerializedObject(mg);
            so.FindProperty("gateId").stringValue = e.gateId;
            so.FindProperty("mirrorSceneName").stringValue = e.mirrorSceneName;
            so.FindProperty("requiredHits").intValue = e.hitPoints;
            so.FindProperty("rewardAbility").intValue = (int)e.rewardAbility;
            so.FindProperty("mirrorPrefab").objectReferenceValue = mirrorPrefab;
            segments.TryGetValue(e.nextSegmentId, out var nextSegment);
            so.FindProperty("nextSegment").objectReferenceValue = nextSegment;
            so.ApplyModifiedProperties();
            return mg;
        }

        static void CreateTrigger(LevelManager m, TriggerEntry e, Dictionary<string, LevelSegment> segments, Dictionary<string, CombatEncounter> encounters, Dictionary<string, AreaGate> gates, Dictionary<string, MirrorGate> mirrorGates)
        {
            var parent = m.GetCategoryRoot("\u89e6\u53d1\u5668");
            var go = new GameObject(string.IsNullOrEmpty(e.triggerId) ? "TR" : e.triggerId);
            go.transform.SetParent(parent);
            go.transform.position = e.position;
            var tr = go.AddComponent<LevelTrigger>();
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            if (e.shape == LevelTriggerShape.Box) col.size = e.boxSize;
            var so = new SerializedObject(tr);
            so.FindProperty("triggerId").stringValue = e.triggerId;
            so.FindProperty("enabledAtStart").boolValue = e.enabledAtStart;
            so.FindProperty("oneShot").boolValue = e.oneShot;
            so.FindProperty("when").intValue = (int)e.when;
            so.FindProperty("shape").intValue = (int)e.shape;
            so.FindProperty("boxSize").vector2Value = e.boxSize;
            so.FindProperty("circleRadius").floatValue = e.circleRadius;
            so.FindProperty("gizmoColor").colorValue = e.gizmoColor;

            var condProp = so.FindProperty("conditions");
            condProp.arraySize = e.conditions.Count;
            for (var i = 0; i < e.conditions.Count; i++)
            {
                var cp = condProp.GetArrayElementAtIndex(i);
                var c = e.conditions[i];
                cp.FindPropertyRelative("type").intValue = (int)c.type;
                cp.FindPropertyRelative("requiredAbility").intValue = (int)c.requiredAbility;
                cp.FindPropertyRelative("invert").boolValue = c.invert;
                var target = ResolveConditionTarget(c.type, c.targetId, segments, encounters, mirrorGates);
                cp.FindPropertyRelative("segment").objectReferenceValue = c.type == LevelConditionType.SegmentNotCompleted ? target : null;
                cp.FindPropertyRelative("encounter").objectReferenceValue = (c.type == LevelConditionType.EncounterNotStarted || c.type == LevelConditionType.EncounterCleared) ? target : null;
                cp.FindPropertyRelative("mirrorGate").objectReferenceValue = (c.type == LevelConditionType.MirrorGateIsSmashed || c.type == LevelConditionType.MirrorGateIsIntact) ? target : null;
            }

            var actProp = so.FindProperty("actions");
            actProp.arraySize = e.actions.Count;
            for (var i = 0; i < e.actions.Count; i++)
            {
                var ap = actProp.GetArrayElementAtIndex(i);
                var a = e.actions[i];
                ap.FindPropertyRelative("type").intValue = (int)a.type;
                ap.FindPropertyRelative("waveIndex").intValue = a.waveIndex;
                ap.FindPropertyRelative("ability").intValue = (int)a.ability;
                ap.FindPropertyRelative("feedbackMessage").stringValue = a.feedbackMessage;
                ap.FindPropertyRelative("delay").floatValue = a.delay;
                var tgt = ResolveActionTarget(a.type, a.targetId, encounters, gates, mirrorGates, segments);
                ap.FindPropertyRelative("encounter").objectReferenceValue = (a.type == LevelActionType.StartEncounter || a.type == LevelActionType.StartWave) ? tgt : null;
                ap.FindPropertyRelative("gate").objectReferenceValue = (a.type == LevelActionType.LockGate || a.type == LevelActionType.OpenGate) ? tgt : null;
                ap.FindPropertyRelative("mirrorGate").objectReferenceValue = a.type == LevelActionType.SmashMirrorGate ? tgt : null;
                ap.FindPropertyRelative("segment").objectReferenceValue = a.type == LevelActionType.EnableSegment ? tgt : null;
                if (a.type == LevelActionType.TeleportPlayer)
                {
                    var tp = new GameObject("TeleportTarget_" + e.triggerId + "_" + i);
                    tp.transform.SetParent(go.transform);
                    tp.transform.position = a.targetPosition;
                    ap.FindPropertyRelative("teleportTarget").objectReferenceValue = tp.transform;
                }
            }
            so.ApplyModifiedProperties();
        }

        static Object ResolveConditionTarget(LevelConditionType type, string id, Dictionary<string, LevelSegment> segments, Dictionary<string, CombatEncounter> encounters, Dictionary<string, MirrorGate> mirrorGates)
        {
            if (string.IsNullOrEmpty(id)) return null;
            switch (type)
            {
                case LevelConditionType.SegmentNotCompleted: return segments.TryGetValue(id, out var s) ? s : null;
                case LevelConditionType.EncounterNotStarted:
                case LevelConditionType.EncounterCleared: return encounters.TryGetValue(id, out var c) ? c : null;
                case LevelConditionType.MirrorGateIsSmashed:
                case LevelConditionType.MirrorGateIsIntact: return mirrorGates.TryGetValue(id, out var mg) ? mg : null;
            }
            return null;
        }

        static Object ResolveActionTarget(LevelActionType type, string id, Dictionary<string, CombatEncounter> encounters, Dictionary<string, AreaGate> gates, Dictionary<string, MirrorGate> mirrorGates, Dictionary<string, LevelSegment> segments)
        {
            if (string.IsNullOrEmpty(id)) return null;
            switch (type)
            {
                case LevelActionType.StartEncounter:
                case LevelActionType.StartWave: return encounters.TryGetValue(id, out var c) ? c : null;
                case LevelActionType.LockGate:
                case LevelActionType.OpenGate: return gates.TryGetValue(id, out var g) ? g : null;
                case LevelActionType.SmashMirrorGate: return mirrorGates.TryGetValue(id, out var mg) ? mg : null;
                case LevelActionType.EnableSegment: return segments.TryGetValue(id, out var s) ? s : null;
            }
            return null;
        }

        static GameObject ResolvePlayerPrefab(LevelConfig config)
        {
            if (config.isMirrorLevel)
                return null;

            return config.playerPrefab ? config.playerPrefab : AssetDatabase.LoadAssetAtPath<GameObject>(DefaultPlayerPrefabPath);
        }

        static Camera EnsureMainCamera()
        {
            var cam = Camera.main;
            if (!cam)
            {
                var go = new GameObject("MainCamera");
                cam = go.AddComponent<Camera>();
                go.tag = "MainCamera";
                cam.orthographic = true;
            }
            var pos = cam.transform.position;
            cam.transform.position = new Vector3(pos.x, pos.y, -10f);
            if (!cam.GetComponent<CinemachineBrain>())
                cam.gameObject.AddComponent<CinemachineBrain>();
            return cam;
        }
    }
}
