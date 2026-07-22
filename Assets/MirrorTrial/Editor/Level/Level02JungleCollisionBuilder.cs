using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MirrorTrial.Editor.Level
{
    /// <summary>
    /// Builds the invisible traversal collision for Level 02.
    /// This intentionally creates no SpriteRenderer, TilemapRenderer or gameplay trigger.
    /// </summary>
    public static class Level02JungleCollisionBuilder
    {
        private const string ScenePath = "Assets/MirrorTrial/Scenes/Level_Reality_02.unity";
        private const string CollisionRootName = "碰撞_第二关路线";

        [MenuItem("MirrorTrial/Level 02/Build Route Colliders Only")]
        public static void Build()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject geometry = GameObject.Find("地形");
            if (!geometry)
                throw new System.InvalidOperationException("Level_Reality_02 缺少地形根节点。");

            Transform existing = geometry.transform.Find(CollisionRootName);
            if (existing)
                Object.DestroyImmediate(existing.gameObject);

            var root = new GameObject(CollisionRootName);
            root.transform.SetParent(geometry.transform, false);
            SetGroundLayer(root);

            // 1. Arrival portal terrace.
            Add(root.transform, "01_入口高台", 3f, -3.35f, 14f, 0.7f);
            Add(root.transform, "01_入口左边界", -4.15f, -0.2f, 0.7f, 7f);

            // 2. Sunken water exploration. Bottom collision sits below the visible water surface.
            Add(root.transform, "02_浅水池底", 13f, -5.05f, 7f, 0.55f);
            Add(root.transform, "02_水中踏石", 19f, -3.45f, 2.2f, 0.45f);
            Add(root.transform, "02_出水低阶", 23.5f, -2.75f, 3.7f, 0.55f);

            // 3. Readable staircase teaching the vertical rhythm.
            Add(root.transform, "03_阶梯一", 28.5f, -1.8f, 4.2f, 0.6f);
            Add(root.transform, "03_阶梯二", 33.5f, -0.65f, 4.5f, 0.6f);
            Add(root.transform, "03_阶梯三", 38f, 0.75f, 4.2f, 0.6f);
            Add(root.transform, "03_阶梯顶", 43.5f, 2.25f, 5.2f, 0.65f);

            // 4. Counterweight bridge approach and the lower safety route.
            Add(root.transform, "04_机关前台", 52.5f, 2.55f, 9f, 0.7f);
            Add(root.transform, "04_机关坑底", 59.5f, -4.75f, 8f, 0.7f);
            Add(root.transform, "04_机关坑左壁", 55.7f, -1.3f, 0.65f, 6.8f);
            Add(root.transform, "04_机关坑右壁", 63.3f, -0.8f, 0.65f, 7.8f);

            // Visual drawbridge is currently upright. This small landing is safe;
            // the traversable bridge collider can later be enabled by puzzle logic.
            Add(root.transform, "04_吊桥前落脚点", 66.5f, 2.0f, 4.6f, 0.65f);
            Add(root.transform, "04_吊桥通路_预留", 72f, 2.55f, 7f, 0.5f, false);

            // 5. Upper pursuit route.
            Add(root.transform, "05_追逐高台一", 76.5f, 2.65f, 6.5f, 0.65f);
            Add(root.transform, "05_追逐高台二", 84.5f, 1.35f, 5.5f, 0.65f);
            Add(root.transform, "05_追逐高台三", 91f, 2.85f, 7.5f, 0.65f);

            // 6. Breakable-floor area. It remains solid for now and can later receive logic.
            Add(root.transform, "06_破裂地板_预留", 96f, 0.55f, 5.5f, 0.55f);
            Add(root.transform, "06_神庙下层地面", 105f, -6.05f, 15f, 0.8f);
            Add(root.transform, "06_神庙左墙", 98.1f, -2.8f, 0.7f, 6.2f);

            // 7. Interior chamber roof and exit step.
            Add(root.transform, "07_神庙室内地面", 111f, -5.05f, 8f, 0.65f);
            Add(root.transform, "07_神庙出口阶", 116.5f, -4.2f, 3.5f, 0.55f);

            // 8. Waterfall courtyard with a lower pool and two traversal stones.
            Add(root.transform, "08_瀑布池底", 121.5f, -6.35f, 10f, 0.65f);
            Add(root.transform, "08_瀑布左踏台", 119f, -4.55f, 3.2f, 0.5f);
            Add(root.transform, "08_瀑布右踏台", 126f, -3.85f, 3.2f, 0.5f);
            Add(root.transform, "08_庭院出口", 130f, -4.75f, 5.5f, 0.6f);

            // 9. Mirror gate / archer arena and bow reward chamber.
            Add(root.transform, "09_Boss庭院地面", 138f, -4.45f, 16f, 0.75f);
            Add(root.transform, "09_Boss左侧台", 133.5f, -1.9f, 4f, 0.55f);
            Add(root.transform, "09_Boss右侧台", 142f, -0.9f, 4f, 0.55f);
            Add(root.transform, "09_弓箭祭坛台", 146f, -2.0f, 4.5f, 0.6f);
            Add(root.transform, "09_终点右边界", 150.2f, -0.7f, 0.7f, 8f);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = root;
            SceneView.FrameLastActiveSceneView();
            Debug.Log("[Level 02] Built invisible route colliders only. No visible renderers or gameplay triggers were added.");
        }

        private static GameObject Add(Transform parent, string name, float x, float y, float width, float height, bool enabled = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, y, 0f);
            SetGroundLayer(go);
            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(width, height);
            collider.isTrigger = false;
            collider.enabled = enabled;
            return go;
        }

        private static void SetGroundLayer(GameObject go)
        {
            int ground = LayerMask.NameToLayer("Ground");
            if (ground >= 0)
                go.layer = ground;
        }
    }
}
