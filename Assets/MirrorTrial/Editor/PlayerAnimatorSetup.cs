using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace MirrorTrial.Editor
{
    [InitializeOnLoad]
    public static class PlayerAnimatorSetup
    {
        const string AnimationFolder = "Assets/MirrorTrial/Animations";
        const string PlayerAnimationFolder = AnimationFolder + "/Player";
        const string ClipFolder = PlayerAnimationFolder + "/Clips";
        const string SharedLaunchFolder = AnimationFolder + "/Enemies/SharedLaunch";
        const string ControllerPath = PlayerAnimationFolder + "/Player_MirrorTrial.controller";
        const string PlayerPrefabPath = "Assets/MirrorTrial/Prefabs/Characters/Player_MirrorTrial.prefab";
        const string SourceAnimationFolder = "Assets/DeadRevolver/PixelPrototypePlayerSprites/Art/Animations/";

        static readonly HashSet<string> LoopingStates = new HashSet<string>
        {
            "Idle", "Run", "JumpRise", "JumpFall",
            "LedgeHang", "MonkeyBarIdle",
            "BowAim", "BowFull",
            "SwordGuard", "SwordSprint",
            "SwordIdle", "SwordWalk", "SwordRun", "SwordRunAltGrip",
            "SwordJumpRise", "SwordJumpMid", "SwordJumpFall"
        };

        static readonly IReadOnlyDictionary<string, string> StateClips =
            new Dictionary<string, string>
            {
                { "Idle", "Idle.anim" },
                { "Run", "Run.anim" },
                { "JumpRise", "JumpRise.anim" },
                { "JumpFall", "JumpFall.anim" },
                { "Land", "Land.anim" },
                { "SwordAttack", "SwordAttack.anim" },
                { "AirSlash", "AirSlash.anim" },
                { "AirSlashUp", "AirSlashUp.anim" },
                { "AirSlashDown", "AirSlashDown.anim" },
                { "Dash", "Dash.anim" },
                { "Roll", "Roll.anim" },
                { "LedgeHang", "LedgeHang.anim" },
                { "LedgeClimb", "LedgeClimb.anim" },
                { "MonkeyBarIdle", "MonkeyBarIdle.anim" },
                { "HitDamage", "HitDamage.anim" },
                { "Die", "Die.anim" },
                { "BowDraw", "BowDraw.anim" },
                { "BowAim", "BowAim.anim" },
                { "BowFull", "BowFull.anim" },
                { "BowFire", "BowFire.anim" },
                // 拳 - 连击
                { "ComboAttackA", "ComboAttackA.anim" },
                { "ComboAttackB", "ComboAttackB.anim" },
                { "ComboAttackC", "ComboAttackC.anim" },
                { "ComboAttackD", "ComboAttackD.anim" },
                // 拳 - 拳击
                { "PunchA", "PunchA.anim" },
                { "PunchB", "PunchB.anim" },
                { "PunchC", "PunchC.anim" },
                // 拳 - 踢击
                { "KickA", "KickA.anim" },
                { "KickB", "KickB.anim" },
                { "KickC", "KickC.anim" },
                // 剑 - 攻击
                { "SwordStandingSlash", "SwordStandingSlash.anim" },
                { "SwordRunSlash", "SwordRunSlash.anim" },
                { "SwordGuard", "SwordGuard.anim" },
                { "SwordGuardImpact", "SwordGuardImpact.anim" },
                { "SwordSprintSlash", "SwordSprintSlash.anim" },
                { "CrouchSlash", "CrouchSlash.anim" },
                // 剑 - 移动 (Loop, 无枚举)
                { "SwordSprint", "SwordSprint.anim" },
                { "SwordIdle", "SwordIdle.anim" },
                { "SwordWalk", "SwordWalk.anim" },
                { "SwordRun", "SwordRun.anim" },
                { "SwordRunAltGrip", "SwordRunAltGrip.anim" },
                { "SwordJumpRise", "SwordJumpRise.anim" },
                { "SwordJumpMid", "SwordJumpMid.anim" },
                { "SwordJumpFall", "SwordJumpFall.anim" }
            };

        static PlayerAnimatorSetup()
        {
            EditorApplication.delayCall += EnsurePlayerAnimatorController;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += EnsurePlayerAnimatorController;
        }

        [MenuItem("Tools/镜像试炼/战斗/重建玩家动画控制器")]
        public static void EnsurePlayerAnimatorController()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            EnsureFolder("Assets/MirrorTrial", "Animations");
            EnsureFolder(AnimationFolder, "Player");
            EnsureFolder(PlayerAnimationFolder, "Clips");

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (!controller)
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            RebuildStates(controller);
            AssignControllerToPlayerPrefab(controller);
            AssetDatabase.SaveAssets();
        }

        static void RebuildStates(AnimatorController controller)
        {
            var stateMachine = controller.layers[0].stateMachine;
            foreach (var childState in stateMachine.states)
                stateMachine.RemoveState(childState.state);

            AnimatorState idleState = null;
            foreach (var pair in StateClips)
            {
                var clip = GetOrCreateLocalClip(pair.Key, pair.Value);
                if (!clip)
                    continue;

                var state = stateMachine.AddState(pair.Key);
                state.motion = clip;
                state.writeDefaultValues = false;

                if (pair.Key == "Idle")
                    idleState = state;
            }

            AddSharedLaunchState(stateMachine, "Launch");
            AddSharedLaunchState(stateMachine, "LaunchGetUp");

            if (idleState != null)
                stateMachine.defaultState = idleState;

            EditorUtility.SetDirty(controller);
        }

        static void AddSharedLaunchState(AnimatorStateMachine stateMachine, string stateName)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{SharedLaunchFolder}/{stateName}.anim");
            if (!clip) return;
            var state = stateMachine.AddState(stateName);
            state.motion = clip;
            state.writeDefaultValues = false;
        }

        static AnimationClip GetOrCreateLocalClip(string stateName, string sourceFileName)
        {
            var sourcePath = SourceAnimationFolder + sourceFileName;
            var targetPath = ClipFolder + "/" + sourceFileName;

            if (!AssetDatabase.LoadAssetAtPath<AnimationClip>(sourcePath))
            {
                Debug.LogError("缺少玩家动画片段：" + sourceFileName);
                return null;
            }

            if (!AssetDatabase.LoadAssetAtPath<AnimationClip>(targetPath))
                AssetDatabase.CopyAsset(sourcePath, targetPath);

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(targetPath);
            if (!clip)
                return null;

            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = LoopingStates.Contains(stateName);
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        static void AssignControllerToPlayerPrefab(AnimatorController controller)
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            if (!root)
                return;

            var animator = root.GetComponentInChildren<Animator>(true);
            if (animator && animator.runtimeAnimatorController != controller)
            {
                animator.runtimeAnimatorController = controller;
                EditorUtility.SetDirty(animator);
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }

            PrefabUtility.UnloadPrefabContents(root);
        }

        static void EnsureFolder(string parent, string child)
        {
            var path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
