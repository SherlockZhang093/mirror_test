using MirrorTrial.Enemies;
using UnityEditor;
using UnityEngine;

namespace MirrorTrial.Editor.Enemies
{
    [CustomEditor(typeof(EnemyAI))]
    public class EnemyAIEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var enemy = (EnemyAI)target;
            var profileProperty = serializedObject.FindProperty("profile");

            EditorGUILayout.LabelField("敌兵基础配置", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("专属AI配置", profileProperty.objectReferenceValue, typeof(EnemyAIProfile), false);

            var maxHp = serializedObject.FindProperty("overrideMaxHitPoints");
            var moveSpeed = serializedObject.FindProperty("overrideMoveSpeed");
            maxHp.intValue = EditorGUILayout.IntField("单独生命值（-1使用默认值）", maxHp.intValue);
            moveSpeed.floatValue = EditorGUILayout.FloatField("单独移动速度（-1使用默认值）", moveSpeed.floatValue);

            if (profileProperty.objectReferenceValue is EnemyAIProfile profile)
                DrawProfile(profile);
            else
                EditorGUILayout.HelpBox("这个敌兵还没有专属AI配置。", MessageType.Warning);

            DrawCollider(enemy);
            DrawAttackEffect(enemy);

            EditorGUILayout.Space(8);
            var showGizmos = serializedObject.FindProperty("showDebugGizmos");
            showGizmos.boolValue = EditorGUILayout.Toggle("显示场景辅助线", showGizmos.boolValue);
            EditorGUILayout.HelpBox("敌兵位置、黄色探测范围和绿色巡逻边界请在关卡编辑器中设置。", MessageType.Info);

            serializedObject.ApplyModifiedProperties();
        }

        static void DrawProfile(EnemyAIProfile profile)
        {
            var profileObject = new SerializedObject(profile);
            profileObject.Update();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("基础属性", EditorStyles.boldLabel);
            IntField(profileObject, "maxHitPoints", "最大生命值");
            FloatField(profileObject, "moveSpeed", "移动速度");
            BoolField(profileObject, "flipVisualByVelocity", "移动时自动转向");

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("探测、巡逻与追击", EditorStyles.boldLabel);
            FloatField(profileObject, "detectionRange", "默认探测距离");
            FloatField(profileObject, "stopDistance", "开始攻击距离");
            FloatField(profileObject, "patrolWaitTime", "巡逻端点等待时间");

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("攻击", EditorStyles.boldLabel);
            FloatField(profileObject, "attackWindup", "攻击前摇");
            FloatField(profileObject, "attackCooldown", "攻击冷却");
            FloatField(profileObject, "attackRange", "攻击判定距离");
            IntField(profileObject, "attackDamage", "攻击伤害");
            FloatField(profileObject, "damageDelay", "伤害帧延迟");
            ObjectField(profileObject, "projectilePrefab", "远程子弹预制体");
            Vector2Field(profileObject, "projectileSpawnOffset", "子弹生成偏移");
            BoolField(profileObject, "lockMovementWhileAttacking", "攻击时停止移动");

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("受击与死亡", EditorStyles.boldLabel);
            FloatField(profileObject, "hurtStun", "受击硬直时间");
            FloatField(profileObject, "knockbackForce", "受击后退力度");
            FloatField(profileObject, "deathFadeDelay", "死亡销毁延迟");
            IntField(profileObject, "deathBlinkCount", "死亡闪烁次数");
            FloatField(profileObject, "deathBlinkInterval", "死亡闪烁间隔");

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("击飞表现", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("技能传入击飞后必定生效。这里仅配置起飞、空中、下落和落地动画，不调整技能的击飞力度。", MessageType.Info);
            EditorGUILayout.PropertyField(profileObject.FindProperty("launchSettings"), new GUIContent("击飞动画与落地表现"), true);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("移动限制", EditorStyles.boldLabel);
            FloatField(profileObject, "ledgeCheckDistance", "悬崖检测距离");
            FloatField(profileObject, "wallCheckDistance", "墙壁检测距离");
            BoolField(profileObject, "canTurnAtLedge", "遇到悬崖时转身");

            if (profileObject.ApplyModifiedProperties())
                EditorUtility.SetDirty(profile);
        }

        static void DrawCollider(EnemyAI enemy)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("碰撞框", EditorStyles.boldLabel);
            var collider = enemy.GetComponent<BoxCollider2D>();
            if (!collider)
            {
                EditorGUILayout.HelpBox("这个敌兵没有矩形碰撞框。", MessageType.Warning);
                return;
            }

            EditorGUI.BeginChangeCheck();
            var offset = EditorGUILayout.Vector2Field("中心偏移", collider.offset);
            var size = EditorGUILayout.Vector2Field("宽 / 高", collider.size);
            if (!EditorGUI.EndChangeCheck()) return;

            Undo.RecordObject(collider, "修改敌兵碰撞框");
            collider.offset = offset;
            collider.size = new Vector2(Mathf.Max(0.01f, size.x), Mathf.Max(0.01f, size.y));
            EditorUtility.SetDirty(collider);
        }

        void DrawAttackEffect(EnemyAI enemy)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("攻击特效", EditorStyles.boldLabel);
            var prefab = serializedObject.FindProperty("attackEffectPrefab");
            var point = serializedObject.FindProperty("attackEffectPoint");
            var lifetime = serializedObject.FindProperty("attackEffectLifetime");
            var mirror = serializedObject.FindProperty("mirrorAttackEffectByFacing");

            prefab.objectReferenceValue = EditorGUILayout.ObjectField("攻击特效预制体", prefab.objectReferenceValue, typeof(GameObject), false);
            point.objectReferenceValue = EditorGUILayout.ObjectField("武器或手部挂点", point.objectReferenceValue, typeof(Transform), true);
            lifetime.floatValue = Mathf.Max(0.01f, EditorGUILayout.FloatField("特效保留时间", lifetime.floatValue));
            mirror.boolValue = EditorGUILayout.Toggle("根据朝向翻转特效", mirror.boolValue);

            if (point.objectReferenceValue == null && GUILayout.Button("创建攻击特效挂点", GUILayout.Height(28)))
                CreateAttackEffectPoint(enemy, point);
        }

        static void FloatField(SerializedObject targetObject, string name, string label)
        {
            var property = targetObject.FindProperty(name);
            property.floatValue = EditorGUILayout.FloatField(label, property.floatValue);
        }

        static void IntField(SerializedObject targetObject, string name, string label)
        {
            var property = targetObject.FindProperty(name);
            property.intValue = EditorGUILayout.IntField(label, property.intValue);
        }

        static void BoolField(SerializedObject targetObject, string name, string label)
        {
            var property = targetObject.FindProperty(name);
            property.boolValue = EditorGUILayout.Toggle(label, property.boolValue);
        }

        static void Vector2Field(SerializedObject targetObject, string name, string label)
        {
            var property = targetObject.FindProperty(name);
            property.vector2Value = EditorGUILayout.Vector2Field(label, property.vector2Value);
        }

        static void ObjectField(SerializedObject targetObject, string name, string label)
        {
            var property = targetObject.FindProperty(name);
            property.objectReferenceValue = EditorGUILayout.ObjectField(label, property.objectReferenceValue, typeof(GameObject), false);
        }

        static void CreateAttackEffectPoint(EnemyAI enemy, SerializedProperty effectPoint)
        {
            var pointObject = new GameObject("攻击特效挂点");
            Undo.RegisterCreatedObjectUndo(pointObject, "创建攻击特效挂点");
            pointObject.transform.SetParent(enemy.transform);
            pointObject.transform.localPosition = new Vector3(0.5f, 0.15f, 0f);
            effectPoint.objectReferenceValue = pointObject.transform;
            effectPoint.serializedObject.ApplyModifiedProperties();
            Selection.activeGameObject = pointObject;
            EditorUtility.SetDirty(enemy);
        }
    }
}
