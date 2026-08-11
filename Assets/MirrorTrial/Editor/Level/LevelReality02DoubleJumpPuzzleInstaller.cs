using MirrorTrial.Player;
using MirrorTrial.Puzzles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MirrorTrial.Editor.Level
{
    public static class LevelReality02DoubleJumpPuzzleInstaller
    {
        const string RootName = "二段跳镜光谜题";
        const string SpriteFolder = "Assets/MirrorTrial/Art/Level02_Jungle/Puzzle_DoubleJump/Sprites/";

        [MenuItem("Tools/Mirror Trial/Level 02/Install Double Jump Light Puzzle")]
        public static void Install()
        {
            if (SceneManager.GetActiveScene().path != "Assets/MirrorTrial/Scenes/Level_Reality_02.unity")
            {
                EditorUtility.DisplayDialog("场景不匹配", "请先打开 Level_Reality_02.unity。", "确定");
                return;
            }

            var existing = GameObject.Find(RootName);
            if (existing)
            {
                Selection.activeGameObject = existing;
                EditorGUIUtility.PingObject(existing);
                return;
            }

            var center = SceneView.lastActiveSceneView ? (Vector2)SceneView.lastActiveSceneView.pivot : Vector2.zero;
            var root = new GameObject(RootName);
            root.transform.position = center;
            Undo.RegisterCreatedObjectUndo(root, "Install double jump light puzzle");

            var source = CreateSprite("固定光源", root.transform, "Assets/MirrorTrial/Sprites/WhiteSquare.png", new Vector2(3.8f, 3.6f), 86);
            source.transform.localScale = Vector3.one * 0.32f;
            source.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            source.GetComponent<SpriteRenderer>().color = new Color(1f, 0.72f, 0.16f, 1f);

            var upperMirror = CreateMirror("上层三档镜子", root.transform, "Puzzle_Mirror_Right_v1.png", new Vector2(3.8f, 1.35f), 2, 0);
            var lowerMirror = CreateMirror("下层三档镜子", root.transform, "Puzzle_Mirror_Left_v1.png", new Vector2(-4.1f, -1.75f), 1, 0);

            var receiverObject = CreateSprite("光照接收装置", root.transform, "Puzzle_Shrine_Sealed_v1.png", new Vector2(-0.75f, -1.6f), 62);
            receiverObject.transform.localScale = Vector3.one * 0.72f;
            var receiverPoint = NewChild("接收点", receiverObject.transform, new Vector2(0f, 0.35f));
            var receiver = receiverObject.AddComponent<LightPuzzleReceiver>();

            var core = CreateSprite("二段跳能力核心", root.transform, "Ability_DoubleJump_Core_v1.png", new Vector2(1.65f, -1.9f), 74);
            core.transform.localScale = Vector3.one * 0.8f;
            var coreTrigger = core.AddComponent<CircleCollider2D>();
            coreTrigger.isTrigger = true;
            coreTrigger.radius = 0.62f;
            core.AddComponent<DoubleJumpAbilityPickup>();

            var airflow = new GameObject("上升气流（含粒子特效）");
            airflow.transform.SetParent(root.transform, false);
            airflow.transform.localPosition = new Vector3(1.65f, 0.45f, 0f);
            var airflowTrigger = airflow.AddComponent<BoxCollider2D>();
            airflowTrigger.isTrigger = true;
            airflowTrigger.size = new Vector2(1.55f, 5.1f);
            airflow.AddComponent<AirflowLiftZone>();
            var particleObject = new GameObject("气流粒子");
            particleObject.transform.SetParent(airflow.transform, false);
            particleObject.transform.localPosition = new Vector3(0f, -2.5f, 0f);
            particleObject.AddComponent<AirflowVfx>();

            CreateLadder(root.transform, new Vector2(6.0f, 0.3f));
            var exitRune = CreateSprite("二段跳出口符文", root.transform, "Puzzle_HighExit_Rune_v1.png", new Vector2(6.0f, 3.55f), 55);
            exitRune.transform.localScale = Vector3.one * 0.72f;

            var controllerObject = new GameObject("节点式光路控制器");
            controllerObject.transform.SetParent(root.transform, false);
            var controller = controllerObject.AddComponent<MirrorBeamPuzzleController>();

            SetObject(controller, "lightSource", source.transform);
            SetObjectArray(controller, "mirrors", new Object[] { upperMirror, lowerMirror });
            SetObject(controller, "receiver", receiver);
            SetObject(receiver, "leftReceivePoint", receiverPoint.transform);
            SetObject(receiver, "shrineRenderer", receiverObject.GetComponent<SpriteRenderer>());
            SetObject(receiver, "openedSprite", LoadSprite("Puzzle_Shrine_Open_v1.png"));
            SetObject(receiver, "abilityCore", core);
            SetObject(receiver, "airflow", airflow);

            core.SetActive(false);
            airflow.SetActive(false);

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log("Level 02 二段跳镜光谜题已安装：E 调镜、光路完成后播放接收器接口并出现能力核心与气流。", root);
        }

        static RotatablePuzzleMirror CreateMirror(string name, Transform parent, string spriteName, Vector2 position, int correct, int current)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = position;
            var visual = CreateSprite("可旋转镜面", holder.transform, spriteName, Vector2.zero, 65);
            visual.transform.localScale = Vector3.one * 0.68f;
            var point = NewChild("反射点", holder.transform, new Vector2(0f, 0.3f));
            var trigger = holder.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = 1.15f;
            var mirror = holder.AddComponent<RotatablePuzzleMirror>();
            SetObject(mirror, "rotatingVisual", visual.transform);
            SetObject(mirror, "beamPoint", point.transform);
            SetInt(mirror, "correctStep", correct);
            SetInt(mirror, "currentStep", current);
            return mirror;
        }

        static void CreateLadder(Transform parent, Vector2 position)
        {
            var holder = new GameObject("通往奖励层的梯子");
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = position;
            var middle = CreateSprite("梯子中段", holder.transform, "Puzzle_Ladder_Middle_v1.png", Vector2.zero, 58);
            middle.transform.localScale = new Vector3(0.72f, 0.82f, 1f);
            var top = CreateSprite("梯子顶部", holder.transform, "Puzzle_Ladder_Top_v1.png", new Vector2(0f, 2.28f), 59);
            top.transform.localScale = Vector3.one * 0.72f;
            var foot = CreateSprite("梯子底部", holder.transform, "Puzzle_Ladder_Foot_v1.png", new Vector2(0f, -2.35f), 59);
            foot.transform.localScale = Vector3.one * 0.72f;
            var trigger = holder.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(1.0f, 5.25f);
            var exit = NewChild("梯顶落脚点", holder.transform, new Vector2(0f, 3.25f));
            var zone = holder.AddComponent<LadderClimbZone>();
            SetObject(zone, "topExit", exit.transform);
        }

        static GameObject CreateSprite(string name, Transform parent, string spritePathOrName, Vector2 position, int order)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = position;
            var renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = spritePathOrName.StartsWith("Assets/")
                ? AssetDatabase.LoadAssetAtPath<Sprite>(spritePathOrName)
                : LoadSprite(spritePathOrName);
            renderer.sortingOrder = order;
            return gameObject;
        }

        static Sprite LoadSprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(SpriteFolder + name);

        static GameObject NewChild(string name, Transform parent, Vector2 localPosition)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            return child;
        }

        static void SetObject(Object target, string property, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(property).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetObjectArray(Object target, string property, Object[] values)
        {
            var serialized = new SerializedObject(target);
            var array = serialized.FindProperty(property);
            array.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetInt(Object target, string property, int value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(property).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
