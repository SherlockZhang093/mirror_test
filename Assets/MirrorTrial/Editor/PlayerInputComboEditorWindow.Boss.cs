#if UNITY_EDITOR
using System.Linq;
using MirrorTrial.Boss;
using MirrorTrial.Combat;
using MirrorTrial.Player;
using UnityEditor;
using UnityEngine;

namespace MirrorTrial.Editor
{
    public sealed partial class PlayerInputComboEditorWindow
    {
        const string BossProfilePath = "Assets/MirrorTrial/Boss/MirrorBossSimpleProfile.asset";
        const string BossPrefabPath = "Assets/MirrorTrial/Prefabs/Boss/MirrorBoss.prefab";

        MirrorBossSimpleProfile bossProfile;
        GameObject bossPreview;
        Animator bossPreviewAnimator;
        Transform bossPreviewHitbox;
        int bossPhase;
        int bossStepIndex;
        bool bossFacingLeft;
        bool bossPreviewWindupConsumed;
        double bossPreviewWindupHoldUntil;

        void DrawBossModeGUI()
        {
            EnsureBossResources();
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                bossProfile = (MirrorBossSimpleProfile)EditorGUILayout.ObjectField(bossProfile, typeof(MirrorBossSimpleProfile), false, GUILayout.MinWidth(250f));
                if (GUILayout.Button("创建/重建预览角色", EditorStyles.toolbarButton, GUILayout.Width(125f))) RebuildBossPreview();
                if (GUILayout.Button("聚焦预览角色", EditorStyles.toolbarButton, GUILayout.Width(100f))) FocusBossPreview();
                if (GUILayout.Button("保存 Profile", EditorStyles.toolbarButton, GUILayout.Width(100f))) SaveBossProfile();
            }

            if (!bossProfile)
            {
                EditorGUILayout.HelpBox("未找到 MirrorBossSimpleProfile。", MessageType.Warning);
                return;
            }

            bossPhase = GUILayout.Toolbar(bossPhase, new[] { "阶段 1", "阶段 2", "阶段 3" });
            var combo = GetBossCombo();
            if (combo == null || combo.Length == 0)
            {
                EditorGUILayout.HelpBox("当前阶段没有剑招。", MessageType.Info);
                return;
            }

            bossStepIndex = Mathf.Clamp(bossStepIndex, 0, combo.Length - 1);
            var names = combo.Select((step, index) => $"{index + 1}. {(step == null ? "空剑招" : step.animationState)}").ToArray();
            bossStepIndex = EditorGUILayout.Popup("当前剑招", bossStepIndex, names);
            var step = combo[bossStepIndex];
            if (step == null) return;

            var clip = GetBossClip(step.animationState);
            EnsureBossStepForClip(step, clip);
            var frameRate = Mathf.Max(1, step.animationFrameRate);
            var maxFrame = Mathf.Max(1, step.animationFrameCount - 1);
            currentFrame = Mathf.Clamp(currentFrame, 0, maxFrame);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("动画逐帧预览与攻击框", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("动画片段", clip ? $"{clip.name}　{clip.length:0.###} 秒　{frameRate} FPS" : "未找到匹配 AnimationClip");

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("|<", GUILayout.Width(32f))) { animationPlaying = false; currentFrame = 0; SampleBossFrame(clip); }
                    if (GUILayout.Button(animationPlaying ? "暂停" : "播放", GUILayout.Width(52f)))
                    {
                        animationPlaying = !animationPlaying;
                        lastAnimationUpdate = EditorApplication.timeSinceStartup;
                        bossPreviewWindupConsumed = false;
                        bossPreviewWindupHoldUntil = 0d;
                        SampleBossFrame(clip);
                    }
                    if (GUILayout.Button("<", GUILayout.Width(32f))) { animationPlaying = false; currentFrame = Mathf.Max(0, currentFrame - 1); SampleBossFrame(clip); }
                    if (GUILayout.Button(">", GUILayout.Width(32f))) { animationPlaying = false; currentFrame = Mathf.Min(maxFrame, currentFrame + 1); SampleBossFrame(clip); }
                    EditorGUI.BeginChangeCheck();
                    currentFrame = EditorGUILayout.IntSlider("当前帧", currentFrame, 0, maxFrame);
                    if (EditorGUI.EndChangeCheck()) { animationPlaying = false; SampleBossFrame(clip); }
                }

                bossFacingLeft = EditorGUILayout.Toggle("预览向左攻击", bossFacingLeft);
                DrawBossFrameTimeline(step, maxFrame);
                DrawBossWindupEditor(step);
                DrawBossCurrentKeyEditor(step);
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("剑招其他参数", EditorStyles.boldLabel);
                step.playbackSpeed = EditorGUILayout.FloatField("动画速度", step.playbackSpeed);
                step.gapAfter = EditorGUILayout.FloatField("招式后间隔", step.gapAfter);
                step.advanceSpeed = EditorGUILayout.FloatField("攻击推进速度", step.advanceSpeed);
                step.knockback = EditorGUILayout.Vector2Field("击退", step.knockback);
                step.hitStop = EditorGUILayout.FloatField("顿帧", step.hitStop);
            }

            EditorGUILayout.HelpBox("Scene 视图：红/蓝框是当前帧攻击框。拖中心移动，拖四角缩放；修改会写入当前帧 Key。橙色时间格表示该帧攻击框开启。", MessageType.Info);
            if (GUI.changed)
            {
                EditorUtility.SetDirty(bossProfile);
                SceneView.RepaintAll();
            }
        }

        void DrawBossFrameTimeline(MirrorBossComboStepV2 step, int maxFrame)
        {
            EditorGUILayout.LabelField("逐帧攻击框（● = Key）", EditorStyles.miniBoldLabel);
            var columns = Mathf.Max(6, Mathf.FloorToInt((position.width - 60f) / 42f));
            var old = GUI.backgroundColor;
            for (var frame = 0; frame <= maxFrame; frame++)
            {
                if (frame % columns == 0) EditorGUILayout.BeginHorizontal();
                var exact = FindBossKey(step, frame);
                PlayerAttackHitboxKey evaluated;
                var enabled = EvaluateBossKey(step, frame, out evaluated) && evaluated.enabled;
                var windup = step.windupFrame == frame && step.windupHoldDuration > 0f;
                GUI.backgroundColor = frame == currentFrame ? new Color(0.25f, 0.9f, 0.55f) :
                    windup ? new Color(1f, 0.82f, 0.12f) :
                    enabled ? new Color(1f, 0.58f, 0.16f) : new Color(0.48f, 0.48f, 0.48f);
                if (GUILayout.Button((windup ? "▲" : exact != null ? "●" : "") + frame, EditorStyles.miniButton, GUILayout.Width(38f)))
                {
                    currentFrame = frame;
                    animationPlaying = false;
                    SampleBossFrame(GetBossClip(step.animationState));
                }
                GUI.backgroundColor = old;
                if (frame % columns == columns - 1 || frame == maxFrame) EditorGUILayout.EndHorizontal();
            }
        }

        void DrawBossWindupEditor(MirrorBossComboStepV2 step)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("定帧前摇（黄色 ▲）", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("将当前帧设为前摇帧", GUILayout.Width(155f)))
                    {
                        Undo.RecordObject(bossProfile, "设置 Boss 前摇帧");
                        step.windupFrame = currentFrame;
                        if (step.windupHoldDuration <= 0f) step.windupHoldDuration = 0.5f;
                        bossPreviewWindupConsumed = false;
                        bossPreviewWindupHoldUntil = 0d;
                    }
                    EditorGUI.BeginDisabledGroup(step.windupFrame < 0);
                    if (GUILayout.Button("取消前摇", GUILayout.Width(90f)))
                    {
                        Undo.RecordObject(bossProfile, "取消 Boss 前摇帧");
                        step.windupFrame = -1;
                        step.windupHoldDuration = 0f;
                    }
                    EditorGUI.EndDisabledGroup();
                }

                EditorGUI.BeginChangeCheck();
                var nextWindupFrame = EditorGUILayout.IntSlider("前摇定格帧", step.windupFrame, -1, Mathf.Max(0, step.animationFrameCount - 1));
                var nextHoldDuration = Mathf.Max(0f, EditorGUILayout.FloatField("定格时间（秒）", step.windupHoldDuration));
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(bossProfile, "编辑 Boss 定帧前摇");
                    step.windupFrame = nextWindupFrame;
                    step.windupHoldDuration = nextHoldDuration;
                    bossPreviewWindupConsumed = false;
                    bossPreviewWindupHoldUntil = 0d;
                }

                var firstActiveFrame = int.MaxValue;
                for (var frame = 0; frame < step.animationFrameCount; frame++)
                {
                    PlayerAttackHitboxKey evaluated;
                    if (EvaluateBossKey(step, frame, out evaluated) && evaluated.enabled)
                    {
                        firstActiveFrame = frame;
                        break;
                    }
                }

                if (step.windupFrame >= 0 && step.windupHoldDuration > 0f && step.windupFrame >= firstActiveFrame)
                    EditorGUILayout.HelpBox("前摇帧必须早于第一个攻击框开启帧。", MessageType.Error);
                else if (step.windupFrame >= 0 && step.windupHoldDuration > 0f)
                    EditorGUILayout.HelpBox($"动画播放到第 {step.windupFrame} 帧后冻结 {step.windupHoldDuration:0.##} 秒，再从该帧继续。", MessageType.Info);
            }
        }
        void DrawBossCurrentKeyEditor(MirrorBossComboStepV2 step)
        {
            var exact = FindBossKey(step, currentFrame);
            PlayerAttackHitboxKey evaluated;
            EvaluateBossKey(step, currentFrame, out evaluated);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(exact == null ? "在当前帧添加 Key" : "更新当前帧 Key", GUILayout.Width(130f)))
                    exact = EnsureBossKey(step, currentFrame, evaluated);
                EditorGUI.BeginDisabledGroup(exact == null);
                if (GUILayout.Button("删除当前帧 Key", GUILayout.Width(120f)) && exact != null)
                {
                    Undo.RecordObject(bossProfile, "删除 Boss 攻击框 Key");
                    step.hitboxKeys.Remove(exact);
                    exact = null;
                }
                EditorGUI.EndDisabledGroup();
            }

            if (exact == null)
            {
                EditorGUILayout.LabelField("当前帧沿用上一枚 Key；拖动 Scene 攻击框会自动在本帧创建 Key。", EditorStyles.miniLabel);
                return;
            }

            Undo.RecordObject(bossProfile, "编辑 Boss 攻击框 Key");
            exact.enabled = EditorGUILayout.Toggle("本帧攻击框开启", exact.enabled);
            exact.offset = EditorGUILayout.Vector2Field("位置 Offset", exact.offset);
            exact.size = ClampHitboxSize(EditorGUILayout.Vector2Field("尺寸 Size", exact.size));
            exact.interpolation = (AttackHitboxInterpolation)EditorGUILayout.EnumPopup("到下一 Key 的插值", exact.interpolation);
        }

        void EnsureBossResources()
        {
            if (!bossProfile) bossProfile = AssetDatabase.LoadAssetAtPath<MirrorBossSimpleProfile>(BossProfilePath);
            if (!bossPreview && !Application.isPlaying) RebuildBossPreview();
        }

        void RebuildBossPreview()
        {
            DisposeBossPreview();
            if (Application.isPlaying) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
            if (!prefab) return;
            bossPreview = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (!bossPreview) return;
            bossPreview.name = "MirrorBoss_HitboxPreview";
            bossPreview.hideFlags = HideFlags.HideAndDontSave;
            bossPreview.transform.position = Vector3.zero;
            bossPreviewAnimator = bossPreview.GetComponentInChildren<Animator>(true);
            var hitbox = bossPreview.GetComponentInChildren<Hitbox>(true);
            bossPreviewHitbox = hitbox ? hitbox.transform : null;
            FocusBossPreview();
            SampleBossFrame(GetBossClip(GetBossStep()?.animationState));
        }

        void FocusBossPreview()
        {
            if (!bossPreview || !SceneView.lastActiveSceneView) return;
            var renderer = bossPreview.GetComponentInChildren<Renderer>(true);
            var bounds = renderer ? renderer.bounds : new Bounds(bossPreview.transform.position, new Vector3(3f, 3f, 0f));
            SceneView.lastActiveSceneView.Frame(bounds, false);
            SceneView.RepaintAll();
        }

        void DisposeBossPreview()
        {
            if (bossPreview) DestroyImmediate(bossPreview);
            bossPreview = null;
            bossPreviewAnimator = null;
            bossPreviewHitbox = null;
        }

        void TickBossAnimationPreview()
        {
            if (!animationPlaying || Application.isPlaying) return;
            var step = GetBossStep();
            var clip = step == null ? null : GetBossClip(step.animationState);
            if (!clip) { animationPlaying = false; return; }

            var now = EditorApplication.timeSinceStartup;
            if (!bossPreviewWindupConsumed && step.windupFrame >= 0 && step.windupHoldDuration > 0f &&
                currentFrame >= step.windupFrame)
            {
                if (bossPreviewWindupHoldUntil <= 0d)
                    bossPreviewWindupHoldUntil = now + step.windupHoldDuration;
                if (now < bossPreviewWindupHoldUntil)
                {
                    SampleBossFrame(clip);
                    Repaint();
                    return;
                }

                bossPreviewWindupConsumed = true;
                bossPreviewWindupHoldUntil = 0d;
                lastAnimationUpdate = now;
            }

            var frameRate = Mathf.Max(1, step.animationFrameRate);
            var advance = Mathf.FloorToInt(Mathf.Max(0f, (float)(now - lastAnimationUpdate)) * frameRate);
            if (advance <= 0) return;
            lastAnimationUpdate += advance / (double)frameRate;
            var previousFrame = currentFrame;
            currentFrame = (currentFrame + advance) % Mathf.Max(2, step.animationFrameCount);
            if (currentFrame < previousFrame)
            {
                bossPreviewWindupConsumed = false;
                bossPreviewWindupHoldUntil = 0d;
            }
            SampleBossFrame(clip);
            Repaint();
        }

        void SampleBossFrame(AnimationClip clip)
        {
            if (Application.isPlaying || !bossPreviewAnimator || !clip) return;
            if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
            var step = GetBossStep();
            var time = Mathf.Min(clip.length, currentFrame / (float)Mathf.Max(1, step.animationFrameRate));
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(bossPreviewAnimator.gameObject, clip, time);
            AnimationMode.EndSampling();
            SceneView.RepaintAll();
        }

        void DrawBossScenePreview(SceneView sceneView)
        {
            if (!bossProfile || !bossPreview || !bossPreviewHitbox || Application.isPlaying) return;
            var step = GetBossStep();
            if (step == null) return;
            PlayerAttackHitboxKey key;
            if (!EvaluateBossKey(step, currentFrame, out key) || key == null) return;

            var localOffset = key.offset;
            if (step.mirrorHitboxByFacing && bossFacingLeft) localOffset.x = -Mathf.Abs(localOffset.x);
            var center = bossPreviewHitbox.TransformPoint(localOffset);
            var size = ClampHitboxSize(key.size);
            var fill = key.enabled ? new Color(1f, 0.2f, 0.08f, 0.18f) : new Color(0.2f, 0.65f, 1f, 0.1f);
            var outline = key.enabled ? new Color(1f, 0.25f, 0.08f, 1f) : new Color(0.2f, 0.65f, 1f, 0.9f);
            Handles.DrawSolidRectangleWithOutline(GetRectCorners(center, size), fill, outline);
            Handles.Label(center + Vector3.up * (size.y * 0.5f + 0.15f), $"{step.animationState} / Frame {currentFrame} / {(key.enabled ? "攻击开启" : "攻击关闭")}");

            EditorGUI.BeginChangeCheck();
            var nextCenter = Handles.PositionHandle(center, Quaternion.identity);
            var nextSize = DrawSizeHandles(ref nextCenter, size);
            if (!EditorGUI.EndChangeCheck()) return;
            Undo.RecordObject(bossProfile, "拖动 Boss 攻击框");
            var writable = EnsureBossKey(step, currentFrame, key);
            var nextLocal = (Vector2)bossPreviewHitbox.InverseTransformPoint(nextCenter);
            if (step.mirrorHitboxByFacing && bossFacingLeft) nextLocal.x = Mathf.Abs(nextLocal.x);
            writable.offset = nextLocal;
            writable.size = ClampHitboxSize(nextSize);
            writable.enabled = true;
            EditorUtility.SetDirty(bossProfile);
            Repaint();
        }

        void EnsureBossStepForClip(MirrorBossComboStepV2 step, AnimationClip clip)
        {
            if (clip)
            {
                var frameRate = Mathf.Max(1, Mathf.RoundToInt(clip.frameRate));
                var frameCount = Mathf.Max(2, Mathf.FloorToInt(clip.length * frameRate) + 1);
                if (!step.hitboxKeysMigratedToClip)
                {
                    Undo.RecordObject(bossProfile, "迁移 Boss 攻击框到动画帧");
                    step.animationFrameRate = frameRate;
                    step.animationFrameCount = frameCount;
                    step.hitboxKeys.Clear();
                    var last = frameCount - 1;
                    var start = Mathf.Clamp(Mathf.RoundToInt(step.activeStart * last), 1, last);
                    var end = Mathf.Clamp(Mathf.RoundToInt(step.activeEnd * last), start + 1, frameCount);
                    step.hitboxKeys.Add(new PlayerAttackHitboxKey { frame = 0, enabled = false, offset = step.hitboxOffset, size = step.hitboxSize });
                    step.hitboxKeys.Add(new PlayerAttackHitboxKey { frame = start, enabled = true, offset = step.hitboxOffset, size = step.hitboxSize, interpolation = AttackHitboxInterpolation.Linear });
                    step.hitboxKeys.Add(new PlayerAttackHitboxKey { frame = end, enabled = false, offset = step.hitboxOffset, size = step.hitboxSize });
                    step.hitboxKeysMigratedToClip = true;
                    EditorUtility.SetDirty(bossProfile);
                }
            }
            step.EnsureHitboxKeys();
        }

        MirrorBossComboStepV2[] GetBossCombo()
        {
            if (!bossProfile) return null;
            return bossPhase == 0 ? bossProfile.phaseOneCombo : bossPhase == 1 ? bossProfile.phaseTwoCombo : bossProfile.phaseThreeCombo;
        }

        MirrorBossComboStepV2 GetBossStep()
        {
            var combo = GetBossCombo();
            return combo != null && combo.Length > 0 ? combo[Mathf.Clamp(bossStepIndex, 0, combo.Length - 1)] : null;
        }

        AnimationClip GetBossClip(string stateName)
        {
            if (!bossPreviewAnimator || !bossPreviewAnimator.runtimeAnimatorController || string.IsNullOrEmpty(stateName)) return null;
            return bossPreviewAnimator.runtimeAnimatorController.animationClips.FirstOrDefault(clip => clip && clip.name == stateName);
        }

        static PlayerAttackHitboxKey FindBossKey(MirrorBossComboStepV2 step, int frame)
        {
            return step.hitboxKeys?.FirstOrDefault(key => key != null && key.frame == frame);
        }

        PlayerAttackHitboxKey EnsureBossKey(MirrorBossComboStepV2 step, int frame, PlayerAttackHitboxKey source)
        {
            var key = FindBossKey(step, frame);
            if (key != null) return key;
            Undo.RecordObject(bossProfile, "添加 Boss 攻击框 Key");
            key = new PlayerAttackHitboxKey
            {
                frame = frame,
                enabled = source != null && source.enabled,
                offset = source != null ? source.offset : step.hitboxOffset,
                size = source != null ? source.size : step.hitboxSize,
                interpolation = source != null ? source.interpolation : AttackHitboxInterpolation.Step
            };
            step.hitboxKeys.Add(key);
            step.hitboxKeys.Sort((a, b) => a.frame.CompareTo(b.frame));
            EditorUtility.SetDirty(bossProfile);
            return key;
        }

        static bool EvaluateBossKey(MirrorBossComboStepV2 step, int frame, out PlayerAttackHitboxKey result)
        {
            result = null;
            if (step.hitboxKeys == null || step.hitboxKeys.Count == 0) return false;
            PlayerAttackHitboxKey previous = null;
            PlayerAttackHitboxKey next = null;
            foreach (var key in step.hitboxKeys)
            {
                if (key == null) continue;
                if (key.frame <= frame && (previous == null || key.frame >= previous.frame)) previous = key;
                if (key.frame > frame && (next == null || key.frame < next.frame)) next = key;
            }
            if (previous == null) return false;
            result = previous;
            if (next == null || previous.interpolation != AttackHitboxInterpolation.Linear || !previous.enabled || !next.enabled || next.frame <= previous.frame) return true;
            var t = Mathf.InverseLerp(previous.frame, next.frame, frame);
            result = new PlayerAttackHitboxKey
            {
                frame = frame,
                enabled = true,
                offset = Vector2.Lerp(previous.offset, next.offset, t),
                size = Vector2.Lerp(previous.size, next.size, t),
                interpolation = previous.interpolation
            };
            return true;
        }

        void SaveBossProfile()
        {
            if (!bossProfile) return;
            EditorUtility.SetDirty(bossProfile);
            AssetDatabase.SaveAssets();
            ShowNotification(new GUIContent("Boss 攻击框已保存"));
        }
    }
}
#endif
