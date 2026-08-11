#if UNITY_EDITOR
using System.Linq;
using MirrorTrial.Boss;
using MirrorTrial.Combat;
using MirrorTrial.Player;
using UnityEditor;
using UnityEngine;

namespace MirrorTrial.Editor.Boss
{
    public sealed class MirrorBossFrameEditorWindow : EditorWindow
    {
        const string ProfilePath = "Assets/MirrorTrial/Boss/MirrorBossSimpleProfile.asset";
        const string PrefabPath = "Assets/MirrorTrial/Prefabs/Boss/MirrorBoss.prefab";

        MirrorBossSimpleProfile profile;
        GameObject prefab;
        GameObject preview;
        Animator animator;
        int phase;
        int stepIndex;
        int frame;
        bool facingLeft;
        bool playing;
        double lastTick;
        Vector2 scroll;

        [MenuItem("Tools/镜像试炼/战斗/第一个 Boss 逐帧动作编辑器")]
        public static void Open()
        {
            var window = GetWindow<MirrorBossFrameEditorWindow>("第一个 Boss 逐帧编辑");
            window.minSize = new Vector2(760f, 520f);
            window.Show();
        }

        void OnEnable()
        {
            profile = AssetDatabase.LoadAssetAtPath<MirrorBossSimpleProfile>(ProfilePath);
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            EditorApplication.update += Tick;
            SceneView.duringSceneGui += DrawScene;
            RebuildPreview();
        }

        void OnDisable()
        {
            EditorApplication.update -= Tick;
            SceneView.duringSceneGui -= DrawScene;
            DisposePreview();
            if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
        }

        void OnGUI()
        {
            DrawToolbar();
            if (!profile || !prefab)
            {
                EditorGUILayout.HelpBox("请绑定 MirrorBossSimpleProfile 和 MirrorBoss.prefab。", MessageType.Warning);
                return;
            }

            EditorGUI.BeginChangeCheck();
            phase = GUILayout.Toolbar(phase, new[] { "阶段 1", "阶段 2", "阶段 3", "蓄力重斩" });
            if (EditorGUI.EndChangeCheck()) { stepIndex = 0; frame = 0; playing = false; Sample(); }

            var combo = GetCombo();
            if (combo == null || combo.Length == 0) return;
            stepIndex = Mathf.Clamp(stepIndex, 0, combo.Length - 1);
            if (phase < 3)
            {
                var labels = combo.Select((s, i) => $"{i + 1}. {(s == null ? "空剑招" : s.animationState)}").ToArray();
                EditorGUI.BeginChangeCheck();
                stepIndex = EditorGUILayout.Popup("当前剑招", stepIndex, labels);
                if (EditorGUI.EndChangeCheck()) { frame = 0; playing = false; Sample(); }
            }

            var step = GetStep();
            if (step == null) return;
            var clip = GetClip(step);
            SyncClipData(step, clip);
            var maxFrame = Mathf.Max(1, step.animationFrameCount - 1);
            frame = Mathf.Clamp(frame, 0, maxFrame);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("动画逐帧预览与攻击框", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("动画片段", clip ? $"{clip.name}  {clip.length:0.###} 秒  {step.animationFrameRate} FPS" : "未找到同名 AnimationClip");
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("|<", GUILayout.Width(34))) SetFrame(0);
                    if (GUILayout.Button(playing ? "暂停" : "播放", GUILayout.Width(55))) { playing = !playing; lastTick = EditorApplication.timeSinceStartup; }
                    if (GUILayout.Button("<", GUILayout.Width(34))) SetFrame(frame - 1);
                    if (GUILayout.Button(">", GUILayout.Width(34))) SetFrame(frame + 1);
                    EditorGUI.BeginChangeCheck();
                    frame = EditorGUILayout.IntSlider("当前帧", frame, 0, maxFrame);
                    if (EditorGUI.EndChangeCheck()) { playing = false; Sample(); }
                }
                facingLeft = EditorGUILayout.Toggle("预览向左攻击", facingLeft);
                DrawTimeline(step, maxFrame);
                DrawWindup(step, maxFrame);
                DrawCurrentKey(step);
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("剑招参数", EditorStyles.boldLabel);
                step.playbackSpeed = Mathf.Max(0.01f, EditorGUILayout.FloatField("动画速度", step.playbackSpeed));
                step.gapAfter = Mathf.Max(0f, EditorGUILayout.FloatField("招式后间隔", step.gapAfter));
                step.advanceSpeed = EditorGUILayout.FloatField("攻击推进速度", step.advanceSpeed);
                step.damage = Mathf.Max(1, EditorGUILayout.IntField("伤害", step.damage));
                step.knockback = EditorGUILayout.Vector2Field("击退", step.knockback);
                step.hitStop = Mathf.Max(0f, EditorGUILayout.FloatField("顿帧", step.hitStop));
                step.playerHitReaction = (HitReactionType)EditorGUILayout.EnumPopup("玩家受击反应", step.playerHitReaction);
                step.mirrorHitboxByFacing = EditorGUILayout.Toggle("攻击框随朝向镜像", step.mirrorHitboxByFacing);
            }
            EditorGUILayout.EndScrollView();

            if (GUI.changed) { EditorUtility.SetDirty(profile); SceneView.RepaintAll(); }
        }

        void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                profile = (MirrorBossSimpleProfile)EditorGUILayout.ObjectField(profile, typeof(MirrorBossSimpleProfile), false, GUILayout.MinWidth(220));
                prefab = (GameObject)EditorGUILayout.ObjectField(prefab, typeof(GameObject), false, GUILayout.MinWidth(180));
                if (GUILayout.Button("重建预览", EditorStyles.toolbarButton, GUILayout.Width(80))) RebuildPreview();
                if (GUILayout.Button("保存", EditorStyles.toolbarButton, GUILayout.Width(55))) { EditorUtility.SetDirty(profile); AssetDatabase.SaveAssets(); }
            }
        }

        void DrawTimeline(MirrorBossComboStepV2 step, int maxFrame)
        {
            EditorGUILayout.LabelField("逐帧攻击框（● = Key，▲ = 前摇）", EditorStyles.miniBoldLabel);
            var columns = Mathf.Max(6, Mathf.FloorToInt((position.width - 70f) / 42f));
            var old = GUI.backgroundColor;
            for (var i = 0; i <= maxFrame; i++)
            {
                if (i % columns == 0) EditorGUILayout.BeginHorizontal();
                var key = FindKey(step, i);
                var enabled = Evaluate(step, i, out var evaluated) && evaluated.enabled;
                GUI.backgroundColor = i == frame ? new Color(.25f, .9f, .55f) : i == step.windupFrame ? new Color(1f, .82f, .12f) : enabled ? new Color(1f, .58f, .16f) : Color.gray;
                if (GUILayout.Button((i == step.windupFrame ? "▲" : key != null ? "●" : "") + i, EditorStyles.miniButton, GUILayout.Width(38))) SetFrame(i);
                GUI.backgroundColor = old;
                if (i % columns == columns - 1 || i == maxFrame) EditorGUILayout.EndHorizontal();
            }
        }

        void DrawWindup(MirrorBossComboStepV2 step, int maxFrame)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("将当前帧设为前摇帧")) step.windupFrame = frame;
                if (GUILayout.Button("取消前摇")) step.windupFrame = -1;
            }
            step.windupFrame = EditorGUILayout.IntSlider("前摇定格帧", step.windupFrame, -1, maxFrame);
            step.windupHoldDuration = Mathf.Max(0f, EditorGUILayout.FloatField("前摇定格时间", step.windupHoldDuration));
            step.windupEffectOffset = EditorGUILayout.Vector2Field("前摇特效 Offset", step.windupEffectOffset);
        }

        void DrawCurrentKey(MirrorBossComboStepV2 step)
        {
            var exact = FindKey(step, frame);
            Evaluate(step, frame, out var evaluated);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(exact == null ? "在当前帧添加 Key" : "更新当前帧 Key")) exact = EnsureKey(step, frame, evaluated);
                using (new EditorGUI.DisabledScope(exact == null))
                    if (GUILayout.Button("删除当前帧 Key") && exact != null) { Undo.RecordObject(profile, "Delete Boss Hitbox Key"); step.hitboxKeys.Remove(exact); }
            }
            if (exact == null) return;
            exact.enabled = EditorGUILayout.Toggle("本帧攻击框开启", exact.enabled);
            exact.offset = EditorGUILayout.Vector2Field("位置 Offset", exact.offset);
            exact.size = ClampSize(EditorGUILayout.Vector2Field("尺寸 Size", exact.size));
            exact.interpolation = (AttackHitboxInterpolation)EditorGUILayout.EnumPopup("到下一 Key 的插值", exact.interpolation);
        }

        void DrawScene(SceneView scene)
        {
            if (!preview || GetStep() == null || !Evaluate(GetStep(), frame, out var value)) return;
            var sign = facingLeft && GetStep().mirrorHitboxByFacing ? -1f : 1f;
            var center = preview.transform.position + new Vector3(value.offset.x * sign, value.offset.y);
            Handles.color = value.enabled ? Color.red : Color.cyan;
            Handles.DrawWireCube(center, value.size);
            EditorGUI.BeginChangeCheck();
            var moved = Handles.PositionHandle(center, Quaternion.identity);
            var scaled = Handles.ScaleHandle(value.size, center, Quaternion.identity, HandleUtility.GetHandleSize(center));
            if (!EditorGUI.EndChangeCheck()) return;
            var key = EnsureKey(GetStep(), frame, value);
            Undo.RecordObject(profile, "Edit Boss Frame Hitbox");
            key.offset = new Vector2((moved.x - preview.transform.position.x) * sign, moved.y - preview.transform.position.y);
            key.size = ClampSize(scaled);
            EditorUtility.SetDirty(profile);
            Repaint();
        }

        void Tick()
        {
            if (!playing || GetStep() == null) return;
            var now = EditorApplication.timeSinceStartup;
            var fps = Mathf.Max(1, GetStep().animationFrameRate);
            if (now - lastTick < 1d / fps) return;
            lastTick = now;
            frame = (frame + 1) % Mathf.Max(2, GetStep().animationFrameCount);
            Sample(); Repaint();
        }

        void SetFrame(int value) { playing = false; frame = Mathf.Clamp(value, 0, Mathf.Max(1, GetStep()?.animationFrameCount - 1 ?? 1)); Sample(); Repaint(); }
        MirrorBossComboStepV2[] GetCombo() => !profile ? null : phase == 0 ? profile.phaseOneCombo : phase == 1 ? profile.phaseTwoCombo : phase == 2 ? profile.phaseThreeCombo : new[] { profile.heavySlash };
        MirrorBossComboStepV2 GetStep() { var c = GetCombo(); return c != null && c.Length > 0 ? c[Mathf.Clamp(stepIndex, 0, c.Length - 1)] : null; }
        AnimationClip GetClip(MirrorBossComboStepV2 step) => animator && animator.runtimeAnimatorController ? animator.runtimeAnimatorController.animationClips.FirstOrDefault(c => c && c.name == step.animationState) : null;

        void SyncClipData(MirrorBossComboStepV2 step, AnimationClip clip)
        {
            if (!clip) { step.EnsureHitboxKeys(); return; }
            step.animationFrameRate = Mathf.Max(1, Mathf.RoundToInt(clip.frameRate));
            step.animationFrameCount = Mathf.Max(2, Mathf.RoundToInt(clip.length * clip.frameRate) + 1);
            step.EnsureHitboxKeys();
        }

        void Sample()
        {
            var step = GetStep(); var clip = step == null ? null : GetClip(step);
            if (!animator || !clip) return;
            if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(animator.gameObject, clip, Mathf.Min(clip.length, frame / Mathf.Max(1f, clip.frameRate)));
            AnimationMode.EndSampling();
            SceneView.RepaintAll();
        }

        void RebuildPreview()
        {
            DisposePreview();
            if (!prefab || Application.isPlaying) return;
            preview = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (!preview) return;
            preview.name = "MirrorBoss_FramePreview"; preview.hideFlags = HideFlags.HideAndDontSave; preview.transform.position = Vector3.zero;
            animator = preview.GetComponentInChildren<Animator>(true);
            var renderer = preview.GetComponentInChildren<Renderer>(true);
            if (renderer && SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.Frame(renderer.bounds, false);
            Sample();
        }

        void DisposePreview() { if (preview) DestroyImmediate(preview); preview = null; animator = null; }
        static PlayerAttackHitboxKey FindKey(MirrorBossComboStepV2 step, int at) => step.hitboxKeys?.FirstOrDefault(k => k != null && k.frame == at);
        PlayerAttackHitboxKey EnsureKey(MirrorBossComboStepV2 step, int at, PlayerAttackHitboxKey source)
        {
            var key = FindKey(step, at); if (key != null) return key;
            Undo.RecordObject(profile, "Add Boss Hitbox Key");
            key = new PlayerAttackHitboxKey { frame = at, enabled = source?.enabled ?? false, offset = source?.offset ?? step.hitboxOffset, size = source?.size ?? step.hitboxSize, interpolation = source?.interpolation ?? AttackHitboxInterpolation.Step };
            step.hitboxKeys.Add(key); step.hitboxKeys.Sort((a, b) => a.frame.CompareTo(b.frame)); return key;
        }
        static bool Evaluate(MirrorBossComboStepV2 step, int at, out PlayerAttackHitboxKey result)
        {
            result = null; if (step?.hitboxKeys == null) return false;
            foreach (var key in step.hitboxKeys.OrderBy(k => k.frame)) { if (key.frame > at) break; result = key; }
            return result != null;
        }
        static Vector2 ClampSize(Vector2 size) => new Vector2(Mathf.Max(.01f, size.x), Mathf.Max(.01f, size.y));
    }
}
#endif
