using MirrorTrial.Player;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace MirrorTrial.Editor
{
    public sealed partial class PlayerInputComboEditorWindow : EditorWindow
    {
        const string DefaultPlayerPrefabPath = "Assets/MirrorTrial/Prefabs/Characters/Player_MirrorTrial.prefab";
        static readonly PlayerInputCommand[] CommandValues =
        {
            PlayerInputCommand.PrimaryAttack,
            PlayerInputCommand.WeaponSlot1,
            PlayerInputCommand.WeaponSlot2,
            PlayerInputCommand.WeaponSlot3,
            PlayerInputCommand.WeaponSlot4,
            PlayerInputCommand.Dodge
        };

        static readonly string[] CommandLabels =
        {
            "\u4e3b\u653b\u51fb",
            "\u526f\u653b\u51fb",
            "\u6b66\u5668\u6280\u80fd",
            "\u56de\u58f0\u51b2\u523a",
            "\u6b66\u5668\u69fd 1",
            "\u6b66\u5668\u69fd 2",
            "\u6b66\u5668\u69fd 3",
            "\u6b66\u5668\u69fd 4",
            "\u95ea\u907f"
        };

        static readonly PlayerMoveCategory[] MoveCategoryValues =
        {
            PlayerMoveCategory.Basic,
            PlayerMoveCategory.Sword,
            PlayerMoveCategory.Punch,
            PlayerMoveCategory.Kick,
            PlayerMoveCategory.Bow,
            PlayerMoveCategory.Skill
        };

        static readonly string[] MoveCategoryLabels =
        {
            "\u57fa\u7840",
            "\u5251\u672f",
            "\u62f3\u6cd5",
            "\u817f\u6cd5",
            "\u5f13\u7bad",
            "\u6280\u80fd"
        };

        static readonly PlayerComboCategory[] ComboCategoryValues =
        {
            PlayerComboCategory.Opener,
            PlayerComboCategory.Chain,
            PlayerComboCategory.Finisher,
            PlayerComboCategory.Launcher,
            PlayerComboCategory.Guard,
            PlayerComboCategory.Special
        };

        static readonly string[] ComboCategoryLabels =
        {
            "\u8d77\u624b",
            "\u8854\u63a5",
            "\u7ec8\u7ed3",
            "\u6d6e\u7a7a",
            "\u9632\u5fa1",
            "\u7279\u6b8a"
        };

        static readonly PlayerActionState[] ActionValues =
        {
            PlayerActionState.Attack,
            PlayerActionState.Cast,
            PlayerActionState.Dash,
            PlayerActionState.BowDraw,
            PlayerActionState.BowAim,
            PlayerActionState.BowFull,
            PlayerActionState.BowFire,
            PlayerActionState.ComboAttackA,
            PlayerActionState.ComboAttackB,
            PlayerActionState.ComboAttackC,
            PlayerActionState.ComboAttackD,
            PlayerActionState.PunchA,
            PlayerActionState.PunchB,
            PlayerActionState.PunchC,
            PlayerActionState.PunchD,
            PlayerActionState.KickA,
            PlayerActionState.KickB,
            PlayerActionState.KickC,
            PlayerActionState.SwordStandingSlash,
            PlayerActionState.SwordRunSlash,
            PlayerActionState.SwordGuard,
            PlayerActionState.SwordGuardImpact,
            PlayerActionState.SwordSprintSlash
        };

        static readonly string[] ActionLabels =
        {
            "\u666e\u901a\u653b\u51fb",
            "\u65bd\u6cd5",
            "\u51b2\u523a",
            "\u62c9\u5f13",
            "\u7784\u51c6",
            "\u6ee1\u5f13",
            "\u5c04\u7bad",
            "\u8fde\u51fb A",
            "\u8fde\u51fb B",
            "\u8fde\u51fb C",
            "\u8fde\u51fb D",
            "\u51fa\u62f3 A",
            "\u51fa\u62f3 B",
            "\u51fa\u62f3 C",
            "\u51fa\u62f3 D",
            "\u8e22\u51fb A",
            "\u8e22\u51fb B",
            "\u8e22\u51fb C",
            "\u7ad9\u7acb\u65a9",
            "\u5954\u8dd1\u65a9",
            "\u5251\u9632\u5fa1",
            "\u9632\u5fa1\u53cd\u51fb",
            "\u75be\u8dd1\u65a9"
        };

        PlayerInputReader input;
        PlayerCombat combat;
        PlayerWeaponController weapons;
        PlayerTuning tuning;
        PlayerBowCombat bowCombat;
        PlayerAbilityLoadout abilityLoadout;
        SerializedObject serializedInput;
        SerializedObject serializedCombat;
        SerializedObject serializedWeapons;
        SerializedObject serializedTuning;
        SerializedObject serializedBow;
        SerializedObject serializedAbilities;
        Vector2 scroll;
        int selectedComboIndex;
        int currentFrame;
        bool scenePreviewEnabled = true;
        bool animationPlaying;
        AnimationClip animationPreviewOverride;
        double lastAnimationUpdate;
        GameObject chargeEffectPreview;
        MirrorTrial.Combat.ChargeTelegraphPresentation chargeEffectPreviewPresentation;
        string chargeEffectPreviewMoveId;
        enum EditorTargetMode { Player, FirstMirrorBoss, MirrorBoss }
        EditorTargetMode targetMode;

        [MenuItem("Tools/镜像试炼/战斗/玩家按键与连招编辑器")]
        public static void OpenFromMenu()
        {
            var selected = Selection.activeGameObject;
            if (!selected || !selected.GetComponent<PlayerInputReader>())
                selected = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultPlayerPrefabPath);
            var window = GetWindow<PlayerInputComboEditorWindow>();
            window.titleContent = new GUIContent("按键与连招");
            window.minSize = new Vector2(780f, 480f);
            window.SetTarget(selected);
            window.Show();
        }

        void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.update += TickAnimationPreview;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += StopAnimationPreview;
            EditorApplication.quitting += StopAnimationPreview;
            EnableRuntimePreviewBridge();
            var selected = Selection.activeGameObject;
            if (!selected || !selected.GetComponent<PlayerInputReader>())
                selected = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultPlayerPrefabPath);
            SetTarget(selected);
        }

        void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            EditorApplication.update -= TickAnimationPreview;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= StopAnimationPreview;
            EditorApplication.quitting -= StopAnimationPreview;
            DisableRuntimePreviewBridge();
            StopAnimationPreview();
            DisposeChargeEffectPreview();
            DisposeBossPreview();
        }

        void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode)
            {
                StopAnimationPreview();
                DisposeChargeEffectPreview();
            }
            HandleRuntimePreviewPlayModeChange(state);
        }

        void OnSelectionChange()
        {
            if (Selection.activeGameObject && Selection.activeGameObject.GetComponent<PlayerInputReader>())
                SetTarget(Selection.activeGameObject);
        }

        void SetTarget(GameObject target)
        {
            if (EditorApplication.isPlaying && target && !PrefabUtility.IsPartOfPrefabAsset(target))
            {
                var runtimeComponent = target.GetComponent<PlayerCombat>();
                var sourceComponent = runtimeComponent ? PrefabUtility.GetCorrespondingObjectFromOriginalSource(runtimeComponent) : null;
                if (sourceComponent)
                    target = sourceComponent.gameObject;
            }
            StopAnimationPreview();
            DisposeChargeEffectPreview();
            input = target ? target.GetComponent<PlayerInputReader>() : null;
            combat = target ? target.GetComponent<PlayerCombat>() : null;
            weapons = target ? target.GetComponent<PlayerWeaponController>() : null;
            tuning = target ? target.GetComponent<PlayerTuning>() : null;
            bowCombat = target ? target.GetComponent<PlayerBowCombat>() : null;
            abilityLoadout = target ? target.GetComponent<PlayerAbilityLoadout>() : null;
            if (combat)
            {
                combat.EnsureComboData();
                EditorUtility.SetDirty(combat);
            }
            serializedInput = input ? new SerializedObject(input) : null;
            serializedCombat = combat ? new SerializedObject(combat) : null;
            serializedWeapons = weapons ? new SerializedObject(weapons) : null;
            serializedTuning = tuning ? new SerializedObject(tuning) : null;
            serializedBow = bowCombat ? new SerializedObject(bowCombat) : null;
            serializedAbilities = abilityLoadout ? new SerializedObject(abilityLoadout) : null;
            selectedComboIndex = Mathf.Max(0, selectedComboIndex);
            Repaint();
            SceneView.RepaintAll();
        }

        void OnGUI()
        {
            var nextMode = (EditorTargetMode)GUILayout.Toolbar(
                (int)targetMode,
                new[] { "玩家连招", "第一个 Boss 逐帧", "第二个 Boss 两阶段技能" },
                GUILayout.Height(25f));
            if (nextMode != targetMode)
            {
                StopAnimationPreview();
                targetMode = nextMode;
                if (targetMode == EditorTargetMode.FirstMirrorBoss)
                    MirrorTrial.Editor.Boss.MirrorBossFrameEditorWindow.Open();
            }
            if (targetMode == EditorTargetMode.FirstMirrorBoss)
            {
                EditorGUILayout.Space(12f);
                EditorGUILayout.HelpBox(
                    "第一个 Boss 使用独立的逐帧动作编辑界面，以避免与第二个 Boss 的两阶段预览状态互相干扰。",
                    MessageType.Info);
                if (GUILayout.Button("打开第一个 Boss 逐帧动作编辑器", GUILayout.Height(36f)))
                    MirrorTrial.Editor.Boss.MirrorBossFrameEditorWindow.Open();
                return;
            }
            if (targetMode == EditorTargetMode.MirrorBoss)
            {
                DrawBossModeGUI();
                return;
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUI.BeginChangeCheck();
                var nextInput = (PlayerInputReader)EditorGUILayout.ObjectField(input, typeof(PlayerInputReader), true, GUILayout.MinWidth(220f));
                if (EditorGUI.EndChangeCheck())
                    SetTarget(nextInput ? nextInput.gameObject : null);
                GUILayout.FlexibleSpace();
                if (AnimationMode.InAnimationMode() && GUILayout.Button("\u9000\u51fa\u52a8\u753b\u9884\u89c8", EditorStyles.toolbarButton, GUILayout.Width(110f)))
                    StopAnimationPreview();
                if (combat && GUILayout.Button("\u4fdd\u5b58\u5230 Prefab", EditorStyles.toolbarButton, GUILayout.Width(105f)))
                    SaveToPrefab();
                if (GUILayout.Button("使用当前选中", EditorStyles.toolbarButton, GUILayout.Width(110f)))
                    SetTarget(Selection.activeGameObject);
            }

            if (!input || !combat)
            {
                EditorGUILayout.HelpBox("请选择带有 PlayerInputReader 和 PlayerCombat 的玩家对象。", MessageType.Info);
                return;
            }

            EnsureSerializedObjects();
            if (serializedInput == null || serializedCombat == null)
                return;

            serializedInput.Update();
            serializedCombat.Update();
            serializedWeapons.Update();
            serializedTuning.Update();
            if (serializedBow != null) serializedBow.Update();
            if (serializedAbilities != null) serializedAbilities.Update();
            DrawRuntimePreviewPanel();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawMoveComboWorkspace();
            EditorGUILayout.EndScrollView();

            if (serializedInput.ApplyModifiedProperties())
                EditorUtility.SetDirty(input);
            if (serializedCombat.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(combat);
                SyncRuntimePreviewIfNeeded();
            }
            if (serializedWeapons.ApplyModifiedProperties())
                EditorUtility.SetDirty(weapons);
            if (serializedTuning.ApplyModifiedProperties())
                EditorUtility.SetDirty(tuning);
            if (serializedBow != null && serializedBow.ApplyModifiedProperties()) EditorUtility.SetDirty(bowCombat);
            if (serializedAbilities != null && serializedAbilities.ApplyModifiedProperties()) EditorUtility.SetDirty(abilityLoadout);
        }

        void EnsureSerializedObjects()
        {
            if (input && serializedInput == null)
                serializedInput = new SerializedObject(input);
            if (weapons && serializedWeapons == null)
                serializedWeapons = new SerializedObject(weapons);
            if (tuning && serializedTuning == null)
                serializedTuning = new SerializedObject(tuning);
            if (combat && serializedCombat == null)
            {
                combat.EnsureComboData();
                EditorUtility.SetDirty(combat);
                serializedCombat = new SerializedObject(combat);
            }
        }

        void DrawWeaponSlots()
        {
            EditorGUILayout.LabelField("\u6b66\u5668\u7cfb\u7edf", EditorStyles.boldLabel);
            var slots = serializedWeapons.FindProperty("slots");
            var activeSlot = serializedWeapons.FindProperty("activeSlotIndex");
            if (slots == null || activeSlot == null)
                return;
            activeSlot.intValue = Mathf.Clamp(activeSlot.intValue, 0, Mathf.Max(0, slots.arraySize - 1));
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (var i = 0; i < slots.arraySize; i++)
                    {
                        var slot = slots.GetArrayElementAtIndex(i);
                        var slotName = slot.FindPropertyRelative("name").stringValue;
                        var oldColor = GUI.backgroundColor;
                        if (i == activeSlot.intValue) GUI.backgroundColor = new Color(0.3f, 0.72f, 1f, 1f);
                        if (GUILayout.Button((i + 1) + "  " + slotName, GUILayout.Height(32f)))
                        {
                            activeSlot.intValue = i;
                            SelectComboSetForWeapon((PlayerWeaponType)slot.FindPropertyRelative("weaponType").enumValueIndex);
                            selectedComboIndex = 0;
                            currentFrame = 0;
                        }
                        GUI.backgroundColor = oldColor;
                    }
                }
                var selected = slots.GetArrayElementAtIndex(activeSlot.intValue);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(selected.FindPropertyRelative("name"), new GUIContent("\u69fd\u4f4d\u540d\u79f0"));
                    EditorGUILayout.PropertyField(selected.FindPropertyRelative("weaponType"), new GUIContent("\u6b66\u5668\u7c7b\u578b"));
                    EditorGUILayout.PropertyField(selected.FindPropertyRelative("available"), new GUIContent("\u9ed8\u8ba4\u53ef\u7528"));
                }
            }
        }

        void DrawSelectedWeaponSettings()
        {
            var slots = serializedWeapons.FindProperty("slots");
            var activeSlot = serializedWeapons.FindProperty("activeSlotIndex");
            if (slots == null || activeSlot == null || slots.arraySize == 0)
                return;
            var slot = slots.GetArrayElementAtIndex(Mathf.Clamp(activeSlot.intValue, 0, slots.arraySize - 1));
            var weapon = (PlayerWeaponType)slot.FindPropertyRelative("weaponType").enumValueIndex;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (weapon == PlayerWeaponType.Bow)
                    DrawBowSettings();
                else if (weapon == PlayerWeaponType.Sword)
                    DrawSwordSettings();
                else if (weapon == PlayerWeaponType.Unarmed)
                    EditorGUILayout.HelpBox("\u7a7a\u624b\uff1aJ \u62f3\u51fb\u4e09\u8fde\uff0cK \u91cd\u62f3\u3002\u4e0b\u65b9\u7f16\u8f91\u62f3\u51fb\u8fde\u62db\u548c\u9010\u5e27\u653b\u51fb\u6846\u3002", MessageType.Info);
                else
                    EditorGUILayout.HelpBox("\u7b2c 4 \u69fd\u4f4d\u4e3a\u672a\u6765\u6b66\u5668\u9884\u7559\u3002", MessageType.Info);
            }
        }

        void DrawBowSettings()
        {
            EditorGUILayout.LabelField("\u5f13\u7bad\u7cfb\u7edf", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("\u5f13\u7bad\u5df2\u7eb3\u5165\u8fde\u62db\u7f16\u8f91\u3002\u8bf7\u5728\u2018\u8fde\u62db\u7f16\u8f91\u2019\u9875\u9009\u62e9\u5f13\u7bad\u8fde\u62db\uff0c\u7f16\u8f91\u2018\u84c4\u529b\u5c04\u7bad\u2019\u590d\u5408\u62db\u5f0f\u3002", MessageType.Info);
        }

        void DrawSwordSettings()
        {
            EditorGUILayout.LabelField("\u5251\u672f\u7cfb\u7edf", EditorStyles.boldLabel);
            var abilities = serializedTuning.FindProperty("abilities");
            if (serializedAbilities != null)
                EditorGUILayout.PropertyField(serializedAbilities.FindProperty("mirrorBladeClip"), new GUIContent("\u955c\u5203\u52a8\u753b"));
            EditorGUILayout.PropertyField(serializedCombat.FindProperty("swordGuardClip"), new GUIContent("\u9632\u5fa1\u52a8\u753b"));
            EditorGUILayout.PropertyField(serializedCombat.FindProperty("swordGuardImpactClip"), new GUIContent("\u683c\u6321\u53cd\u9988\u52a8\u753b"));
            EditorGUILayout.PropertyField(abilities.FindPropertyRelative("mirrorBladeDamage"), new GUIContent("\u955c\u5203\u4f24\u5bb3"));
            EditorGUILayout.PropertyField(abilities.FindPropertyRelative("mirrorBladeCooldown"), new GUIContent("\u955c\u5203\u51b7\u5374"));
            EditorGUILayout.PropertyField(abilities.FindPropertyRelative("mirrorBladeSpeed"), new GUIContent("\u955c\u5203\u901f\u5ea6"));
            EditorGUILayout.PropertyField(abilities.FindPropertyRelative("mirrorBladeRange"), new GUIContent("\u955c\u5203\u5c04\u7a0b"));
            EditorGUILayout.HelpBox("\u5e73 A \u7684\u5251\u672f\u5206\u6b67\u5df2\u7eb3\u5165\u8fde\u62db\u56fe\uff1aRun \u72b6\u6001\u8fdb\u5165\u5954\u8dd1\u65a9\uff0c\u5176\u4ed6\u72b6\u6001\u8d70\u9ed8\u8ba4\u8d77\u624b\u3002K \u6309\u4f4f\u9632\u5fa1\uff1bL \u955c\u5203\u3002", MessageType.None);
        }

        void SelectComboSetForWeapon(PlayerWeaponType weaponType)
        {
            var sets = serializedCombat.FindProperty("comboSets");
            var activeSet = serializedCombat.FindProperty("activeComboSetIndex");
            if (sets == null || activeSet == null)
                return;
            for (var i = 0; i < sets.arraySize; i++)
            {
                var type = sets.GetArrayElementAtIndex(i).FindPropertyRelative("weaponType");
                if (type != null && type.enumValueIndex == (int)weaponType)
                {
                    activeSet.intValue = i;
                    return;
                }
            }
        }

        void DrawInputBindings()
        {
            EditorGUILayout.LabelField("按键映射", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(serializedInput.FindProperty("horizontalAxis"), new GUIContent("水平移动轴"));
                EditorGUILayout.PropertyField(serializedInput.FindProperty("jumpButton"), new GUIContent("跳跃按钮"));
                var bindings = serializedInput.FindProperty("bindings");
                DrawBindingHeader();
                for (var i = 0; i < bindings.arraySize; i++)
                {
                    var row = bindings.GetArrayElementAtIndex(i);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        DrawCommandPopup(row.FindPropertyRelative("command"), GUIContent.none, GUILayout.Width(150f));
                        EditorGUILayout.PropertyField(row.FindPropertyRelative("buttonName"), GUIContent.none, GUILayout.MinWidth(150f));
                        EditorGUILayout.PropertyField(row.FindPropertyRelative("key"), GUIContent.none, GUILayout.Width(130f));
                        if (GUILayout.Button("删除", GUILayout.Width(48f)))
                        {
                            bindings.DeleteArrayElementAtIndex(i);
                            break;
                        }
                    }
                }
                if (GUILayout.Button("添加按键映射", GUILayout.Width(120f)))
                    bindings.InsertArrayElementAtIndex(bindings.arraySize);
            }
        }

        void DrawBindingHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("命令", EditorStyles.miniBoldLabel, GUILayout.Width(150f));
                GUILayout.Label("输入管理器按钮", EditorStyles.miniBoldLabel, GUILayout.MinWidth(150f));
                GUILayout.Label("直接按键", EditorStyles.miniBoldLabel, GUILayout.Width(130f));
                GUILayout.Space(52f);
            }
        }

        SerializedProperty GetActiveComboProperty()
        {
            if (serializedCombat == null)
                return null;
            var sets = serializedCombat.FindProperty("comboSets");
            var activeIndex = serializedCombat.FindProperty("activeComboSetIndex");
            if (sets == null || activeIndex == null || sets.arraySize == 0)
                return serializedCombat.FindProperty("combo");
            activeIndex.intValue = Mathf.Clamp(activeIndex.intValue, 0, sets.arraySize - 1);
            return sets.GetArrayElementAtIndex(activeIndex.intValue).FindPropertyRelative("steps");
        }

        SerializedProperty GetPreviewMoveProperty()
        {
            if (workspacePage == 0)
            {
                var selectedMove = GetSelectedGraphMoveProperty();
                if (selectedMove != null)
                    return selectedMove;
            }
            var combo = GetActiveComboProperty();
            if (combo == null || combo.arraySize == 0)
                return null;
            selectedComboIndex = Mathf.Clamp(selectedComboIndex, 0, combo.arraySize - 1);
            return combo.GetArrayElementAtIndex(selectedComboIndex);
        }

        void DrawComboSetToolbar()
        {
            var sets = serializedCombat.FindProperty("comboSets");
            var activeIndex = serializedCombat.FindProperty("activeComboSetIndex");
            if (sets == null || activeIndex == null)
                return;
            if (sets.arraySize == 0 && combat)
            {
                combat.EnsureComboData();
                serializedCombat = new SerializedObject(combat);
                serializedCombat.Update();
                sets = serializedCombat.FindProperty("comboSets");
                activeIndex = serializedCombat.FindProperty("activeComboSetIndex");
            }
            if (sets == null || sets.arraySize == 0)
                return;
            activeIndex.intValue = Mathf.Clamp(activeIndex.intValue, 0, sets.arraySize - 1);
            var labels = new string[sets.arraySize];
            for (var i = 0; i < sets.arraySize; i++)
            {
                var name = sets.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue;
                labels[i] = string.IsNullOrEmpty(name) ? "\u8fde\u62db\u65b9\u6848 " + (i + 1) : name;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    activeIndex.intValue = EditorGUILayout.Popup(new GUIContent("\u5f53\u524d\u8fde\u62db\u65b9\u6848"), activeIndex.intValue, labels);
                    if (GUILayout.Button("\u65b0\u5efa", GUILayout.Width(52f)))
                    {
                        sets.InsertArrayElementAtIndex(sets.arraySize);
                        var created = sets.GetArrayElementAtIndex(sets.arraySize - 1);
                        created.FindPropertyRelative("name").stringValue = "\u65b0\u8fde\u62db " + sets.arraySize;
                        created.FindPropertyRelative("steps").ClearArray();
                        activeIndex.intValue = sets.arraySize - 1;
                        selectedComboIndex = 0;
                        currentFrame = 0;
                    }
                    if (GUILayout.Button("\u590d\u5236", GUILayout.Width(52f)))
                    {
                        var sourceIndex = activeIndex.intValue;
                        sets.InsertArrayElementAtIndex(sourceIndex);
                        activeIndex.intValue = sourceIndex + 1;
                        var copied = sets.GetArrayElementAtIndex(activeIndex.intValue);
                        copied.FindPropertyRelative("name").stringValue = labels[sourceIndex] + " \u526f\u672c";
                        selectedComboIndex = 0;
                        currentFrame = 0;
                    }
                    using (new EditorGUI.DisabledScope(sets.arraySize <= 1))
                    {
                        if (GUILayout.Button("\u5220\u9664", GUILayout.Width(52f)))
                        {
                            sets.DeleteArrayElementAtIndex(activeIndex.intValue);
                            activeIndex.intValue = Mathf.Clamp(activeIndex.intValue, 0, sets.arraySize - 1);
                            selectedComboIndex = 0;
                            currentFrame = 0;
                        }
                    }
                }
                var selectedSet = sets.GetArrayElementAtIndex(activeIndex.intValue);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(selectedSet.FindPropertyRelative("name"), new GUIContent("\u65b9\u6848\u540d\u79f0"));
                    EditorGUILayout.PropertyField(selectedSet.FindPropertyRelative("weaponType"), new GUIContent("\u6240\u5c5e\u6b66\u5668"));
                }
            }
        }

        void SaveToPrefab()
        {
            if (!combat)
                return;
            StopAnimationPreview();
            if (serializedInput != null) serializedInput.ApplyModifiedProperties();
            if (serializedCombat != null) serializedCombat.ApplyModifiedProperties();
            if (serializedWeapons != null) serializedWeapons.ApplyModifiedProperties();
            if (serializedTuning != null) serializedTuning.ApplyModifiedProperties();
            if (serializedBow != null) serializedBow.ApplyModifiedProperties();
            if (serializedAbilities != null) serializedAbilities.ApplyModifiedProperties();

            if (PrefabUtility.IsPartOfPrefabAsset(combat.gameObject))
            {
                EditorUtility.SetDirty(combat);
                AssetDatabase.SaveAssets();
                ShowNotification(new GUIContent("已保存 Prefab 资源"));
                return;
            }
            var root = PrefabUtility.GetOutermostPrefabInstanceRoot(combat.gameObject);
            if (root)
            {
                PrefabUtility.ApplyPrefabInstance(root, InteractionMode.UserAction);
                ShowNotification(new GUIContent("已应用到 Prefab"));
                return;
            }
            ShowNotification(new GUIContent("当前对象不是 Prefab"));
        }

        PlayerWeaponType GetSelectedWeaponType()
        {
            if (serializedWeapons == null)
                return PlayerWeaponType.Unarmed;
            var slots = serializedWeapons.FindProperty("slots");
            var active = serializedWeapons.FindProperty("activeSlotIndex");
            if (slots == null || active == null || slots.arraySize == 0)
                return PlayerWeaponType.Unarmed;
            var slot = slots.GetArrayElementAtIndex(Mathf.Clamp(active.intValue, 0, slots.arraySize - 1));
            return (PlayerWeaponType)slot.FindPropertyRelative("weaponType").enumValueIndex;
        }

        void DrawCombo()
        {
            var selectedWeapon = GetSelectedWeaponType();
            if (selectedWeapon == PlayerWeaponType.Bow || selectedWeapon == PlayerWeaponType.Reserved)
                return;
            EditorGUILayout.LabelField("连招链", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(serializedCombat.FindProperty("attackHitbox"), new GUIContent("攻击判定框"));
                DrawComboSetToolbar();
                var combo = GetActiveComboProperty();
                if (combo == null)
                    return;
                for (var i = 0; i < combo.arraySize; i++)
                {
                    var step = combo.GetArrayElementAtIndex(i);
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField("第 " + (i + 1) + " 段", EditorStyles.boldLabel, GUILayout.Width(70f));
                            EditorGUILayout.PropertyField(step.FindPropertyRelative("name"), GUIContent.none);
                            if (GUILayout.Button("上移", GUILayout.Width(44f)) && i > 0)
                                combo.MoveArrayElement(i, i - 1);
                            if (GUILayout.Button("下移", GUILayout.Width(44f)) && i < combo.arraySize - 1)
                                combo.MoveArrayElement(i, i + 1);
                            if (GUILayout.Button("删除", GUILayout.Width(48f)))
                            {
                                combo.DeleteArrayElementAtIndex(i);
                                break;
                            }
                        }
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            DrawMoveCategoryPopup(step.FindPropertyRelative("moveCategory"), new GUIContent("\u62db\u5f0f\u5927\u7c7b"));
                            DrawComboCategoryPopup(step.FindPropertyRelative("comboCategory"), new GUIContent("\u8fde\u62db\u5c0f\u7c7b"));
                        }
                        DrawCommandPopup(step.FindPropertyRelative("input"), new GUIContent("\u8fdb\u5165\u4e0b\u4e00\u6bb5\u6240\u9700\u8f93\u5165"));
                        EditorGUILayout.PropertyField(step.FindPropertyRelative("animationClip"), new GUIContent("\u52a8\u753b\u7247\u6bb5"));
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.PropertyField(step.FindPropertyRelative("animationFrameCount"), new GUIContent("\u603b\u5e27\u6570"));
                            EditorGUILayout.PropertyField(step.FindPropertyRelative("mirrorHitboxByFacing"), new GUIContent("\u6309\u671d\u5411\u955c\u50cf"));
                        }
                        DrawHitboxKeyEditor(step, i);
                        DrawHitFeedbackEditor(step);
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.PropertyField(step.FindPropertyRelative("startup"), new GUIContent("\u524d\u6447"));
                            EditorGUILayout.PropertyField(step.FindPropertyRelative("activeTime"), new GUIContent("有效时间"));
                            EditorGUILayout.PropertyField(step.FindPropertyRelative("recovery"), new GUIContent("后摇"));
                        }
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.PropertyField(step.FindPropertyRelative("comboWindowStart"), new GUIContent("连招窗口开始"));
                            EditorGUILayout.PropertyField(step.FindPropertyRelative("comboWindowEnd"), new GUIContent("连招窗口结束"));
                        }
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.PropertyField(step.FindPropertyRelative("damageMultiplier"), new GUIContent("伤害倍率"));
                            EditorGUILayout.PropertyField(step.FindPropertyRelative("knockbackMultiplier"), new GUIContent("击退倍率"));
                            EditorGUILayout.PropertyField(step.FindPropertyRelative("lockMovement"), new GUIContent("锁定移动"));
                        }
                    }
                }
                if (GUILayout.Button("添加连招段", GUILayout.Width(120f)))
                    combo.InsertArrayElementAtIndex(combo.arraySize);
            }
        }

        void DrawHitFeedbackEditor(SerializedProperty step)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("\u547d\u4e2d\u5224\u5b9a\u4e0e\u53cd\u9988", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("\u8fd9\u4e9b\u9009\u9879\u5c5e\u4e8e\u4e0a\u65b9\u9010\u5e27\u653b\u51fb\u6846\uff1a\u8be5\u653b\u51fb\u6846\u547d\u4e2d\u65f6\u6267\u884c\u3002\u591a\u76ee\u6807\u65f6\u6bcf\u4e2a\u76ee\u6807\u90fd\u53d7\u4f24\u5e76\u64ad\u653e\u547d\u4e2d\u7279\u6548\uff0c\u987f\u5e27/\u955c\u5934/\u97f3\u6548/\u73a9\u5bb6\u53cd\u4f5c\u7528\u53ea\u89e6\u53d1\u4e00\u6b21\u3002", MessageType.Info);

                EditorGUILayout.PropertyField(step.FindPropertyRelative("attackType"), new GUIContent("\u6280\u80fd\u653b\u51fb\u7c7b\u578b"));
                EditorGUILayout.PropertyField(step.FindPropertyRelative("hitFlashType"),
                    new GUIContent("\u53d7\u51fb\u95ea\u5149", "\u8be5\u62db\u547d\u4e2d\u654c\u5175\u65f6\u5168\u8eab\u95ea\u767d\u6216\u95ea\u7ea2\u3002"));

                var targetReaction = step.FindPropertyRelative("enableTargetReaction");
                EditorGUILayout.PropertyField(targetReaction, new GUIContent("\u542f\u7528\u76ee\u6807\u53cd\u5e94"));
                if (targetReaction.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(step.FindPropertyRelative("targetReaction"), new GUIContent("\u53cd\u5e94\u7c7b\u578b\uff08\u6280\u80fd\u76f4\u63a5\u51b3\u5b9a\uff09"));
                    var customKnockback = step.FindPropertyRelative("useCustomKnockback");
                    EditorGUILayout.PropertyField(customKnockback, new GUIContent("\u81ea\u5b9a\u4e49\u51fb\u9000/\u51fb\u98de\u5411\u91cf"));
                    if (customKnockback.boolValue)
                        EditorGUILayout.PropertyField(step.FindPropertyRelative("customKnockback"), new GUIContent("\u6c34\u5e73 X / \u5782\u76f4 Y"));
                    else
                        EditorGUILayout.PropertyField(step.FindPropertyRelative("knockbackMultiplier"), new GUIContent("\u4f7f\u7528\u73a9\u5bb6\u57fa\u7840\u51fb\u9000\u500d\u7387"));
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PropertyField(step.FindPropertyRelative("interruptPower"), new GUIContent("\u4e2d\u65ad\u5f3a\u5ea6"));
                        EditorGUILayout.PropertyField(step.FindPropertyRelative("poiseDamage"), new GUIContent("\u524a\u97e7"));
                        EditorGUILayout.PropertyField(step.FindPropertyRelative("breaksSuperArmor"), new GUIContent("\u7834\u9738\u4f53"));
                    }
                    EditorGUI.indentLevel--;
                }

                var feedback = step.FindPropertyRelative("hitFeedback");
                if (feedback == null) return;

                var effectEnabled = feedback.FindPropertyRelative("enableHitEffect");
                EditorGUILayout.PropertyField(effectEnabled, new GUIContent("\u64ad\u653e\u547d\u4e2d\u7279\u6548"));
                if (effectEnabled.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(feedback.FindPropertyRelative("hitEffectPrefab"), new GUIContent("\u7ed1\u5b9a\u7279\u6548 Prefab"));
                    EditorGUILayout.PropertyField(feedback.FindPropertyRelative("hitEffectOffset"), new GUIContent("\u63a5\u89e6\u70b9\u504f\u79fb"));
                    EditorGUILayout.PropertyField(feedback.FindPropertyRelative("mirrorHitEffectByDirection"), new GUIContent("\u6309\u653b\u51fb\u65b9\u5411\u7ffb\u8f6c"));
                    EditorGUILayout.PropertyField(feedback.FindPropertyRelative("hitEffectLifetime"), new GUIContent("\u5b58\u5728\u65f6\u95f4"));
                    EditorGUI.indentLevel--;
                }

                var hitStopEnabled = feedback.FindPropertyRelative("enableHitStop");
                EditorGUILayout.PropertyField(hitStopEnabled, new GUIContent("\u5168\u5c40\u547d\u4e2d\u987f\u5e27"));
                if (hitStopEnabled.boolValue)
                {
                    EditorGUI.indentLevel++;
                    var useDefault = feedback.FindPropertyRelative("useAttackTypeDefaultHitStop");
                    EditorGUILayout.PropertyField(useDefault, new GUIContent("\u4f7f\u7528\u666e\u901a/\u91cd\u51fb\u9ed8\u8ba4\u65f6\u95f4"));
                    if (!useDefault.boolValue)
                        EditorGUILayout.PropertyField(feedback.FindPropertyRelative("hitStopDuration"), new GUIContent("\u987f\u5e27\u65f6\u95f4"));
                    EditorGUI.indentLevel--;
                }

                var cameraEnabled = feedback.FindPropertyRelative("enableCameraFeedback");
                EditorGUILayout.PropertyField(cameraEnabled, new GUIContent("\u955c\u5934\u53cd\u9988"));
                if (cameraEnabled.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(feedback.FindPropertyRelative("cameraPower"), new GUIContent("\u955c\u5934\u5f3a\u5ea6"));
                    EditorGUI.indentLevel--;
                }

                var soundEnabled = feedback.FindPropertyRelative("enableHitSound");
                EditorGUILayout.PropertyField(soundEnabled, new GUIContent("\u547d\u4e2d\u97f3\u6548"));
                if (soundEnabled.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(feedback.FindPropertyRelative("hitSound"), new GUIContent("AudioClip"));
                    EditorGUILayout.PropertyField(feedback.FindPropertyRelative("hitSoundVolume"), new GUIContent("\u97f3\u91cf"));
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.PropertyField(feedback.FindPropertyRelative("notifyPlayerReaction"), new GUIContent("\u901a\u77e5 Player \u6267\u884c\u53cd\u4f5c\u7528"));
            }
        }
        void DrawHitboxKeyEditor(SerializedProperty step, int stepIndex)
        {
            animationPreviewOverride = null;
            var frameCountProperty = step.FindPropertyRelative("animationFrameCount");
            var frameRateProperty = step.FindPropertyRelative("animationFrameRate");
            var keys = step.FindPropertyRelative("hitboxKeys");
            var action = (PlayerActionState)step.FindPropertyRelative("animationState").enumValueIndex;
            var clip = animationPreviewOverride;
            if (!clip)
            {
                var clipProperty = step.FindPropertyRelative("animationClip");
                clip = clipProperty != null ? clipProperty.objectReferenceValue as AnimationClip : null;
                if (!clip) clip = GetAnimationClip(action);
            }
            var frameRate = clip ? Mathf.Max(1, Mathf.RoundToInt(clip.frameRate)) : Mathf.Max(1, frameRateProperty.intValue);
            if (clip)
            {
                frameRateProperty.intValue = frameRate;
                frameCountProperty.intValue = Mathf.Max(1, Mathf.FloorToInt(clip.length * frameRate) + 1);
            }
            var maxFrame = Mathf.Max(0, frameCountProperty.intValue - 1);
            if (selectedComboIndex == stepIndex)
                currentFrame = Mathf.Clamp(currentFrame, 0, maxFrame);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var oldBackground = GUI.backgroundColor;
                    if (selectedComboIndex == stepIndex)
                        GUI.backgroundColor = new Color(0.35f, 0.7f, 1f, 1f);
                    if (GUILayout.Button("\u9884\u89c8\u6b64\u52a8\u4f5c", EditorStyles.miniButton, GUILayout.Width(78f)))
                    {
                        selectedComboIndex = stepIndex;
                        currentFrame = 0;
                        animationPlaying = false;
                        SampleAnimationFrame(clip, currentFrame, frameRate);
                    }
                    GUI.backgroundColor = oldBackground;
                    EditorGUI.BeginDisabledGroup(selectedComboIndex != stepIndex);
                    if (GUILayout.Button("|<", GUILayout.Width(30f)))
                    {
                        animationPlaying = false;
                        currentFrame = 0;
                        SampleAnimationFrame(clip, currentFrame, frameRate);
                    }
                    if (GUILayout.Button(animationPlaying ? "||" : ">", GUILayout.Width(30f)))
                    {
                        animationPlaying = !animationPlaying;
                        lastAnimationUpdate = EditorApplication.timeSinceStartup;
                        SampleAnimationFrame(clip, currentFrame, frameRate);
                    }
                    if (GUILayout.Button("<", GUILayout.Width(28f)))
                    {
                        animationPlaying = false;
                        currentFrame = Mathf.Max(0, currentFrame - 1);
                        SampleAnimationFrame(clip, currentFrame, frameRate);
                    }
                    if (GUILayout.Button(">", GUILayout.Width(28f)))
                    {
                        animationPlaying = false;
                        currentFrame = Mathf.Min(maxFrame, currentFrame + 1);
                        SampleAnimationFrame(clip, currentFrame, frameRate);
                    }
                    EditorGUI.BeginChangeCheck();
                    currentFrame = EditorGUILayout.IntSlider(new GUIContent("\u5f53\u524d\u5e27"), currentFrame, 0, maxFrame);
                    if (EditorGUI.EndChangeCheck())
                    {
                        animationPlaying = false;
                        SampleAnimationFrame(clip, currentFrame, frameRate);
                    }
                    if (GUILayout.Button("\u6dfb\u52a0/\u66f4\u65b0 Key", GUILayout.Width(110f)))
                        AddOrUpdateCurrentFrameKey(step, currentFrame);
                    if (GUILayout.Button("\u5220\u9664 Key", GUILayout.Width(78f)))
                        DeleteKeyAtFrame(keys, currentFrame);
                    EditorGUI.EndDisabledGroup();
                }

                if (selectedComboIndex == stepIndex)
                {
                    EditorGUILayout.LabelField("\u52a8\u753b\u7247\u6bb5\uff1a" + (clip ? clip.name + "  " + clip.length.ToString("0.###") + "s" : "\u672a\u627e\u5230\u8be5\u52a8\u4f5c\u5bf9\u5e94\u7684 AnimationClip"), EditorStyles.miniLabel);
                    DrawFrameHitboxTimeline(step, keys, clip, frameRate, maxFrame);
                }

                var key = FindKeyAtFrame(keys, currentFrame);
                var effectiveKey = key ?? FindLastKeyAtOrBeforeFrame(keys, currentFrame);
                var drawsHitbox = effectiveKey != null && effectiveKey.FindPropertyRelative("enabled").boolValue;
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    var nextDrawsHitbox = EditorGUILayout.Toggle(new GUIContent("\u672c\u5e27\u7ed8\u5236\u653b\u51fb\u6846"), drawsHitbox);
                    if (EditorGUI.EndChangeCheck())
                    {
                        if (key == null)
                        {
                            AddOrUpdateCurrentFrameKey(step, currentFrame);
                            key = FindKeyAtFrame(keys, currentFrame);
                        }
                        key.FindPropertyRelative("enabled").boolValue = nextDrawsHitbox;
                        SceneView.RepaintAll();
                    }
                    if (key != null)
                        EditorGUILayout.PropertyField(key.FindPropertyRelative("interpolation"), new GUIContent("\u5230\u4e0b\u4e00\u4e2a Key"));
                }
                EditorGUILayout.HelpBox("\u9009\u62e9\u52a8\u4f5c\u540e\u53ef\u64ad\u653e\u6216\u9010\u5e27\u67e5\u770b\u771f\u5b9e\u52a8\u753b\u59ff\u52bf\uff1bScene \u4e2d\u62d6\u52a8\u653b\u51fb\u6846\u4f1a\u5728\u5f53\u524d\u5e27\u521b\u5efa/\u66f4\u65b0 Key\u3002", MessageType.None);
            }
        }

        void DrawFrameHitboxTimeline(SerializedProperty step, SerializedProperty keys, AnimationClip clip, int frameRate, int maxFrame)
        {
            EditorGUILayout.LabelField("\u9010\u5e27\u653b\u51fb\u6846\uff08\u25cf = Key\uff09", EditorStyles.miniBoldLabel);
            var columns = Mathf.Max(6, Mathf.FloorToInt((position.width - 80f) / 42f));
            var oldBackground = GUI.backgroundColor;
            for (var frame = 0; frame <= maxFrame; frame++)
            {
                if (frame % columns == 0)
                    EditorGUILayout.BeginHorizontal();
                var exactKey = FindKeyAtFrame(keys, frame);
                var effectiveKey = exactKey ?? FindLastKeyAtOrBeforeFrame(keys, frame);
                var enabled = effectiveKey != null && effectiveKey.FindPropertyRelative("enabled").boolValue;
                if (frame == currentFrame)
                    GUI.backgroundColor = new Color(0.25f, 0.9f, 0.55f, 1f);
                else if (enabled)
                    GUI.backgroundColor = new Color(1f, 0.58f, 0.16f, 1f);
                else
                    GUI.backgroundColor = new Color(0.48f, 0.48f, 0.48f, 1f);
                var label = (exactKey != null ? "\u25cf" : string.Empty) + frame;
                if (GUILayout.Button(label, EditorStyles.miniButton, GUILayout.Width(38f)))
                {
                    selectedComboIndex = Mathf.Max(0, selectedComboIndex);
                    currentFrame = frame;
                    animationPlaying = false;
                    SampleAnimationFrame(clip, currentFrame, frameRate);
                    SceneView.RepaintAll();
                }
                GUI.backgroundColor = oldBackground;
                if (frame % columns == columns - 1 || frame == maxFrame)
                    EditorGUILayout.EndHorizontal();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                var activeStyle = new GUIStyle(EditorStyles.miniLabel); activeStyle.normal.textColor = new Color(1f, 0.65f, 0.2f);
                var inactiveStyle = new GUIStyle(EditorStyles.miniLabel); inactiveStyle.normal.textColor = Color.gray;
                GUILayout.Label("\u25a0 \u653b\u51fb\u6846\u751f\u6548", activeStyle, GUILayout.Width(105f));
                GUILayout.Label("\u25a0 \u653b\u51fb\u6846\u5173\u95ed", inactiveStyle, GUILayout.Width(105f));
            }
        }

        void TickAnimationPreview()
        {
            TickChargeEffectPreview();
            if (targetMode == EditorTargetMode.FirstMirrorBoss) return;
            if (targetMode == EditorTargetMode.MirrorBoss)
            {
                TickBossAnimationPreview();
                return;
            }
            if (!animationPlaying || !combat || Application.isPlaying)
                return;
            EnsureSerializedObjects();
            if (serializedCombat == null)
                return;
            serializedCombat.Update();
            var step = GetPreviewMoveProperty();
            if (step == null)
                return;
            var action = (PlayerActionState)step.FindPropertyRelative("animationState").enumValueIndex;
            var clip = animationPreviewOverride;
            if (!clip)
            {
                var clipProperty = step.FindPropertyRelative("animationClip");
                clip = clipProperty != null ? clipProperty.objectReferenceValue as AnimationClip : null;
                if (!clip) clip = GetAnimationClip(action);
            }
            var frameRate = clip ? Mathf.Max(1, Mathf.RoundToInt(clip.frameRate)) : 1;
            if (!clip)
            {
                animationPlaying = false;
                return;
            }
            var now = EditorApplication.timeSinceStartup;
            var framesToAdvance = Mathf.FloorToInt(Mathf.Max(0f, (float)(now - lastAnimationUpdate)) * frameRate);
            if (framesToAdvance <= 0)
                return;
            lastAnimationUpdate += framesToAdvance / (double)frameRate;
            var maxFrame = Mathf.Max(0, Mathf.CeilToInt(clip.length * frameRate));
            currentFrame = (currentFrame + framesToAdvance) % (maxFrame + 1);
            SampleAnimationFrame(clip, currentFrame, frameRate);
            Repaint();
        }

        void SampleAnimationFrame(AnimationClip clip, int frame, int frameRate)
        {
            if (!combat || !clip)
                return;
            if (Application.isPlaying)
            {
                animationPlaying = false;
                ShowNotification(new GUIContent("\u8fd0\u884c\u4e2d\u8bf7\u4f7f\u7528\u4e0a\u65b9\u7684\u5b9e\u673a\u9884\u89c8\uff1b\u9010\u5e27\u52a8\u4f5c\u9884\u89c8\u9700\u8981\u5148\u9000\u51fa\u8fd0\u884c\u6a21\u5f0f"));
                return;
            }
            if (EditorUtility.IsPersistent(combat.gameObject))
            {
                var sceneCombat = FindScenePreviewCombat(combat);
                if (!sceneCombat)
                {
                    animationPlaying = false;
                    ShowNotification(new GUIContent("\u573a\u666f\u4e2d\u6ca1\u6709\u8fd9\u4e2a Player Prefab \u7684\u5b9e\u4f8b\uff0c\u65e0\u6cd5\u9884\u89c8\u52a8\u4f5c"));
                    return;
                }
                SetTarget(sceneCombat.gameObject);
            }
            var animator = GetEditorAnimator();
            if (!animator)
            {
                animationPlaying = false;
                ShowNotification(new GUIContent("\u5f53\u524d Player \u6ca1\u6709\u53ef\u7528\u4e8e\u9884\u89c8\u7684 Animator"));
                return;
            }
            if (!AnimationMode.InAnimationMode())
                AnimationMode.StartAnimationMode();
            var time = Mathf.Min(clip.length, Mathf.Max(0, frame) / (float)Mathf.Max(1, frameRate));
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(animator.gameObject, clip, time);
            AnimationMode.EndSampling();
            SceneView.RepaintAll();
        }

        static PlayerCombat FindScenePreviewCombat(PlayerCombat prefabCombat)
        {
            if (!prefabCombat)
                return null;
            var candidates = Resources.FindObjectsOfTypeAll<PlayerCombat>();
            for (var i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                if (!candidate || EditorUtility.IsPersistent(candidate.gameObject) ||
                    !candidate.gameObject.scene.IsValid() || !candidate.gameObject.scene.isLoaded)
                    continue;
                var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(candidate);
                if (source == prefabCombat)
                    return candidate;
            }
            return null;
        }

        void StopAnimationPreview()
        {
            animationPlaying = false;
            animationPreviewOverride = null;
            if (AnimationMode.InAnimationMode())
                AnimationMode.StopAnimationMode();
            SceneView.RepaintAll();
        }

        void RebuildChargeEffectPreview(SerializedProperty node)
        {
            DisposeChargeEffectPreview();
            if (Application.isPlaying || node == null)
                return;

            serializedCombat.ApplyModifiedProperties();
            var prefab = node.FindPropertyRelative("chargeEffectPrefab").objectReferenceValue as GameObject;
            if (!prefab)
            {
                ShowNotification(new GUIContent("请先指定蓄力特效 Prefab"));
                return;
            }

            var sceneCombat = combat;
            if (EditorUtility.IsPersistent(sceneCombat.gameObject))
                sceneCombat = FindScenePreviewCombat(sceneCombat);
            if (!sceneCombat)
            {
                ShowNotification(new GUIContent("场景中没有当前 Player Prefab 的实例，无法加载特效预览"));
                return;
            }

            chargeEffectPreview = PrefabUtility.InstantiatePrefab(prefab, sceneCombat.transform) as GameObject;
            if (!chargeEffectPreview)
                return;
            chargeEffectPreview.name = prefab.name + "_ComboEditorPreview";
            chargeEffectPreview.hideFlags = HideFlags.HideAndDontSave;
            chargeEffectPreview.transform.localPosition = Vector3.zero;
            chargeEffectPreview.transform.localRotation = Quaternion.identity;
            chargeEffectPreviewPresentation = chargeEffectPreview.GetComponent<MirrorTrial.Combat.ChargeTelegraphPresentation>();
            chargeEffectPreviewMoveId = node.FindPropertyRelative("id").stringValue;
            RefreshChargeEffectPreview(node);
            Selection.activeGameObject = chargeEffectPreview;
            SceneView.lastActiveSceneView?.FrameSelected(false);
        }

        void RefreshChargeEffectPreview(SerializedProperty node)
        {
            if (!chargeEffectPreview || node == null)
                return;
            var offset = node.FindPropertyRelative("chargeEffectOffset").vector2Value;
            var rotationSpeed = node.FindPropertyRelative("chargeEffectRotationSpeed").floatValue;
            if (chargeEffectPreviewPresentation)
                chargeEffectPreviewPresentation.EditorSetPreview(true, 0.9f, true, offset, 0f, rotationSpeed);
            else
                chargeEffectPreview.transform.localPosition = offset;
            SceneView.RepaintAll();
        }

        void TickChargeEffectPreview()
        {
            if (!chargeEffectPreview || Application.isPlaying || serializedCombat == null)
                return;
            serializedCombat.UpdateIfRequiredOrScript();
            var graphs = serializedCombat.FindProperty("comboGraphs");
            if (selectedGraphIndex < 0 || selectedGraphIndex >= graphs.arraySize)
            {
                DisposeChargeEffectPreview();
                return;
            }
            var moves = graphs.GetArrayElementAtIndex(selectedGraphIndex).FindPropertyRelative("instances");
            SerializedProperty previewNode = null;
            for (var i = 0; i < moves.arraySize; i++)
            {
                var candidate = moves.GetArrayElementAtIndex(i);
                if (candidate.FindPropertyRelative("id").stringValue == chargeEffectPreviewMoveId)
                {
                    previewNode = candidate;
                    break;
                }
            }
            if (previewNode == null || !previewNode.FindPropertyRelative("showChargeEffect").boolValue)
            {
                DisposeChargeEffectPreview();
                return;
            }
            RefreshChargeEffectPreview(previewNode);
        }

        void DisposeChargeEffectPreview()
        {
            if (chargeEffectPreviewPresentation)
                chargeEffectPreviewPresentation.EditorSetPreview(false, 0f, true);
            if (chargeEffectPreview)
                DestroyImmediate(chargeEffectPreview);
            chargeEffectPreview = null;
            chargeEffectPreviewPresentation = null;
            chargeEffectPreviewMoveId = null;
        }

        Animator GetEditorAnimator()
        {
            if (!combat)
                return null;
            var driver = combat.GetComponent<PlayerAnimationDriver>();
            if (driver)
            {
                var serializedDriver = new SerializedObject(driver);
                var animatorProperty = serializedDriver.FindProperty("animator");
                var assignedAnimator = animatorProperty != null ? animatorProperty.objectReferenceValue as Animator : null;
                if (assignedAnimator)
                    return assignedAnimator;
            }
            return combat.GetComponentInChildren<Animator>(true);
        }

        AnimationClip GetAnimationClip(PlayerActionState action)
        {
            if (!combat)
                return null;
            var driver = combat.GetComponent<PlayerAnimationDriver>();
            var animator = GetEditorAnimator();
            if (!driver || !animator || !animator.runtimeAnimatorController)
                return null;
            string animatorState = null;
            var bindings = driver.Animations;
            for (var i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding != null && binding.Action == action)
                {
                    animatorState = binding.AnimatorState;
                    break;
                }
            }
            return FindClipForAnimatorState(animator.runtimeAnimatorController, animatorState);
        }

        static AnimationClip FindClipForAnimatorState(RuntimeAnimatorController runtimeController, string stateName)
        {
            if (!runtimeController || string.IsNullOrEmpty(stateName))
                return null;
            var overrideController = runtimeController as AnimatorOverrideController;
            var controller = overrideController ? overrideController.runtimeAnimatorController as AnimatorController : runtimeController as AnimatorController;
            if (!controller)
                return null;
            var overrides = new Dictionary<AnimationClip, AnimationClip>();
            if (overrideController)
            {
                var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                overrideController.GetOverrides(pairs);
                for (var i = 0; i < pairs.Count; i++)
                    overrides[pairs[i].Key] = pairs[i].Value;
            }
            for (var i = 0; i < controller.layers.Length; i++)
            {
                var clip = FindClipInStateMachine(controller.layers[i].stateMachine, stateName, overrides);
                if (clip)
                    return clip;
            }
            return null;
        }

        static AnimationClip FindClipInStateMachine(AnimatorStateMachine stateMachine, string stateName, Dictionary<AnimationClip, AnimationClip> overrides)
        {
            var states = stateMachine.states;
            for (var i = 0; i < states.Length; i++)
            {
                if (!string.Equals(states[i].state.name, stateName, System.StringComparison.Ordinal))
                    continue;
                var clip = GetFirstAnimationClip(states[i].state.motion);
                AnimationClip replacement;
                if (clip && overrides.TryGetValue(clip, out replacement) && replacement)
                    clip = replacement;
                return clip;
            }
            var children = stateMachine.stateMachines;
            for (var i = 0; i < children.Length; i++)
            {
                var clip = FindClipInStateMachine(children[i].stateMachine, stateName, overrides);
                if (clip)
                    return clip;
            }
            return null;
        }

        static AnimationClip GetFirstAnimationClip(Motion motion)
        {
            var clip = motion as AnimationClip;
            if (clip)
                return clip;
            var tree = motion as BlendTree;
            if (!tree)
                return null;
            var children = tree.children;
            for (var i = 0; i < children.Length; i++)
            {
                clip = GetFirstAnimationClip(children[i].motion);
                if (clip)
                    return clip;
            }
            return null;
        }

        void AddOrUpdateCurrentFrameKey(SerializedProperty step, int frame)
        {
            var keys = step.FindPropertyRelative("hitboxKeys");
            var key = FindKeyAtFrame(keys, frame);
            if (key == null)
            {
                keys.InsertArrayElementAtIndex(keys.arraySize);
                key = keys.GetArrayElementAtIndex(keys.arraySize - 1);
            }
            key.FindPropertyRelative("frame").intValue = frame;
            key.FindPropertyRelative("enabled").boolValue = true;
            if (key.FindPropertyRelative("size").vector2Value == Vector2.zero)
                key.FindPropertyRelative("size").vector2Value = new Vector2(1f, 0.8f);
            SortKeys(keys);
        }

        static SerializedProperty FindKeyAtFrame(SerializedProperty keys, int frame)
        {
            for (var i = 0; i < keys.arraySize; i++)
            {
                var key = keys.GetArrayElementAtIndex(i);
                if (key.FindPropertyRelative("frame").intValue == frame)
                    return key;
            }
            return null;
        }

        static SerializedProperty FindLastKeyAtOrBeforeFrame(SerializedProperty keys, int frame)
        {
            SerializedProperty result = null;
            var best = int.MinValue;
            for (var i = 0; i < keys.arraySize; i++)
            {
                var key = keys.GetArrayElementAtIndex(i);
                var keyFrame = key.FindPropertyRelative("frame").intValue;
                if (keyFrame <= frame && keyFrame >= best)
                {
                    best = keyFrame;
                    result = key;
                }
            }
            return result;
        }

        void DeleteKeyAtFrame(SerializedProperty keys, int frame)
        {
            for (var i = 0; i < keys.arraySize; i++)
            {
                if (keys.GetArrayElementAtIndex(i).FindPropertyRelative("frame").intValue == frame)
                {
                    keys.DeleteArrayElementAtIndex(i);
                    return;
                }
            }
        }

        static void SortKeys(SerializedProperty keys)
        {
            for (var i = 0; i < keys.arraySize - 1; i++)
                for (var j = i + 1; j < keys.arraySize; j++)
                    if (keys.GetArrayElementAtIndex(i).FindPropertyRelative("frame").intValue > keys.GetArrayElementAtIndex(j).FindPropertyRelative("frame").intValue)
                        keys.MoveArrayElement(j, i);
        }

        void OnSceneGUI(SceneView sceneView)
        {
            if (targetMode == EditorTargetMode.FirstMirrorBoss) return;
            if (targetMode == EditorTargetMode.MirrorBoss)
            {
                DrawBossScenePreview(sceneView);
                return;
            }
            if (!scenePreviewEnabled || !combat)
                return;

            EnsureSerializedObjects();
            if (serializedCombat == null)
                return;

            serializedCombat.Update();
            var step = GetPreviewMoveProperty();
            if (step == null)
                return;
            var keys = step.FindPropertyRelative("hitboxKeys");
            var key = FindKeyAtFrame(keys, currentFrame);
            if (key == null)
            {
                key = FindLastKeyAtOrBeforeFrame(keys, currentFrame);
                if (key == null)
                    return;
            }

            var mirrorProperty = step.FindPropertyRelative("mirrorHitboxByFacing");
            var attackHitboxProperty = serializedCombat.FindProperty("attackHitbox");
            var hitbox = attackHitboxProperty.objectReferenceValue as MirrorTrial.Combat.Hitbox;
            var enabledProperty = key.FindPropertyRelative("enabled");
            var keyEnabled = enabledProperty.boolValue;

            var offsetProperty = key.FindPropertyRelative("offset");
            var sizeProperty = key.FindPropertyRelative("size");
            var offset = offsetProperty.vector2Value;
            var size = ClampHitboxSize(sizeProperty.vector2Value);
            var previewOffset = offset;
            if (mirrorProperty.boolValue && combat.transform.lossyScale.x < 0f)
                previewOffset.x = -Mathf.Abs(previewOffset.x);

            var center = combat.transform.TransformPoint(previewOffset);
            ApplyScenePreviewToHitbox(hitbox, previewOffset, size);
            DrawHitboxGhosts(step, keys);

            var fillColor = keyEnabled ? new Color(1f, 0.45f, 0.05f, 0.12f) : new Color(0.35f, 0.7f, 1f, 0.08f);
            var outlineColor = keyEnabled ? new Color(1f, 0.65f, 0.1f, 1f) : new Color(0.35f, 0.7f, 1f, 0.9f);
            Handles.DrawSolidRectangleWithOutline(GetRectCorners(center, size), fillColor, outlineColor);
            Handles.Label(center + Vector3.up * (size.y * 0.5f + 0.15f), "\u7b2c " + (selectedComboIndex + 1) + " \u6bb5 / Frame " + currentFrame + (keyEnabled ? string.Empty : "  [\u672a\u542f\u7528\uff0c\u62d6\u52a8\u5373\u521b\u5efa Key]"));

            EditorGUI.BeginChangeCheck();
            var nextCenter = Handles.PositionHandle(center, Quaternion.identity);
            var nextSize = DrawSizeHandles(ref nextCenter, size);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(combat, "\u8c03\u6574\u653b\u51fb\u6846 Key");
                var writableKey = FindKeyAtFrame(keys, currentFrame);
                if (writableKey == null)
                {
                    AddOrUpdateCurrentFrameKey(step, currentFrame);
                    writableKey = FindKeyAtFrame(keys, currentFrame);
                }
                var nextLocal = (Vector2)combat.transform.InverseTransformPoint(nextCenter);
                if (mirrorProperty.boolValue && combat.transform.lossyScale.x < 0f)
                    nextLocal.x = Mathf.Abs(nextLocal.x);
                writableKey.FindPropertyRelative("enabled").boolValue = true;
                writableKey.FindPropertyRelative("offset").vector2Value = nextLocal;
                writableKey.FindPropertyRelative("size").vector2Value = ClampHitboxSize(nextSize);
                serializedCombat.ApplyModifiedProperties();
                EditorUtility.SetDirty(combat);
                Repaint();
                SceneView.RepaintAll();
            }
        }

        void DrawHitboxGhosts(SerializedProperty step, SerializedProperty keys)
        {
            for (var i = 0; i < keys.arraySize; i++)
            {
                var key = keys.GetArrayElementAtIndex(i);
                if (!key.FindPropertyRelative("enabled").boolValue)
                    continue;
                var offset = key.FindPropertyRelative("offset").vector2Value;
                var size = ClampHitboxSize(key.FindPropertyRelative("size").vector2Value);
                var center = combat.transform.TransformPoint(offset);
                Handles.DrawSolidRectangleWithOutline(GetRectCorners(center, size), new Color(1f, 1f, 0f, 0.035f), new Color(1f, 1f, 0f, 0.25f));
            }
        }

        static Vector2 DrawSizeHandles(ref Vector3 center, Vector2 size)
        {
            var nextSize = size;
            var half = size * 0.5f;
            var topLeft = center + new Vector3(-half.x, half.y, 0f);
            var topRight = center + new Vector3(half.x, half.y, 0f);
            var bottomLeft = center + new Vector3(-half.x, -half.y, 0f);
            var bottomRight = center + new Vector3(half.x, -half.y, 0f);
            Handles.color = new Color(1f, 0.75f, 0.1f, 1f);

            DrawCornerSizeHandle(topLeft, bottomRight, ref center, ref nextSize);
            DrawCornerSizeHandle(topRight, bottomLeft, ref center, ref nextSize);
            DrawCornerSizeHandle(bottomLeft, topRight, ref center, ref nextSize);
            DrawCornerSizeHandle(bottomRight, topLeft, ref center, ref nextSize);
            return ClampHitboxSize(nextSize);
        }

        static void DrawCornerSizeHandle(Vector3 position, Vector3 oppositeCorner, ref Vector3 center, ref Vector2 size)
        {
            var handleSize = HandleUtility.GetHandleSize(position) * 0.09f;
            EditorGUI.BeginChangeCheck();
            var draggedCorner = Handles.FreeMoveHandle(position, handleSize, Vector3.zero, Handles.RectangleHandleCap);
            if (!EditorGUI.EndChangeCheck())
                return;

            center = (draggedCorner + oppositeCorner) * 0.5f;
            size.x = Mathf.Max(0.01f, Mathf.Abs(draggedCorner.x - oppositeCorner.x));
            size.y = Mathf.Max(0.01f, Mathf.Abs(draggedCorner.y - oppositeCorner.y));
        }

        void ApplyScenePreviewToHitbox(MirrorTrial.Combat.Hitbox hitbox, Vector2 localOffset, Vector2 size)
        {
            if (!hitbox)
                return;

            hitbox.transform.localPosition = localOffset;
            var box = hitbox.GetComponent<BoxCollider2D>();
            if (box)
            {
                box.size = size;
                box.offset = Vector2.zero;
                return;
            }

            var capsule = hitbox.GetComponent<CapsuleCollider2D>();
            if (capsule)
            {
                capsule.size = size;
                capsule.offset = Vector2.zero;
                return;
            }

            var circle = hitbox.GetComponent<CircleCollider2D>();
            if (circle)
            {
                circle.radius = Mathf.Max(size.x, size.y) * 0.5f;
                circle.offset = Vector2.zero;
            }
        }

        static Vector2 ClampHitboxSize(Vector2 size)
        {
            return new Vector2(Mathf.Max(0.01f, size.x), Mathf.Max(0.01f, size.y));
        }

        static Vector3[] GetRectCorners(Vector3 center, Vector2 size)
        {
            var half = size * 0.5f;
            return new[]
            {
                center + new Vector3(-half.x, -half.y, 0f),
                center + new Vector3(-half.x, half.y, 0f),
                center + new Vector3(half.x, half.y, 0f),
                center + new Vector3(half.x, -half.y, 0f)
            };
        }

        static void DrawCommandPopup(SerializedProperty property, GUIContent label, params GUILayoutOption[] options)
        {
            var current = (PlayerInputCommand)property.enumValueIndex;
            var selected = IndexOf(CommandValues, current);
            if (selected < 0)
            {
                EditorGUILayout.PropertyField(property, label, options);
                return;
            }
            selected = label == GUIContent.none
                ? EditorGUILayout.Popup(selected, CommandLabels, options)
                : EditorGUILayout.Popup(label, selected, CommandLabels, options);
            property.enumValueIndex = (int)CommandValues[Mathf.Clamp(selected, 0, CommandValues.Length - 1)];
        }

        static void DrawActionPopup(SerializedProperty property, GUIContent label)
        {
            var current = (PlayerActionState)property.enumValueIndex;
            var selected = IndexOf(ActionValues, current);
            selected = EditorGUILayout.Popup(label, selected, ActionLabels);
            property.enumValueIndex = (int)ActionValues[Mathf.Clamp(selected, 0, ActionValues.Length - 1)];
        }

        static void DrawMoveCategoryPopup(SerializedProperty property, GUIContent label)
        {
            var current = (PlayerMoveCategory)property.enumValueIndex;
            var selected = IndexOf(MoveCategoryValues, current);
            selected = EditorGUILayout.Popup(label, selected, MoveCategoryLabels);
            property.enumValueIndex = (int)MoveCategoryValues[Mathf.Clamp(selected, 0, MoveCategoryValues.Length - 1)];
        }

        static void DrawComboCategoryPopup(SerializedProperty property, GUIContent label)
        {
            var current = (PlayerComboCategory)property.enumValueIndex;
            var selected = IndexOf(ComboCategoryValues, current);
            selected = EditorGUILayout.Popup(label, selected, ComboCategoryLabels);
            property.enumValueIndex = (int)ComboCategoryValues[Mathf.Clamp(selected, 0, ComboCategoryValues.Length - 1)];
        }

        static int IndexOf<T>(T[] values, T value)
        {
            for (var i = 0; i < values.Length; i++)
                if (values[i].Equals(value))
                    return i;
            return 0;
        }
    }
}
