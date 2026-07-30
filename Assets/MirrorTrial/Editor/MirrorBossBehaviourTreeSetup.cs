#if UNITY_EDITOR
using MirrorTrial.Boss.NodeCanvasIntegration;
using NodeCanvas.BehaviourTrees;
using NodeCanvas.Framework;
using UnityEditor;
using UnityEngine;

namespace MirrorTrial.Boss.Editor
{
    [InitializeOnLoad]
    public static class MirrorBossBehaviourTreeSetup
    {
        const string TreePath = "Assets/MirrorTrial/Boss/MirrorBossAI_BT.asset";
        const string OldFsmPath = "Assets/MirrorTrial/Boss/MirrorBossAI_FSM.asset";
        const string PrefabPath = "Assets/MirrorTrial/Prefabs/Boss/MirrorBoss.prefab";
        const string SessionKey = "MirrorTrial.MirrorBossBehaviourTreeSetup.v3";

        static MirrorBossBehaviourTreeSetup() => EditorApplication.delayCall += InstallOnce;
        public static void RebuildFromMenu() => Build(true);

        static void InstallOnce()
        {
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);
            Build(false);
        }

        static void Build(bool force)
        {
            var tree = AssetDatabase.LoadAssetAtPath<BehaviourTree>(TreePath);
            var needsRebuild = force || !tree || !tree.comments.Contains("PrioritySelector-v3-heavy-slash");
            if (needsRebuild)
            {
                if (tree) AssetDatabase.DeleteAsset(TreePath);
                tree = ScriptableObject.CreateInstance<BehaviourTree>();
                tree.name = "镜中Boss_AI_中文行为树";
                tree.comments =
                    "结构版本：PrioritySelector-v3-heavy-slash\n" +
                    "顶层 Dynamic Selector 每帧重查优先级：异常状态 > 出场准备 > 核心战斗。\n" +
                    "核心战斗 Selector：攻击范围内执行剑招与硬直，否则持续追击玩家。\n" +
                    "Blackboard 可直接观察 isDead、isStunned、hasAppeared、DistanceToPlayer、AttackRange。";
                tree.repeat = true;
                tree.updateInterval = 0f;
                tree.blackboard.AddVariable("isDead", false);
                tree.blackboard.AddVariable("isStunned", false);
                tree.blackboard.AddVariable("hasAppeared", false);
                tree.blackboard.AddVariable("DistanceToPlayer", float.PositiveInfinity);
                tree.blackboard.AddVariable("AttackRange", 1.5f);

                var rootSelector = tree.AddNode<Selector>(new Vector2(600, 0));
                rootSelector.dynamic = true;
                rootSelector.comments = "中文备注：顶层动态优先选择器。每帧从左到右重查，异常状态可立即打断登场、追击、连招和硬直。";

                var abnormalSequence = tree.AddNode<Sequencer>(new Vector2(100, 170));
                abnormalSequence.dynamic = true;
                abnormalSequence.comments = "优先级 1：死亡或受击硬直。条件成立时立即中断低优先级行为。";
                var abnormalCondition = AddCondition<MirrorBossAbnormalCondition>(tree, new Vector2(0, 350),
                    "检查 Blackboard：isDead == true 或 isStunned == true。 ");
                var abnormalAction = AddAction<MirrorBossAbnormalAction>(tree, new Vector2(220, 350),
                    "死亡时保持停止；受击时关闭攻击框、停止移动并播放 HitDamage。 ");

                var appearanceSequence = tree.AddNode<Sequencer>(new Vector2(600, 170));
                appearanceSequence.dynamic = true;
                appearanceSequence.comments = "优先级 2：只执行一次的出场准备。完成后 hasAppeared 设置为 true。";
                var appearanceCondition = AddCondition<MirrorBossHasNotAppearedCondition>(tree, new Vector2(480, 350),
                    "检查 Blackboard：hasAppeared == false。 ");
                var introAction = AddAction<MirrorBossIntroAction>(tree, new Vector2(720, 350),
                    "播放登场准备动作；duration 可直接编辑，完成后设置 hasAppeared。 ");

                var combatSelector = tree.AddNode<Selector>(new Vector2(1100, 170));
                combatSelector.dynamic = false;
                combatSelector.comments = "优先级 3：核心战斗。先尝试攻击分支；不在范围时进入追击。连招开始后不会因玩家短暂离开范围而中断。";

                var attackSequence = tree.AddNode<Sequencer>(new Vector2(980, 350));
                attackSequence.dynamic = false;
                attackSequence.comments = "攻击分支：距离条件通过后，完整执行当前阶段剑招，再进入攻击后硬直。";
                var rangeCondition = AddCondition<MirrorBossInAttackRangeCondition>(tree, new Vector2(850, 540),
                    "检查 Blackboard：DistanceToPlayer <= AttackRange。 ");
                var attackChoice = tree.AddNode<Selector>(new Vector2(1100, 540));
                attackChoice.dynamic = false;
                attackChoice.comments = "每轮只选择一次：重斩条件通过时执行独立重斩，否则执行普通阶段连招。";
                var heavySequence = tree.AddNode<Sequencer>(new Vector2(980, 720));
                heavySequence.dynamic = false;
                heavySequence.comments = "重斩分支：25% 初始概率、5 秒冷却、禁止连续使用。";
                var heavyCondition = AddCondition<MirrorBossHeavySlashCondition>(tree, new Vector2(900, 900),
                    "锁定本轮重斩决定；冷却中或上一招为重斩时失败。");
                var heavyAction = AddAction<MirrorBossHeavySlashAction>(tree, new Vector2(1080, 900),
                    "锁定起手朝向，播放 0.8 秒蓄力和单次 ComboAttackD 重斩。");
                var comboAction = AddAction<MirrorBossComboAction>(tree, new Vector2(1260, 720),
                    "执行当前阶段固定剑招；每一招按 Boss 动画编辑器设置的黄色前摇帧定格，结束后从该帧继续，攻击框严格按逐帧关键帧开关。 ");
                var recoveryAction = AddAction<MirrorBossRecoveryAction>(tree, new Vector2(1320, 540),
                    "整套剑招完成后的移动冷却：可以缓慢追击，但攻击权限保持关闭；三个阶段的时间范围可以直接修改。 ");

                var approachAction = AddAction<MirrorBossApproachAction>(tree, new Vector2(1430, 350),
                    "追击分支：持续向玩家移动；进入 AttackRange 后返回成功，下一帧重新选择攻击分支。 ");

                tree.primeNode = rootSelector;
                tree.ConnectNodes(rootSelector, abnormalSequence);
                tree.ConnectNodes(rootSelector, appearanceSequence);
                tree.ConnectNodes(rootSelector, combatSelector);
                tree.ConnectNodes(abnormalSequence, abnormalCondition);
                tree.ConnectNodes(abnormalSequence, abnormalAction);
                tree.ConnectNodes(appearanceSequence, appearanceCondition);
                tree.ConnectNodes(appearanceSequence, introAction);
                tree.ConnectNodes(combatSelector, attackSequence);
                tree.ConnectNodes(combatSelector, approachAction);
                tree.ConnectNodes(attackSequence, rangeCondition);
                tree.ConnectNodes(attackSequence, attackChoice);
                tree.ConnectNodes(attackSequence, recoveryAction);
                tree.ConnectNodes(attackChoice, heavySequence);
                tree.ConnectNodes(attackChoice, comboAction);
                tree.ConnectNodes(heavySequence, heavyCondition);
                tree.ConnectNodes(heavySequence, heavyAction);
                tree.SelfSerialize();
                AssetDatabase.CreateAsset(tree, TreePath);
            }

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (!root) return;
            try
            {
                // Remove retired V1 components before installing the V2 behaviour tree.
                // MirrorBossHudV2 and MirrorBossInputIsolation both require the old actor,
                // so they must be removed first or Unity can keep restoring it.
                Remove<MirrorBossHudV2>(root);
                Remove<MirrorBossInputIsolation>(root);
                Remove<MirrorBossActor>(root);
                Remove<MirrorBossFSMSynchronizer>(root);
                Remove<NodeCanvas.StateMachines.FSMOwner>(root);
                Remove<MirrorBossPhysicsAnimationFix>(root);
                Remove<MirrorBossWalkAnimationOverride>(root);
                foreach (var gate in root.GetComponentsInChildren<MirrorBossHitboxAnimationGate>(true))
                    Object.DestroyImmediate(gate);
                foreach (var relay in root.GetComponentsInChildren<MirrorTrial.Combat.HitboxRelayV2>(true))
                    Object.DestroyImmediate(relay);

                var owner = root.GetComponent<BehaviourTreeOwner>() ?? root.AddComponent<BehaviourTreeOwner>();
                owner.behaviour = tree;
                owner.repeat = true;
                owner.updateInterval = 0f;
                owner.firstActivation = GraphOwner.FirstActivation.OnEnable;
                owner.enableAction = GraphOwner.EnableAction.EnableBehaviour;

                if (!root.GetComponent<MirrorBossPlayerCollisionIgnore>()) root.AddComponent<MirrorBossPlayerCollisionIgnore>();
                if (!root.GetComponent<MirrorBossAttackDebugGizmo>()) root.AddComponent<MirrorBossAttackDebugGizmo>();
                if (!root.GetComponent<MirrorBossBlackboardSync>()) root.AddComponent<MirrorBossBlackboardSync>();
                if (!root.GetComponent<MirrorBossRuntimeColliderDebug>()) root.AddComponent<MirrorBossRuntimeColliderDebug>();

                var body = root.GetComponent<Rigidbody2D>();
                if (body)
                {
                    body.bodyType = RigidbodyType2D.Dynamic;
                    body.gravityScale = 1f;
                    body.freezeRotation = true;
                    body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                }

                var hitbox = root.GetComponentInChildren<MirrorTrial.Combat.Hitbox>(true);
                if (hitbox)
                {
                    var p = hitbox.transform.localPosition;
                    p.x = 0f;
                    hitbox.transform.localPosition = p;
                    var collider = hitbox.GetComponent<Collider2D>();
                    if (collider) collider.enabled = false;
                }

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            if (AssetDatabase.LoadAssetAtPath<Object>(OldFsmPath)) AssetDatabase.DeleteAsset(OldFsmPath);
            EditorUtility.SetDirty(tree);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MirrorTrial] 中文 Boss Behaviour Tree 已生成并绑定；重力、攻击框镜像和动画控制源已统一。" );
        }

        static ActionNode AddAction<T>(BehaviourTree tree, Vector2 position, string comments) where T : ActionTask
        {
            var node = tree.AddNode<ActionNode>(position);
            node.action = (ActionTask)Task.Create(typeof(T), tree);
            node.comments = comments;
            return node;
        }


        static ConditionNode AddCondition<T>(BehaviourTree tree, Vector2 position, string comments) where T : ConditionTask
        {
            var node = tree.AddNode<ConditionNode>(position);
            node.condition = (ConditionTask)Task.Create(typeof(T), tree);
            node.comments = comments;
            return node;
        }        static void Remove<T>(GameObject root) where T : Component
        {
            var component = root.GetComponent<T>();
            if (component) Object.DestroyImmediate(component);
        }
    }
}
#endif
