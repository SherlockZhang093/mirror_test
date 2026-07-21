using NodeCanvas.Framework;
using ParadoxNotion.Design;

namespace MirrorTrial.Boss.NodeCanvasIntegration
{
    [Name("条件：死亡或受击硬直")]
    [Description("读取 Blackboard：isDead == true 或 isStunned == true 时通过。用于最高优先级异常分支。")]
    public sealed class MirrorBossAbnormalCondition : ConditionTask<MirrorBossActorV2>
    {
        protected override bool OnCheck()
        {
            return blackboard.GetVariableValue<bool>("isDead") ||
                   blackboard.GetVariableValue<bool>("isStunned");
        }
    }

    [Name("条件：尚未完成出场")]
    [Description("读取 Blackboard：hasAppeared == false 时通过。出场节点完成后会把它设置为 true。")]
    public sealed class MirrorBossHasNotAppearedCondition : ConditionTask<MirrorBossActorV2>
    {
        protected override bool OnCheck() => !blackboard.GetVariableValue<bool>("hasAppeared");
    }

    [Name("条件：玩家在攻击范围内")]
    [Description("读取 Blackboard：DistanceToPlayer <= AttackRange 时通过。距离与攻击范围都能在运行时 Blackboard 中观察。")]
    public sealed class MirrorBossInAttackRangeCondition : ConditionTask<MirrorBossActorV2>
    {
        protected override bool OnCheck()
        {
            // 直接读取 Boss，Blackboard 只负责可视化，避免 Update 顺序导致距离慢一帧。
            return agent && agent.DistanceToTarget <= agent.AttackRange;
        }
    }

    [Name("执行死亡或受击动作")]
    [Description("死亡时保持该分支运行；受击硬直时停止移动并播放受击动画，硬直结束后让顶层 Selector 返回核心战斗。")]
    public sealed class MirrorBossAbnormalAction : ActionTask<MirrorBossActorV2>
    {
        protected override void OnUpdate()
        {
            if (agent.CurrentState == MirrorBossActorV2.State.Dead) return;
            if (agent.IsStunned)
            {
                agent.BTPlayStunned();
                return;
            }
            EndAction(true);
        }
    }
}
