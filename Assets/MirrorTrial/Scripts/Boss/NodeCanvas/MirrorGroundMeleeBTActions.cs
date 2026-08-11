using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;

namespace MirrorTrial.Boss.NodeCanvasIntegration
{
    /// <summary>
    /// Bridge the ground-phase controller exposes so behaviour-tree tasks can trigger the
    /// rockfall attack (art / telegraph / hit) without the tree owning that logic. Keeps the
    /// rockfall implementation in <see cref="MirrorArcherGroundPhaseController"/> (separation of
    /// concerns) while the tree owns the DECISION of when to cast it.
    /// </summary>
    public interface IMirrorGroundRockfallDriver
    {
        bool RockfallReady { get; }
        void BeginRockfall();
        bool RockfallRunning { get; }
        float GroundCloseRange { get; }
    }

    /// <summary>
    /// Locates the rockfall driver next to the boss actor once, then caches it.
    /// </summary>
    public abstract class MirrorGroundMeleeTask : ActionTask<MirrorBossActorV2>
    {
        IMirrorGroundRockfallDriver cachedDriver;

        protected IMirrorGroundRockfallDriver Driver
        {
            get
            {
                if (cachedDriver == null && agent)
                    cachedDriver = agent.GetComponent<IMirrorGroundRockfallDriver>();
                return cachedDriver;
            }
        }

        protected bool Dead => agent.CurrentState == MirrorBossActorV2.State.Dead;
        protected bool Interrupted =>
            agent.CurrentState == MirrorBossActorV2.State.PhaseChange ||
            agent.CurrentState == MirrorBossActorV2.State.Launch;
    }

    [Category("镜中猎手/地面")]
    [Name("地面登场：倒地起身")]
    [Description("坠地后播放GroundKnockdown → GroundGetUp，完成后标记已登场并进入战斗。仅在尚未登场时执行一次。")]
    public sealed class MirrorGroundIntroAction : MirrorGroundMeleeTask
    {
        [Tooltip("倒地停顿秒数。")] public float knockdownDuration = 0.5f;
        [Tooltip("起身停顿秒数。")] public float getUpDuration = 0.75f;
        [Tooltip("倒地动画状态名。")] public string knockdownState = "GroundKnockdown";
        [Tooltip("起身动画状态名。")] public string getUpState = "GroundGetUp";

        int stage;
        float phaseElapsed;

        protected override void OnExecute()
        {
            stage = 0;
            phaseElapsed = 0f;
            agent.BTStopHorizontal();
            agent.ExternalPlayAnimation(knockdownState);
        }

        protected override void OnUpdate()
        {
            if (Dead) return;
            if (Interrupted) { EndAction(false); return; }

            phaseElapsed += Time.deltaTime;
            if (stage == 0 && phaseElapsed >= knockdownDuration)
            {
                stage = 1;
                phaseElapsed = 0f;
                agent.ExternalPlayAnimation(getUpState);
            }
            else if (stage == 1 && phaseElapsed >= getUpDuration)
            {
                agent.BTFinishIntro();
                EndAction(true);
            }
        }
    }

    [Category("镜中猎手/地面")]
    [Name("条件：玩家在远程区（超出贴身距离）")]
    [Description("DistanceToTarget > GroundCloseRange 时通过，用于进入朝天落石分支。")]
    public sealed class MirrorGroundPlayerIsFarCondition : ConditionTask<MirrorBossActorV2>
    {
        [Tooltip("留空则读取落石驱动的 GroundCloseRange。")]
        public float overrideCloseRange = -1f;

        IMirrorGroundRockfallDriver driver;

        protected override bool OnCheck()
        {
            if (!agent) return false;
            if (driver == null) driver = agent.GetComponent<IMirrorGroundRockfallDriver>();
            var close = overrideCloseRange > 0f ? overrideCloseRange
                : driver != null ? driver.GroundCloseRange : 3f;
            return agent.DistanceToTarget > close;
        }
    }

    [Category("镜中猎手/地面")]
    [Name("条件：落石已就绪")]
    [Description("落石不在冷却/未在进行中时通过。")]
    public sealed class MirrorGroundRockfallReadyCondition : ConditionTask<MirrorBossActorV2>
    {
        IMirrorGroundRockfallDriver driver;

        protected override bool OnCheck()
        {
            if (!agent) return false;
            if (driver == null) driver = agent.GetComponent<IMirrorGroundRockfallDriver>();
            return driver != null && driver.RockfallReady;
        }
    }

    [Category("镜中猎手/地面")]
    [Name("执行朝天落石")]
    [Description("触发落石驱动（Boss 动画 + 全场落点预警 + 多波落石），全部结束后返回成功。")]
    public sealed class MirrorGroundRockfallAction : MirrorGroundMeleeTask
    {
        protected override void OnExecute()
        {
            if (Driver == null || !Driver.RockfallReady) { EndAction(false); return; }
            Driver.BeginRockfall();
        }

        protected override void OnUpdate()
        {
            if (Dead) return;
            if (Interrupted) { EndAction(false); return; }
            if (Driver == null) { EndAction(false); return; }
            if (!Driver.RockfallRunning) EndAction(true);
        }
    }

    [Category("镜中猎手/地面")]
    [Name("追击玩家进入攻击距离")]
    [Description("每帧朝玩家横向移动，进入 AttackRange 后停下返回成功。单平台，无跳跃/垂直追击。")]
    public sealed class MirrorGroundApproachAction : MirrorGroundMeleeTask
    {
        [Tooltip("追击超时保护（秒），超时也返回成功以免卡死。")]
        public float maxDuration = 4f;

        protected override void OnUpdate()
        {
            if (Dead) return;
            if (Interrupted) { EndAction(false); return; }
            if (elapsedTime >= maxDuration) { agent.BTStopHorizontal(); EndAction(true); return; }
            if (agent.BTTickApproach()) EndAction(true);
        }

        protected override void OnStop(bool interrupted)
        {
            if (interrupted) agent.BTStopHorizontal();
        }
    }

    [Category("镜中猎手/地面")]
    [Name("贴身连招")]
    [Description("播放当前阶段基础连招（含内置假动作），进入后摇状态时返回成功。")]
    public sealed class MirrorGroundComboAction : MirrorGroundMeleeTask
    {
        protected override void OnExecute()
        {
            if (!agent.BTBeginCombo()) EndAction(false);
        }

        protected override void OnUpdate()
        {
            if (Dead) return;
            if (Interrupted) { EndAction(false); return; }
            if (agent.CurrentState == MirrorBossActorV2.State.Recovery) EndAction(true);
        }
    }

    [Category("镜中猎手/地面")]
    [Name("条件：本轮用重斩")]
    [Description("按配置概率与冷却决定本轮是否使用蓄力重斩。")]
    public sealed class MirrorGroundHeavySlashCondition : ConditionTask<MirrorBossActorV2>
    {
        protected override bool OnCheck() => agent && agent.BTShouldUseHeavySlash();
    }

    [Category("镜中猎手/地面")]
    [Name("蓄力重斩")]
    [Description("锁定朝向，播放可读蓄力与单次重斩，进入后摇时返回成功。")]
    public sealed class MirrorGroundHeavySlashAction : MirrorGroundMeleeTask
    {
        protected override void OnExecute()
        {
            if (!agent.BTBeginHeavySlash()) EndAction(false);
        }

        protected override void OnUpdate()
        {
            if (Dead) return;
            if (Interrupted) { EndAction(false); return; }
            if (agent.CurrentState == MirrorBossActorV2.State.Recovery) EndAction(true);
        }
    }

    [Category("镜中猎手/地面")]
    [Name("攻击后摇")]
    [Description("关闭攻击框进入可反击窗口；期间轻微贴近玩家，硬直结束后恢复。")]
    public sealed class MirrorGroundRecoveryAction : MirrorGroundMeleeTask
    {
        [Tooltip("普通连招后摇时长范围。")] public Vector2 comboRecovery = new Vector2(0.55f, 0.85f);
        [Tooltip("重斩后摇时长范围。")] public Vector2 heavyRecovery = new Vector2(0.9f, 1.2f);
        float duration;

        protected override void OnExecute()
        {
            agent.BTBeginRecovery();
            var range = agent.LastAttackWasHeavySlash ? heavyRecovery : comboRecovery;
            duration = Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));
        }

        protected override void OnUpdate()
        {
            if (Dead) return;
            if (Interrupted) { EndAction(false); return; }
            agent.BTTickRecoveryApproach();
            if (elapsedTime < duration) return;
            agent.BTFinishRecovery();
            EndAction(true);
        }
    }

    [Category("镜中猎手/地面")]
    [Name("拉开间距（后撤/侧移）")]
    [Description("按概率决定本轮是否做一次走位：太贴脸时后撤，否则侧移换位，制造落石/重斩空间。单平台横向移动。")]
    public sealed class MirrorGroundRepositionAction : MirrorGroundMeleeTask
    {
        [Range(0f, 1f), Tooltip("每轮触发走位的概率；不触发时立即返回成功不打断节奏。")]
        public float chance = 0.35f;
        [Tooltip("走位持续时间范围（秒）。")] public Vector2 durationRange = new Vector2(0.35f, 0.6f);
        [Tooltip("贴脸阈值：与玩家距离小于该值时选择后撤，否则侧移。")]
        public float tooCloseDistance = 1.4f;
        [Range(0.2f, 1f), Tooltip("走位速度倍率。")] public float speedMultiplier = 0.8f;

        float duration;
        float direction;

        protected override void OnExecute()
        {
            if (Random.value > chance) { EndAction(true); return; }
            duration = Random.Range(Mathf.Min(durationRange.x, durationRange.y),
                Mathf.Max(durationRange.x, durationRange.y));

            var toPlayer = agent.DistanceToTarget;
            var playerOnRight = agent.FacingRight; // boss faces the player during combat
            if (toPlayer < tooCloseDistance)
                direction = playerOnRight ? -1f : 1f; // back-pedal away from player
            else
                direction = Random.value < 0.5f ? -1f : 1f; // neutral side-step
        }

        protected override void OnUpdate()
        {
            if (Dead) return;
            if (Interrupted) { EndAction(false); return; }
            if (elapsedTime >= duration) { agent.BTStopHorizontal(); EndAction(true); return; }
            agent.BTStrafe(direction, speedMultiplier);
        }

        protected override void OnStop(bool interrupted)
        {
            if (interrupted) agent.BTStopHorizontal();
        }
    }
}
