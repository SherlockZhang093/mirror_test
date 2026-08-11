#if UNITY_EDITOR
using System.Linq;
using MirrorTrial.Boss;
using UnityEditor;
using UnityEngine;

namespace MirrorTrial.Editor
{
    public sealed partial class PlayerInputComboEditorWindow
    {
        const string TwoStageProfilePath = "Assets/MirrorTrial/Boss/MirrorArcherTwoStageProfile.asset";

        MirrorArcherTwoStageProfile bossProfile;
        GameObject bossPreview;
        GameObject bossSkillPrefabPreview;
        Animator bossPreviewAnimator;
        int bossStage;
        int bossSkillIndex;
        bool bossFacingLeft;
        MirrorArcherSkillType previewSkillType = (MirrorArcherSkillType)(-1);
        GameObject previewActionPrefab;

        void DrawBossModeGUI()
        {
            EnsureBossResources();
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUI.BeginChangeCheck();
                bossProfile = (MirrorArcherTwoStageProfile)EditorGUILayout.ObjectField(
                    bossProfile, typeof(MirrorArcherTwoStageProfile), false, GUILayout.MinWidth(280f));
                if (EditorGUI.EndChangeCheck())
                {
                    bossSkillIndex = 0;
                    RebuildBossPreview();
                }
                if (GUILayout.Button("重建两阶段资源", EditorStyles.toolbarButton, GUILayout.Width(120f)))
                    MirrorTrial.Boss.Editor.MirrorArcherTwoStageSetup.RebuildFromMenu();
                if (GUILayout.Button("重建真实预览", EditorStyles.toolbarButton, GUILayout.Width(105f))) RebuildBossPreview();
                if (GUILayout.Button("保存 Profile", EditorStyles.toolbarButton, GUILayout.Width(95f))) SaveBossProfile();
            }

            if (!bossProfile)
            {
                EditorGUILayout.HelpBox("未找到 MirrorArcherTwoStageProfile。请点击“重建两阶段资源”。", MessageType.Warning);
                return;
            }
            bossProfile.EnsureDefaults();

            EditorGUI.BeginChangeCheck();
            bossStage = GUILayout.Toolbar(bossStage, new[] { "空中阶段（独立坐骑血量）", "地面阶段（最终 Boss 血量）" }, GUILayout.Height(25f));
            if (EditorGUI.EndChangeCheck())
            {
                bossSkillIndex = 0;
                currentFrame = 0;
                animationPlaying = false;
                RebuildBossPreview();
            }

            var profileObject = new SerializedObject(bossProfile);
            profileObject.Update();
            DrawStageContract(profileObject);
            var list = profileObject.FindProperty(bossStage == 0 ? "airSkills" : "groundSkills");
            if (list == null || list.arraySize == 0)
            {
                EditorGUILayout.HelpBox("当前阶段没有技能。", MessageType.Warning);
                profileObject.ApplyModifiedProperties();
                return;
            }

            bossSkillIndex = Mathf.Clamp(bossSkillIndex, 0, list.arraySize - 1);
            var names = Enumerable.Range(0, list.arraySize).Select(index =>
            {
                var item = list.GetArrayElementAtIndex(index);
                var name = item.FindPropertyRelative("displayName").stringValue;
                var type = (MirrorArcherSkillType)item.FindPropertyRelative("type").enumValueIndex;
                return $"{index + 1}. {name} ({type})";
            }).ToArray();
            EditorGUI.BeginChangeCheck();
            bossSkillIndex = EditorGUILayout.Popup("当前技能", bossSkillIndex, names);
            if (EditorGUI.EndChangeCheck())
            {
                currentFrame = 0;
                animationPlaying = false;
                RebuildBossSkillPrefabPreview();
            }

            var skill = list.GetArrayElementAtIndex(bossSkillIndex);
            DrawSkillEditor(skill);
            profileObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(bossProfile);

            var runtimeSkill = GetBossSkill();
            if (runtimeSkill != null && (runtimeSkill.type != previewSkillType || runtimeSkill.projectilePrefab != previewActionPrefab))
                RebuildBossSkillPrefabPreview();
            SceneView.RepaintAll();
        }

        void DrawStageContract(SerializedObject profileObject)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(bossStage == 0 ? "空中阶段 Prefab / 美术契约" : "地面阶段 Prefab / 共享近战数据", EditorStyles.boldLabel);
                if (bossStage == 0)
                {
                    EditorGUILayout.PropertyField(profileObject.FindProperty("mountedBossPrefab"), new GUIContent("骑乘 Boss Prefab"));
                    EditorGUILayout.HelpBox("最终坐骑美术只替换 VisualRoot。必须保留 MountRoot、RiderSocket、ProjectileSocket、ImpactSocket，并实现 MountedIdle / MountedBowFire / MountedDive / MountedFall / MountedImpact 状态。", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.PropertyField(profileObject.FindProperty("groundBossPrefab"), new GUIContent("地面 Boss Prefab"));
                    EditorGUILayout.PropertyField(profileObject.FindProperty("sharedGroundMeleeProfile"), new GUIContent("共享第一个 Boss 近战 Profile"));
                    EditorGUILayout.HelpBox("基础连招、蓄力重斩和假动作直接引用第一个 Boss 的 Profile；这里不保存第二份近战数值。远距落石的绿色圆是强制安全区。", MessageType.Info);
                }
            }
        }

        void DrawSkillEditor(SerializedProperty skill)
        {
            var type = (MirrorArcherSkillType)skill.FindPropertyRelative("type").enumValueIndex;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("独立动画与时序", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(skill.FindPropertyRelative("displayName"), new GUIContent("技能名称"));
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.PropertyField(skill.FindPropertyRelative("type"), new GUIContent("技能类型"));
                EditorGUILayout.PropertyField(skill.FindPropertyRelative("animationClip"), new GUIContent("独立动画 Clip"));
                EditorGUILayout.PropertyField(skill.FindPropertyRelative("animatorState"), new GUIContent("Animator 状态"));
                EditorGUILayout.PropertyField(skill.FindPropertyRelative("windup"), new GUIContent("前摇（秒）"));
                EditorGUILayout.PropertyField(skill.FindPropertyRelative("recovery"), new GUIContent("后摇（秒）"));
                EditorGUILayout.PropertyField(skill.FindPropertyRelative("lockMoment"), new GUIContent("锁定时刻（秒）"));
                EditorGUILayout.PropertyField(skill.FindPropertyRelative("releaseMoment"), new GUIContent("发射/生效时刻（秒）"));
                EditorGUILayout.PropertyField(skill.FindPropertyRelative("windupEffectPrefab"), new GUIContent("独立前摇特效"));
                EditorGUILayout.PropertyField(skill.FindPropertyRelative("effectOffset"), new GUIContent("特效 Offset"));
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("真实 Prefab 与战斗数值", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(skill.FindPropertyRelative("projectilePrefab"), new GUIContent(type == MirrorArcherSkillType.SkyRockfall ? "落石 Prefab" : "箭/行动 Prefab"));
                EditorGUILayout.PropertyField(skill.FindPropertyRelative("impactPrefab"), new GUIContent("命中特效 Prefab"));
                EditorGUILayout.PropertyField(skill.FindPropertyRelative("arrowCount"), new GUIContent("箭数量"));
                EditorGUILayout.PropertyField(skill.FindPropertyRelative("initialAngleOffset"),
                    new GUIContent("初始角度 Offset", "在瞄准方向上额外旋转；正数逆时针，负数顺时针。箭的视觉会自动对齐最终飞行方向。"));
                EditorGUILayout.PropertyField(skill.FindPropertyRelative("arrowAngle"), new GUIContent("扇形总角度"));
                EditorGUILayout.PropertyField(skill.FindPropertyRelative("arrowSpeed"), new GUIContent("箭速度"));
                EditorGUILayout.PropertyField(skill.FindPropertyRelative("arrowRange"), new GUIContent("箭射程"));
                EditorGUILayout.PropertyField(skill.FindPropertyRelative("damage"), new GUIContent("伤害"));
                EditorGUILayout.PropertyField(skill.FindPropertyRelative("knockback"), new GUIContent("击退"));
                EditorGUILayout.PropertyField(skill.FindPropertyRelative("movementOffset"), new GUIContent("移动 Offset"));
                EditorGUILayout.PropertyField(skill.FindPropertyRelative("movementDuration"), new GUIContent("移动时长"));
                EditorGUILayout.PropertyField(skill.FindPropertyRelative("effectRadius"), new GUIContent("命中/落石半径"));
                EditorGUILayout.PropertyField(skill.FindPropertyRelative("range"), new GUIContent(type == MirrorArcherSkillType.SkyRockfall ? "落石外圈范围" : "行动/箭雨范围"));
                if (type == MirrorArcherSkillType.SkyRockfall)
                {
                    EditorGUILayout.PropertyField(skill.FindPropertyRelative("safeRadius"), new GUIContent("Boss 周围安全区"));
                    EditorGUILayout.PropertyField(skill.FindPropertyRelative("waveCount"), new GUIContent("落石波数"));
                    EditorGUILayout.PropertyField(skill.FindPropertyRelative("impactsPerWave"), new GUIContent("每波落点数"));
                    EditorGUILayout.PropertyField(skill.FindPropertyRelative("waveInterval"), new GUIContent("波间隔"));
                }
            }

            DrawSharedMeleeSource(type);
            DrawBossAnimationPreview();
            if (!skill.FindPropertyRelative("projectilePrefab").objectReferenceValue &&
                type != MirrorArcherSkillType.SharedBaseCombo && type != MirrorArcherSkillType.SharedHeavySlash &&
                type != MirrorArcherSkillType.SharedFeint && type != MirrorArcherSkillType.AirReposition &&
                type != MirrorArcherSkillType.MountedDive)
                EditorGUILayout.HelpBox("请绑定真实箭或落石 Prefab；运行时虽有后备临时视觉，但编辑器无法验证最终轨迹资源。", MessageType.Error);
        }

        void DrawSharedMeleeSource(MirrorArcherSkillType type)
        {
            if (bossStage != 1 || !bossProfile.sharedGroundMeleeProfile) return;
            var source = new SerializedObject(bossProfile.sharedGroundMeleeProfile);
            source.Update();
            SerializedProperty property = null;
            string title = null;
            if (type == MirrorArcherSkillType.SharedBaseCombo)
            {
                title = "共享来源：第一个 Boss 基础连招";
                property = source.FindProperty("phaseOneCombo");
            }
            else if (type == MirrorArcherSkillType.SharedHeavySlash)
            {
                title = "共享来源：第一个 Boss 蓄力重斩";
                property = source.FindProperty("heavySlash");
            }
            else if (type == MirrorArcherSkillType.SharedFeint)
            {
                title = "共享来源：第一个 Boss 假动作";
            }
            if (title == null) return;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                if (property != null) EditorGUILayout.PropertyField(property, true);
                else
                {
                    EditorGUILayout.PropertyField(source.FindProperty("phaseOneFeintChance"), new GUIContent("地面假动作概率"));
                    EditorGUILayout.PropertyField(source.FindProperty("feintHoldDuration"), new GUIContent("假动作停留"));
                    EditorGUILayout.PropertyField(source.FindProperty("feintResetDuration"), new GUIContent("重新出招间隔"));
                    EditorGUILayout.PropertyField(source.FindProperty("maxFeintsPerCombo"), new GUIContent("每套最多次数"));
                }
            }
            if (source.ApplyModifiedProperties()) EditorUtility.SetDirty(bossProfile.sharedGroundMeleeProfile);
        }

        void DrawBossAnimationPreview()
        {
            var clip = GetBossClip();
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("真实 Prefab 动画与 Scene 轨迹预览", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("动画", clip ? $"{clip.name} · {clip.length:0.###} 秒 · {clip.frameRate:0.#} FPS" : "未绑定/未找到动画");
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("|<", GUILayout.Width(34f))) { animationPlaying = false; currentFrame = 0; SampleBossFrame(); }
                    if (GUILayout.Button(animationPlaying ? "暂停" : "播放", GUILayout.Width(58f)))
                    {
                        animationPlaying = !animationPlaying;
                        lastAnimationUpdate = EditorApplication.timeSinceStartup;
                    }
                    var maxFrame = clip ? Mathf.Max(1, Mathf.FloorToInt(clip.length * clip.frameRate)) : 1;
                    currentFrame = EditorGUILayout.IntSlider("当前帧", currentFrame, 0, maxFrame);
                    bossFacingLeft = EditorGUILayout.ToggleLeft("向左", bossFacingLeft, GUILayout.Width(52f));
                }
                EditorGUILayout.HelpBox("Scene 视图使用当前阶段的真实 Boss Prefab；黄色线为箭轨迹/移动路径，绿色圆为安全区，红圈为落点或伤害区。", MessageType.None);
            }
        }

        void EnsureBossResources()
        {
            if (!bossProfile) bossProfile = AssetDatabase.LoadAssetAtPath<MirrorArcherTwoStageProfile>(TwoStageProfilePath);
            if (!bossPreview && !Application.isPlaying) RebuildBossPreview();
        }

        void RebuildBossPreview()
        {
            DisposeBossPreview();
            if (Application.isPlaying || !bossProfile) return;
            var prefab = bossStage == 0 ? bossProfile.mountedBossPrefab : bossProfile.groundBossPrefab;
            if (!prefab) return;
            bossPreview = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (!bossPreview) return;
            bossPreview.name = bossStage == 0 ? "MountedBoss_SkillPreview" : "GroundBoss_SkillPreview";
            bossPreview.hideFlags = HideFlags.HideAndDontSave;
            bossPreview.transform.position = Vector3.zero;
            bossPreviewAnimator = bossPreview.GetComponentInChildren<Animator>(true);
            RebuildBossSkillPrefabPreview();
            FocusBossPreview();
            SampleBossFrame();
        }

        void RebuildBossSkillPrefabPreview()
        {
            if (bossSkillPrefabPreview) DestroyImmediate(bossSkillPrefabPreview);
            bossSkillPrefabPreview = null;
            var skill = GetBossSkill();
            previewSkillType = skill == null ? (MirrorArcherSkillType)(-1) : skill.type;
            previewActionPrefab = skill == null ? null : skill.projectilePrefab;
            if (!bossPreview || skill == null || !skill.projectilePrefab) return;
            bossSkillPrefabPreview = PrefabUtility.InstantiatePrefab(skill.projectilePrefab) as GameObject;
            if (!bossSkillPrefabPreview) return;
            bossSkillPrefabPreview.name = "SkillPrefab_RealPreview";
            bossSkillPrefabPreview.hideFlags = HideFlags.HideAndDontSave;
            bossSkillPrefabPreview.transform.SetParent(bossPreview.transform, false);
            bossSkillPrefabPreview.transform.localPosition = skill.effectOffset;
        }

        void FocusBossPreview()
        {
            if (!bossPreview || !SceneView.lastActiveSceneView) return;
            var renderers = bossPreview.GetComponentsInChildren<Renderer>(true);
            var bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(Vector3.zero, new Vector3(8f, 5f, 0f));
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            bounds.Expand(4f);
            SceneView.lastActiveSceneView.Frame(bounds, false);
            SceneView.RepaintAll();
        }

        void DisposeBossPreview()
        {
            if (bossSkillPrefabPreview) DestroyImmediate(bossSkillPrefabPreview);
            if (bossPreview) DestroyImmediate(bossPreview);
            bossSkillPrefabPreview = null;
            bossPreview = null;
            bossPreviewAnimator = null;
        }

        void TickBossAnimationPreview()
        {
            if (!animationPlaying || Application.isPlaying) return;
            var clip = GetBossClip();
            if (!clip) { animationPlaying = false; return; }
            var now = EditorApplication.timeSinceStartup;
            var elapsed = now - lastAnimationUpdate;
            if (elapsed <= 0d) return;
            lastAnimationUpdate = now;
            var maxFrame = Mathf.Max(1, Mathf.FloorToInt(clip.length * clip.frameRate));
            currentFrame = (currentFrame + Mathf.Max(1, Mathf.FloorToInt((float)elapsed * clip.frameRate))) % (maxFrame + 1);
            SampleBossFrame();
            Repaint();
        }

        void SampleBossFrame()
        {
            var clip = GetBossClip();
            if (Application.isPlaying || !bossPreviewAnimator || !clip) return;
            if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
            var time = Mathf.Min(clip.length, currentFrame / Mathf.Max(1f, clip.frameRate));
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(bossPreviewAnimator.gameObject, clip, time);
            AnimationMode.EndSampling();
            SceneView.RepaintAll();
        }

        AnimationClip GetBossClip()
        {
            var skill = GetBossSkill();
            if (skill == null) return null;
            if (skill.animationClip) return skill.animationClip;
            if (!bossPreviewAnimator || !bossPreviewAnimator.runtimeAnimatorController || string.IsNullOrEmpty(skill.animatorState)) return null;
            return bossPreviewAnimator.runtimeAnimatorController.animationClips.FirstOrDefault(clip => clip && clip.name == skill.animatorState);
        }

        MirrorArcherSkillConfig GetBossSkill()
        {
            if (!bossProfile) return null;
            var list = bossStage == 0 ? bossProfile.airSkills : bossProfile.groundSkills;
            return list != null && list.Count > 0 ? list[Mathf.Clamp(bossSkillIndex, 0, list.Count - 1)] : null;
        }

        void DrawBossScenePreview(SceneView sceneView)
        {
            if (!bossPreview || !bossProfile || Application.isPlaying) return;
            var skill = GetBossSkill();
            if (skill == null) return;
            var origin = FindPreviewSocket("ProjectileSocket");
            var facing = bossFacingLeft ? -1f : 1f;
            var start = origin ? origin.position : bossPreview.transform.position + (Vector3)skill.effectOffset;
            Handles.color = new Color(1f, 0.78f, 0.2f, 1f);

            switch (skill.type)
            {
                case MirrorArcherSkillType.LockedShot:
                case MirrorArcherSkillType.FanShot:
                    DrawArrowTrajectories(start, skill, facing);
                    break;
                case MirrorArcherSkillType.GroundArrowRain:
                    DrawArrowRain(start, skill);
                    break;
                case MirrorArcherSkillType.MountedDive:
                    DrawDivePath(start, skill, facing);
                    break;
                case MirrorArcherSkillType.AirReposition:
                    DrawReposition(start, skill, facing);
                    break;
                case MirrorArcherSkillType.SkyRockfall:
                    DrawRockfall(skill);
                    break;
            }
            Handles.color = new Color(0.3f, 0.85f, 1f, 1f);
            Handles.DrawWireDisc((Vector2)bossPreview.transform.position + skill.effectOffset, Vector3.forward, 0.18f);
            Handles.Label((Vector2)bossPreview.transform.position + skill.effectOffset + Vector2.up * 0.25f, "独立前摇特效 Offset");
        }

        void DrawArrowTrajectories(Vector3 start, MirrorArcherSkillConfig skill, float facing)
        {
            var count = Mathf.Max(1, skill.arrowCount);
            for (var i = 0; i < count; i++)
            {
                var spreadAngle = count <= 1 ? 0f : Mathf.Lerp(-skill.arrowAngle * 0.5f, skill.arrowAngle * 0.5f, i / (float)(count - 1));
                var angle = skill.initialAngleOffset * facing + spreadAngle;
                var direction = Quaternion.Euler(0f, 0f, angle) * new Vector3(facing, -0.08f, 0f);
                Handles.DrawAAPolyLine(3f, start, start + direction.normalized * skill.arrowRange);
            }
            Handles.Label(start + Vector3.up * 0.3f, $"{skill.arrowCount} 箭 · {skill.arrowSpeed:0.#} 速 · {skill.damage} 伤害");
        }

        void DrawArrowRain(Vector3 start, MirrorArcherSkillConfig skill)
        {
            var count = Mathf.Max(1, skill.arrowCount);
            for (var i = 0; i < count; i++)
            {
                var t = count <= 1 ? 0.5f : i / (float)(count - 1);
                var x = Mathf.Lerp(-skill.range * 0.5f, skill.range * 0.5f, t);
                var top = start + new Vector3(x, 2f, 0f);
                var direction = Quaternion.Euler(0f, 0f, skill.initialAngleOffset) * Vector3.down;
                Handles.DrawAAPolyLine(3f, top, top + direction.normalized * Mathf.Min(skill.arrowRange, 7f));
            }
        }

        void DrawDivePath(Vector3 start, MirrorArcherSkillConfig skill, float facing)
        {
            var end = start + new Vector3(facing * Mathf.Abs(skill.movementOffset.x), -Mathf.Abs(skill.movementOffset.y), 0f);
            var control = (start + end) * 0.5f + Vector3.down * Mathf.Max(1f, skill.movementOffset.y);
            var points = new Vector3[25];
            for (var i = 0; i < points.Length; i++)
            {
                var t = i / (float)(points.Length - 1);
                points[i] = Vector3.Lerp(Vector3.Lerp(start, control, t), Vector3.Lerp(control, end, t), t);
            }
            Handles.DrawAAPolyLine(4f, points);
            Handles.color = Color.red;
            Handles.DrawWireDisc(end, Vector3.forward, skill.effectRadius);
        }

        void DrawReposition(Vector3 start, MirrorArcherSkillConfig skill, float facing)
        {
            var end = start + new Vector3(facing * skill.movementOffset.x, skill.movementOffset.y, 0f);
            Handles.DrawAAPolyLine(4f, start, end);
            Handles.ConeHandleCap(0, end, Quaternion.LookRotation(Vector3.forward, end - start), 0.35f, EventType.Repaint);
        }

        void DrawRockfall(MirrorArcherSkillConfig skill)
        {
            var center = bossPreview.transform.position;
            Handles.color = new Color(0.2f, 1f, 0.45f, 1f);
            Handles.DrawWireDisc(center, Vector3.forward, skill.safeRadius);
            Handles.Label(center + Vector3.up * skill.safeRadius, "清楚安全区 / 真实输出窗口");
            Handles.color = new Color(1f, 0.22f, 0.12f, 0.9f);
            Handles.DrawWireDisc(center, Vector3.forward, skill.range);
            var count = Mathf.Max(1, skill.impactsPerWave);
            for (var i = 0; i < count; i++)
            {
                var angle = i / (float)count * Mathf.PI * 2f;
                var radius = Mathf.Lerp(skill.safeRadius + skill.effectRadius, skill.range, (i + 1f) / count);
                var point = center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * 0.38f, 0f);
                Handles.DrawWireDisc(point, Vector3.forward, skill.effectRadius);
            }
        }

        Transform FindPreviewSocket(string socketName)
        {
            if (!bossPreview) return null;
            return bossPreview.GetComponentsInChildren<Transform>(true).FirstOrDefault(value => value.name == socketName);
        }

        void SaveBossProfile()
        {
            if (!bossProfile) return;
            EditorUtility.SetDirty(bossProfile);
            if (bossProfile.sharedGroundMeleeProfile) EditorUtility.SetDirty(bossProfile.sharedGroundMeleeProfile);
            AssetDatabase.SaveAssets();
            ShowNotification(new GUIContent("两阶段 Boss 技能已保存"));
        }
    }
}
#endif
