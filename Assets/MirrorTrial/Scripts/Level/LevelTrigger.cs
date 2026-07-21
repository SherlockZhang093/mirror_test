using System.Collections.Generic;
using MirrorTrial.Player;
using UnityEngine;

namespace MirrorTrial.Level
{
    [RequireComponent(typeof(Collider2D))]
    public class LevelTrigger : MonoBehaviour
    {
        [Header("基础")]
        [ChineseLabel("触发器ID")] [Tooltip("触发器ID")] [SerializeField] string triggerId = "TR_01";
        [ChineseLabel("开始时启用")] [Tooltip("开始时启用")] [SerializeField] bool enabledAtStart = true;
        [ChineseLabel("只触发一次")] [Tooltip("只触发一次")] [SerializeField] bool oneShot = true;
        [ChineseLabel("触发时机")] [Tooltip("触发时机")] [SerializeField] LevelTriggerWhen when = LevelTriggerWhen.Manual;

        [Header("触发区域")]
        [ChineseLabel("区域形状")] [Tooltip("区域形状")] [SerializeField] LevelTriggerShape shape = LevelTriggerShape.Box;
        [ChineseLabel("矩形范围")] [Tooltip("矩形范围")] [SerializeField] Vector2 boxSize = new Vector2(4f, 3f);
        [ChineseLabel("圆形半径")] [Tooltip("圆形半径")] [SerializeField] float circleRadius = 1.5f;
        [ChineseLabel("Scene视图颜色")] [Tooltip("Scene视图颜色")] [SerializeField] Color gizmoColor = new Color(0.2f, 1f, 1f, 0.9f);

        [Header("条件与动作")]
        [ChineseLabel("触发条件")] [Tooltip("触发条件")] [SerializeField] LevelCondition[] conditions = new LevelCondition[0];
        [ChineseLabel("执行动作")] [Tooltip("执行动作")] [SerializeField] LevelAction[] actions = new LevelAction[0];

        [Header("监听目标")]
        [ChineseLabel("目标段落")] [Tooltip("目标段落")] [SerializeField] LevelSegment segmentTarget;
        [ChineseLabel("目标战斗")] [Tooltip("目标战斗")] [SerializeField] CombatEncounter encounterTarget;
        [ChineseLabel("目标镜子门")] [Tooltip("目标镜子门")] [SerializeField] MirrorGate mirrorGateTarget;

        public string TriggerId => triggerId;
        public bool OneShot => oneShot;
        public LevelTriggerWhen When => when;
        public IReadOnlyList<LevelAction> Actions => actions;

        bool triggered;
        bool subscribed;

        void Awake()
        {
            var col = GetComponent<Collider2D>();
            if (col) col.isTrigger = true;
        }

        void Start()
        {
            SubscribeToEvents();
        }

        void OnEnable()
        {
            SubscribeToEvents();
        }

        void OnDisable()
        {
            UnsubscribeFromEvents();
        }

        void SubscribeToEvents()
        {
            if (subscribed) return;
            subscribed = true;

            if (encounterTarget)
                encounterTarget.OnCleared += OnEncounterCleared;

            if (mirrorGateTarget)
            {
                mirrorGateTarget.OnSmashed += OnMirrorSmashed;
                mirrorGateTarget.OnCompleted += OnMirrorCompleted;
            }
        }

        void UnsubscribeFromEvents()
        {
            if (!subscribed) return;
            subscribed = false;

            if (encounterTarget)
                encounterTarget.OnCleared -= OnEncounterCleared;

            if (mirrorGateTarget)
            {
                mirrorGateTarget.OnSmashed -= OnMirrorSmashed;
                mirrorGateTarget.OnCompleted -= OnMirrorCompleted;
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (when != LevelTriggerWhen.OnPlayerEnter) return;
            if (IsPlayer(other))
                Evaluate(other.gameObject);
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (when != LevelTriggerWhen.OnPlayerExit) return;
            if (IsPlayer(other))
                Evaluate(other.gameObject);
        }

        void OnEncounterCleared(CombatEncounter encounter)
        {
            if (when == LevelTriggerWhen.OnEncounterClear)
                Evaluate(null);
        }

        void OnMirrorSmashed(MirrorGate gate)
        {
            if (when == LevelTriggerWhen.OnMirrorSmashed)
                Evaluate(null);
        }

        void OnMirrorCompleted(MirrorGate gate)
        {
            if (when == LevelTriggerWhen.OnMirrorCompleted)
                Evaluate(null);
        }

        public void Fire(GameObject instigator = null)
        {
            Evaluate(instigator);
        }

        public void Evaluate(GameObject instigator)
        {
            if (!enabledAtStart || (oneShot && triggered)) return;

            if (!ConditionsMet(instigator)) return;

            triggered = true;
            ExecuteActions(instigator);
        }

        bool ConditionsMet(GameObject instigator)
        {
            foreach (var c in conditions)
            {
                if (c == null) continue;
                bool ok = true;
                switch (c.type)
                {
                    case LevelConditionType.SegmentNotCompleted:
                        ok = c.segment && !c.segment.IsEnabled;
                        break;
                    case LevelConditionType.EncounterNotStarted:
                        ok = c.encounter && !c.encounter.IsCleared;
                        break;
                    case LevelConditionType.EncounterCleared:
                        ok = c.encounter && c.encounter.IsCleared;
                        break;
                    case LevelConditionType.MirrorGateIsSmashed:
                        ok = c.mirrorGate && c.mirrorGate.State == MirrorGateState.Smashed;
                        break;
                    case LevelConditionType.MirrorGateIsIntact:
                        ok = c.mirrorGate && c.mirrorGate.State == MirrorGateState.Intact;
                        break;
                    case LevelConditionType.PlayerHasAbility:
                        ok = HasAbility(instigator, c.requiredAbility);
                        break;
                }
                if (c.invert) ok = !ok;
                if (!ok) return false;
            }
            return true;
        }

        bool HasAbility(GameObject player, MirrorRewardAbility ability)
        {
            if (!player || ability == MirrorRewardAbility.None) return false;
            var tuning = player.GetComponent<PlayerTuning>();
            if (!tuning) return false;
            switch (ability)
            {
                case MirrorRewardAbility.MirrorBlade: return tuning.abilities.mirrorBladeUnlocked;
                case MirrorRewardAbility.EchoDash: return tuning.abilities.echoDashUnlocked;
            }
            return false;
        }

        void ExecuteActions(GameObject instigator)
        {
            foreach (var action in actions)
            {
                if (action == null) continue;
                if (action.delay > 0f)
                {
                    StartCoroutine(DelayedAction(action, instigator));
                }
                else
                {
                    ExecuteAction(action, instigator);
                }
            }
        }

        System.Collections.IEnumerator DelayedAction(LevelAction action, GameObject instigator)
        {
            yield return new WaitForSeconds(action.delay);
            ExecuteAction(action, instigator);
        }

        void ExecuteAction(LevelAction action, GameObject instigator)
        {
            switch (action.type)
            {
                case LevelActionType.StartEncounter:
                    if (action.encounter) action.encounter.StartEncounter();
                    break;
                case LevelActionType.StartWave:
                    if (action.encounter) action.encounter.StartWave(action.waveIndex);
                    break;
                case LevelActionType.LockGate:
                    if (action.gate) action.gate.Close();
                    break;
                case LevelActionType.OpenGate:
                    if (action.gate) action.gate.Open();
                    break;
                case LevelActionType.SmashMirrorGate:
                    if (action.mirrorGate) action.mirrorGate.Smash(null);
                    break;
                case LevelActionType.TeleportPlayer:
                    if (instigator && action.teleportTarget)
                        instigator.transform.position = action.teleportTarget.position;
                    break;
                case LevelActionType.UnlockAbility:
                    UnlockAbility(instigator, action.ability);
                    break;
                case LevelActionType.EnableSegment:
                    if (action.segment) action.segment.Enable();
                    break;
                case LevelActionType.PlayFeedback:
                    if (!string.IsNullOrWhiteSpace(action.feedbackMessage))
                        Debug.Log(action.feedbackMessage, this);
                    break;
            }
        }

        void UnlockAbility(GameObject player, MirrorRewardAbility ability)
        {
            if (!player) return;
            var tuning = player.GetComponent<PlayerTuning>();
            if (!tuning) return;
            switch (ability)
            {
                case MirrorRewardAbility.MirrorBlade: tuning.abilities.mirrorBladeUnlocked = true; break;
                case MirrorRewardAbility.EchoDash: tuning.abilities.echoDashUnlocked = true; break;
            }
        }

        bool IsPlayer(Collider2D other)
        {
            return other.CompareTag("Player") || other.GetComponent<PlayerInputReader>() != null;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = gizmoColor;
            if (shape == LevelTriggerShape.Box)
            {
                Gizmos.DrawCube(transform.position, new Vector3(boxSize.x, boxSize.y, 0.02f));
                Gizmos.DrawWireCube(transform.position, new Vector3(boxSize.x, boxSize.y, 0.02f));
            }
            else
            {
                Gizmos.DrawSphere(transform.position, circleRadius);
                Gizmos.DrawWireSphere(transform.position, circleRadius);
            }
        }
    }
}
