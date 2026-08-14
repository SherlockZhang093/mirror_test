using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MirrorTrial.Editor.Level
{
    public static class Level02PlatformArtDresser
    {
        private const string ScenePath = "Assets/MirrorTrial/Scenes/Level_Reality_02.unity";
        private const string ArtPath = "Assets/MirrorTrial/Art/Level02_Jungle/";
        private const string CollisionRootName = "碰撞_第二关路线";
        private const string ArtRootName = "美术_第二关_丛林";
        private const string PlatformGroupName = "02_遗迹平台外观";

        [MenuItem("MirrorTrial/Level 02/Dress Art From Route Colliders")]
        public static void Dress()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Transform collisionRoot = FindInScene(scene, CollisionRootName);
            Transform artRoot = FindInScene(scene, ArtRootName);
            if (!collisionRoot || !artRoot)
                throw new System.InvalidOperationException("请先生成第二关美术和路线碰撞节点。");

            Transform oldGroup = artRoot.Find(PlatformGroupName);
            if (oldGroup)
                Object.DestroyImmediate(oldGroup.gameObject);

            var group = new GameObject(PlatformGroupName);
            group.transform.SetParent(artRoot, false);

            int index = 0;
            foreach (Transform child in collisionRoot)
            {
                BoxCollider2D box = child.GetComponent<BoxCollider2D>();
                if (!box || child.name.Contains("边界") || child.name.Contains("吊桥通路"))
                    continue;

                Vector2 center = (Vector2)child.position + box.offset;
                Vector2 size = Vector2.Scale(box.size, new Vector2(Mathf.Abs(child.lossyScale.x), Mathf.Abs(child.lossyScale.y)));
                if (child.name.Contains("墙"))
                {
                    AddWall(group.transform, child.name, center, size, index++);
                }
                else
                {
                    AddFloor(group.transform, child.name, center, size, index++);
                }
            }

            AddSetDressing(group.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = group;
            SceneView.FrameLastActiveSceneView();
            Debug.Log("[Level 02] Platform art rebuilt from route colliders. Collision data was not modified.");
        }

        private static void AddFloor(Transform parent, string sourceName, Vector2 center, Vector2 size, int seed)
        {
            string spriteFile;
            if (sourceName.Contains("破裂")) spriteFile = "Platforms/breakable_floor_v1.png";
            else if (sourceName.Contains("踏石") || sourceName.Contains("踏台")) spriteFile = "Platforms/platform_pillar_v1.png";
            else if (size.x <= 4.3f) spriteFile = "Platforms/platform_medium_v1.png";
            else spriteFile = "Platforms/platform_long_v1.png";

            Sprite sprite = Load(spriteFile);
            if (!sprite) return;

            float maxSegmentWidth = spriteFile.Contains("pillar") ? 3.3f : 6.2f;
            int segmentCount = Mathf.Max(1, Mathf.CeilToInt(size.x / maxSegmentWidth));
            float segmentWidth = size.x / segmentCount;
            float visualHeight = sourceName.Contains("池底") ? 0.9f : 1.2f;
            float scaleY = visualHeight / Mathf.Max(0.01f, sprite.bounds.size.y);

            for (int i = 0; i < segmentCount; i++)
            {
                float x = center.x - size.x * 0.5f + segmentWidth * (i + 0.5f);
                float scaleX = segmentWidth / Mathf.Max(0.01f, sprite.bounds.size.x);
                float y = center.y + size.y * 0.5f - sprite.bounds.extents.y * scaleY + 0.08f;
                Color tint = ((seed + i) & 1) == 0
                    ? Color.white
                    : new Color(0.91f, 0.96f, 0.88f, 1f);
                AddSprite(parent, sourceName + "_表面_" + (i + 1), sprite, new Vector2(x, y), new Vector2(scaleX, scaleY), 0, ((seed + i) % 3) == 0, tint);
            }

            // Supports create depth without affecting collision.
            if (size.x >= 4.5f && !sourceName.Contains("池底") && !sourceName.Contains("坑底"))
            {
                Sprite support = Load(seed % 2 == 0 ? "Platforms/root_support_v1.png" : "Platforms/ruin_column_v1.png");
                if (support)
                {
                    float supportHeight = Mathf.Clamp(2.0f + (seed % 3) * 0.7f, 2f, 3.5f);
                    float sy = supportHeight / support.bounds.size.y;
                    float sx = Mathf.Min(1.15f, sy);
                    float top = center.y + size.y * 0.5f - 0.35f;
                    float py = top - support.bounds.extents.y * sy;

                    if (size.x >= 8f)
                    {
                        // Long platforms need two readable load paths; a single column
                        // creates an implausibly long cantilever.
                        float inset = Mathf.Min(size.x * 0.28f, size.x * 0.5f - 1.1f);
                        AddSprite(parent, sourceName + "_左支撑", support, new Vector2(center.x - inset, py), new Vector2(sx, sy), -2, false, new Color(0.86f, 0.92f, 0.82f, 1f));
                        AddSprite(parent, sourceName + "_右支撑", support, new Vector2(center.x + inset, py), new Vector2(sx, sy), -2, true, new Color(0.82f, 0.90f, 0.80f, 1f));
                    }
                    else
                    {
                        float px = center.x + (((seed & 1) == 0) ? -size.x * 0.24f : size.x * 0.24f);
                        AddSprite(parent, sourceName + "_支撑", support, new Vector2(px, py), new Vector2(sx, sy), -2, (seed & 1) == 1, new Color(0.86f, 0.92f, 0.82f, 1f));
                    }
                }
            }
        }

        private static void AddWall(Transform parent, string sourceName, Vector2 center, Vector2 size, int seed)
        {
            Sprite sprite = Load("Platforms/ruin_column_v1.png");
            if (!sprite) return;
            float sx = Mathf.Max(0.45f, size.x / sprite.bounds.size.x * 1.35f);
            float sy = size.y / sprite.bounds.size.y;
            AddSprite(parent, sourceName + "_石柱", sprite, center, new Vector2(sx, sy), -1, (seed & 1) == 0, new Color(0.88f, 0.94f, 0.84f, 1f));

            Sprite vines = Load("Vegetation/hanging_vines_v1.png");
            if (vines)
            {
                float top = center.y + size.y * 0.5f;
                AddSprite(parent, sourceName + "_垂藤", vines, new Vector2(center.x + size.x * 0.25f, top - 1.0f), new Vector2(0.42f, 0.65f), 2, (seed & 1) == 1, Color.white);
            }
        }

        private static void AddSetDressing(Transform parent)
        {
            AddDecoration(parent, "入口断拱", "Platforms/broken_arch_v1.png", -1.2f, -1.35f, 1.25f, -3, false);
            AddDecoration(parent, "攀升树根", "Platforms/root_support_v1.png", 39.5f, -1.15f, 1.15f, -2, false);
            AddDecoration(parent, "机关石柱", "Platforms/ruin_column_v1.png", 58.5f, -0.35f, 1.15f, -2, true);
            AddDecoration(parent, "追逐断拱", "Platforms/broken_arch_v1.png", 87.5f, -0.35f, 1.05f, -3, true);
            AddDecoration(parent, "神庙入口框", "Platforms/temple_arch_v1.png", 100.5f, -2.2f, 1.15f, 3, false);

            AddDecoration(parent, "蕨丛_入口", "Vegetation/fern_cluster_v1.png", 7f, -2.8f, 0.8f, 3, false);
            AddDecoration(parent, "阔叶_浅水", "Vegetation/broadleaf_cluster_v1.png", 21f, -2.4f, 0.65f, 3, true);
            AddDecoration(parent, "蕨丛_阶梯", "Vegetation/fern_cluster_v1.png", 37f, 1.1f, 0.65f, 3, true);
            AddDecoration(parent, "阔叶_机关", "Vegetation/broadleaf_cluster_v1.png", 50f, 3.1f, 0.6f, 3, false);
            AddDecoration(parent, "蕨丛_追逐", "Vegetation/fern_cluster_v1.png", 82f, 1.8f, 0.62f, 3, false);
            AddDecoration(parent, "阔叶_庭院", "Vegetation/broadleaf_cluster_v1.png", 129f, -4.2f, 0.72f, 3, true);
            AddDecoration(parent, "蕨丛_祭坛", "Vegetation/fern_cluster_v1.png", 147f, -1.55f, 0.65f, 3, false);
        }

        private static void AddDecoration(Transform parent, string name, string path, float x, float y, float scale, int order, bool flip)
        {
            Sprite sprite = Load(path);
            if (sprite) AddSprite(parent, name, sprite, new Vector2(x, y), new Vector2(scale, scale), order, flip, Color.white);
        }

        private static GameObject AddSprite(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 scale, int order, bool flip, Color tint)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(position.x, position.y, 0f);
            go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            renderer.flipX = flip;
            renderer.color = tint;
            return go;
        }

        private static Sprite Load(string relativePath)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath + relativePath);
            if (!sprite) Debug.LogWarning("[Level 02] Missing art sprite: " + relativePath);
            return sprite;
        }

        private static Transform FindInScene(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    if (child.name == name) return child;
            }
            return null;
        }
    }
}
