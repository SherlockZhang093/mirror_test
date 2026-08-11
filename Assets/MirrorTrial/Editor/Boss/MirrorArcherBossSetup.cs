#if UNITY_EDITOR
using MirrorTrial.Boss.NodeCanvasIntegration;
using NodeCanvas.BehaviourTrees;
using NodeCanvas.Framework;
using UnityEditor;
using UnityEngine;

namespace MirrorTrial.Boss.Editor
{
    public static class MirrorArcherBossSetup
    {
        const string SourcePrefabPath = "Assets/MirrorTrial/Prefabs/Boss/MirrorBoss.prefab";
        const string BossPrefabPath = "Assets/MirrorTrial/Prefabs/Boss/MirrorArcherBoss.prefab";
        const string BattleAreaPrefabPath = "Assets/MirrorTrial/Prefabs/Boss/MirrorArcherBossBattleArea.prefab";
        const string ProfilePath = "Assets/MirrorTrial/Boss/MirrorArcherBossProfile.asset";
        const string TreePath = "Assets/MirrorTrial/Boss/MirrorArcherBossAI_BT.asset";
        const string DefaultMinionPath = "Assets/MirrorTrial/Enemies/E001_NewEnemy/Prefabs/Enemy_E001_NewEnemy.prefab";

        [MenuItem("Mirror Trial/Boss/Build Mirror Archer Boss")]
        public static void RebuildFromMenu() => MirrorArcherTwoStageSetup.RebuildFromMenu();

        static void Build(bool force)
        {
            var profile = BuildProfile(force);
            var tree = BuildTree(force);
            BuildPrefab(profile, tree, force);
            BuildBattleAreaPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static MirrorBossSimpleProfile BuildProfile(bool force)
        {
            var profile = AssetDatabase.LoadAssetAtPath<MirrorBossSimpleProfile>(ProfilePath);
            if (profile && !force) return profile;
            if (!profile)
            {
                profile = ScriptableObject.CreateInstance<MirrorBossSimpleProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            profile.displayName = "镜中猎手";
            profile.maxHitPoints = 300;
            profile.enableRangedTeleportKit = true;
            profile.attackRange = 1.45f;
            profile.introDuration = 0.8f;
            profile.minionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultMinionPath);
            profile.phaseOneCombo = OneCloseDefenseStep();
            profile.phaseTwoCombo = OneCloseDefenseStep();
            profile.phaseThreeCombo = OneCloseDefenseStep();
            profile.phaseOneFeintChance = 0f;
            profile.phaseTwoFeintChance = 0f;
            profile.phaseThreeFeintChance = 0f;
            EditorUtility.SetDirty(profile);
            return profile;
        }

        static MirrorBossComboStepV2[] OneCloseDefenseStep()
        {
            var step = new MirrorBossComboStepV2
            {
                animationState = "ComboAttackA",
                damage = 1,
                activeStart = 0.3f,
                activeEnd = 0.52f,
                playbackSpeed = 1f,
                gapAfter = 0f,
                advanceSpeed = 0.4f
            };
            step.EnsureHitboxKeys();
            return new[] { step };
        }

        static BehaviourTree BuildTree(bool force)
        {
            var existing = AssetDatabase.LoadAssetAtPath<BehaviourTree>(TreePath);
            if (existing && !force) return existing;
            if (existing) AssetDatabase.DeleteAsset(TreePath);

            var tree = ScriptableObject.CreateInstance<BehaviourTree>();
            tree.name = "镜中猎手_AI_行为树";
            tree.comments =
                "核心博弈：射箭压制，玩家逼近后决定是否提前交瞬移；瞬移进入冷却后可被近身惩罚。\n" +
                "每轮行动只随机一次，完整执行射箭、瞬移、召唤或近身防御后再重新决策。";
            tree.repeat = true;
            tree.updateInterval = 0f;
            tree.blackboard.AddVariable("isDead", false);
            tree.blackboard.AddVariable("isStunned", false);
            tree.blackboard.AddVariable("hasAppeared", false);
            tree.blackboard.AddVariable("DistanceToPlayer", float.PositiveInfinity);
            tree.blackboard.AddVariable("AttackRange", 1.45f);

            var root = tree.AddNode<Selector>(new Vector2(620f, 0f));
            root.dynamic = true;
            root.comments = "动态优先级：异常状态 > 等待激活 > 登场 > 核心战斗。";

            var abnormal = tree.AddNode<Sequencer>(new Vector2(80f, 180f));
            abnormal.dynamic = true;
            var abnormalCondition = AddCondition<MirrorBossAbnormalCondition>(tree, new Vector2(0f, 360f));
            var abnormalAction = AddAction<MirrorBossAbnormalAction>(tree, new Vector2(180f, 360f));

            var waiting = tree.AddNode<Sequencer>(new Vector2(390f, 180f));
            var notActivated = AddCondition<MirrorBossNotActivatedCondition>(tree, new Vector2(320f, 360f));
            var waitForActivation = AddAction<WaitForMirrorBossActivation>(tree, new Vector2(500f, 360f));

            var appearance = tree.AddNode<Sequencer>(new Vector2(700f, 180f));
            var hasNotAppeared = AddCondition<MirrorBossHasNotAppearedCondition>(tree, new Vector2(650f, 360f));
            var intro = AddAction<MirrorBossIntroAction>(tree, new Vector2(830f, 360f));

            var combat = tree.AddNode<Selector>(new Vector2(1120f, 180f));
            combat.dynamic = false;
            combat.comments = "读取本轮锁定决定；条件顺序不会改变已经选定的动作。";

            var actions = new[]
            {
                MirrorBossTacticalAction.Summon,
                MirrorBossTacticalAction.TeleportRetreat,
                MirrorBossTacticalAction.TeleportAttack,
                MirrorBossTacticalAction.CloseDefense,
                MirrorBossTacticalAction.Shoot
            };
            for (var i = 0; i < actions.Length; i++)
            {
                var x = 940f + i * 245f;
                var sequence = tree.AddNode<Sequencer>(new Vector2(x, 390f));
                sequence.dynamic = false;
                sequence.comments = GetActionComment(actions[i]);
                var condition = AddDecisionCondition(tree, new Vector2(x, 570f), actions[i]);
                var execute = AddAction<MirrorBossExecuteTacticalAction>(tree, new Vector2(x + 115f, 570f));
                tree.ConnectNodes(combat, sequence);
                tree.ConnectNodes(sequence, condition);
                tree.ConnectNodes(sequence, execute);
            }

            tree.primeNode = root;
            tree.ConnectNodes(root, abnormal);
            tree.ConnectNodes(root, waiting);
            tree.ConnectNodes(root, appearance);
            tree.ConnectNodes(root, combat);
            tree.ConnectNodes(abnormal, abnormalCondition);
            tree.ConnectNodes(abnormal, abnormalAction);
            tree.ConnectNodes(waiting, notActivated);
            tree.ConnectNodes(waiting, waitForActivation);
            tree.ConnectNodes(appearance, hasNotAppeared);
            tree.ConnectNodes(appearance, intro);
            tree.SelfSerialize();
            AssetDatabase.CreateAsset(tree, TreePath);
            return tree;
        }

        static string GetActionComment(MirrorBossTacticalAction action)
        {
            switch (action)
            {
                case MirrorBossTacticalAction.Summon: return "召唤：长前摇，可被强打断，补充当前阶段允许的小兵。";
                case MirrorBossTacticalAction.TeleportRetreat: return "撤退瞬移：玩家贴身时交掉逃生资源。";
                case MirrorBossTacticalAction.TeleportAttack: return "换位射击：落点提示、出现后摇，然后从新角度射箭。";
                case MirrorBossTacticalAction.CloseDefense: return "近身防御：瞬移冷却时只打一招，打完留下硬直。";
                default: return "射箭：完成整组射击，达到组数后进入较长装填破绽。";
            }
        }

        static void BuildPrefab(MirrorBossSimpleProfile profile, BehaviourTree tree, bool force)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
            if (!existing)
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath);
                if (!source)
                {
                    Debug.LogError("[MirrorArcherBossSetup] Missing source boss prefab: " + SourcePrefabPath);
                    return;
                }
                var instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                instance.name = "MirrorArcherBoss";
                PrefabUtility.SaveAsPrefabAsset(instance, BossPrefabPath);
                Object.DestroyImmediate(instance);
            }
            else if (!force)
            {
                // Still refresh graph/profile references below without rebuilding the prefab hierarchy.
            }

            var root = PrefabUtility.LoadPrefabContents(BossPrefabPath);
            try
            {
                var actor = root.GetComponent<MirrorBossActorV2>();
                if (!actor) actor = root.AddComponent<MirrorBossActorV2>();
                var actorData = new SerializedObject(actor);
                actorData.FindProperty("profile").objectReferenceValue = profile;
                actorData.FindProperty("behaviourTreeControlled").boolValue = true;
                actorData.ApplyModifiedPropertiesWithoutUndo();

                if (!root.GetComponent<MirrorBossMinionController>()) root.AddComponent<MirrorBossMinionController>();
                if (!root.GetComponent<MirrorBossBlackboardSync>()) root.AddComponent<MirrorBossBlackboardSync>();
                var owner = root.GetComponent<BehaviourTreeOwner>() ?? root.AddComponent<BehaviourTreeOwner>();
                owner.behaviour = tree;
                owner.repeat = true;
                owner.updateInterval = 0f;
                owner.firstActivation = GraphOwner.FirstActivation.OnEnable;
                owner.enableAction = GraphOwner.EnableAction.EnableBehaviour;
                PrefabUtility.SaveAsPrefabAsset(root, BossPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void BuildBattleAreaPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BattleAreaPrefabPath)) return;
            var bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
            if (!bossPrefab) return;

            var root = new GameObject("MirrorArcherBossBattleArea");
            var box = root.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(18f, 7f);
            var encounter = root.AddComponent<MirrorTrial.Level.CombatEncounter>();
            var battleArea = root.AddComponent<MirrorBossBattleAreaV3>();

            var spawnObject = new GameObject("BossSpawnPoint");
            spawnObject.transform.SetParent(root.transform, false);
            spawnObject.transform.localPosition = new Vector3(5f, 0f, 0f);
            var spawnPoint = spawnObject.AddComponent<MirrorBossSpawnPoint>();

            var data = new SerializedObject(battleArea);
            data.FindProperty("bossPrefab").objectReferenceValue = bossPrefab.GetComponent<MirrorBossActorV2>();
            data.FindProperty("spawnPoint").objectReferenceValue = spawnPoint;
            data.FindProperty("encounter").objectReferenceValue = encounter;
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, BattleAreaPrefabPath);
            Object.DestroyImmediate(root);
        }

        static ActionNode AddAction<T>(BehaviourTree tree, Vector2 position) where T : ActionTask
        {
            var node = tree.AddNode<ActionNode>(position);
            node.action = (ActionTask)Task.Create(typeof(T), tree);
            return node;
        }

        static ConditionNode AddCondition<T>(BehaviourTree tree, Vector2 position) where T : ConditionTask
        {
            var node = tree.AddNode<ConditionNode>(position);
            node.condition = (ConditionTask)Task.Create(typeof(T), tree);
            return node;
        }

        static ConditionNode AddDecisionCondition(BehaviourTree tree, Vector2 position,
            MirrorBossTacticalAction action)
        {
            var node = AddCondition<MirrorBossTacticalDecisionCondition>(tree, position);
            ((MirrorBossTacticalDecisionCondition)node.condition).expected = action;
            return node;
        }
    }
}
#endif
