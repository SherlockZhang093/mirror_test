using UnityEngine;

namespace MirrorTrial.Player
{
    /// <summary>
    /// 每帧 Update 第一件事：解算出唯一权威 State。
    /// Motor / AnimationDriver / Combat 等全部读这个 State，不再各自维护隐式状态。
    ///
    /// 用 [DefaultExecutionOrder] 保证它比 Motor / AnimationDriver 先跑，
    /// 这样它们本帧读到的 CurrentState 就是最新解算结果。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(PlayerInputReader), typeof(PlayerMotor))]
    public class PlayerStateMachine : MonoBehaviour
    {
        [Header("Land buffer")]
        [Tooltip("落地后维持 Land 态的时间（秒），播放着地动画/缓冲。0 = 不用 Land 态")]
        [SerializeField] float landStateDuration = 0.05f;

        [Header("Debug")]
        [SerializeField] bool logStateChanges = true;

        PlayerInputReader input;
        PlayerMotor motor;

        // 唯一权威状态
        public PlayerActionState CurrentState { get; private set; } = PlayerActionState.Idle;
        public PlayerActionState PreviousState { get; private set; } = PlayerActionState.Idle;

        // 动作态锁：由 Combat / Loadout / DamageReceiver 通过 RequestAction / ReleaseAction 设置
        PlayerActionState requestedAction = PlayerActionState.None;

        // 落地事件检测
        bool wasGroundedLastFrame;
        float landTimer;

        // 便捷查询
        public bool IsInActionState =>
            CurrentState == PlayerActionState.Attack ||
            CurrentState == PlayerActionState.Cast ||
            CurrentState == PlayerActionState.Dash ||
            CurrentState == PlayerActionState.Hurt ||
            CurrentState == PlayerActionState.Dead;

        /// <summary>动作态期间应锁定水平移动（Attack/Hurt/Dead 锁，Dash 由自身控制速度）</summary>
        public bool ShouldLockMovement =>
            CurrentState == PlayerActionState.Attack ||
            CurrentState == PlayerActionState.Hurt ||
            CurrentState == PlayerActionState.Dead;

        void Awake()
        {
            input = GetComponent<PlayerInputReader>();
            motor = GetComponent<PlayerMotor>();
            wasGroundedLastFrame = motor.IsGrounded;
        }

        void Update()
        {
            Tick();
        }

        /// <summary>
        /// 每帧解算权威 State。优先级从高到低。
        /// </summary>
        void Tick()
        {
            // 1) 落地事件检测：空中 → 地面 的那一帧，进入 Land 缓冲
            // PlayerMotor owns the single authoritative grounded result.
            bool grounded = motor.IsGrounded;
            if (grounded && !wasGroundedLastFrame)
                landTimer = landStateDuration;
            else if (landTimer > 0f)
                landTimer -= Time.deltaTime;
            wasGroundedLastFrame = grounded;

            var next = Resolve(grounded);

            if (next != CurrentState)
            {
                PreviousState = CurrentState;
                CurrentState = next;
                if (logStateChanges)
                    Debug.Log($"[SM] {PreviousState} → {CurrentState} | grounded={grounded} vel.y={motor.Velocity.y:F2} moveX={input.MoveX:F1}");
            }
        }

        PlayerActionState Resolve(bool grounded)
        {
            // 最高优先级：外部请求的动作态（死亡/受击/攻击/施法/冲刺）
            if (requestedAction != PlayerActionState.None)
                return requestedAction;

            // 空中：上升 / 下落
            if (!grounded)
                return motor.IsRising ? PlayerActionState.JumpRise : PlayerActionState.JumpFall;

            // 刚落地缓冲帧
            if (landTimer > 0f)
                return PlayerActionState.Land;

            // 地面移动 / 站立
            return Mathf.Abs(input.MoveX) > 0.01f ? PlayerActionState.Run : PlayerActionState.Idle;
        }

        /// <summary>Combat / Loadout / DamageReceiver 请求进入某动作态（锁定优先级最高）</summary>
        public void RequestAction(PlayerActionState state)
        {
            requestedAction = state;
        }

        /// <summary>释放动作态（仅当当前请求就是该态时才清除，避免误清别人的态）</summary>
        public void ReleaseAction(PlayerActionState state)
        {
            if (requestedAction == state)
                requestedAction = PlayerActionState.None;
        }
    }
}
