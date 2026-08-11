using System;
using System.Collections.Generic;
using UnityEngine;

namespace MirrorTrial.Boss
{
    public enum MirrorArcherCombatStage
    {
        Air,
        Ground
    }

    public enum MirrorArcherSkillType
    {
        LockedShot,
        FanShot,
        GroundArrowRain,
        MountedDive,
        AirReposition,
        SkyRockfall,
        SharedBaseCombo,
        SharedHeavySlash,
        SharedFeint
    }

    [Serializable]
    public sealed class MirrorArcherSkillConfig
    {
        public string displayName = "Skill";
        public MirrorArcherCombatStage stage;
        public MirrorArcherSkillType type;

        [Header("Animation and timing")]
        public AnimationClip animationClip;
        public string animatorState = "BowAim";
        [Min(0f)] public float windup = 0.6f;
        [Min(0f)] public float recovery = 0.7f;
        [Min(0f)] public float lockMoment = 0.25f;
        [Min(0f)] public float releaseMoment = 0.6f;

        [Header("Independent telegraph")]
        public GameObject windupEffectPrefab;
        public Vector2 effectOffset = new Vector2(0.65f, 0.9f);

        [Header("Real preview/runtime prefabs")]
        public GameObject projectilePrefab;
        public GameObject impactPrefab;

        [Header("Arrow values")]
        [Min(1)] public int arrowCount = 1;
        [Tooltip("Angle added to the computed launch direction. Positive values rotate counter-clockwise.")]
        public float initialAngleOffset;
        [Min(0f)] public float arrowAngle = 18f;
        [Min(0.1f)] public float arrowSpeed = 12f;
        [Min(0.1f)] public float arrowRange = 20f;
        [Min(0)] public int damage = 1;
        public Vector2 knockback = new Vector2(1.5f, 0.3f);

        [Header("Movement / rockfall range")]
        public Vector2 movementOffset = new Vector2(4f, 1.5f);
        [Min(0.1f)] public float movementDuration = 0.6f;
        [Min(0.1f)] public float effectRadius = 0.75f;
        [Min(0f)] public float range = 7f;
        [Min(0f)] public float safeRadius = 2.2f;
        [Min(1)] public int waveCount = 3;
        [Min(1)] public int impactsPerWave = 5;
        [Min(0f)] public float waveInterval = 0.45f;
        [Tooltip("同一波内每颗落石之间的错开时间（秒），让石头先后落下而不是同时并排落。")]
        [Min(0f)] public float perImpactStagger = 0.18f;

        public float EffectiveReleaseMoment => Mathf.Max(lockMoment, releaseMoment);
    }

    [CreateAssetMenu(menuName = "Mirror Trial/Boss/Mirror Archer Two Stage Profile",
        fileName = "MirrorArcherTwoStageProfile")]
    public sealed class MirrorArcherTwoStageProfile : ScriptableObject
    {
        [Header("Stage prefabs")]
        public GameObject mountedBossPrefab;
        public GameObject groundBossPrefab;
        [Tooltip("The first sword boss profile is referenced directly. Its combo, heavy slash and feint data are never copied.")]
        public MirrorBossSimpleProfile sharedGroundMeleeProfile;

        [Header("Mounted health and air space")]
        public string mountedDisplayName = "镜中猎手 · 骑乘";
        [Min(1)] public int mountedHitPoints = 180;
        [Min(0.1f)] public float airMoveSpeed = 5f;
        public Vector2 airPadding = new Vector2(1.2f, 0.8f);
        [Min(0f)] public float firstActionDelay = 0.8f;

        [Header("Crash transition")]
        [Min(0.1f)] public float crashDuration = 1.15f;
        public AnimationCurve crashCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 0f),
            new Keyframe(0.45f, 0.22f, 0.7f, 0.15f),
            new Keyframe(1f, 1f, 2.2f, 2.2f));
        public Vector2 crashLandingOffset = Vector2.zero;
        public GameObject crashImpactPrefab;
        [Min(0f)] public float impactCoverDuration = 0.35f;
        public string mountedFallState = "MountedFall";
        public string mountedFallLoopState = "MountedFallLoop";
        [Min(0f)] public float mountedFallStartDuration = 0.55f;
        public string mountedImpactState = "MountedImpact";
        public string groundKnockdownState = "GroundKnockdown";
        public string groundGetUpState = "GroundGetUp";
        [Min(0f)] public float groundKnockdownDuration = 0.5f;
        [Min(0f)] public float groundGetUpDuration = 0.75f;

        [Header("Ground decisions")]
        [Min(0.1f)] public float groundCloseRange = 3f;
        [Min(0f)] public float groundDecisionGap = 0.35f;

        [Header("Configured rotation (no immediate repeats)")]
        public List<MirrorArcherSkillConfig> airSkills = new List<MirrorArcherSkillConfig>();
        public List<MirrorArcherSkillConfig> groundSkills = new List<MirrorArcherSkillConfig>();

        public MirrorArcherSkillConfig Find(MirrorArcherSkillType type)
        {
            var list = IsAirSkill(type) ? airSkills : groundSkills;
            if (list == null) return null;
            return list.Find(skill => skill != null && skill.type == type);
        }

        public void EnsureDefaults()
        {
            if (airSkills == null) airSkills = new List<MirrorArcherSkillConfig>();
            if (groundSkills == null) groundSkills = new List<MirrorArcherSkillConfig>();
            Ensure(airSkills, Air("锁定箭", MirrorArcherSkillType.LockedShot, 1));
            Ensure(airSkills, Air("扇形箭", MirrorArcherSkillType.FanShot, 5));
            Ensure(airSkills, Air("对地箭雨", MirrorArcherSkillType.GroundArrowRain, 6));
            Ensure(airSkills, Air("坐骑俯冲", MirrorArcherSkillType.MountedDive, 1));
            Ensure(airSkills, Air("空中换位", MirrorArcherSkillType.AirReposition, 1));

            Ensure(groundSkills, Ground("朝天射箭 / 多波落石", MirrorArcherSkillType.SkyRockfall));
            Ensure(groundSkills, Ground("共享基础连招", MirrorArcherSkillType.SharedBaseCombo));
            Ensure(groundSkills, Ground("共享蓄力重斩", MirrorArcherSkillType.SharedHeavySlash));
            Ensure(groundSkills, Ground("共享假动作", MirrorArcherSkillType.SharedFeint));
        }

        void OnValidate()
        {
            EnsureDefaults();
            Normalize(airSkills, MirrorArcherCombatStage.Air);
            Normalize(groundSkills, MirrorArcherCombatStage.Ground);
        }

        static void Normalize(List<MirrorArcherSkillConfig> skills, MirrorArcherCombatStage stage)
        {
            if (skills == null) return;
            foreach (var skill in skills)
            {
                if (skill == null) continue;
                skill.stage = stage;
                skill.arrowCount = Mathf.Max(1, skill.arrowCount);
                skill.damage = Mathf.Max(0, skill.damage);
                skill.releaseMoment = Mathf.Max(skill.lockMoment, skill.releaseMoment);
            }
        }

        static void Ensure(List<MirrorArcherSkillConfig> list, MirrorArcherSkillConfig value)
        {
            if (list.Exists(skill => skill != null && skill.type == value.type)) return;
            list.Add(value);
        }

        static MirrorArcherSkillConfig Air(string name, MirrorArcherSkillType type, int arrows)
        {
            var result = new MirrorArcherSkillConfig
            {
                displayName = name,
                stage = MirrorArcherCombatStage.Air,
                type = type,
                arrowCount = arrows,
                animatorState = type == MirrorArcherSkillType.MountedDive
                    ? "MountedDive"
                    : type == MirrorArcherSkillType.AirReposition ? "MountedFlight" : "MountedBowFire"
            };
            if (type == MirrorArcherSkillType.GroundArrowRain)
            {
                result.arrowAngle = 0f;
                result.range = 5f;
            }
            if (type == MirrorArcherSkillType.MountedDive)
            {
                result.windup = 0.75f;
                result.movementDuration = 0.65f;
                result.effectRadius = 1.1f;
                result.damage = 2;
            }
            if (type == MirrorArcherSkillType.AirReposition)
            {
                result.windup = 0.2f;
                result.recovery = 0.25f;
                result.movementOffset = new Vector2(4.5f, 1.8f);
            }
            return result;
        }

        static MirrorArcherSkillConfig Ground(string name, MirrorArcherSkillType type)
        {
            var result = new MirrorArcherSkillConfig
            {
                displayName = name,
                stage = MirrorArcherCombatStage.Ground,
                type = type,
                animatorState = type == MirrorArcherSkillType.SkyRockfall ? "BowFire" : "ComboAttackA"
            };
            if (type == MirrorArcherSkillType.SkyRockfall)
            {
                result.windup = 0.9f;
                result.recovery = 1.15f;
                result.releaseMoment = 0.8f;
                result.range = 8f;
                result.safeRadius = 2.4f;
                result.effectRadius = 0.8f;
                result.waveCount = 3;
                result.impactsPerWave = 5;
            }
            return result;
        }

        static bool IsAirSkill(MirrorArcherSkillType type)
        {
            return type <= MirrorArcherSkillType.AirReposition;
        }
    }
}
