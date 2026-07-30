using NodeCanvas.Framework;
using ParadoxNotion.Design;

namespace MirrorTrial.Boss.NodeCanvasIntegration
{
    [Name("条件：Boss 尚未激活")]
    public sealed class MirrorBossNotActivatedCondition : ConditionTask<MirrorBossActorV2>
    {
        protected override bool OnCheck() => agent && !agent.IsActivated;
    }

    [Name("条件：远程 Boss 本轮行动")]
    [Description("读取 Boss 已锁定的本轮战术决定，避免行为树每帧重新随机。")]
    public sealed class MirrorBossTacticalDecisionCondition : ConditionTask<MirrorBossActorV2>
    {
        public MirrorBossTacticalAction expected = MirrorBossTacticalAction.Shoot;
        protected override bool OnCheck() => agent && agent.BTDecisionIs(expected);
    }

    [Name("执行远程 Boss 本轮行动")]
    [Description("执行已经锁定的射箭、瞬移、召唤或近身防御动作，完整动作结束后返回成功。")]
    public sealed class MirrorBossExecuteTacticalAction : ActionTask<MirrorBossActorV2>
    {
        protected override void OnExecute()
        {
            if (!agent.BTBeginTacticalDecision()) EndAction(false);
        }

        protected override void OnUpdate()
        {
            if (agent.CurrentState == MirrorBossActorV2.State.Dead) return;
            if (agent.CurrentState == MirrorBossActorV2.State.PhaseChange ||
                agent.CurrentState == MirrorBossActorV2.State.Launch)
            {
                EndAction(false);
                return;
            }
            if (!agent.IsActionRunning && agent.CurrentState == MirrorBossActorV2.State.Approach)
                EndAction(true);
        }
    }
}
