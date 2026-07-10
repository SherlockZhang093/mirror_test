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

        [MenuItem("Tools/Mirror Trial/Create Test Scene")]
        public static void CreateTestScene()
        {
            // Ensure player prefab exists
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (!playerPrefab)
            {
                if (EditorUtility.DisplayDialog("Missing Player Prefab",
                    "Player_MirrorTrial.prefab not found.\nRun 'Tools/Mirror Trial/Create Player Test Prefab' first?",
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

            // Setup camera
            var mainCam = Camera.main;
            if (mainCam)
            {
                mainCam.orthographic = true;
                mainCam.orthographicSize = 7f;
                mainCam.transform.position = new Vector3(0f, 2f, -10f);
                mainCam.backgroundColor = new Color(0.12f, 0.12f, 0.18f);
                mainCam.clearFlags = CameraClearFlags.SolidColor;
            }

            // Create ground
            CreateGround("Ground_Main", new Vector3(0f, -2f, 0f), new Vector2(30f, 1f));

            // Create floating platforms for jump testing
            CreateGround("Platform_Left", new Vector3(-5f, 1f, 0f), new Vector2(4f, 0.5f));
            CreateGround("Platform_Right", new Vector3(5f, 2.5f, 0f), new Vector2(4f, 0.5f));
            CreateGround("Platform_High", new Vector3(0f, 4.5f, 0f), new Vector2(3f, 0.5f));

            // Create walls for boundary
            CreateGround("Wall_Left", new Vector3(-16f, 3f, 0f), new Vector2(1f, 12f));
            CreateGround("Wall_Right", new Vector3(16f, 3f, 0f), new Vector2(1f, 12f));

            // Spawn player (y=-0.95 places collider bottom exactly on ground top)
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            player.transform.position = new Vector3(0f, -0.95f, 0f);
            player.name = "Player_MirrorTrial";

            // Create a dummy target (punching bag) for combat testing
            CreateDummyTarget(new Vector3(4f, -1f, 0f));
            CreateDummyTarget(new Vector3(-4f, -1f, 0f));
            CreateDummyTarget(new Vector3(8f, -1f, 0f));

            // Ensure folder exists and save scene
            EnsureFolder("Assets/MirrorTrial", "Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorSceneManager.OpenScene(ScenePath);

            Selection.activeGameObject = player;
            Debug.Log("[Mirror Trial] Test scene created and saved: " + ScenePath +
                      "\n- Press Play to start debugging movement, jump, combat." +
                      "\n- WASD/Arrows = move, Space = jump, J = attack, K = MirrorBlade, L = EchoDash");
        }

        [MenuItem("Tools/Mirror Trial/Open Test Scene")]
        public static void OpenTestScene()
        {
            if (!System.IO.File.Exists(ScenePath.Replace('/', '\\')))
            {
                if (EditorUtility.DisplayDialog("Test Scene Not Found",
                    "MirrorTrial_TestGym.unity doesn't exist yet. Create it?",
                    "Create", "Cancel"))
                {
                    CreateTestScene();
                }
                return;
            }

            if (EditorSceneManager.GetActiveScene().isDirty)
                EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

            EditorSceneManager.OpenScene(ScenePath);
            Debug.Log("[Mirror Trial] Opened test scene. Press Play to debug.");
        }

        static GameObject CreateGround(string name, Vector3 position, Vector2 size)
        {
            var ground = new GameObject(name);
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
