using MirrorTrial.Player;
using UnityEditor;
using UnityEngine;

namespace MirrorTrial.Editor.Player
{
    [CustomEditor(typeof(PlayerTuning))]
    public class PlayerTuningEditor : UnityEditor.Editor
    {
        bool showMovement;
        bool showCombat;
        bool showHurt = true;
        bool showMirrorBlade;
        bool showEchoDash;
        bool showBow;

        SerializedProperty movement;
        SerializedProperty combat;
        SerializedProperty hurt;
        SerializedProperty abilities;

        void OnEnable()
        {
            movement = serializedObject.FindProperty("movement");
            combat = serializedObject.FindProperty("combat");
            hurt = serializedObject.FindProperty("hurt");
            abilities = serializedObject.FindProperty("abilities");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("玩家参数设置", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("修改玩家的移动、攻击和受伤手感。数值会直接影响游戏运行。", MessageType.Info);
            EditorGUILayout.Space(4);

            showMovement = DrawFoldout(showMovement, "移动与跳跃");
            if (showMovement)
                DrawMovement();

            showCombat = DrawFoldout(showCombat, "普通攻击");
            if (showCombat)
                DrawCombat();

            showHurt = DrawFoldout(showHurt, "受伤与击退");
            if (showHurt)
                DrawHurt();

            showMirrorBlade = DrawFoldout(showMirrorBlade, "镜刃能力");
            if (showMirrorBlade)
                DrawMirrorBlade();

            showEchoDash = DrawFoldout(showEchoDash, "回声冲刺");
            if (showEchoDash)
                DrawEchoDash();

            showBow = DrawFoldout(showBow, "弓箭");
            if (showBow)
                DrawBow();

            serializedObject.ApplyModifiedProperties();
        }

        void DrawMovement()
        {
            BeginSection();
            Field(movement, "moveSpeed", "移动速度");
            Field(movement, "acceleration", "地面加速度");
            Field(movement, "deceleration", "地面减速度");
            Field(movement, "airAcceleration", "空中加速度");
            Field(movement, "airDeceleration", "空中减速度");
            Field(movement, "jumpSpeed", "跳跃速度");
            Field(movement, "baseGravityModifier", "上升重力倍率");
            Field(movement, "fallGravityMultiplier", "下落重力倍率");
            Field(movement, "jumpCutMultiplier", "松开跳跃后的速度倍率");
            Field(movement, "coyoteTime", "离地后仍可跳跃时间");
            Field(movement, "jumpBufferTime", "提前按跳跃的保留时间");
            EndSection();
        }

        void DrawCombat()
        {
            BeginSection();
            Field(combat, "attackDamage", "基础伤害");
            Field(combat, "attackStartup", "攻击前摇");
            Field(combat, "attackActiveTime", "伤害有效时间");
            Field(combat, "attackRecovery", "攻击后摇");
            Field(combat, "attackMoveLock", "锁定移动时间");
            Field(combat, "attackKnockback", "击退敌人 X / Y");
            Field(combat, "hitStop", "命中停顿");
            EndSection();
        }

        void DrawHurt()
        {
            BeginSection();
            EditorGUILayout.HelpBox("玩家受击后的硬直、停顿和闪烁反馈。受击不会再推动角色。", MessageType.None);
            Field(hurt, "hurtLockTime", "受伤锁定时间");
            Field(hurt, "invincibleTime", "受伤后无敌时间");
            Field(hurt, "hurtHitStop", "受伤停顿");
            Field(hurt, "hurtTintTime", "受击染红时间");
            Field(hurt, "hurtTintColor", "受击染色");
            Field(hurt, "invincibleBlinkInterval", "无敌闪烁间隔");
            EditorGUILayout.HelpBox("染红和闪烁使用实时计时，因此受伤停顿期间也能清楚显示。", MessageType.Info);
            EndSection();
        }

        void DrawMirrorBlade()
        {
            BeginSection();
            Field(abilities, "mirrorBladeUnlocked", "已解锁");
            Field(abilities, "mirrorBladeDamage", "伤害");
            Field(abilities, "mirrorBladeStartup", "释放前摇");
            Field(abilities, "mirrorBladeRecovery", "释放后摇");
            Field(abilities, "mirrorBladeCooldown", "冷却时间");
            Field(abilities, "mirrorBladeSpeed", "飞行速度");
            Field(abilities, "mirrorBladeRange", "飞行距离");
            Field(abilities, "mirrorBladeKnockback", "击退 X / Y");
            Field(abilities, "mirrorBladeHitStop", "命中停顿");
            EndSection();
        }

        void DrawEchoDash()
        {
            BeginSection();
            Field(abilities, "echoDashUnlocked", "已解锁");
            Field(abilities, "echoDashDistance", "冲刺距离");
            Field(abilities, "echoDashDuration", "冲刺时间");
            Field(abilities, "echoDashInvincibleTime", "无敌时间");
            Field(abilities, "echoDashCooldown", "冷却时间");
            EndSection();
        }

        void DrawBow()
        {
            BeginSection();
            Field(abilities, "bowMinChargeTime", "最短蓄力时间");
            Field(abilities, "bowMaxChargeTime", "最长蓄力时间");
            Field(abilities, "bowRecovery", "射击后摇");
            Field(abilities, "bowMinDamage", "最低伤害");
            Field(abilities, "bowMaxDamage", "最高伤害");
            Field(abilities, "bowMinSpeed", "最低箭速");
            Field(abilities, "bowMaxSpeed", "最高箭速");
            Field(abilities, "bowRange", "射程");
            Field(abilities, "bowKnockback", "击退 X / Y");
            Field(abilities, "bowHitStop", "命中停顿");
            EndSection();
        }

        static bool DrawFoldout(bool expanded, string label)
        {
            return EditorGUILayout.Foldout(expanded, label, true, EditorStyles.foldoutHeader);
        }

        static void BeginSection()
        {
            EditorGUI.indentLevel++;
        }

        static void EndSection()
        {
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(2);
        }

        static void Field(SerializedProperty parent, string name, string label)
        {
            EditorGUILayout.PropertyField(parent.FindPropertyRelative(name), new GUIContent(label));
        }
    }
}
