using NodeCanvas.BehaviourTrees;
using NodeCanvas.Framework;
using UnityEngine;

namespace MirrorTrial.Boss.NodeCanvasIntegration
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MirrorBossActorV2), typeof(BehaviourTreeOwner))]
    public sealed class MirrorBossBlackboardSync : MonoBehaviour
    {
        MirrorBossActorV2 boss;
        BehaviourTreeOwner owner;

        void Awake()
        {
            boss = GetComponent<MirrorBossActorV2>();
            owner = GetComponent<BehaviourTreeOwner>();
        }

        void Update()
        {
            var bb = owner && owner.behaviour ? owner.behaviour.blackboard : null;
            if (bb == null || !boss) return;
            bb.SetVariableValue("isDead", boss.CurrentState == MirrorBossActorV2.State.Dead);
            bb.SetVariableValue("isStunned", boss.IsStunned);
            bb.SetVariableValue("hasAppeared", boss.IntroCompleted);
            bb.SetVariableValue("DistanceToPlayer", boss.DistanceToTarget);
            bb.SetVariableValue("AttackRange", boss.AttackRange);
        }
    }
}
