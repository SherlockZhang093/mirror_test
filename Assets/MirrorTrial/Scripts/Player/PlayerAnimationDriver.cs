using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace MirrorTrial.Player
{
    public enum PlayerAnimationTimingSource
    {
        ClipDuration,
        AttackWindow,
        CastWindow,
        DashDuration,
        DodgeDuration,
        HurtLock
    }

    [Serializable]
    public sealed class PlayerAnimationBinding
    {
        [SerializeField] PlayerActionState action;
        [SerializeField] string animatorState;
        [SerializeField] float sourceClipDuration;
        [SerializeField] PlayerAnimationTimingSource timingSource;

        public PlayerActionState Action => action;
        public string AnimatorState => animatorState;
        public float SourceClipDuration => sourceClipDuration;
        public PlayerAnimationTimingSource TimingSource => timingSource;

        public PlayerAnimationBinding()
        {
        }

        public PlayerAnimationBinding(PlayerActionState action, string animatorState, float sourceClipDuration, PlayerAnimationTimingSource timingSource)
        {
            this.action = action;
            this.animatorState = animatorState;
            this.sourceClipDuration = sourceClipDuration;
            this.timingSource = timingSource;
        }

        public float GetPlaybackSpeed(PlayerTuning tuning)
        {
            if (sourceClipDuration <= 0f)
                return 1f;

            var targetDuration = GetTargetDuration(tuning);
            return sourceClipDuration / Mathf.Max(0.01f, targetDuration);
        }

        public float GetTargetDuration(PlayerTuning tuning)
        {
            if (!tuning)
                return sourceClipDuration;

            switch (timingSource)
            {
                case PlayerAnimationTimingSource.AttackWindow:
                    return tuning.combat.attackStartup + tuning.combat.attackActiveTime + tuning.combat.attackRecovery;
                case PlayerAnimationTimingSource.CastWindow:
                    return tuning.abilities.mirrorBladeStartup + tuning.abilities.mirrorBladeRecovery;
                case PlayerAnimationTimingSource.DashDuration:
                    return tuning.abilities.echoDashDuration;
                case PlayerAnimationTimingSource.DodgeDuration:
                    return tuning.dodge.duration;
                case PlayerAnimationTimingSource.HurtLock:
                    return tuning.hurt.hurtLockTime;
                default:
                    return sourceClipDuration;
            }
        }
    }

    [RequireComponent(typeof(PlayerMotor), typeof(PlayerStateMachine), typeof(PlayerTuning))]
    public class PlayerAnimationDriver : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] Animator animator;

        [Header("Blend")]
        [SerializeField] float fadeDuration = 0.04f;

        [Header("Animation Table")]
        [SerializeField] List<PlayerAnimationBinding> animations = CreateDefaultBindings();

        PlayerStateMachine stateMachine;
        PlayerTuning tuning;
        PlayerWeaponController weapons;
        PlayerActionState playingState = PlayerActionState.None;
        PlayableGraph actionGraph;
        AnimationClipPlayable actionPlayable;
        double actionPlayableSpeed = 1d;
        float animatorSpeedBeforePause = 1f;
        bool actionClipPlaying;

        public Animator Animator => animator;
        public float FadeDuration => fadeDuration;
        public IList<PlayerAnimationBinding> Animations { get { return animations; } }

        void Awake()
        {
            if (!animator)
                animator = GetComponentInChildren<Animator>(true);

            if (animator)
            {
                animator.enabled = true;
                animator.applyRootMotion = false;
            }

            stateMachine = GetComponent<PlayerStateMachine>();
            tuning = GetComponent<PlayerTuning>();
            weapons = GetComponent<PlayerWeaponController>();
            if (weapons) weapons.WeaponChanged += OnWeaponChanged;
            EnsureAnimationBindings();
        }

        void OnValidate()
        {
            EnsureAnimationBindings();
        }

        void OnDestroy()
        {
            if (weapons) weapons.WeaponChanged -= OnWeaponChanged;
            StopActionClip();
        }

        void OnWeaponChanged(PlayerWeaponType weapon)
        {
            playingState = PlayerActionState.None;
        }

        void Update()
        {
            if (!animator || stateMachine == null || actionClipPlaying)
                return;

            Play(stateMachine.CurrentState);
        }

        public void PlayActionClip(AnimationClip clip, float targetDuration)
        {
            PlayActionClip(clip, targetDuration, 0f);
        }

        public void PlayActionClip(AnimationClip clip, float targetDuration, float startTime)
        {
            if (!animator || !clip)
                return;
            StopActionClip();
            actionGraph = PlayableGraph.Create("Player Direct Action Clip");
            var output = AnimationPlayableOutput.Create(actionGraph, "Action", animator);
            actionPlayable = AnimationClipPlayable.Create(actionGraph, clip);
            actionPlayable.SetApplyFootIK(false);
            var clampedStartTime = Mathf.Clamp(startTime, 0f, clip.length);
            var remainingDuration = Mathf.Max(0.0001f, clip.length - clampedStartTime);
            actionPlayableSpeed = targetDuration > 0f ? remainingDuration / targetDuration : 1f;
            actionPlayable.SetTime(clampedStartTime);
            actionPlayable.SetSpeed(actionPlayableSpeed);
            output.SetSourcePlayable(actionPlayable);
            actionGraph.Play();
            actionClipPlaying = true;
        }

        public void SetActionClipPaused(bool paused)
        {
            if (actionGraph.IsValid() && actionPlayable.IsValid())
            {
                actionPlayable.SetSpeed(paused ? 0d : actionPlayableSpeed);
                return;
            }

            if (!animator)
                return;
            if (paused)
            {
                animatorSpeedBeforePause = animator.speed;
                animator.speed = 0f;
            }
            else
                animator.speed = animatorSpeedBeforePause;
        }

        public void StopActionClip()
        {
            var wasPlaying = actionGraph.IsValid();
            if (wasPlaying)
                actionGraph.Destroy();
            actionClipPlaying = false;
            actionPlayable = default;
            actionPlayableSpeed = 1d;
            playingState = PlayerActionState.None;
            if (wasPlaying && animator)
            {
                animator.Rebind();
                animator.Update(0f);
            }
        }
        public void ForceState(PlayerActionState state)
        {
            if (stateMachine != null)
                stateMachine.RequestAction(state);
        }

        public void PlayStateImmediately(PlayerActionState state)
        {
            StopActionClip();
            playingState = PlayerActionState.None;
            Play(state);
        }

        public void ClearForcedState(PlayerActionState state)
        {
            if (stateMachine != null)
                stateMachine.ReleaseAction(state);
        }

        void Play(PlayerActionState state)
        {
            if (state == playingState)
                return;

            var binding = FindBinding(state);
            if (binding == null || string.IsNullOrEmpty(binding.AnimatorState))
                return;

            var animatorState = ResolveAnimatorState(state, binding.AnimatorState);
            var stateHash = Animator.StringToHash("Base Layer." + animatorState);
            if (!animator.HasState(0, stateHash))
            {
                Debug.LogError("Player Animator is missing state: " + animatorState, animator);
                return;
            }

            animator.speed = binding.GetPlaybackSpeed(tuning);
            animator.CrossFade(stateHash, fadeDuration, 0, 0f);
            playingState = state;
        }

        string ResolveAnimatorState(PlayerActionState state, string fallback)
        {
            if (!weapons || weapons.CurrentWeapon != PlayerWeaponType.Sword)
                return fallback;
            switch (state)
            {
                case PlayerActionState.Idle: return "SwordIdle";
                case PlayerActionState.Run: return "SwordRun";
                case PlayerActionState.JumpRise: return "SwordJumpRise";
                case PlayerActionState.JumpFall: return "SwordJumpFall";
                default: return fallback;
            }
        }

        PlayerAnimationBinding FindBinding(PlayerActionState state)
        {
            for (var i = 0; i < animations.Count; i++)
                if (animations[i] != null && animations[i].Action == state)
                    return animations[i];
            return null;
        }

        void EnsureAnimationBindings()
        {
            if (animations == null)
                animations = new List<PlayerAnimationBinding>();

            var defaults = CreateDefaultBindings();
            for (var i = 0; i < defaults.Count; i++)
            {
                var exists = false;
                for (var j = 0; j < animations.Count; j++)
                {
                    if (animations[j] != null && animations[j].Action == defaults[i].Action)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                    animations.Add(defaults[i]);
            }
        }

        static List<PlayerAnimationBinding> CreateDefaultBindings()
        {
            return new List<PlayerAnimationBinding>
            {
                B(PlayerActionState.Idle, "Idle"),
                B(PlayerActionState.Run, "Run"),
                B(PlayerActionState.JumpRise, "JumpRise"),
                B(PlayerActionState.JumpFall, "JumpFall"),
                B(PlayerActionState.DoubleJump, "DoubleJump", 0.3333333f),
                B(PlayerActionState.Land, "Land", 0.1667f),
                B(PlayerActionState.Attack, "SwordAttack", 0.5f, PlayerAnimationTimingSource.AttackWindow),
                B(PlayerActionState.Cast, "AirSlash", 0.2857f, PlayerAnimationTimingSource.CastWindow),
                B(PlayerActionState.Dash, "Dash", 0.75f, PlayerAnimationTimingSource.DashDuration),
                B(PlayerActionState.Dodge, "Roll", 0.8333333f, PlayerAnimationTimingSource.DodgeDuration),
                B(PlayerActionState.LedgeHang, "LedgeHang", 0.5f),
                B(PlayerActionState.LedgeClimb, "LedgeClimb", 0.6666666f),
                B(PlayerActionState.MonkeyBarIdle, "MonkeyBarIdle", 0.5833333f),
                B(PlayerActionState.LadderGrab, "LadderGrab", 0.25f),
                B(PlayerActionState.LadderIdle, "LadderIdle", 0.5f),
                B(PlayerActionState.LadderClimbUpLeft, "LadderClimbUpLeft", 0.6666666f),
                B(PlayerActionState.LadderClimbUpRight, "LadderClimbUpRight", 0.6666666f),
                B(PlayerActionState.LadderClimbDownLeft, "LadderClimbDownLeft", 0.6666666f),
                B(PlayerActionState.LadderClimbDownRight, "LadderClimbDownRight", 0.6666666f),
                B(PlayerActionState.LadderClimbFinish, "LadderClimbFinish", 0.6666666f),
                B(PlayerActionState.LadderJumpPrepare, "LadderJumpPrepare", 0.3333333f),
                B(PlayerActionState.AirSlashUp, "AirSlashUp", 0.42857143f),
                B(PlayerActionState.AirSlashDown, "AirSlashDown", 0.42857143f),
                B(PlayerActionState.Hurt, "HitDamage", 0.2143f, PlayerAnimationTimingSource.HurtLock),
                B(PlayerActionState.Dead, "Die"),
                B(PlayerActionState.BowDraw, "BowDraw", 0.25f),
                B(PlayerActionState.BowAim, "BowAim"),
                B(PlayerActionState.BowFull, "BowFull"),
                B(PlayerActionState.BowFire, "BowFire", 0.25f),
                B(PlayerActionState.ComboAttackA, "ComboAttackA", 0.25f),
                B(PlayerActionState.ComboAttackB, "ComboAttackB", 0.25f),
                B(PlayerActionState.ComboAttackC, "ComboAttackC", 0.25f),
                B(PlayerActionState.ComboAttackD, "ComboAttackD", 0.25f),
                B(PlayerActionState.PunchA, "PunchA", 0.25f),
                B(PlayerActionState.PunchB, "PunchB", 0.25f),
                B(PlayerActionState.PunchC, "PunchC", 0.25f),
                B(PlayerActionState.PunchD, "PunchD", 0.4375f),
                B(PlayerActionState.KickA, "KickA", 0.25f),
                B(PlayerActionState.KickB, "KickB", 0.25f),
                B(PlayerActionState.KickC, "KickC", 0.25f),
                B(PlayerActionState.SwordStandingSlash, "SwordStandingSlash", 0.35f),
                B(PlayerActionState.SwordRunSlash, "SwordRunSlash", 0.35f),
                B(PlayerActionState.SwordGuard, "SwordGuard"),
                B(PlayerActionState.SwordGuardImpact, "SwordGuardImpact", 0.2f),
                B(PlayerActionState.SwordSprintSlash, "SwordSprintSlash", 0.35f),
                B(PlayerActionState.CrouchSlash, "CrouchSlash", 0.35f)
            };
        }

        static PlayerAnimationBinding B(PlayerActionState action, string animatorState, float sourceClipDuration = 0f, PlayerAnimationTimingSource timingSource = PlayerAnimationTimingSource.ClipDuration)
        {
            return new PlayerAnimationBinding(action, animatorState, sourceClipDuration, timingSource);
        }
    }
}
