using NodeCanvas.StateMachines;
using UnityEngine;

namespace MirrorTrial.Boss.NodeCanvasIntegration
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MirrorBossActorV2), typeof(FSMOwner))]
    public sealed class MirrorBossFSMSynchronizer : MonoBehaviour
    {
        MirrorBossActorV2 boss;
        FSMOwner owner;
        MirrorBossActorV2.State lastState = (MirrorBossActorV2.State)(-1);

        void Awake()
        {
            boss = GetComponent<MirrorBossActorV2>();
            owner = GetComponent<FSMOwner>();
        }

        void LateUpdate()
        {
            if (!boss || !owner || !owner.isRunning || boss.CurrentState == lastState) return;
            lastState = boss.CurrentState;
            owner.TriggerState(GetChineseStateName(lastState), FSM.TransitionCallMode.Clean);
        }

        static string GetChineseStateName(MirrorBossActorV2.State state)
        {
            switch (state)
            {
                case MirrorBossActorV2.State.Dormant: return "01 待机：等待玩家进入战斗区域";
                case MirrorBossActorV2.State.Intro: return "02 登场：锁定玩家并播放准备动作";
                case MirrorBossActorV2.State.Approach: return "03 追击：靠近玩家直到进入攻击距离";
                case MirrorBossActorV2.State.Windup: return "04 前摇：攻击动画在编辑器指定帧定格";
                case MirrorBossActorV2.State.Combo: return "04 连招：执行当前阶段的固定剑招";
                case MirrorBossActorV2.State.Recovery: return "05 硬直：攻击结束后留给玩家反击时间";
                case MirrorBossActorV2.State.PhaseChange: return "06 转阶段：变色并短暂防御";
                case MirrorBossActorV2.State.Dead: return "07 死亡：停止战斗并沿用关卡结算流程";
                default: return state.ToString();
            }
        }
    }
}
