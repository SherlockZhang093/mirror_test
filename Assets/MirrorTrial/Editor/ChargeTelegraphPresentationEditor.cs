using MirrorTrial.Combat;
using UnityEditor;
using UnityEngine;

namespace MirrorTrial.EditorTools
{
    [CustomEditor(typeof(ChargeTelegraphPresentation))]
    public sealed class ChargeTelegraphPresentationEditor : UnityEditor.Editor
    {
        bool previewEnabled;
        bool previewFacingRight = true;
        float previewProgress = 0.9f;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            var settingsChanged = EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("特效预览", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("无需运行游戏。开启后，修改上方参数会立即刷新 Scene 和 Prefab 视图。绿色框是汇聚粒子范围，脚边圆圈是尘土位置。", MessageType.Info);

            EditorGUI.BeginChangeCheck();
            previewEnabled = EditorGUILayout.Toggle("启用特效预览", previewEnabled);
            using (new EditorGUI.DisabledScope(!previewEnabled))
            {
                previewProgress = EditorGUILayout.Slider("预览蓄力进度", previewProgress, 0f, 1f);
                previewFacingRight = EditorGUILayout.Toggle("角色朝右", previewFacingRight);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("起手 20%")) previewProgress = 0.2f;
                    if (GUILayout.Button("蓄力中 60%")) previewProgress = 0.6f;
                    if (GUILayout.Button("接近满蓄 90%")) previewProgress = 0.9f;
                    if (GUILayout.Button("满蓄 100%")) previewProgress = 1f;
                }
            }
            var previewChanged = EditorGUI.EndChangeCheck();

            if (settingsChanged || previewChanged || Event.current.type == EventType.Repaint)
                ApplyPreview();
        }

        void ApplyPreview()
        {
            foreach (var item in targets)
            {
                var presentation = item as ChargeTelegraphPresentation;
                if (presentation)
                    presentation.EditorSetPreview(previewEnabled, previewProgress, previewFacingRight);
            }
        }

        void OnDisable()
        {
            foreach (var item in targets)
            {
                var presentation = item as ChargeTelegraphPresentation;
                if (presentation)
                    presentation.EditorSetPreview(false, previewProgress, previewFacingRight);
            }
        }
    }
}
