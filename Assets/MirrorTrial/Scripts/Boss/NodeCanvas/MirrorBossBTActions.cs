using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;

namespace MirrorTrial.Boss.NodeCanvasIntegration
{
    [Name("等待 Boss 被战斗区域激活")]
    [Description("持续等待 MirrorBossBattleAreaV3 传入真实玩家目标。没有目标时 Boss 不移动、不攻击。")]
    public sealed class WaitForMirrorBossActivation : ActionTask<MirrorBossActorV2>
    {
        protected override void OnUpdate()
        {
            if (agent.IsActivated) EndAction(true);
        }
    }

    [Name("登场准备")]
    [Description("首次激活后播放持剑待机，让玩家看清 Boss。持续时间可以直接在 BT 节点中修改。")]
    public sealed class MirrorBossIntroAction : ActionTask<MirrorBossActorV2>
    {
        [Min(0f), Tooltip("中文备注：Boss 激活后的登场停顿秒数。")]
        public float duration = 0.8f;

        protected override void OnExecute()
        {
            if (agent.IntroCompleted) { EndAction(true); return; }
            agent.BTBeginIntro();
        }

        protected override void OnUpdate()
        {
            if (agent.IntroCompleted) { EndAction(true); return; }
            if (elapsedTime < duration) return;
            agent.BTFinishIntro();
            EndAction(true);
        }
    }

    [Name("等待可行动状态")]
    [Description("转阶段期间暂停普通决策；死亡后永久停止。转阶段由受伤逻辑立即触发，结束后行为树从追击继续。")]
    public sealed class WaitForMirrorBossReady : ActionTask<MirrorBossActorV2>
    {
        protected override void OnUpdate()
        {
            if (agent.CurrentState == MirrorBossActorV2.State.Dead) return;
            if (agent.CurrentState != MirrorBossActorV2.State.PhaseChange) EndAction(true);
        }
    }

    [Name("追击玩家直到进入攻击距离")]
    [Description("每帧朝真实玩家移动。超出攻击距离时播放 SwordWalk；进入攻击距离后停止并返回成功，让 BT 进入连招节点。")]
    public sealed class MirrorBossApproachAction : ActionTask<MirrorBossActorV2>
    {
        protected override void OnUpdate()
        {
            if (agent.CurrentState == MirrorBossActorV2.State.Dead) return;
            if (agent.CurrentState == MirrorBossActorV2.State.PhaseChange) { EndAction(false); return; }
            if (agent.BTTickApproach()) EndAction(true);
        }

        protected override void OnStop(bool interrupted)
        {
            if (interrupted) agent.BTStopHorizontal();
        }
    }

    [Name("执行当前阶段固定剑招")]
    [Description("播放当前阶段连招；每一招可在 Boss 动画编辑器指定一个动画帧并定格一段时间作为前摇，定格期间攻击框强制关闭，之后从该帧继续播放。")]
    public sealed class MirrorBossComboAction : ActionTask<MirrorBossActorV2>
    {
        protected override void OnExecute()
        {
            if (!agent.BTBeginCombo()) EndAction(false);
        }

        protected override void OnUpdate()
        {
            if (agent.CurrentState == MirrorBossActorV2.State.Dead) return;
            if (agent.CurrentState == MirrorBossActorV2.State.PhaseChange) { EndAction(false); return; }
            if (agent.CurrentState == MirrorBossActorV2.State.Recovery) EndAction(true);
        }
    }

    [Name("执行独立重斩")]
    [Description("锁定开始时的朝向，播放可读蓄力和单次重斩；完成后进入重斩专属后摇。")]
    public sealed class MirrorBossHeavySlashAction : ActionTask<MirrorBossActorV2>
    {
        protected override void OnExecute()
        {
            if (!agent.BTBeginHeavySlash()) EndAction(false);
        }

        protected override void OnUpdate()
        {
            if (agent.CurrentState == MirrorBossActorV2.State.Dead) return;
            if (agent.CurrentState == MirrorBossActorV2.State.PhaseChange) { EndAction(false); return; }
            if (agent.CurrentState == MirrorBossActorV2.State.Recovery) EndAction(true);
        }
    }

    [Name("攻击后硬直")]
    [Description("关闭攻击框并进入可反击窗口。三个阶段的随机时长都可以直接在 BT 节点里调整。")]
    public sealed class MirrorBossRecoveryAction : ActionTask<MirrorBossActorV2>
    {
        [Tooltip("中文备注：第一阶段攻击后的硬直时间范围。")]
        public Vector2 phaseOne = new Vector2(3f, 3.4f);
        [Tooltip("中文备注：第二阶段攻击后的硬直时间范围。")]
        public Vector2 phaseTwo = new Vector2(2.6f, 3f);
        [Tooltip("中文备注：第三阶段攻击后的硬直时间范围。")]
        public Vector2 phaseThree = new Vector2(2.2f, 2.6f);
        float duration;

        protected override void OnExecute()
        {
            agent.BTBeginRecovery();
            if (agent.LastAttackWasHeavySlash)
                duration = Mathf.Max(0f, agent.HeavySlashRecovery);
            else
            {
                var range = agent.Phase == 1 ? phaseOne : agent.Phase == 2 ? phaseTwo : phaseThree;
                duration = Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));
            }
        }

        protected override void OnUpdate()
        {
            if (agent.CurrentState == MirrorBossActorV2.State.Dead) return;
            if (agent.CurrentState == MirrorBossActorV2.State.PhaseChange) { EndAction(false); return; }
            agent.BTTickRecoveryApproach();
            if (elapsedTime < duration) return;
            agent.BTFinishRecovery();
            EndAction(true);
        }
    }
}
