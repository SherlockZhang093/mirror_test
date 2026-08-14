using MirrorTrial.Player;
using Platformer.Mechanics;
using UnityEditor;
using UnityEngine;

namespace MirrorTrial.Editor.Player
{
    public sealed class PlayerStatsMonitorWindow : EditorWindow
    {
        Vector2 scroll;
        PlayerRuntimeStats runtime;
        PlayerInitialStats profile;
        UnityEditor.Editor profileEditor;
        double nextRefresh;

        [MenuItem("Tools/镜像试炼/玩家属性监控")]
        public static void Open()
        {
            var window = GetWindow<PlayerStatsMonitorWindow>();
            window.titleContent = new GUIContent("玩家属性监控");
            window.minSize = new Vector2(420f, 520f);
            window.Show();
        }

        void OnEnable()
        {
            LoadProfile();
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            if (profileEditor) DestroyImmediate(profileEditor);
        }

        void Update()
        {
            if (EditorApplication.timeSinceStartup < nextRefresh) return;
            nextRefresh = EditorApplication.timeSinceStartup + 0.15d;
            ResolveRuntime();
            Repaint();
        }

        void OnGUI()
        {
            DrawToolbar();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (Application.isPlaying) DrawRuntime();
            else DrawConfiguration();
            EditorGUILayout.EndScrollView();
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label(Application.isPlaying ? "运行时监控" : "初始属性配置", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("定位玩家", EditorStyles.toolbarButton))
            {
                var target = runtime ? runtime.gameObject : AssetDatabase.LoadAssetAtPath<GameObject>(PlayerStatsSetup.PlayerPrefabPath);
                Selection.activeObject = target;
                EditorGUIUtility.PingObject(target);
            }
            EditorGUILayout.EndHorizontal();
        }

        void DrawConfiguration()
        {
            if (!profile) LoadProfile();
            if (!profile)
            {
                EditorGUILayout.HelpBox("尚未生成玩家初始属性。", MessageType.Warning);
                if (GUILayout.Button("创建玩家属性系统", GUILayout.Height(32)))
                {
                    PlayerStatsSetup.EnsureSetup();
                    LoadProfile();
                }
                return;
            }

            EditorGUILayout.HelpBox("这里是玩家基础数值的统一入口。运行时成长不会写回这个资产。", MessageType.Info);
            if (!profileEditor) profileEditor = UnityEditor.Editor.CreateEditor(profile);
            profileEditor.OnInspectorGUI();
            EditorGUILayout.Space(8);
            if (GUILayout.Button("检查并修复玩家 Prefab", GUILayout.Height(30)))
                PlayerStatsSetup.EnsureSetup();
        }

        void DrawRuntime()
        {
            if (!runtime)
            {
                EditorGUILayout.HelpBox("正在等待玩家实例生成。请从 GameEntry 开始游戏。", MessageType.Info);
                return;
            }

            var health = runtime.Health;
            var reserve = runtime.Reserve;
            var tuning = runtime.Tuning;
            var growth = runtime.Growth;
            var gain = runtime.EnergyGain;
            var motor = runtime.Motor;

            DrawSection("生存", () =>
            {
                Row("当前生命", health ? $"{health.CurrentHP} / {health.maxHP}" : "--");
                Row("存活", YesNo(health && health.IsAlive));
                Row("无敌", YesNo(runtime.DamageReceiver && runtime.DamageReceiver.IsInvincible));
                Row("韧性", runtime.GetComponent<PlayerBodyStateController>() is PlayerBodyStateController body
                    ? $"{body.CurrentPoise:0.##} / {body.MaxPoise:0.##}"
                    : "--");
            });

            DrawSection("生命能量", () =>
            {
                Row("当前 / 上限", reserve ? $"{reserve.Current} / {reserve.Capacity}" : "--");
                Row("基础容量", reserve ? reserve.BaseCapacity.ToString() : "--");
                Row("会话容量加成", reserve ? reserve.SessionCapacityBonus.ToString() : "--");
                Row("临时容量加成", reserve ? reserve.TemporaryCapacityBonus.ToString() : "--");
                Row("伤害转化率", gain ? $"{gain.BaseDamageConversionRate:P0}" : "--");
                Row("转化倍率", gain ? gain.PlayerConversionMultiplier.ToString("0.##") : "--");
                Row("小数累计", gain ? gain.FractionalCarry.ToString("0.###") : "--");
            });

            DrawSection("成长与资源", () =>
            {
                Row("生命精华", runtime.LifeEssence.ToString());
                Row("已领取节点", runtime.ClaimedNodeCount.ToString());
                Row("最大生命成长", growth ? "+" + growth.MaxHealthBonus : "--");
                Row("攻击成长", growth ? "+" + growth.AttackDamageBonus : "--");
                Row("移速成长", growth ? "+" + growth.MoveSpeedBonus.ToString("0.##") : "--");
            });

            DrawSection("战斗与移动", () =>
            {
                Row("当前攻击力", tuning ? tuning.combat.attackDamage.ToString() : "--");
                Row("当前移动速度", tuning ? tuning.movement.moveSpeed.ToString("0.##") : "--");
                Row("实际速度", motor ? $"{motor.Velocity.x:0.##}, {motor.Velocity.y:0.##}" : "--");
                Row("动作状态", runtime.StateMachine ? runtime.StateMachine.CurrentState.ToString() : "--");
                Row("当前武器", runtime.Weapons ? runtime.Weapons.CurrentWeapon.ToString() : "--");
                Row("所在层", runtime.IsInMirror ? "镜中" : "现实");
            });

            DrawRuntimeWeapons(runtime.Weapons);
            DrawRuntimeAbilities(tuning);

            DrawDebugActions(health, reserve, growth);
        }

        void DrawRuntimeWeapons(PlayerWeaponController weapons)
        {
            DrawSection("武器（运行时可修改）", () =>
            {
                if (!weapons)
                {
                    EditorGUILayout.HelpBox("玩家缺少 PlayerWeaponController，无法修改武器。", MessageType.Warning);
                    return;
                }

                var swordUnlocked = weapons.IsWeaponAvailable(PlayerWeaponType.Sword);
                var bowUnlocked = weapons.IsWeaponAvailable(PlayerWeaponType.Bow);

                EditorGUI.BeginChangeCheck();
                var nextSwordUnlocked = EditorGUILayout.Toggle("解锁剑", swordUnlocked);
                var nextBowUnlocked = EditorGUILayout.Toggle("解锁弓", bowUnlocked);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(weapons, "修改运行时玩家武器");
                    weapons.SetWeaponAvailable(PlayerWeaponType.Sword, nextSwordUnlocked);
                    weapons.SetWeaponAvailable(PlayerWeaponType.Bow, nextBowUnlocked);
                    EditorUtility.SetDirty(weapons);
                }

                EditorGUILayout.Space(5f);
                EditorGUILayout.LabelField("立即装备", EditorStyles.miniBoldLabel);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("空手")) weapons.ForceEquipWeapon(PlayerWeaponType.Unarmed);
                using (new EditorGUI.DisabledScope(!weapons.IsWeaponAvailable(PlayerWeaponType.Sword)))
                    if (GUILayout.Button("剑")) weapons.ForceEquipWeapon(PlayerWeaponType.Sword);
                using (new EditorGUI.DisabledScope(!weapons.IsWeaponAvailable(PlayerWeaponType.Bow)))
                    if (GUILayout.Button("弓")) weapons.ForceEquipWeapon(PlayerWeaponType.Bow);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("解锁全部武器")) SetAllWeapons(weapons, true);
                if (GUILayout.Button("只保留空手")) SetAllWeapons(weapons, false);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.HelpBox("数字键 1 / 2 / 3 对应空手、剑、弓。停止播放后恢复原进度。", MessageType.None);
            });
        }

        static void SetAllWeapons(PlayerWeaponController weapons, bool unlocked)
        {
            if (!weapons) return;
            Undo.RecordObject(weapons, unlocked ? "解锁全部运行时武器" : "关闭运行时武器");
            weapons.SetWeaponAvailable(PlayerWeaponType.Sword, unlocked);
            weapons.SetWeaponAvailable(PlayerWeaponType.Bow, unlocked);
            EditorUtility.SetDirty(weapons);
        }

        void DrawRuntimeAbilities(PlayerTuning tuning)
        {
            DrawSection("能力与范围（运行时可修改）", () =>
            {
                if (!tuning)
                {
                    EditorGUILayout.HelpBox("玩家缺少 PlayerTuning，无法修改能力。", MessageType.Warning);
                    return;
                }

                var abilities = tuning.abilities;
                EditorGUI.BeginChangeCheck();

                var doubleJumpUnlocked = EditorGUILayout.Toggle("二段跳", abilities.doubleJumpUnlocked);
                var doubleJumpSpeed = abilities.doubleJumpSpeed;
                using (new EditorGUI.DisabledScope(!doubleJumpUnlocked))
                    doubleJumpSpeed = EditorGUILayout.FloatField("二段跳速度", doubleJumpSpeed);

                EditorGUILayout.Space(3f);
                var mirrorBladeUnlocked = EditorGUILayout.Toggle("镜刃", abilities.mirrorBladeUnlocked);
                var mirrorBladeRange = abilities.mirrorBladeRange;
                using (new EditorGUI.DisabledScope(!mirrorBladeUnlocked))
                    mirrorBladeRange = EditorGUILayout.FloatField("镜刃射程", mirrorBladeRange);

                EditorGUILayout.Space(3f);
                var echoDashUnlocked = EditorGUILayout.Toggle("回响冲刺", abilities.echoDashUnlocked);
                var echoDashDistance = abilities.echoDashDistance;
                using (new EditorGUI.DisabledScope(!echoDashUnlocked))
                    echoDashDistance = EditorGUILayout.FloatField("冲刺距离", echoDashDistance);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(tuning, "修改运行时玩家能力");
                    abilities.doubleJumpUnlocked = doubleJumpUnlocked;
                    abilities.doubleJumpSpeed = Mathf.Max(0f, doubleJumpSpeed);
                    abilities.mirrorBladeUnlocked = mirrorBladeUnlocked;
                    abilities.mirrorBladeRange = Mathf.Max(0f, mirrorBladeRange);
                    abilities.echoDashUnlocked = echoDashUnlocked;
                    abilities.echoDashDistance = Mathf.Max(0f, echoDashDistance);
                    EditorUtility.SetDirty(tuning);
                }

                EditorGUILayout.Space(5f);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("全部解锁")) SetAllAbilities(tuning, true);
                if (GUILayout.Button("全部关闭")) SetAllAbilities(tuning, false);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.HelpBox("仅修改当前运行实例；停止播放后会恢复原设置。", MessageType.None);
            });
        }

        static void SetAllAbilities(PlayerTuning tuning, bool unlocked)
        {
            if (!tuning) return;
            Undo.RecordObject(tuning, unlocked ? "解锁全部运行时能力" : "关闭全部运行时能力");
            tuning.abilities.doubleJumpUnlocked = unlocked;
            tuning.abilities.mirrorBladeUnlocked = unlocked;
            tuning.abilities.echoDashUnlocked = unlocked;
            EditorUtility.SetDirty(tuning);
        }

        void DrawDebugActions(Health health, PlayerHealthReserve reserve, PlayerSessionGrowth growth)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("开发调试", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("回满生命") && health) health.RestoreFull();
            if (GUILayout.Button("生命能量 +1") && reserve) reserve.Add(1);
            if (GUILayout.Button("清空生命能量") && reserve) reserve.Clear();
            EditorGUILayout.EndHorizontal();
            using (new EditorGUI.DisabledScope(!growth))
                if (GUILayout.Button("重置本次成长") && growth) growth.ResetGrowth();
        }

        void ResolveRuntime()
        {
            if (!Application.isPlaying) { runtime = null; return; }
            if (runtime) return;
            runtime = FindObjectOfType<PlayerRuntimeStats>(true);
        }

        void LoadProfile()
        {
            profile = AssetDatabase.LoadAssetAtPath<PlayerInitialStats>(PlayerStatsSetup.ProfilePath);
            if (profileEditor) DestroyImmediate(profileEditor);
            profileEditor = null;
        }

        void OnPlayModeChanged(PlayModeStateChange change)
        {
            runtime = null;
            Repaint();
        }

        static void DrawSection(string title, System.Action content)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            content();
            EditorGUILayout.EndVertical();
        }

        static void Row(string label, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(150f));
            EditorGUILayout.SelectableLabel(value, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            EditorGUILayout.EndHorizontal();
        }

        static string YesNo(bool value) => value ? "是" : "否";
    }
}
