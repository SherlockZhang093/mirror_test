using System;
using UnityEngine;

namespace MirrorTrial.Level
{
    // 触发时机
    public enum LevelTriggerWhen
    {
        [InspectorName("手动触发")] Manual,
        [InspectorName("玩家进入")] OnPlayerEnter,
        [InspectorName("玩家离开")] OnPlayerExit,
        [InspectorName("战斗清场")] OnEncounterClear,
        [InspectorName("Boss击败")] OnBossDefeated,
        [InspectorName("镜子门击碎")] OnMirrorSmashed,
        [InspectorName("镜子门完成")] OnMirrorCompleted,
        [InspectorName("段落启用")] OnSegmentEnabled
    }

    // 触发区域形状
    public enum LevelTriggerShape
    {
        [InspectorName("矩形")] Box,
        [InspectorName("圆形")] Circle
    }

    // 条件类型
    public enum LevelConditionType
    {
        [InspectorName("无")] None,
        [InspectorName("段落未完成")] SegmentNotCompleted,
        [InspectorName("战斗未开始")] EncounterNotStarted,
        [InspectorName("战斗已清场")] EncounterCleared,
        [InspectorName("镜子门已击碎")] MirrorGateIsSmashed,
        [InspectorName("镜子门完好")] MirrorGateIsIntact,
        [InspectorName("玩家拥有能力")] PlayerHasAbility
    }

    // 动作类型
    public enum LevelActionType
    {
        [InspectorName("开始战斗")] StartEncounter,
        [InspectorName("开始波次")] StartWave,
        [InspectorName("关门")] LockGate,
        [InspectorName("开门")] OpenGate,
        [InspectorName("击碎镜子门")] SmashMirrorGate,
        [InspectorName("传送玩家")] TeleportPlayer,
        [InspectorName("解锁能力")] UnlockAbility,
        [InspectorName("启用段落")] EnableSegment,
        [InspectorName("播放提示")] PlayFeedback
    }

    public enum MirrorGateState
    {
        [InspectorName("完好")] Intact,
        [InspectorName("已击碎")] Smashed,
        [InspectorName("已完成")] Completed
    }

    public enum MirrorRewardAbility
    {
        [InspectorName("无")] None,
        [InspectorName("镜刃")] MirrorBlade,
        [InspectorName("回响冲刺")] EchoDash
    }

    public enum CombatClearCondition
    {
        [InspectorName("敌人全灭")] AllEnemiesDefeated,
        [InspectorName("波次完成且敌人全灭")] WavesCompletedAndEnemiesCleared
    }

    [Serializable]
    public class LevelCondition
    {
        [ChineseLabel("条件类型")] [Tooltip("条件类型")] public LevelConditionType type = LevelConditionType.None;
        [ChineseLabel("目标段落")] [Tooltip("目标段落")] public LevelSegment segment;
        [ChineseLabel("目标战斗")] [Tooltip("目标战斗")] public CombatEncounter encounter;
        [ChineseLabel("目标镜子门")] [Tooltip("目标镜子门")] public MirrorGate mirrorGate;
        [ChineseLabel("需要的能力")] [Tooltip("需要的能力")] public MirrorRewardAbility requiredAbility = MirrorRewardAbility.None;
        [ChineseLabel("条件取反")] [Tooltip("条件取反")] public bool invert;
    }

    [Serializable]
    public class LevelAction
    {
        [ChineseLabel("动作类型")] [Tooltip("动作类型")] public LevelActionType type = LevelActionType.PlayFeedback;
        [ChineseLabel("目标战斗")] [Tooltip("目标战斗")] public CombatEncounter encounter;
        [ChineseLabel("波次索引")] [Tooltip("波次索引")] public int waveIndex;
        [ChineseLabel("目标门")] [Tooltip("目标门")] public AreaGate gate;
        [ChineseLabel("目标镜子门")] [Tooltip("目标镜子门")] public MirrorGate mirrorGate;
        [ChineseLabel("目标段落")] [Tooltip("目标段落")] public LevelSegment segment;
        [ChineseLabel("传送目标点")] [Tooltip("传送目标点")] public Transform teleportTarget;
        [ChineseLabel("要解锁的能力")] [Tooltip("要解锁的能力")] public MirrorRewardAbility ability = MirrorRewardAbility.None;
        [ChineseLabel("提示文本")] [Tooltip("提示文本")] [TextArea] public string feedbackMessage;
        [ChineseLabel("延迟执行时间")] [Tooltip("延迟执行时间")] public float delay;
    }

    [Serializable]
    public class WaveSpawnEntry
    {
        [ChineseLabel("敌人预制体")] [Tooltip("敌人预制体")] public GameObject enemyPrefab;
        [ChineseLabel("刷怪点")] [Tooltip("刷怪点")] public SpawnPoint spawnPoint;
        [ChineseLabel("生成数量")] [Tooltip("生成数量")] [Min(1)] public int count = 1;
        [ChineseLabel("生成间隔")] [Tooltip("生成间隔")] [Min(0f)] public float interval;
        [ChineseLabel("首只延迟")] [Tooltip("首只延迟")] [Min(0f)] public float delay;
    }

    [Serializable]
    public class WaveDefinition
    {
        [ChineseLabel("波次ID")] [Tooltip("波次ID")] public string waveId = "Wave_01";
        [ChineseLabel("清场条件")] [Tooltip("清场条件")] public CombatClearCondition clearCondition = CombatClearCondition.AllEnemiesDefeated;
        [ChineseLabel("生成条目")] [Tooltip("生成条目")] public WaveSpawnEntry[] spawnEntries = new WaveSpawnEntry[0];
        [ChineseLabel("下一波延迟")] [Tooltip("下一波延迟")] [Min(0f)] public float nextWaveDelay;
    }

    public enum LevelValidationSeverity
    {
        [InspectorName("错误")] Error,
        [InspectorName("警告")] Warning,
        [InspectorName("信息")] Info
    }

    [Serializable]
    public struct LevelValidationIssue
    {
        public LevelValidationSeverity severity;
        public string message;
        public UnityEngine.Object context;

        public LevelValidationIssue(LevelValidationSeverity severity, string message, UnityEngine.Object context)
        {
            this.severity = severity;
            this.message = message;
            this.context = context;
        }
    }
}
