using MirrorTrial.Boss.NodeCanvasIntegration;
using NodeCanvas.BehaviourTrees;
using UnityEngine;

namespace MirrorTrial.Boss
{
    /// <summary>
    /// Runtime context for the grounded phase. NodeCanvas owns combat decisions and sequencing;
    /// this component only supplies references and the periodic rockfall clock.
    /// </summary>
    [RequireComponent(typeof(MirrorBossActorV2))]
    public sealed class MirrorArcherGroundPhaseController : MonoBehaviour
    {
        [SerializeField] MirrorArcherTwoStageProfile profile;
        [SerializeField] MirrorBossActorV2 actor;
        [SerializeField] BehaviourTree groundMeleeTree;
        [SerializeField] MirrorArcherRockfallArea rockfallArea;

        [Header("Rockfall platform ward")]
        [SerializeField] Texture2D platformAnimationSheet;
        [SerializeField] AudioClip blockedHitSound;

        Transform target;
        BehaviourTreeOwner treeOwner;
        float nextRockfallReadyAt;

        public MirrorArcherTwoStageProfile Profile => profile;
        public Transform Target => target;
        public MirrorArcherRockfallArea RockfallArea => rockfallArea;
        public bool RockfallReady => Time.time >= nextRockfallReadyAt;
        public Texture2D PlatformAnimationSheet => platformAnimationSheet;
        public AudioClip BlockedHitSound => blockedHitSound;
        public float PlatformLiftHeight => rockfallArea ? rockfallArea.PlatformLiftHeight : 2.2f;
        public float PlatformPixelsPerUnit => rockfallArea ? rockfallArea.PlatformPixelsPerUnit : 100f;
        public float PlatformSurfaceOffset => rockfallArea ? rockfallArea.PlatformSurfaceOffset : 0.2f;
        public float PlatformAnimationDuration => rockfallArea ? rockfallArea.PlatformAnimationDuration : 2f;

        public void Configure(MirrorArcherTwoStageProfile nextProfile,
            MirrorArcherRockfallArea sceneRockfallArea = null)
        {
            profile = nextProfile;
            if (sceneRockfallArea) rockfallArea = sceneRockfallArea;
            if (!actor) actor = GetComponent<MirrorBossActorV2>();
        }

        void Awake()
        {
            if (!actor) actor = GetComponent<MirrorBossActorV2>();
            treeOwner = GetComponent<BehaviourTreeOwner>();
        }

        public void BeginFromCrash(Transform playerTarget, bool facingRight)
        {
            if (!profile || !actor || !playerTarget) return;
            target = playerTarget;
            nextRockfallReadyAt = Time.time + (rockfallArea ? rockfallArea.FirstDelay : 3f);
            actor.SetExternalCombatDriver(true, 1);
            actor.Activate(playerTarget);
            actor.ExternalFaceTarget();
            StartGroundTree();
        }

        void StartGroundTree()
        {
            if (!treeOwner)
            {
                Debug.LogError("[GroundPhase] No BehaviourTreeOwner on the ground boss.", this);
                return;
            }

            var chosen = groundMeleeTree ? groundMeleeTree : treeOwner.behaviour;
            if (!chosen)
            {
                Debug.LogError("[GroundPhase] No ground-phase behaviour tree assigned.", this);
                return;
            }

            treeOwner.enabled = true;
            var sync = GetComponent<MirrorBossBlackboardSync>();
            if (sync) sync.enabled = true;
            treeOwner.StartBehaviour(chosen);
        }

        public void ScheduleNextRockfall()
        {
            nextRockfallReadyAt = Time.time + (rockfallArea ? rockfallArea.Cooldown : 7f);
        }
    }
}
