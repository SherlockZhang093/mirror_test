using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MirrorTrial.Editor.Level
{
    public static class Level02JungleArtBuilder
    {
        private const string ScenePath = "Assets/MirrorTrial/Scenes/Level_Reality_02.unity";
        private const string ArtRoot = "Assets/MirrorTrial/Art/Level02_Jungle/";
        private const string GeneratedRootName = "美术_第二关_丛林";

        [MenuItem("MirrorTrial/Level 02/Build Jungle Art Only")]
        public static void Build()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject geometry = GameObject.Find("地形");
            if (geometry == null)
            {
                throw new System.InvalidOperationException("Level_Reality_02 缺少地形根节点。");
            }

            Transform oldRoot = geometry.transform.Find(GeneratedRootName);
            if (oldRoot != null)
            {
                Object.DestroyImmediate(oldRoot.gameObject);
            }

            Transform root = NewGroup(GeneratedRootName, geometry.transform);
            Transform backgrounds = NewGroup("01_背景", root);
            Transform far = NewGroup("远景天空", backgrounds);
            Transform middle = NewGroup("中景遗迹", backgrounds);
            Transform architecture = NewGroup("02_遗迹平台外观", root);
            Transform water = NewGroup("03_水体外观", root);
            Transform mechanisms = NewGroup("04_机关与地标外观", root);
            Transform vegetation = NewGroup("05_植被", root);
            Transform foreground = NewGroup("06_前景遮框", root);

            // Outdoor backgrounds: repeated wide plates cover the complete exploration route.
            for (int i = 0; i < 6; i++)
            {
                float x = 8f + i * 17f;
                AddSprite(far, "天空_" + (i + 1), "Background/jungle_sky_far_v1.png", new Vector2(x, 3.2f), new Vector2(1.05f, 1.05f), -100, i % 2 == 1);
                AddSprite(middle, "远景遗迹_" + (i + 1), "Background/jungle_midground_v1.png", new Vector2(x + 1.5f, 1.5f), new Vector2(1.05f, 1.05f), -82, i % 2 == 0, new Color(0.82f, 0.95f, 0.91f, 0.78f));
            }

            AddSprite(far, "神庙内部背景_1", "Background/temple_interior_v1.png", new Vector2(105f, -3f), new Vector2(1.15f, 1.15f), -99);
            AddSprite(far, "神庙内部背景_2", "Background/temple_interior_v1.png", new Vector2(124f, -3f), new Vector2(1.15f, 1.15f), -99, true);

            // A longer, varied opening route before the player reaches the first major landmark.
            Platform(architecture, "入口长台", "platform_long_v1.png", 3f, -3.7f, 1.45f, 1f);
            Platform(architecture, "浅水岸台", "platform_medium_v1.png", 15f, -3.1f, 1.15f, 1f);
            Platform(architecture, "低阶遗迹", "platform_medium_v1.png", 25f, -1.9f, 0.95f, 0.9f);
            Platform(architecture, "树根高台", "platform_long_v1.png", 35f, -0.4f, 1.05f, 0.9f);
            Platform(architecture, "第一攀升台", "platform_medium_v1.png", 45f, 1.4f, 0.9f, 0.86f);
            Platform(architecture, "断桥前台", "platform_long_v1.png", 56f, 3.0f, 1.1f, 0.9f);
            Platform(architecture, "断桥后台", "platform_medium_v1.png", 70f, 2.5f, 1.0f, 0.9f);
            Platform(architecture, "追逐低台", "platform_medium_v1.png", 79f, 0.5f, 0.85f, 0.8f);
            Platform(architecture, "追逐高台", "platform_long_v1.png", 87f, 2.4f, 0.95f, 0.85f);
            Platform(architecture, "坠落地板", "breakable_floor_v1.png", 96f, 0.2f, 0.95f, 0.8f);
            Platform(architecture, "神庙下层台", "platform_long_v1.png", 104f, -6.4f, 1.25f, 0.95f);
            Platform(architecture, "瀑布庭院左", "platform_medium_v1.png", 115f, -5.0f, 1.0f, 0.9f);
            Platform(architecture, "瀑布庭院中", "platform_pillar_v1.png", 123f, -3.4f, 0.9f, 1.1f);
            Platform(architecture, "Boss庭院", "platform_long_v1.png", 136f, -4.8f, 1.55f, 1.0f);

            AddSprite(architecture, "入口断拱", "Platforms/broken_arch_v1.png", new Vector2(-1f, -0.8f), new Vector2(1.15f, 1.15f), -4);
            AddSprite(architecture, "中段遗迹柱", "Platforms/ruin_column_v1.png", new Vector2(42f, -0.5f), new Vector2(0.9f, 1.35f), -5);
            AddSprite(architecture, "吊桥支撑柱", "Platforms/ruin_column_v1.png", new Vector2(62f, 0.1f), new Vector2(1.0f, 1.55f), -5);
            AddSprite(architecture, "追逐根架", "Platforms/root_support_v1.png", new Vector2(83f, -1.8f), new Vector2(1.0f, 1.0f), -4);
            AddSprite(architecture, "神庙入口拱", "Platforms/temple_arch_v1.png", new Vector2(101f, -1.8f), new Vector2(1.15f, 1.15f), 4);

            AddSprite(water, "入口浅水", "Water/shallow_water_v1.png", new Vector2(12f, -4.7f), new Vector2(2.0f, 0.72f), -2, false, new Color(0.72f, 1f, 1f, 0.88f));
            AddSprite(water, "林间水池", "Water/water_basin_v1.png", new Vector2(30f, -3.8f), new Vector2(1.4f, 0.8f), -1);
            AddSprite(water, "窄瀑布", "Water/waterfall_narrow_v1.png", new Vector2(109f, -1.4f), new Vector2(1.05f, 1.65f), -6, false, new Color(0.78f, 1f, 1f, 0.82f));
            AddSprite(water, "宽瀑布", "Water/waterfall_wide_v1.png", new Vector2(121f, -0.9f), new Vector2(1.2f, 1.8f), -6, false, new Color(0.75f, 1f, 1f, 0.76f));
            AddSprite(water, "庭院水雾_1", "FX/humid_mist_v1.png", new Vector2(114f, -5.2f), new Vector2(1.7f, 0.75f), 3, false, new Color(0.75f, 1f, 1f, 0.48f));
            AddSprite(water, "庭院水雾_2", "FX/humid_mist_v1.png", new Vector2(124f, -4.6f), new Vector2(1.8f, 0.8f), 3, true, new Color(0.75f, 1f, 1f, 0.42f));

            AddSprite(mechanisms, "入口传送门_仅外观", "Mechanisms/arrival_portal_v1.png", new Vector2(0f, -2.0f), new Vector2(1.15f, 1.15f), 2);
            AddSprite(mechanisms, "吊桥_竖起_仅外观", "Mechanisms/drawbridge_upright_v1.png", new Vector2(64f, 3.8f), new Vector2(1.05f, 1.05f), 1);
            AddSprite(mechanisms, "滑轮_仅外观", "Mechanisms/pulley_v1.png", new Vector2(61f, 7.1f), new Vector2(0.6f, 0.6f), 2);
            AddSprite(mechanisms, "配重_仅外观", "Mechanisms/counterweight_v1.png", new Vector2(58.5f, 4.8f), new Vector2(0.62f, 0.62f), 2);
            AddSprite(mechanisms, "末段镜门_仅外观", "Mechanisms/mirror_gate_v1.png", new Vector2(131f, -1.4f), new Vector2(1.15f, 1.15f), 2);
            AddSprite(mechanisms, "弓箭祭坛_仅外观", "Mechanisms/bow_shrine_v1.png", new Vector2(144f, -2.2f), new Vector2(1.05f, 1.05f), 3);
            AddSprite(mechanisms, "弓箭奖励_仅外观", "Mechanisms/bow_reward_v1.png", new Vector2(144f, -0.9f), new Vector2(0.72f, 0.72f), 5);
            AddSprite(mechanisms, "神庙火把_1", "Mechanisms/temple_torch_v1.png", new Vector2(106f, -1.7f), new Vector2(0.48f, 0.48f), 2);
            AddSprite(mechanisms, "神庙火把_2", "Mechanisms/temple_torch_v1.png", new Vector2(126f, -1.3f), new Vector2(0.48f, 0.48f), 2, true);

            Plant(vegetation, "入口蕨丛", "fern_cluster_v1.png", 7f, -3.1f, 0.95f, false);
            Plant(vegetation, "浅水阔叶", "broadleaf_cluster_v1.png", 19f, -2.5f, 0.75f, true);
            Plant(vegetation, "攀升蕨丛", "fern_cluster_v1.png", 34f, 0.3f, 0.8f, true);
            Plant(vegetation, "桥前阔叶", "broadleaf_cluster_v1.png", 52f, 3.5f, 0.72f, false);
            Plant(vegetation, "追逐蕨丛", "fern_cluster_v1.png", 78f, 1.0f, 0.72f, false);
            Plant(vegetation, "神庙根拱", "root_arch_v1.png", 99f, -4.9f, 1.0f, false);
            Plant(vegetation, "庭院阔叶", "broadleaf_cluster_v1.png", 118f, -3.9f, 0.85f, false);
            AddSprite(vegetation, "高处垂藤_1", "Vegetation/hanging_vines_v1.png", new Vector2(39f, 6.3f), new Vector2(0.8f, 1.1f), 1);
            AddSprite(vegetation, "高处垂藤_2", "Vegetation/hanging_vines_v1.png", new Vector2(91f, 6.3f), new Vector2(0.75f, 1.2f), 1, true);
            AddSprite(vegetation, "神庙垂藤", "Vegetation/hanging_vines_v1.png", new Vector2(119f, 2.3f), new Vector2(0.8f, 1.3f), 1);

            AddSprite(foreground, "入口左前景", "Vegetation/foreground_left_v1.png", new Vector2(-3.8f, -0.5f), new Vector2(1.2f, 1.2f), 20);
            AddSprite(foreground, "桥段右前景", "Vegetation/foreground_right_v1.png", new Vector2(73f, 1.7f), new Vector2(1.0f, 1.0f), 18);
            AddSprite(foreground, "庭院左前景", "Vegetation/foreground_left_v1.png", new Vector2(108f, -1.0f), new Vector2(1.05f, 1.05f), 20);
            AddSprite(foreground, "终点右前景", "Vegetation/foreground_right_v1.png", new Vector2(149f, -1.2f), new Vector2(1.15f, 1.15f), 20);
            AddSprite(foreground, "顶部树冠_入口", "Vegetation/canopy_strip_v1.png", new Vector2(13f, 7.4f), new Vector2(2.1f, 0.85f), 16);
            AddSprite(foreground, "顶部树冠_中段", "Vegetation/canopy_strip_v1.png", new Vector2(55f, 8.1f), new Vector2(2.2f, 0.85f), 16, true);
            AddSprite(foreground, "顶部树冠_神庙", "Vegetation/canopy_strip_v1.png", new Vector2(116f, 3.8f), new Vector2(2.0f, 0.8f), 16);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = root.gameObject;
            Debug.Log("[Level 02] Jungle art-only layout built. No colliders, triggers, gameplay scripts, enemies, or encounter logic were added.");
        }

        private static Transform NewGroup(string name, Transform parent)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static void Platform(Transform parent, string name, string file, float x, float y, float sx, float sy)
        {
            AddSprite(parent, name, "Platforms/" + file, new Vector2(x, y), new Vector2(sx, sy), 0);
        }

        private static void Plant(Transform parent, string name, string file, float x, float y, float scale, bool flip)
        {
            AddSprite(parent, name, "Vegetation/" + file, new Vector2(x, y), new Vector2(scale, scale), 2, flip);
        }

        private static GameObject AddSprite(Transform parent, string name, string relativePath, Vector2 position, Vector2 scale, int order, bool flipX = false, Color? tint = null)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + relativePath);
            if (sprite == null)
            {
                Debug.LogWarning("[Level 02] Missing sprite: " + ArtRoot + relativePath);
            }

            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(position.x, position.y, 0f);
            go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            renderer.flipX = flipX;
            renderer.color = tint ?? Color.white;
            return go;
        }
    }
}
