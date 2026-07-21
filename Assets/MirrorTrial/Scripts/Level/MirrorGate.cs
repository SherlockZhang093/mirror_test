using System.Collections;
using MirrorTrial.Combat;
using MirrorTrial.Player;
using UnityEngine;
using UnityEngine.Serialization;

namespace MirrorTrial.Level
{
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(Hurtbox))]
    public class MirrorGate : MonoBehaviour
    {
        [Header("Basic")]
        [ChineseLabel("镜子门 ID")] [Tooltip("镜子门的唯一 ID")] [SerializeField] string gateId = "MirrorGate_Blade";
        [ChineseLabel("镜中场景名")] [Tooltip("击碎后加载的镜中场景名或路径")] [SerializeField] string mirrorSceneName = "level_01_mirror";

        [Header("Health")]
        [ChineseLabel("需要命中次数")] [Tooltip("无论单次伤害多少，镜子都必须被有效命中指定次数")]
        [FormerlySerializedAs("hitPoints")] [SerializeField] int requiredHits = 3;
        [ChineseLabel("当前命中次数")] [Tooltip("本次运行中已经命中的次数（只读）")]
        [FormerlySerializedAs("currentHitPoints")] [SerializeField] int currentHits;

        [Header("Reward And Progression")]
        [ChineseLabel("奖励能力")] [Tooltip("镜中战斗完成后解锁的能力")] [SerializeField] MirrorRewardAbility rewardAbility = MirrorRewardAbility.None;
        [ChineseLabel("后续关卡段落")] [Tooltip("完成镜子门后启用的关卡段落")] [SerializeField] LevelSegment nextSegment;

        [Header("Visual")]
        [ChineseLabel("镜子 Prefab")] [Tooltip("包含镜面、边框、裂纹和碎裂效果的镜子 Prefab")]
        [SerializeField] GameObject mirrorPrefab;

        [Header("Transition")]
        [SerializeField] float enterMirrorDelay = -1f;

        GameObject visualObject;
        MirrorShatterEffect shatterEffect;
        Collider2D gateCollider;
        PlayerInputReader lockedInput;
        bool completionProcessed;
        Coroutine enterMirrorRoutine;

        public string GateId => gateId;
        public string MirrorSceneName => mirrorSceneName;
        public MirrorRewardAbility RewardAbility => rewardAbility;
        public LevelSegment NextSegment => nextSegment;
        public GameObject MirrorPrefab => mirrorPrefab;
        public MirrorGateState State { get; private set; } = MirrorGateState.Intact;
        public int HitPoints => requiredHits;
        public int CurrentHitPoints => currentHits;
        public bool IsCompleted => State == MirrorGateState.Completed;

        public System.Action<MirrorGate> OnSmashed;
        public System.Action<MirrorGate> OnCompleted;

        void Awake()
        {
            requiredHits = Mathf.Max(1, requiredHits);
            currentHits = 0;
            gateCollider = GetComponent<Collider2D>();
            if (gateCollider) gateCollider.isTrigger = true;

            shatterEffect = GetComponentInChildren<MirrorShatterEffect>();
            if (shatterEffect)
                visualObject = shatterEffect.gameObject;
            else if (mirrorPrefab)
            {
                visualObject = Instantiate(mirrorPrefab, transform);
                visualObject.name = mirrorPrefab.name;
                shatterEffect = visualObject.GetComponentInChildren<MirrorShatterEffect>();
            }

            var bridge = MirrorTransitionBridge.Ensure();
            if (bridge.IsGateCompleted(gateId))
            {
                State = MirrorGateState.Completed;
                if (shatterEffect) shatterEffect.ShowCompletedFrameOnly();
                if (gateCollider) gateCollider.enabled = false;
                var hurtbox = GetComponent<Hurtbox>();
                if (hurtbox) hurtbox.enabled = false;
            }
        }

        void Start()
        {
            if (State == MirrorGateState.Completed && !completionProcessed)
            {
                completionProcessed = true;
                FinalizeCompletion();
            }
        }

        public void OnDamagePayloadReceived(DamagePayload payload)
        {
            if (State != MirrorGateState.Intact) return;

            if (payload.damage <= 0) return;
            currentHits = Mathf.Min(requiredHits, currentHits + 1);
            if (shatterEffect)
            {
                shatterEffect.SetHitProgress(currentHits, requiredHits);
                if (currentHits < requiredHits) shatterEffect.PlayHit(payload.direction);
            }

            if (currentHits >= requiredHits)
                Smash(payload.source, payload.direction);
        }

        public void Smash(GameObject attacker)
        {
            var direction = attacker
                ? (Vector2)(transform.position - attacker.transform.position)
                : Vector2.right;
            Smash(attacker, direction);
        }

        void Smash(GameObject attacker, Vector2 impactDirection)
        {
            if (State != MirrorGateState.Intact) return;
            currentHits = requiredHits;
            State = MirrorGateState.Smashed;
            OnSmashed?.Invoke(this);

            var player = FindPlayerReader(attacker);
            LockPlayerInput(player);
            if (shatterEffect) shatterEffect.Play(impactDirection);

            if (gateCollider) gateCollider.enabled = false;
            var hurtbox = GetComponent<Hurtbox>();
            if (hurtbox) hurtbox.enabled = false;

            if (enterMirrorRoutine == null)
                enterMirrorRoutine = StartCoroutine(EnterMirrorAfterShatter(player));
        }

        IEnumerator EnterMirrorAfterShatter(PlayerInputReader player)
        {
            var delay = enterMirrorDelay >= 0f
                ? enterMirrorDelay
                : (shatterEffect ? shatterEffect.RecommendedEnterDelay : 0f);
            if (delay > 0f)
                yield return new WaitForSecondsRealtime(delay);

            UnlockPlayerInput();
            var playerObject = player ? player.gameObject : FindPlayer();
            var bridge = MirrorTransitionBridge.Ensure();
            bridge.EnterMirror(gateId, mirrorSceneName, playerObject);
            enterMirrorRoutine = null;
        }

        void FinalizeCompletion()
        {
            if (shatterEffect) shatterEffect.ShowCompletedFrameOnly();
            var bridge = MirrorTransitionBridge.Instance;
            var player = bridge ? bridge.PersistentPlayer : FindPlayer();

            UnlockAbility(player);
            if (player)
                player.transform.position = transform.position;
            else if (bridge)
                bridge.PlacePlayerAt(transform.position);

            if (nextSegment) nextSegment.Enable();
            OnCompleted?.Invoke(this);
        }

        void LockPlayerInput(PlayerInputReader player)
        {
            lockedInput = player;
            if (lockedInput) lockedInput.InputEnabled = false;
        }

        void UnlockPlayerInput()
        {
            if (lockedInput) lockedInput.InputEnabled = true;
            lockedInput = null;
        }

        PlayerInputReader FindPlayerReader(GameObject attacker = null)
        {
            if (attacker)
            {
                var reader = attacker.GetComponentInParent<PlayerInputReader>();
                if (reader) return reader;
            }
            return FindObjectOfType<PlayerInputReader>();
        }

        void UnlockAbility(GameObject player)
        {
            if (!player) return;
            var tuning = player.GetComponent<PlayerTuning>();
            if (!tuning) return;
            switch (rewardAbility)
            {
                case MirrorRewardAbility.MirrorBlade: tuning.abilities.mirrorBladeUnlocked = true; break;
                case MirrorRewardAbility.EchoDash: tuning.abilities.echoDashUnlocked = true; break;
            }
        }

        GameObject FindPlayer()
        {
            var player = FindObjectOfType<PlayerInputReader>();
            return player ? player.gameObject : null;
        }

        void OnDisable()
        {
            UnlockPlayerInput();
        }

        void OnDrawGizmos()
        {
            Color color;
            switch (State)
            {
                case MirrorGateState.Smashed: color = new Color(0.25f, 0.9f, 1f, 0.95f); break;
                case MirrorGateState.Completed: color = new Color(0.9f, 0.45f, 0.3f, 0.95f); break;
                default: color = new Color(0.75f, 0.85f, 1f, 0.95f); break;
            }
            Gizmos.color = color;
            Gizmos.DrawWireCube(transform.position, new Vector3(0.8f, 1.2f, 0.1f));
        }
    }
}
