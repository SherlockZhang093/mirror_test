using MirrorTrial.Abilities;
using MirrorTrial.Combat;
using MirrorTrial.Player;
using Platformer.Mechanics;
using UnityEditor;
using UnityEngine;

namespace MirrorTrial.Editor
{
    public static class PlayerFrameworkSetup
    {
        const string CharacterPrefabFolder = "Assets/MirrorTrial/Prefabs/Characters";
        const string AbilityPrefabFolder = "Assets/MirrorTrial/Prefabs/Abilities";
        const string PlayerPrefabPath = CharacterPrefabFolder + "/Player_MirrorTrial.prefab";
        const string MirrorBladePrefabPath = AbilityPrefabFolder + "/MirrorBladeProjectile.prefab";
        const string AnimatorPath = "Assets/DeadRevolver/PixelPrototypePlayerSprites/Art/Animations/Animators/PlayerAnimator.controller";
        const string IdleSpritePath = "Assets/DeadRevolver/PixelPrototypePlayerSprites/Art/Sprites/Idle/Idle01.png";
        const string MirrorBladeSpritePath = "Assets/DeadRevolver/PixelPrototypePlayerSprites/Art/Sprites/Combat/AirSlash/AirSlash01.png";

        [MenuItem("Tools/Mirror Trial/Create Player Test Prefab")]
        public static void CreatePlayerTestPrefab()
        {
            EnsureProjectFolders();
            var mirrorBladePrefab = CreateMirrorBladeProjectilePrefab();
            var playerPrefab = CreatePlayerPrefab(mirrorBladePrefab);

            Selection.activeObject = playerPrefab;
            EditorGUIUtility.PingObject(playerPrefab);
            Debug.Log("Created Mirror Trial player test prefab: " + PlayerPrefabPath);
        }

        [MenuItem("Tools/Mirror Trial/Fix Existing Player Prefab (add StateMachine)")]
        public static void FixExistingPlayerPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (!prefab)
            {
                Debug.LogError("找不到 Player prefab：" + PlayerPrefabPath + "，请先 Create Player Test Prefab。");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            bool changed = false;

            if (!root.GetComponent<PlayerStateMachine>())
            {
                root.AddComponent<PlayerStateMachine>();
                changed = true;
                Debug.Log("已为 Player prefab 补上 PlayerStateMachine 组件。");
            }
            else
            {
                Debug.Log("Player prefab 已有 PlayerStateMachine，无需修复。");
            }

            if (changed)
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.Refresh();
        }

        static GameObject CreatePlayerPrefab(MirrorBladeProjectile mirrorBladePrefab)
        {
            var player = new GameObject("Player_MirrorTrial");
            ApplyTagAndLayer(player);

            var spriteRenderer = player.AddComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = 5;
            spriteRenderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IdleSpritePath);

            var animator = player.AddComponent<Animator>();
            animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimatorPath);

            var body = player.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.useFullKinematicContacts = false;
            body.interpolation = RigidbodyInterpolation2D.None;
            body.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
            body.freezeRotation = true;

            var collider = player.AddComponent<BoxCollider2D>();
            collider.offset = new Vector2(0f, -0.10f);
            collider.size = new Vector2(0.45f, 0.90f);

            var health = player.AddComponent<Health>();
            health.maxHP = 100;

            player.AddComponent<AudioSource>();
            player.AddComponent<PlayerInputReader>();
            var tuning = player.AddComponent<PlayerTuning>();
            tuning.abilities.mirrorBladeUnlocked = true;
            tuning.abilities.echoDashUnlocked = true;

            player.AddComponent<PlayerStateMachine>();
            player.AddComponent<PlayerMotor>();
            player.AddComponent<PlayerAnimationDriver>();
            var combat = player.AddComponent<PlayerCombat>();
            var loadout = player.AddComponent<PlayerAbilityLoadout>();
            player.AddComponent<Hurtbox>();
            player.AddComponent<PlayerDamageReceiver>();

            var attackHitbox = CreateAttackHitbox(player.transform);
            AssignObject(combat, "attackHitbox", attackHitbox);
            AssignObject(loadout, "mirrorBladeProjectilePrefab", mirrorBladePrefab);

            var prefab = PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);
            Object.DestroyImmediate(player);
            return prefab;
        }

        static Hitbox CreateAttackHitbox(Transform parent)
        {
            var attackHitbox = new GameObject("AttackHitbox");
            attackHitbox.transform.SetParent(parent);
            attackHitbox.transform.localPosition = new Vector3(0.55f, 0f, 0f);
            attackHitbox.transform.localRotation = Quaternion.identity;
            attackHitbox.transform.localScale = Vector3.one;

            var attackCollider = attackHitbox.AddComponent<BoxCollider2D>();
            attackCollider.isTrigger = true;
            attackCollider.enabled = false;
            attackCollider.size = new Vector2(0.65f, 0.55f);

            return attackHitbox.AddComponent<Hitbox>();
        }

        static MirrorBladeProjectile CreateMirrorBladeProjectilePrefab()
        {
            var projectile = new GameObject("MirrorBladeProjectile");

            var spriteRenderer = projectile.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(MirrorBladeSpritePath);
            spriteRenderer.sortingOrder = 6;

            var collider = projectile.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(0.7f, 0.35f);

            var blade = projectile.AddComponent<MirrorBladeProjectile>();
            var prefabObject = PrefabUtility.SaveAsPrefabAsset(projectile, MirrorBladePrefabPath);
            Object.DestroyImmediate(projectile);
            return prefabObject.GetComponent<MirrorBladeProjectile>();
        }

        static void EnsureProjectFolders()
        {
            EnsureFolder("Assets", "MirrorTrial");
            EnsureFolder("Assets/MirrorTrial", "Prefabs");
            EnsureFolder("Assets/MirrorTrial/Prefabs", "Characters");
            EnsureFolder("Assets/MirrorTrial/Prefabs", "Abilities");
        }

        static void EnsureFolder(string parent, string child)
        {
            var path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }

        static void ApplyTagAndLayer(GameObject player)
        {
            if (TagExists("Player"))
                player.tag = "Player";

            var playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0)
                player.layer = playerLayer;
        }

        static bool TagExists(string tag)
        {
            foreach (var existingTag in UnityEditorInternal.InternalEditorUtility.tags)
            {
                if (existingTag == tag)
                    return true;
            }

            return false;
        }

        static void AssignObject(Object target, string propertyName, Object value)
        {
            var serializedObject = new SerializedObject(target);
            serializedObject.FindProperty(propertyName).objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
