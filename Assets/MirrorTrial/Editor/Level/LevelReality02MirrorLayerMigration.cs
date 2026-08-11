using MirrorTrial.Puzzles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MirrorTrial.Editor.Level
{
    public static class LevelReality02MirrorLayerMigration
    {
        const string ScenePath = "Assets/MirrorTrial/Scenes/Level_Reality_02.unity";
        const string BaseSpritePath = "Assets/MirrorTrial/Art/Level02_Jungle/Puzzle_DoubleJump/Sprites/Puzzle_Mirror_Left_Base_v2.png";
        const string RotatorSpritePath = "Assets/MirrorTrial/Art/Level02_Jungle/Puzzle_DoubleJump/Sprites/Puzzle_Mirror_Left_Rotator_v2.png";

        [MenuItem("Tools/Mirror Trial/Level 02/Split Lower Mirror Base And Rotator")]
        public static void Apply()
        {
            if (SceneManager.GetActiveScene().path != ScenePath)
            {
                EditorUtility.DisplayDialog("场景不匹配", "请先打开 Level_Reality_02.unity。", "确定");
                return;
            }

            var holder = GameObject.Find("二段跳镜光谜题/下层三档镜子");
            if (!holder) holder = GameObject.Find("下层三档镜子");
            if (!holder)
            {
                EditorUtility.DisplayDialog("未找到镜子", "场景中没有找到“下层三档镜子”。", "确定");
                return;
            }

            var mirror = holder.GetComponent<RotatablePuzzleMirror>();
            if (!mirror) return;
            var existingBase = holder.transform.Find("固定基座");
            var existingPivot = holder.transform.Find("镜子旋转中心");
            if (existingBase && existingPivot)
            {
                Selection.activeTransform = existingPivot;
                return;
            }

            var oldVisual = holder.transform.Find("可旋转镜面");
            var oldRenderer = oldVisual ? oldVisual.GetComponent<SpriteRenderer>() : null;
            var targetWidth = oldRenderer ? oldRenderer.bounds.size.x : 1.1f;
            var targetBottomWorldY = oldRenderer ? oldRenderer.bounds.min.y : holder.transform.position.y - 0.8f;
            var order = oldRenderer ? oldRenderer.sortingOrder : 65;

            var baseObject = NewSprite("固定基座", holder.transform, BaseSpritePath, order);
            FitWidth(baseObject.transform, baseObject.GetComponent<SpriteRenderer>(), targetWidth);
            AlignBottomAndCenter(baseObject.transform, baseObject.GetComponent<SpriteRenderer>(), holder.transform.position.x, targetBottomWorldY);

            var pivotObject = new GameObject("镜子旋转中心");
            Undo.RegisterCreatedObjectUndo(pivotObject, "Create mirror pivot");
            pivotObject.transform.SetParent(holder.transform, true);
            var baseBounds = baseObject.GetComponent<SpriteRenderer>().bounds;
            pivotObject.transform.position = new Vector3(baseBounds.center.x, baseBounds.max.y, holder.transform.position.z);

            var rotatingObject = NewSprite("旋转镜面", pivotObject.transform, RotatorSpritePath, order + 1);
            var rotatingRenderer = rotatingObject.GetComponent<SpriteRenderer>();
            FitWidth(rotatingObject.transform, rotatingRenderer, targetWidth * 0.95f);

            // The generated rotating layer's lower-right connector is the point
            // that sits on the fixed turntable. Keep this local offset separate
            // from the Transform pivot so the base never rotates.
            var sprite = rotatingRenderer.sprite;
            var attachmentPixels = new Vector2(930f, 526f); // pixels from bottom-left
            var attachmentLocal = new Vector2(
                (attachmentPixels.x - sprite.pivot.x) / sprite.pixelsPerUnit,
                (attachmentPixels.y - sprite.pivot.y) / sprite.pixelsPerUnit);
            rotatingObject.transform.localPosition = -(Vector3)Vector2.Scale(attachmentLocal, rotatingObject.transform.localScale);

            var beamPoint = holder.transform.Find("反射点");
            if (beamPoint)
            {
                beamPoint.SetParent(pivotObject.transform, false);
                beamPoint.localPosition = rotatingObject.transform.localPosition;
            }

            var serialized = new SerializedObject(mirror);
            serialized.FindProperty("rotatingVisual").objectReferenceValue = pivotObject.transform;
            if (beamPoint) serialized.FindProperty("beamPoint").objectReferenceValue = beamPoint;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            pivotObject.transform.localRotation = Quaternion.Euler(0f, 0f, -28f);
            if (oldVisual) Undo.DestroyObjectImmediate(oldVisual.gameObject);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Selection.activeGameObject = pivotObject;
            Debug.Log("下层镜子已拆分：固定基座顶部中心为旋转轴，三档旋转只作用于镜面。", pivotObject);
        }

        static GameObject NewSprite(string name, Transform parent, string spritePath, int order)
        {
            var gameObject = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(gameObject, "Create " + name);
            gameObject.transform.SetParent(parent, false);
            var renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            renderer.sortingOrder = order;
            return gameObject;
        }

        static void FitWidth(Transform target, SpriteRenderer renderer, float targetWorldWidth)
        {
            var width = Mathf.Max(0.001f, renderer.bounds.size.x);
            var factor = targetWorldWidth / width;
            target.localScale *= factor;
        }

        static void AlignBottomAndCenter(Transform target, SpriteRenderer renderer, float centerWorldX, float bottomWorldY)
        {
            var bounds = renderer.bounds;
            target.position += new Vector3(centerWorldX - bounds.center.x, bottomWorldY - bounds.min.y, 0f);
        }
    }
}
