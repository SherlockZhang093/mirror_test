#if UNITY_EDITOR
using System.Linq;
using MirrorTrial.Abilities;
using MirrorTrial.Combat;
using MirrorTrial.Level;
using UnityEditor;
using UnityEngine;

namespace MirrorTrial.Boss.Editor
{
    public static class MirrorArcherTwoStageSetup
    {
        const string ProfilePath = "Assets/MirrorTrial/Boss/MirrorArcherTwoStageProfile.asset";
        const string MountedPrefabPath = "Assets/MirrorTrial/Prefabs/Boss/MirrorArcherMountedBoss.prefab";
        const string GroundPrefabPath = "Assets/MirrorTrial/Prefabs/Boss/MirrorArcherBoss.prefab";
        const string BattleAreaPrefabPath = "Assets/MirrorTrial/Prefabs/Boss/MirrorArcherBossBattleArea.prefab";
        const string HudOuterFramePath = "Assets/MirrorTrial/Art/MinimalMirrorBossBar_AssetPack/Sprites/BossBar_OuterFrame.png";
        const string HudEmptyPath = "Assets/MirrorTrial/Art/MinimalMirrorBossBar_AssetPack/Sprites/BossBar_Fill_Empty.png";
        const string HudFillPath = "Assets/MirrorTrial/Art/MinimalMirrorBossBar_AssetPack/Sprites/BossBar_Fill_Normal.png";
        const string HudTickLeftPath = "Assets/MirrorTrial/Art/MinimalMirrorBossBar_AssetPack/Sprites/BossBar_NameTick_Left.png";
        const string HudTickRightPath = "Assets/MirrorTrial/Art/MinimalMirrorBossBar_AssetPack/Sprites/BossBar_NameTick_Right.png";
        const string SharedMeleeProfilePath = "Assets/MirrorTrial/Boss/MirrorBossSimpleProfile.asset";
        const string ArrowPrefabPath = "Assets/MirrorTrial/Prefabs/Boss/Temporary/MirrorArcherArrow_Temporary.prefab";
        const string RockPrefabPath = "Assets/MirrorTrial/Prefabs/Boss/Temporary/MirrorArcherRock_Temporary.prefab";
        const string WindupPrefabPath = "Assets/MirrorTrial/Prefabs/VFX/BlueChargeTelegraphVFX.prefab";
        const string ImpactPrefabPath = "Assets/MirrorTrial/Prefabs/VFX/EnemyLaunch/PhysicalV1/EnemyLaunch_LandingImpact_VFX.prefab";

        [InitializeOnLoadMethod]
        static void BuildMissingResourcesAfterCompile()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;
                var profile = AssetDatabase.LoadAssetAtPath<MirrorArcherTwoStageProfile>(ProfilePath);
                var mounted = AssetDatabase.LoadAssetAtPath<GameObject>(MountedPrefabPath);
                var area = AssetDatabase.LoadAssetAtPath<GameObject>(BattleAreaPrefabPath);
                if (!profile || !mounted || !area || !area.GetComponent<MirrorArcherTwoStageCoordinator>())
                    Build();
            };
        }

        [MenuItem("Mirror Trial/Boss/Build Mirror Archer Two Stage Fight")]
        public static void RebuildFromMenu()
        {
            Build();
            Debug.Log("[MirrorArcherTwoStageSetup] Two-stage profile, mounted boss, ground boss and battle area are ready.");
        }

        public static void Build()
        {
            EnsureFolder("Assets/MirrorTrial/Prefabs/Boss/Temporary");
            var arrow = BuildArrowPrefab();
            var rock = BuildRockPrefab();
            var profile = BuildProfile(arrow, rock);
            var mounted = BuildMountedPrefab(profile);
            var ground = ConfigureGroundPrefab(profile);
            profile.mountedBossPrefab = mounted;
            profile.groundBossPrefab = ground;
            EditorUtility.SetDirty(profile);
            BuildBattleArea(profile, mounted, ground);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateGeneratedAssets();
        }

        [MenuItem("Mirror Trial/Boss/Validate Mirror Archer Two Stage Fight")]
        public static void ValidateGeneratedAssets()
        {
            var profile = AssetDatabase.LoadAssetAtPath<MirrorArcherTwoStageProfile>(ProfilePath);
            var mounted = AssetDatabase.LoadAssetAtPath<GameObject>(MountedPrefabPath);
            var ground = AssetDatabase.LoadAssetAtPath<GameObject>(GroundPrefabPath);
            var area = AssetDatabase.LoadAssetAtPath<GameObject>(BattleAreaPrefabPath);
            Require(profile, "Missing two-stage profile");
            Require(mounted && mounted.GetComponent<MirrorArcherMountedBoss>(), "Mounted prefab or actor missing");
            Require(ground && ground.GetComponent<MirrorBossActorV2>() &&
                    ground.GetComponent<MirrorArcherGroundPhaseController>(), "Ground prefab/controller missing");
            Require(area && area.GetComponent<MirrorArcherTwoStageCoordinator>(), "Battle area coordinator missing");
            Require(area && area.GetComponentInChildren<MirrorArcherRockfallArea>(true), "Rockfall area/config missing");
            Require(area && area.GetComponent<MirrorArcherTwoStageHud>(), "Two-stage boss health HUD missing");
            Require(!area.GetComponent<MirrorBossBattleAreaV3>(), "Legacy V3 battle area is still present");
            Require(profile.airSkills != null && profile.airSkills.Count >= 5, "Air skill rotation is incomplete");
            Require(profile.groundSkills != null && profile.groundSkills.Count >= 4, "Ground skill set is incomplete");
            Require(profile.sharedGroundMeleeProfile &&
                    profile.sharedGroundMeleeProfile == AssetDatabase.LoadAssetAtPath<MirrorBossSimpleProfile>(SharedMeleeProfilePath),
                "Ground melee data is not referencing the first boss profile");
            foreach (var type in new[] { MirrorArcherSkillType.LockedShot, MirrorArcherSkillType.FanShot,
                         MirrorArcherSkillType.GroundArrowRain, MirrorArcherSkillType.SkyRockfall })
                Require(profile.Find(type) != null && profile.Find(type).projectilePrefab,
                    "Runtime projectile prefab missing for " + type);
            foreach (var socket in new[] { MirrorArcherMountedBoss.VisualRootName, MirrorArcherMountedBoss.MountRootName,
                         MirrorArcherMountedBoss.RiderSocketName, MirrorArcherMountedBoss.ProjectileSocketName,
                         MirrorArcherMountedBoss.ImpactSocketName })
                Require(mounted.GetComponentsInChildren<Transform>(true).Any(item => item.name == socket),
                    "Mounted art contract socket missing: " + socket);
            foreach (var path in new[] { ProfilePath, MountedPrefabPath, GroundPrefabPath, BattleAreaPrefabPath,
                         ArrowPrefabPath, RockPrefabPath })
                Require(!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)), "Asset GUID missing: " + path);
            Debug.Log("[MirrorArcherTwoStageSetup] Validation passed: two phases, 5 air skills, shared ground melee, " +
                      "rockfall safety zone, transition sockets and all prefab GUIDs are valid.");
        }

        static void Require(Object value, string message)
        {
            if (!value) throw new System.InvalidOperationException("[MirrorArcherTwoStageSetup] " + message);
        }

        static void Require(bool value, string message)
        {
            if (!value) throw new System.InvalidOperationException("[MirrorArcherTwoStageSetup] " + message);
        }

        static MirrorArcherTwoStageProfile BuildProfile(GameObject arrowPrefab, GameObject rockPrefab)
        {
            var profile = AssetDatabase.LoadAssetAtPath<MirrorArcherTwoStageProfile>(ProfilePath);
            if (!profile)
            {
                profile = ScriptableObject.CreateInstance<MirrorArcherTwoStageProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            profile.sharedGroundMeleeProfile = AssetDatabase.LoadAssetAtPath<MirrorBossSimpleProfile>(SharedMeleeProfilePath);
            profile.EnsureDefaults();
            var windup = AssetDatabase.LoadAssetAtPath<GameObject>(WindupPrefabPath);
            var impact = AssetDatabase.LoadAssetAtPath<GameObject>(ImpactPrefabPath);
            profile.crashImpactPrefab = impact;

            foreach (var skill in profile.airSkills)
            {
                if (skill == null) continue;
                if (skill.type == MirrorArcherSkillType.LockedShot || skill.type == MirrorArcherSkillType.FanShot ||
                    skill.type == MirrorArcherSkillType.GroundArrowRain)
                    skill.projectilePrefab = arrowPrefab;
                if (!skill.windupEffectPrefab) skill.windupEffectPrefab = windup;
                if (!skill.impactPrefab) skill.impactPrefab = impact;
            }
            foreach (var skill in profile.groundSkills)
            {
                if (skill == null) continue;
                if (skill.type == MirrorArcherSkillType.SkyRockfall)
                {
                    skill.projectilePrefab = rockPrefab;
                    skill.impactPrefab = impact;
                    if (!skill.windupEffectPrefab) skill.windupEffectPrefab = windup;
                }
            }
            EditorUtility.SetDirty(profile);
            return profile;
        }

        static GameObject BuildMountedPrefab(MirrorArcherTwoStageProfile profile)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(MountedPrefabPath);
            var root = existing
                ? PrefabUtility.LoadPrefabContents(MountedPrefabPath)
                : new GameObject("MirrorArcherMountedBoss", typeof(Rigidbody2D), typeof(CapsuleCollider2D),
                    typeof(Hurtbox), typeof(MirrorArcherMountedBoss));
            try
            {
                root.name = "MirrorArcherMountedBoss";
                var body = root.GetComponent<Rigidbody2D>() ?? root.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Kinematic;
                body.gravityScale = 0f;
                body.freezeRotation = true;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                var collider = root.GetComponent<CapsuleCollider2D>() ?? root.AddComponent<CapsuleCollider2D>();
                collider.size = new Vector2(2.25f, 1.25f);
                collider.direction = CapsuleDirection2D.Horizontal;
                if (!root.GetComponent<Hurtbox>()) root.AddComponent<Hurtbox>();

                var visualRoot = EnsureChild(root.transform, MirrorArcherMountedBoss.VisualRootName);
                if (!visualRoot.GetComponent<MirrorArcherTemporaryVisual>()) visualRoot.gameObject.AddComponent<MirrorArcherTemporaryVisual>();
                var mountRoot = EnsureChild(visualRoot, MirrorArcherMountedBoss.MountRootName);
                var rider = EnsureChild(mountRoot, MirrorArcherMountedBoss.RiderSocketName);
                rider.localPosition = new Vector3(0.1f, 0.65f, 0f);
                var projectile = EnsureChild(rider, MirrorArcherMountedBoss.ProjectileSocketName);
                projectile.localPosition = new Vector3(0.65f, 0.3f, 0f);
                var impact = EnsureChild(mountRoot, MirrorArcherMountedBoss.ImpactSocketName);
                impact.localPosition = new Vector3(0f, -0.55f, 0f);

                var actor = root.GetComponent<MirrorArcherMountedBoss>() ?? root.AddComponent<MirrorArcherMountedBoss>();
                var actorData = new SerializedObject(actor);
                actorData.FindProperty("profile").objectReferenceValue = profile;
                actorData.FindProperty("visualRoot").objectReferenceValue = visualRoot;
                actorData.FindProperty("projectileSocket").objectReferenceValue = projectile;
                actorData.FindProperty("impactSocket").objectReferenceValue = impact;
                actorData.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, MountedPrefabPath);
            }
            finally
            {
                if (existing) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
            }
            return AssetDatabase.LoadAssetAtPath<GameObject>(MountedPrefabPath);
        }

        static GameObject ConfigureGroundPrefab(MirrorArcherTwoStageProfile profile)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GroundPrefabPath);
            if (!prefab)
            {
                Debug.LogError("[MirrorArcherTwoStageSetup] Ground prefab missing: " + GroundPrefabPath);
                return null;
            }
            var root = PrefabUtility.LoadPrefabContents(GroundPrefabPath);
            try
            {
                var actor = root.GetComponent<MirrorBossActorV2>();
                if (!actor) actor = root.AddComponent<MirrorBossActorV2>();
                var actorData = new SerializedObject(actor);
                actorData.FindProperty("profile").objectReferenceValue = profile.sharedGroundMeleeProfile;
                actorData.FindProperty("behaviourTreeControlled").boolValue = true;
                actorData.ApplyModifiedPropertiesWithoutUndo();
                var controller = root.GetComponent<MirrorArcherGroundPhaseController>() ?? root.AddComponent<MirrorArcherGroundPhaseController>();
                var controllerData = new SerializedObject(controller);
                controllerData.FindProperty("profile").objectReferenceValue = profile;
                controllerData.FindProperty("actor").objectReferenceValue = actor;
                controllerData.ApplyModifiedPropertiesWithoutUndo();
                foreach (var component in root.GetComponents<MonoBehaviour>())
                {
                    if (!component) continue;
                    var typeName = component.GetType().Name;
                    if (typeName == "BehaviourTreeOwner" || typeName == "MirrorBossBlackboardSync" ||
                        typeName == "MirrorBossFSMSynchronizer")
                        component.enabled = false;
                }
                PrefabUtility.SaveAsPrefabAsset(root, GroundPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            return AssetDatabase.LoadAssetAtPath<GameObject>(GroundPrefabPath);
        }

        static void BuildBattleArea(MirrorArcherTwoStageProfile profile, GameObject mounted, GameObject ground)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(BattleAreaPrefabPath);
            GameObject root;
            var loaded = existing;
            if (existing)
                root = PrefabUtility.LoadPrefabContents(BattleAreaPrefabPath);
            else
            {
                root = new GameObject("MirrorArcherBossBattleArea");
                var collider = root.AddComponent<BoxCollider2D>();
                collider.isTrigger = true;
                collider.size = new Vector2(18f, 7f);
                root.AddComponent<CombatEncounter>();
            }
            try
            {
                foreach (var legacy in root.GetComponents<MirrorBossBattleAreaV3>()) Object.DestroyImmediate(legacy);
                foreach (var legacy in root.GetComponents<MirrorBossBattleAreaV2>()) Object.DestroyImmediate(legacy);
                foreach (var legacy in root.GetComponents<MirrorBossBattleArea>()) Object.DestroyImmediate(legacy);
                var encounter = root.GetComponent<CombatEncounter>() ?? root.AddComponent<CombatEncounter>();
                var air = EnsureSpawnPoint(root.transform, "AirSpawnPoint", new Vector3(4.5f, 2.6f, 0f));
                var groundPoint = EnsureSpawnPoint(root.transform, "GroundSpawnPoint", new Vector3(4.5f, -2.7f, 0f));
                var rockfallRoot = EnsureChild(root.transform, "RockfallArea");
                rockfallRoot.localPosition = Vector3.zero;
                var rockfallCollider = rockfallRoot.GetComponent<BoxCollider2D>() ??
                                      rockfallRoot.gameObject.AddComponent<BoxCollider2D>();
                rockfallCollider.isTrigger = true;
                rockfallCollider.size = new Vector2(18f, 11f);
                var rockfallArea = rockfallRoot.GetComponent<MirrorArcherRockfallArea>() ??
                                   rockfallRoot.gameObject.AddComponent<MirrorArcherRockfallArea>();
                var oldSpawn = root.transform.Find("BossSpawnPoint");
                if (oldSpawn && oldSpawn != air.transform && oldSpawn != groundPoint.transform)
                    Object.DestroyImmediate(oldSpawn.gameObject);
                var coordinator = root.GetComponent<MirrorArcherTwoStageCoordinator>() ?? root.AddComponent<MirrorArcherTwoStageCoordinator>();
                var data = new SerializedObject(coordinator);
                data.FindProperty("profile").objectReferenceValue = profile;
                data.FindProperty("mountedBossPrefab").objectReferenceValue = mounted ? mounted.GetComponent<MirrorArcherMountedBoss>() : null;
                data.FindProperty("groundBossPrefab").objectReferenceValue = ground ? ground.GetComponent<MirrorBossActorV2>() : null;
                data.FindProperty("airSpawnPoint").objectReferenceValue = air;
                data.FindProperty("groundSpawnPoint").objectReferenceValue = groundPoint;
                data.FindProperty("rockfallArea").objectReferenceValue = rockfallArea;
                data.FindProperty("encounter").objectReferenceValue = encounter;
                data.ApplyModifiedPropertiesWithoutUndo();
                var hud = root.GetComponent<MirrorArcherTwoStageHud>() ?? root.AddComponent<MirrorArcherTwoStageHud>();
                var hudData = new SerializedObject(hud);
                hudData.FindProperty("coordinator").objectReferenceValue = coordinator;
                hudData.FindProperty("outerFrameSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(HudOuterFramePath);
                hudData.FindProperty("emptySprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(HudEmptyPath);
                hudData.FindProperty("fillSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(HudFillPath);
                hudData.FindProperty("nameTickLeftSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(HudTickLeftPath);
                hudData.FindProperty("nameTickRightSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(HudTickRightPath);
                hudData.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, BattleAreaPrefabPath);
            }
            finally
            {
                if (loaded) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
            }
        }

        static GameObject BuildArrowPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(ArrowPrefabPath);
            if (existing) return existing;
            var root = new GameObject("MirrorArcherArrow_Temporary");
            var collider = root.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(0.5f, 0.08f);
            var body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            var line = root.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.SetPosition(0, new Vector3(-0.25f, 0f));
            line.SetPosition(1, new Vector3(0.25f, 0f));
            line.startWidth = 0.055f;
            line.endWidth = 0.025f;
            line.startColor = new Color(1f, 0.72f, 0.18f, 1f);
            line.endColor = Color.white;
            line.material = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Line.mat");
            root.AddComponent<BowArrowProjectile>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, ArrowPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static GameObject BuildRockPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(RockPrefabPath);
            if (existing) return existing;
            var root = new GameObject("MirrorArcherRock_Temporary");
            root.AddComponent<MirrorBossRockProjectile>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, RockPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static MirrorBossSpawnPoint EnsureSpawnPoint(Transform parent, string name, Vector3 localPosition)
        {
            var child = parent.Find(name);
            if (!child)
            {
                child = new GameObject(name).transform;
                child.SetParent(parent, false);
            }
            child.localPosition = localPosition;
            return child.GetComponent<MirrorBossSpawnPoint>() ?? child.gameObject.AddComponent<MirrorBossSpawnPoint>();
        }

        static Transform EnsureChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child) return child;
            child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
