using UnityEditor;
using UnityEngine;

namespace MirrorTrial.Editor.Level
{
    [CustomPropertyDrawer(typeof(MirrorTrial.Level.ChineseLabelAttribute))]
    public class ChineseLabelDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var attr = attribute as MirrorTrial.Level.ChineseLabelAttribute;
            if (attr == null)
            {
                EditorGUI.PropertyField(position, property, label, true);
                return;
            }

            var newLabel = new GUIContent(attr.label, label.tooltip);
            EditorGUI.PropertyField(position, property, newLabel, true);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }
    }
}
