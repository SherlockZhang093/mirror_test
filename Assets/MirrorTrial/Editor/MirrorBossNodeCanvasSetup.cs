#if UNITY_EDITOR
using MirrorTrial.Boss.NodeCanvasIntegration;
using NodeCanvas.StateMachines;
using UnityEditor;
using UnityEngine;

namespace MirrorTrial.Boss.Editor
{
    [InitializeOnLoad]
    public static class MirrorBossNodeCanvasSetup
    {
        const string GraphPath = "Assets/MirrorTrial/Boss/MirrorBossAI_FSM.asset";
        const string PrefabPath = "Assets/MirrorTrial/Prefabs/Boss/MirrorBoss.prefab";
        const string VersionKey = "MirrorTrial.MirrorBossNodeCanvasSetup.v2";

        static MirrorBossNodeCanvasSetup()
        {
            EditorApplication.delayCall += InstallOnce;
        }

        [MenuItem("Mirror Trial/Boss/NodeCanvas/重新生成中文 Boss FSM")]
        public static void RebuildFromMenu()
        {
            BuildOrUpdate(true);
        }

        static void InstallOnce()
        {
            if (SessionState.GetBool(VersionKey, false)) return;
            SessionState.SetBool(VersionKey, true);
            BuildOrUpdate(false);
        }

        static void BuildOrUpdate(bool forceRebuild)
        {
            var graph = AssetDatabase.LoadAssetAtPath<FSM>(GraphPath);
            if (!graph || forceRebuild)
            {
                if (graph) AssetDatabase.DeleteAsset(GraphPath);
                graph = ScriptableObject.CreateInstance<FSM>();
                graph.name = "镜中Boss_AI_中文FSM";
                graph.comments =
                    "镜中 Boss 的 AI 总览。\n" +
                    "绿色高亮表示游戏运行时 Boss 当前所处状态。\n" +
                    "目前此图负责可视化现有 MirrorBossActorV2 的真实状态；不要删除状态名称前的编号。\n" +
                    "后续动作参数会逐步迁移为可直接编辑的 NodeCanvas Action。";

                CreateState(graph, "01 待机：等待玩家进入战斗区域", new Vector2(0, 0),
                    "Boss 尚未激活。玩家进入关卡绘制的 Boss 战斗区域后，由 BattleArea 把真实玩家 Transform 传给 Boss。此时不能移动、攻击或产生剑伤害。");
                CreateState(graph, "02 登场：锁定玩家并播放准备动作", new Vector2(310, 0),
                    "停止水平移动，播放持剑待机/准备动作，等待 Profile 中的 introDuration。用于让玩家看清 Boss 已经启动。");
                CreateState(graph, "03 追击：靠近玩家直到进入攻击距离", new Vector2(620, 0),
                    "只在水平方向追踪真实玩家；超过 attackRange 时移动并播放跑步动画，进入攻击距离后停止并开始连招。");
                CreateState(graph, "04 连招：执行当前阶段的固定剑招", new Vector2(930, 0),
                    "按当前阶段读取 Profile 中的固定剑招数组。每一刀的攻击判定由动画 normalizedTime 的 activeStart/activeEnd 控制；每次命中玩家固定扣 1 点生命。");
                CreateState(graph, "05 硬直：攻击结束后留给玩家反击时间", new Vector2(930, 250),
                    "关闭剑 Hitbox、恢复动画速度并停止移动。等待当前阶段 Recovery 时间后返回追击。这是玩家最明确的反击窗口。");
                CreateState(graph, "06 转阶段：变色并短暂防御", new Vector2(620, 250),
                    "Boss 生命降到 70%/35% 阈值时进入。中断当前动作、关闭 Hitbox、改变颜色并播放防御动作，结束后返回追击。");
                CreateState(graph, "07 死亡：停止战斗并沿用关卡结算流程", new Vector2(310, 250),
                    "生命归零后中断所有动作、关闭攻击判定并播放死亡动画，然后通知现有关卡战斗区域执行结算。Boss 最大生命为 Profile 中的 300。 ");

                var nodes = graph.allNodes;
                graph.primeNode = nodes[0];
                graph.ConnectNodes(nodes[0], nodes[1]);
                graph.ConnectNodes(nodes[1], nodes[2]);
                graph.ConnectNodes(nodes[2], nodes[3]);
                graph.ConnectNodes(nodes[3], nodes[4]);
                graph.ConnectNodes(nodes[4], nodes[2]);
                graph.ConnectNodes(nodes[2], nodes[5]);
                graph.ConnectNodes(nodes[3], nodes[5]);
                graph.ConnectNodes(nodes[4], nodes[5]);
                graph.ConnectNodes(nodes[5], nodes[2]);
                graph.ConnectNodes(nodes[2], nodes[6]);
                graph.ConnectNodes(nodes[3], nodes[6]);
                graph.ConnectNodes(nodes[4], nodes[6]);
                graph.SelfSerialize();
                AssetDatabase.CreateAsset(graph, GraphPath);
            }

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (!root) return;
            try
            {
                var owner = root.GetComponent<FSMOwner>() ?? root.AddComponent<FSMOwner>();
                owner.behaviour = graph;
                owner.firstActivation = NodeCanvas.Framework.GraphOwner.FirstActivation.OnEnable;
                owner.enableAction = NodeCanvas.Framework.GraphOwner.EnableAction.EnableBehaviour;
                if (!root.GetComponent<MirrorBossFSMSynchronizer>()) root.AddComponent<MirrorBossFSMSynchronizer>();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            EditorUtility.SetDirty(graph);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MirrorTrial] NodeCanvas 中文 Boss FSM 已生成并绑定到 MirrorBoss.prefab。路径：" + GraphPath);
        }

        static void CreateState(FSM graph, string title, Vector2 position, string chineseComment)
        {
            var state = graph.AddNode<MirrorBossObservedState>(position);
            state.name = title;
            state.comments = chineseComment;
        }
    }
}
#endif
