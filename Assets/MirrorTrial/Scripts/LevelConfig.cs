using System.Collections.Generic;
using MirrorTrial.Enemies;
using MirrorTrial.Player;
using UnityEngine;

namespace MirrorTrial.Level
{
    [CreateAssetMenu(menuName = "镜像试炼/关卡配置", fileName = "LevelConfig")]
    public class LevelConfig : ScriptableObject
    {
        [Header("基础信息")]
        [ChineseLabel("关卡ID")] [Tooltip("全局唯一，用于存档与跨场景引用")] public string levelId = "Level_Reality_01";
        [ChineseLabel("显示名")] public string displayName = "第一关";
        [ChineseLabel("是否镜中关卡")] [Tooltip("镜中关卡不含镜子门，且自动挂接 MirrorReturnOnClear")] public bool isMirrorLevel;

        [Header("引用预制体")]
        [ChineseLabel("玩家预制体")] public GameObject playerPrefab;
        [ChineseLabel("玩家生命UI")] [Tooltip("进入该关卡时使用的生命值与生命储备界面")]
        public PlayerHealthBarView playerHealthHudPrefab;
        [ChineseLabel("默认敌人预制体")] public GameObject enemyPrefab;
        [ChineseLabel("默认地形精灵")] public Sprite defaultSprite;

        [Header("玩家出生点")]
        [ChineseLabel("出生点位置")] public Vector3 playerSpawn;

        [Header("地形")]
        [ChineseLabel("地形条目")] public List<GeometryEntry> geometry = new List<GeometryEntry>();

        [Header("段落")]
        [ChineseLabel("段落条目")] public List<SegmentEntry> segments = new List<SegmentEntry>();

        [Header("刷怪点")]
        [ChineseLabel("刷怪点条目")] public List<SpawnPointEntry> spawnPoints = new List<SpawnPointEntry>();

        [Header("战斗")]
        [ChineseLabel("战斗条目")] public List<EncounterEntry> encounters = new List<EncounterEntry>();

        [Header("门")]
        [ChineseLabel("门条目")] public List<GateEntry> gates = new List<GateEntry>();

        [Header("镜子门")]
        [ChineseLabel("镜子门条目")] public List<MirrorGateEntry> mirrorGates = new List<MirrorGateEntry>();

        [Header("触发器")]
        [ChineseLabel("触发器条目")] public List<TriggerEntry> triggers = new List<TriggerEntry>();
    }

    public enum GeometryType
    {
        Unknown = 0,
        Platform = 1,
        Boundary = 2,
        SolidBlock = 3,
        ClimbableWall = 4
    }

    [System.Serializable]
    public class GeometryEntry
    {
        public string name;
        public Vector3 position;
        public Vector2 size = Vector2.one;
        public bool isBoundary;
        public bool isPlatform;
        public GeometryType geometryType;
        public int platformVisualIndex;
        public Color color = Color.white;
    }

    [System.Serializable]
    public class SegmentEntry
    {
        public string segmentId;
        public string displayName;
        public Vector3 position;
        public Vector2 size = Vector2.one;
        public bool startEnabled = true;
    }

    [System.Serializable]
    public class SpawnPointEntry
    {
        public string spawnId;
        public Vector3 position;
        public SpawnPointRole role = SpawnPointRole.Melee;
        public GameObject enemyPrefab;
        public EnemyAIProfile aiProfile;
        public int overrideHitPoints = -1;
        public float overrideMoveSpeed = -1f;
        public float facingDegrees;
    }

    [System.Serializable]
    public class EncounterEntry
    {
        public string encounterId;
        public Vector3 position;
        public Vector2 size = Vector2.one;
        public bool startOnPlayerEnter = true;
        public CombatClearCondition clearCondition = CombatClearCondition.WavesCompletedAndEnemiesCleared;
        public List<string> lockGateIds = new List<string>();
        public List<WaveEntry> waves = new List<WaveEntry>();
    }

    [System.Serializable]
    public class WaveEntry
    {
        public string waveId = "Wave_01";
        public CombatClearCondition clearCondition = CombatClearCondition.AllEnemiesDefeated;
        public float nextWaveDelay = 0.8f;
        public List<WaveSpawnEntryData> spawnEntries = new List<WaveSpawnEntryData>();
    }

    [System.Serializable]
    public class WaveSpawnEntryData
    {
        public string spawnPointId;
        public int count = 1;
        public float delay;
        public float interval;
        public GameObject enemyPrefab;
    }

    [System.Serializable]
    public class GateEntry
    {
        public string gateId;
        public Vector3 position;
        public bool initialOpen = true;
    }

    [System.Serializable]
    public class MirrorGateEntry
    {
        public string gateId;
        public Vector3 position;
        public string mirrorSceneName = "level_01_mirror";
        public int hitPoints = 3;
        public Vector3 returnPointPosition;
        public MirrorRewardAbility rewardAbility = MirrorRewardAbility.None;
        public string nextSegmentId;
        public int platformVisualIndex;
        public Color color = new Color(0.6f, 0.85f, 1f, 0.75f);
    }

    [System.Serializable]
    public class TriggerEntry
    {
        public string triggerId;
        public Vector3 position;
        public LevelTriggerWhen when = LevelTriggerWhen.Manual;
        public LevelTriggerShape shape = LevelTriggerShape.Box;
        public Vector2 boxSize = new Vector2(4f, 3f);
        public float circleRadius = 1.5f;
        public bool enabledAtStart = true;
        public bool oneShot = true;
        public Color gizmoColor = new Color(0.2f, 1f, 1f, 0.9f);
        public List<ConditionEntry> conditions = new List<ConditionEntry>();
        public List<ActionEntry> actions = new List<ActionEntry>();
    }

    [System.Serializable]
    public class ConditionEntry
    {
        public LevelConditionType type = LevelConditionType.None;
        public string targetId;
        public MirrorRewardAbility requiredAbility = MirrorRewardAbility.None;
        public bool invert;
    }

    [System.Serializable]
    public class ActionEntry
    {
        public LevelActionType type = LevelActionType.PlayFeedback;
        public string targetId;
        public Vector3 targetPosition;
        public int waveIndex;
        public MirrorRewardAbility ability = MirrorRewardAbility.None;
        public string feedbackMessage;
        public float delay;
    }
}
