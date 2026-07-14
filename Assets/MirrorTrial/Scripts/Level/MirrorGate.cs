using MirrorTrial.Player;
using UnityEngine;

namespace MirrorTrial.Level
{
    [RequireComponent(typeof(Collider2D))]
    public class MirrorGate : MonoBehaviour
    {
        [Header("基础")]
        [ChineseLabel("镜子门ID")] [Tooltip("镜子门ID")] [SerializeField] string gateId = "MirrorGate_Blade";
        [ChineseLabel("目标Boss战斗名")] [Tooltip("目标Boss战斗名")] [SerializeField] string targetEncounter = "Boss_Blade";
        [Header("Boss Completion")]
        [SerializeField] CombatEncounter bossEncounter;
        [SerializeField] bool completeOnBossEncounterClear = true;


        [Header("传送点")]
        [ChineseLabel("Boss入口点")] [Tooltip("Boss入口点")] [SerializeField] Transform encounterEntryPoint;
        [ChineseLabel("返回点")] [Tooltip("返回点")] [SerializeField] Transform returnPoint;

        [Header("奖励与下一段")]
        [ChineseLabel("奖励能力")] [Tooltip("奖励能力")] [SerializeField] MirrorRewardAbility rewardAbility = MirrorRewardAbility.None;
        [ChineseLabel("完成后启用的段落")] [Tooltip("完成后启用的段落")] [SerializeField] LevelSegment nextSegment;
        [ChineseLabel("进入时直接传送")] [Tooltip("进入时直接传送")] [SerializeField] bool teleportOnEnter = true;

        public string GateId => gateId;
        public CombatEncounter BossEncounter => bossEncounter;
        public string TargetEncounter => targetEncounter;
        public Transform ReturnPoint => returnPoint;
        public MirrorRewardAbility RewardAbility => rewardAbility;
        public LevelSegment NextSegment => nextSegment;
        public MirrorGateState State { get; private set; } = MirrorGateState.Locked;

        public System.Action<MirrorGate> OnActivated;
        public System.Action<MirrorGate> OnBroken;

        void Awake()
        {
            var col = GetComponent<Collider2D>();
            if (col) col.isTrigger = true;
        }

        void OnEnable()
        {
            if (bossEncounter)
                bossEncounter.OnCleared += OnBossEncounterCleared;
        }

        void OnDisable()
        {
            if (bossEncounter)
                bossEncounter.OnCleared -= OnBossEncounterCleared;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!teleportOnEnter || State != MirrorGateState.Active) return;
            if (IsPlayer(other))
                EnterGate(other.gameObject);
        }

        public void Activate()
        {
            if (State == MirrorGateState.Broken) return;
            State = MirrorGateState.Active;
            OnActivated?.Invoke(this);
        }

        public void EnterGate(GameObject player)
        {
            if (State != MirrorGateState.Active || !player) return;
            if (encounterEntryPoint)
                player.transform.position = encounterEntryPoint.position;
        }

        public void MarkBossDefeated(GameObject player)
        {
            if (State != MirrorGateState.Active && State != MirrorGateState.Locked) return;
            State = MirrorGateState.Completed;
            UnlockAbility(player);
            if (player && returnPoint)
                player.transform.position = returnPoint.position;
            Break(player);
        }

        void OnBossEncounterCleared(CombatEncounter encounter)
        {
            if (completeOnBossEncounterClear)
                MarkBossDefeated(FindPlayer());
        }

        public void Break(GameObject player)
        {
            if (State == MirrorGateState.Broken) return;
            State = MirrorGateState.Broken;
            if (nextSegment) nextSegment.Enable();
            OnBroken?.Invoke(this);
        }

        void UnlockAbility(GameObject player)
        {
            if (!player) return;
            var tuning = player.GetComponent<PlayerTuning>();
            if (!tuning) return;

            switch (rewardAbility)
            {
                case MirrorRewardAbility.MirrorBlade:
                    tuning.abilities.mirrorBladeUnlocked = true;
                    break;
                case MirrorRewardAbility.EchoDash:
                    tuning.abilities.echoDashUnlocked = true;
                    break;
            }
        }

        bool IsPlayer(Collider2D other)
        {
            return other.CompareTag("Player") || other.GetComponent<PlayerInputReader>() != null;
        }

        GameObject FindPlayer()
        {
            var player = FindObjectOfType<PlayerInputReader>();
            return player ? player.gameObject : null;
        }


        void OnDrawGizmos()
        {
            Color color;
            switch (State)
            {
                case MirrorGateState.Active: color = new Color(0.25f, 0.9f, 1f, 0.95f); break;
                case MirrorGateState.Completed: color = new Color(1f, 0.75f, 0.2f, 0.95f); break;
                case MirrorGateState.Broken: color = new Color(0.9f, 0.45f, 0.3f, 0.95f); break;
                default: color = new Color(0.65f, 0.65f, 0.65f, 0.95f); break;
            }
            Gizmos.color = color;
            Gizmos.DrawWireCube(transform.position, new Vector3(0.8f, 1.2f, 0.1f));
        }
    }
}
