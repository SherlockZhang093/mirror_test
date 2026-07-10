using UnityEngine;

namespace MirrorTrial.Player
{
    /// <summary>
    /// 纯展示层：只读 PlayerStateMachine.CurrentState，映射到 Animator 播放。
    /// 不再自己判断 locomotion，也不再持有 forcedState —— 唯一真相在状态机。
    ///
    /// ForceState / ClearForcedState 保留为兼容 API，转发给状态机的 RequestAction / ReleaseAction。
    /// </summary>
    [RequireComponent(typeof(Animator), typeof(PlayerMotor), typeof(PlayerStateMachine))]
    public class PlayerAnimationDriver : MonoBehaviour
    {
        [Header("Blend")]
        [SerializeField] float fadeDuration = 0.08f;

        [Header("Animation States")]
        [SerializeField] string idleState = "Idle";
        [SerializeField] string runState = "Run";
        [SerializeField] string jumpRiseState = "JumpRise";
        [SerializeField] string jumpFallState = "JumpFall";
        [SerializeField] string landState = "Land";
        [SerializeField] string attackState = "SwordAttack";
        [SerializeField] string castState = "AirSlash";
        [SerializeField] string dashState = "Dash";
        [SerializeField] string hurtState = "HitDamage";
        [SerializeField] string deadState = "Die";

        Animator animator;
        PlayerStateMachine stateMachine;
        PlayerActionState playingState = PlayerActionState.None;

        void Awake()
        {
            animator = GetComponent<Animator>();
            stateMachine = GetComponent<PlayerStateMachine>();
        }

        void Update()
        {
            if (stateMachine == null) return;
            // 状态机已在本帧更早（DefaultExecutionOrder -100）解算好 CurrentState
            Play(stateMachine.CurrentState);
        }

        // ── 兼容旧调用：转发给状态机 ──
        public void ForceState(PlayerActionState state) { if (stateMachine != null) stateMachine.RequestAction(state); }
        public void ClearForcedState(PlayerActionState state) { if (stateMachine != null) stateMachine.ReleaseAction(state); }

        void Play(PlayerActionState state)
        {
            if (state == playingState)
                return;

            // Land 态若没有对应动画名，回退成 Idle，避免卡在无效 state
            var stateName = GetAnimationStateName(state);
            if (string.IsNullOrEmpty(stateName))
            {
                if (state == PlayerActionState.Land)
                    stateName = idleState;
                if (string.IsNullOrEmpty(stateName))
                    return;
            }

            animator.CrossFade(stateName, fadeDuration);
            playingState = state;
        }

        string GetAnimationStateName(PlayerActionState state)
        {
            switch (state)
            {
                case PlayerActionState.Idle: return idleState;
                case PlayerActionState.Run: return runState;
                case PlayerActionState.JumpRise: return jumpRiseState;
                case PlayerActionState.JumpFall: return jumpFallState;
                case PlayerActionState.Land: return landState;
                case PlayerActionState.Attack: return attackState;
                case PlayerActionState.Cast: return castState;
                case PlayerActionState.Dash: return dashState;
                case PlayerActionState.Hurt: return hurtState;
                case PlayerActionState.Dead: return deadState;
                default: return string.Empty;
            }
        }
    }
}
