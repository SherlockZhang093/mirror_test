using MirrorTrial.Boss;
using UnityEditor;
using UnityEngine;

namespace MirrorTrial.Editor.Enemies
{
    [CustomEditor(typeof(MirrorBossActorV2))]
    public sealed class MirrorBossEnemyEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Boss 敌兵配置", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("玩家技能传入击飞后，Boss 必定进入公共击飞状态。这里配置 Boss 的动画与落地表现。", MessageType.Info);

            var profileProperty = serializedObject.FindProperty("profile");
            EditorGUILayout.PropertyField(profileProperty, new GUIContent("Boss 配置资源"));

            if (profileProperty.objectReferenceValue is MirrorBossSimpleProfile profile)
            {
                var profileObject = new SerializedObject(profile);
                profileObject.Update();
                EditorGUILayout.Space(6f);
                EditorGUILayout.LabelField("击飞表现", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(
                    profileObject.FindProperty("launchSettings"),
                    new GUIContent("击飞动画与落地表现"),
                    true);
                if (profileObject.ApplyModifiedProperties())
                    EditorUtility.SetDirty(profile);
            }
            else
            {
                EditorGUILayout.HelpBox("请先绑定 MirrorBossSimpleProfile。", MessageType.Warning);
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Boss 组件引用", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("swordHitbox"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("swordCollider"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("sprite"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("behaviourTreeControlled"));

            serializedObject.ApplyModifiedProperties();
        }
    }
}
