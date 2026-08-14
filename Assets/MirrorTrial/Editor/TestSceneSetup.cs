using MirrorTrial.Editor;
using MirrorTrial.Level;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MirrorTrial.Editor
{
    public static class TestSceneSetup
    {
        const string ScenePath = "Assets/MirrorTrial/Scenes/MirrorTrial_TestGym.unity";
        const string PlayerPrefabPath = "Assets/MirrorTrial/Prefabs/Characters/Player_MirrorTrial.prefab";

        [MenuItem("Tools/镜像试炼/关卡/创建测试场景")]
        public static void CreateTestScene()
        {
            // Ensure player prefab exists
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (!playerPrefab)
            {
                if (EditorUtility.DisplayDialog("缺少玩家预制体",
                    "Player_MirrorTrial.prefab not found.\nRun 'Tools/镜像试炼/战斗/创建玩家测试预制体' first?",
                    "Run it now", "Cancel"))
                {
                    PlayerFrameworkSetup.CreatePlayerTestPrefab();
                    playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
                }

                if (!playerPrefab)
                    return;
            }

            // Save current scene if dirty
            if (EditorSceneManager.GetActiveScene().isDirty)
                EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

            // Create new scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            scene.name = "MirrorTrial_TestGym";

            // Remove default directional light (we are 2D)
            var existingLight = GameObject.Find("Directional Light");
            if (existingLight)
                Object.DestroyImmediate(existingLight);

            // Create geometry root for clean hierarchy and camera bounds
            var geometryRoot = new GameObject("Geometry").transform;

            // Create ground
            CreateGround(geometryRoot, "Ground_Main", new Vector3(0f, -2f, 0f), new Vector2(30f, 1f));

            // Create floating platforms for jump testing
            CreateGround(geometryRoot, "Platform_Left", new Vector3(-5f, 1f, 0f), new Vector2(4f, 0.5f));
            CreateGround(geometryRoot, "Platform_Right", new Vector3(5f, 2.5f, 0f), new Vector2(4f, 0.5f));
            CreateGround(geometryRoot, "Platform_High", new Vector3(0f, 4.5f, 0f), new Vector2(3f, 0.5f));

            // Create walls for boundary
            CreateGround(geometryRoot, "Wall_Left", new Vector3(-16f, 3f, 0f), new Vector2(1f, 12f));
            CreateGround(geometryRoot, "Wall_Right", new Vector3(16f, 3f, 0f), new Vector2(1f, 12f));

            // Spawn player (y=-0.95 places collider bottom exactly on ground top)
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            player.transform.position = new Vector3(0f, -0.95f, 0f);
            player.name = "Player_MirrorTrial";

            var bounds = CameraSetupUtil.ComputeSceneBounds(geometryRoot);
            CameraSetupUtil.ApplyDefault(Camera.main, player.transform, boundsMinX: bounds.minX, boundsMaxX: bounds.maxX);

            // Create a dummy target (punching bag) for combat testing
            CreateDummyTarget(new Vector3(4f, -1f, 0f));
            CreateDummyTarget(new Vector3(-4f, -1f, 0f));
            CreateDummyTarget(new Vector3(8f, -1f, 0f));

            // Ensure folder exists and save scene
            EnsureFolder("Assets/MirrorTrial", "Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorSceneManager.OpenScene(ScenePath);

            Selection.activeGameObject = player;
            Debug.Log("[镜像试炼] 测试场景已创建并保存：" + ScenePath +
                      "\n- 点击 Play 开始调试移动、跳跃和战斗。" +
                      "\n- WASD/Arrows = move, Space = jump, J = attack, K = MirrorBlade, L = EchoDash");
        }

        [MenuItem("Tools/镜像试炼/关卡/打开测试场景")]
        public static void OpenTestScene()
        {
            if (!System.IO.File.Exists(ScenePath.Replace('/', '\\')))
            {
                if (EditorUtility.DisplayDialog("找不到测试场景",
                    "MirrorTrial_TestGym.unity 还不存在。要现在创建吗？",
                    "Create", "Cancel"))
                {
                    CreateTestScene();
                }
                return;
            }

            if (EditorSceneManager.GetActiveScene().isDirty)
                EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

            EditorSceneManager.OpenScene(ScenePath);
            Debug.Log("[镜像试炼] 已打开测试场景。点击 Play 开始调试。");
        }

        static GameObject CreateGround(Transform parent, string name, Vector3 position, Vector2 size)
        {
            var ground = new GameObject(name);
            ground.transform.SetParent(parent, false);
            ground.transform.position = position;
            ground.layer = LayerMask.NameToLayer("Default");
            ground.isStatic = true;

            var sr = ground.AddComponent<SpriteRenderer>();
            sr.sprite = CreateWhiteSquareSprite();
            sr.color = new Color(0.3f, 0.35f, 0.4f);
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = size;

            var collider = ground.AddComponent<BoxCollider2D>();
            collider.size = size;

            return ground;
        }

        static void CreateDummyTarget(Vector3 position)
        {
            var dummy = new GameObject("DummyTarget");
            dummy.transform.position = position;

            var sr = dummy.AddComponent<SpriteRenderer>();
            sr.sprite = CreateWhiteSquareSprite();
            sr.color = new Color(0.8f, 0.2f, 0.2f);
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(0.6f, 1.2f);

            // Solid collider so player doesn't walk through
            var solidCollider = dummy.AddComponent<BoxCollider2D>();
            solidCollider.size = new Vector2(0.6f, 1.2f);

            // Trigger collider for Hurtbox to receive hits
            var triggerCollider = dummy.AddComponent<BoxCollider2D>();
            triggerCollider.size = new Vector2(0.7f, 1.3f);
            triggerCollider.isTrigger = true;

            dummy.AddComponent<MirrorTrial.Combat.Hurtbox>();

            var health = dummy.AddComponent<Platformer.Mechanics.Health>();
            health.maxHP = 50;
        }

        static Sprite CreateWhiteSquareSprite()
        {
            // Look for an existing white square sprite asset; create one if missing
            const string spritePath = "Assets/MirrorTrial/Sprites/WhiteSquare.png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite)
                return sprite;

            EnsureFolder("Assets/MirrorTrial", "Sprites");

            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var pixels = new Color[16];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply();

            System.IO.File.WriteAllBytes(spritePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(spritePath);

            // Set texture import settings for tiled sprite usage
            var importer = (TextureImporter)AssetImporter.GetAtPath(spritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 1;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        }

        static void EnsureFolder(string parent, string child)
        {
            var path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
