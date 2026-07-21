using NodeCanvas.StateMachines;
using ParadoxNotion.Design;

namespace MirrorTrial.Boss.NodeCanvasIntegration
{
    [Name("镜中 Boss 状态（运行观察）")]
    [Description("显示 MirrorBossActorV2 当前正在执行的状态。该节点保持运行，由同步器在 Boss 状态变化时切换节点。节点的中文备注用于说明设计意图。")]
    public sealed class MirrorBossObservedState : FSMState
    {
        protected override void OnEnter()
        {
            // 状态由现有 Boss 执行层驱动；保持 Running 以便在 NodeCanvas 中持续高亮。
        }
    }
}
