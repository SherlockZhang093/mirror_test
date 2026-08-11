using System.Collections.Generic;
using NodeCanvas.Framework;
using ParadoxNotion.Design;
using UnityEngine;

namespace MirrorTrial.Boss.NodeCanvasIntegration
{
    public abstract class MirrorArcherGroundTask : ActionTask<MirrorBossActorV2>
    {
        MirrorArcherGroundPhaseController context;

        protected MirrorArcherGroundPhaseController Context
        {
            get
            {
                if (!context && agent) context = agent.GetComponent<MirrorArcherGroundPhaseController>();
                return context;
            }
        }

        protected MirrorArcherSkillConfig Rockfall => Context && Context.Profile
            ? Context.Profile.Find(MirrorArcherSkillType.SkyRockfall)
            : null;

        protected bool CannotAct => !agent || agent.CurrentState == MirrorBossActorV2.State.Dead ||
                                    agent.CurrentState == MirrorBossActorV2.State.PhaseChange ||
                                    agent.CurrentState == MirrorBossActorV2.State.Launch;
    }

    [Category("Mirror Archer/Ground Phase")]
    [Name("Condition: In melee range")]
    public sealed class MirrorArcherGroundInMeleeRange : ConditionTask<MirrorBossActorV2>
    {
        protected override bool OnCheck() => agent && agent.DistanceToTarget <= agent.AttackRange;
    }

    [Category("Mirror Archer/Ground Phase")]
    [Name("Condition: Periodic rockfall ready")]
    public sealed class MirrorArcherGroundRockfallOpportunity : ConditionTask<MirrorBossActorV2>
    {
        protected override bool OnCheck()
        {
            if (!agent) return false;
            var context = agent.GetComponent<MirrorArcherGroundPhaseController>();
            return context && context.RockfallReady;
        }
    }

    [Category("Mirror Archer/Ground Phase")]
    [Name("Chase player")]
    public sealed class MirrorArcherGroundChaseAction : MirrorArcherGroundTask
    {
        const float DecisionTick = 0.2f;

        protected override void OnUpdate()
        {
            if (CannotAct) { EndAction(false); return; }
            if (agent.BTTickApproach()) { EndAction(true); return; }
            if (elapsedTime < DecisionTick) return;
            EndAction(true);
        }

        protected override void OnStop(bool interrupted)
        {
            if (interrupted) agent.BTStopHorizontal();
        }
    }

    [Category("Mirror Archer/Ground Phase")]
    [Name("Rockfall: Lock target")]
    public sealed class MirrorArcherRockfallLockAction : MirrorArcherGroundTask
    {
        MirrorArcherRockfallPlatformPresentation platform;
        float duration;

        protected override void OnExecute()
        {
            var skill = Rockfall;
            if (skill == null || !Context || !Context.RockfallArea) { EndAction(false); return; }
            agent.BTStopHorizontal();
            agent.ExternalFaceTarget();
            agent.ExternalPlayAnimation(skill.animatorState);
            platform = agent.GetComponent<MirrorArcherRockfallPlatformPresentation>();
            if (!platform) platform = agent.gameObject.AddComponent<MirrorArcherRockfallPlatformPresentation>();
            duration = Mathf.Max(0.1f, Context.PlatformAnimationDuration);
            var raiseStartPoint = Context.RockfallArea.GetLandingPoint(agent.transform.position.x);
            if (!platform.BeginRaise(Context.PlatformAnimationSheet, Context.PlatformPixelsPerUnit,
                    Context.PlatformLiftHeight, Context.PlatformSurfaceOffset, Context.BlockedHitSound,
                    raiseStartPoint))
                EndAction(false);
        }

        protected override void OnUpdate()
        {
            if (CannotAct)
            {
                AbortWard();
                Context?.ScheduleNextRockfall();
                EndAction(false);
                return;
            }
            platform.TickRaise(elapsedTime / duration);
            if (elapsedTime < duration) return;
            platform.HoldRaised();
            EndAction(true);
        }

        protected override void OnStop(bool interrupted)
        {
            if (!interrupted) return;
            AbortWard();
            Context?.ScheduleNextRockfall();
        }

        void AbortWard()
        {
            if (platform) platform.CancelAndRestore();
        }
    }

    [Category("Mirror Archer/Ground Phase")]
    [Name("Rockfall: Execute waves")]
    public sealed class MirrorArcherRockfallWavesAction : MirrorArcherGroundTask
    {
        readonly List<float> landingXs = new List<float>();
        int wave;
        int impact;
        float nextSpawnAt;

        protected override void OnExecute()
        {
            if (Rockfall == null || !Context || !Context.Target || !Context.RockfallArea) { EndAction(false); return; }
            wave = 0;
            impact = 0;
            BuildWave();
            nextSpawnAt = 0f;
        }

        protected override void OnUpdate()
        {
            if (CannotAct)
            {
                AbortWard();
                EndAction(false);
                return;
            }
            var skill = Rockfall;
            if (skill == null)
            {
                AbortWard();
                EndAction(false);
                return;
            }
            if (elapsedTime < nextSpawnAt) return;

            if (impact < landingXs.Count)
            {
                SpawnImpact(skill, landingXs[impact++]);
                nextSpawnAt = elapsedTime + Context.RockfallArea.PerImpactStagger;
                return;
            }

            wave++;
            if (wave >= Context.RockfallArea.WaveCount) { EndAction(true); return; }
            BuildWave();
            impact = 0;
            nextSpawnAt = elapsedTime + Context.RockfallArea.WaveInterval;
        }

        protected override void OnStop(bool interrupted)
        {
            if (interrupted) AbortWard();
        }

        void AbortWard()
        {
            var platform = agent ? agent.GetComponent<MirrorArcherRockfallPlatformPresentation>() : null;
            if (platform) platform.CancelAndRestore();
            Context?.ScheduleNextRockfall();
        }

        void BuildWave()
        {
            landingXs.Clear();
            var center = Context.Target.position.x;
            var count = Context.RockfallArea.ImpactsPerWave;
            var spacing = Context.RockfallArea.ImpactSpacing;
            var shift = wave % 2 == 0 ? 0f : spacing * 0.5f;
            var first = -(count - 1) * 0.5f * spacing + shift;
            GetArenaSpan(out var left, out var right);

            for (var i = 0; i < count; i++)
            {
                var x = Mathf.Clamp(center + first + i * spacing, left, right);
                if (!landingXs.Contains(x)) landingXs.Add(x);
            }
        }

        void SpawnImpact(MirrorArcherSkillConfig skill, float x)
        {
            var area = Context.RockfallArea;
            var point = area.GetLandingPoint(x);
            Vector2? startPoint = area.GetStartPoint(x);
            MirrorBossRockProjectile.Spawn(skill.projectilePrefab, point, agent.gameObject,
                Context.Target.gameObject, area.Damage, area.ImpactRadius, area.Knockback,
                null, area.FallDuration, startPoint);
        }

        void GetArenaSpan(out float left, out float right)
        {
            var bounds = Context.RockfallArea.WorldBounds;
            left = bounds.min.x;
            right = bounds.max.x;
        }

    }

    [Category("Mirror Archer/Ground Phase")]
    [Name("Rockfall: Recovery and cooldown")]
    public sealed class MirrorArcherRockfallRecoveryAction : MirrorArcherGroundTask
    {
        MirrorArcherRockfallPlatformPresentation platform;
        float lowerStartedAt = -1f;
        float lowerDuration;

        protected override void OnExecute()
        {
            if (Rockfall == null || !Context || !Context.RockfallArea) { EndAction(false); return; }
            agent.ExternalPlayAnimation("SwordIdle");
            agent.BTStopHorizontal();
            platform = agent.GetComponent<MirrorArcherRockfallPlatformPresentation>();
            lowerStartedAt = -1f;
            lowerDuration = Context ? Mathf.Max(0.1f, Context.PlatformAnimationDuration) : 1f;
        }

        protected override void OnUpdate()
        {
            if (CannotAct)
            {
                AbortWard();
                Context?.ScheduleNextRockfall();
                EndAction(false);
                return;
            }
            if (elapsedTime < Context.RockfallArea.Recovery) return;
            if (lowerStartedAt < 0f) lowerStartedAt = elapsedTime;
            var lowerProgress = (elapsedTime - lowerStartedAt) / lowerDuration;
            if (platform) platform.TickLower(lowerProgress);
            if (lowerProgress < 1f) return;
            if (platform) platform.FinishLowering();
            Context?.ScheduleNextRockfall();
            EndAction(true);
        }

        protected override void OnStop(bool interrupted)
        {
            if (!interrupted) return;
            AbortWard();
            Context?.ScheduleNextRockfall();
        }

        void AbortWard()
        {
            if (platform) platform.CancelAndRestore();
        }
    }
}
