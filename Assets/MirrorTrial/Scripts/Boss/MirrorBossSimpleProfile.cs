using System;
using System.Collections.Generic;
using MirrorTrial.Combat;
using MirrorTrial.Enemies;
using MirrorTrial.Player;
using UnityEngine;

namespace MirrorTrial.Boss
{
    [Serializable]
    public sealed class MirrorBossGetUpProtectionSettings
    {
        [InspectorName("起身期间无敌")]
        [Tooltip("Whether the boss ignores damage while the get-up animation is playing.")]
        public bool invincibleDuringGetUp = true;

        [InspectorName("起身后无敌时间")]
        [Tooltip("Additional invincibility after the boss finishes getting up and resumes acting.")]
        [Min(0f)] public float postGetUpInvincibleDuration = 1f;

        [InspectorName("无敌闪烁间隔")]
        [Tooltip("Seconds between visibility toggles while get-up invincibility is active.")]
        [Min(0.02f)] public float blinkInterval = 0.08f;
    }

    public enum MirrorBossTacticalAction
    {
        Shoot,
        TeleportAttack,
        TeleportRetreat,
        Summon,
        CloseDefense
    }

    [Serializable]
    public sealed class MirrorBossComboStepV2
    {
        public string animationState = "ComboAttackA";
        [Range(0f, 1f)] public float activeStart = 0.3f;
        [Range(0f, 1f)] public float activeEnd = 0.52f;
        [Min(0.1f)] public float playbackSpeed = 1f;
        [Min(0f)] public float gapAfter = 0.08f;
        [Min(1)] public int damage = 1;
        public Vector2 hitboxOffset = new Vector2(0.8f, 0.05f);
        public Vector2 hitboxSize = new Vector2(1.3f, 0.9f);
        public Vector2 knockback = new Vector2(1.2f, 0.5f);
        [Range(0f, 0.2f)] public float hitStop = 0.06f;
        [Min(0)] public int interruptPower = 1;
        [Min(0f)] public float poiseDamage = 1f;
        public HitReactionType playerHitReaction = HitReactionType.LightHurt;
        public bool breaksSuperArmor;
        [Min(0f)] public float advanceSpeed = 1.2f;

        [Header("Animation Windup Hold")]
        [Tooltip("前摇定格帧；-1 表示本招不使用定格前摇。")]
        [Min(-1)] public int windupFrame = -1;
        [Tooltip("动画到达前摇帧后冻结的时间。")]
        [Min(0f)] public float windupHoldDuration;
        [Tooltip("Charge-point offset used by the shared windup presentation.")]
        public Vector2 windupEffectOffset = new Vector2(0.7f, 0.9f);
        [Tooltip("Extra rotation applied to the windup effect. Mirrored automatically when facing left.")]
        public float windupEffectAngle;
        [Tooltip("Whether this move may use the boss feint system.")]
        public bool allowFeint = true;
        [Tooltip("-1 uses the phase chance. Otherwise this value overrides the chance for this move.")]
        [Range(-1f, 1f)] public float feintChanceOverride = -1f;

        [Header("逐帧攻击框（与玩家共用格式）")]
        [Min(1)] public int animationFrameRate = 12;
        [Min(2)] public int animationFrameCount = 12;
        public bool mirrorHitboxByFacing = true;
        [HideInInspector] public bool hitboxKeysMigratedToClip;
        public List<PlayerAttackHitboxKey> hitboxKeys = new List<PlayerAttackHitboxKey>();

        public void EnsureHitboxKeys()
        {
            if (hitboxKeys == null) hitboxKeys = new List<PlayerAttackHitboxKey>();
            if (hitboxKeys.Count != 0) { hitboxKeys.Sort((a, b) => a.frame.CompareTo(b.frame)); return; }
            var lastFrame = Mathf.Max(1, animationFrameCount - 1);
            var startFrame = Mathf.Clamp(Mathf.RoundToInt(activeStart * lastFrame), 1, lastFrame);
            var endFrame = Mathf.Clamp(Mathf.RoundToInt(activeEnd * lastFrame), startFrame + 1, animationFrameCount);
            hitboxKeys.Add(new PlayerAttackHitboxKey { frame = 0, enabled = false, offset = hitboxOffset, size = hitboxSize });
            hitboxKeys.Add(new PlayerAttackHitboxKey { frame = startFrame, enabled = true, offset = hitboxOffset, size = hitboxSize, interpolation = AttackHitboxInterpolation.Linear });
            hitboxKeys.Add(new PlayerAttackHitboxKey { frame = endFrame, enabled = false, offset = hitboxOffset, size = hitboxSize });
        }
    }

    [CreateAssetMenu(menuName = "Mirror Trial/Boss/Simple Mirror Boss Profile", fileName = "MirrorBossSimpleProfile")]
    public sealed class MirrorBossSimpleProfile : ScriptableObject
    {
        public string displayName = "镜中行刑者";
        [Min(1)] public int maxHitPoints = 300;

        [Header("Approach")]
        [Min(0f)] public float moveSpeed = 2.8f;
        [Min(0f)] public float acceleration = 18f;
        [Min(0f)] public float attackRange = 1.5f;
        [Min(0f)] public float introDuration = 0.8f;
        [Range(0f, 1f)] public float cooldownMoveSpeedMultiplier = 0.55f;

        [Header("Phases")]
        [Range(0f, 1f)] public float phaseTwoAt = 0.7f;
        [Range(0f, 1f)] public float phaseThreeAt = 0.35f;
        [Min(0f)] public float phaseTransitionDuration = 1.2f;
        public Vector2 phaseOneRecovery = new Vector2(3f, 3.4f);
        public Vector2 phaseTwoRecovery = new Vector2(2.6f, 3f);
        public Vector2 phaseThreeRecovery = new Vector2(2.2f, 2.6f);

        [Header("Feint")]
        [Tooltip("Chance to feint once during a combo in each phase. Phase 1 defaults to zero so the player can first learn the real tell.")]
        [Range(0f, 1f)] public float phaseOneFeintChance;
        [Range(0f, 1f)] public float phaseTwoFeintChance = 0.3f;
        [Range(0f, 1f)] public float phaseThreeFeintChance = 0.45f;
        [Tooltip("How long the boss holds the fake windup before pulling the attack back.")]
        [Min(0.05f)] public float feintHoldDuration = 0.18f;
        [Tooltip("Readable pause between pulling back and repeating the real attack.")]
        [Min(0.05f)] public float feintResetDuration = 0.24f;
        [Min(1)] public int maxFeintsPerCombo = 1;

        [Header("Windup Presentation")]
        [Tooltip("Fallback used when a phase-specific windup prefab is not assigned.")]
        public GameObject windupEffectPrefab;
        public GameObject phaseOneWindupEffectPrefab;
        public GameObject phaseTwoWindupEffectPrefab;
        public GameObject phaseThreeWindupEffectPrefab;
        [Min(0f)] public float phaseOneWindupHoldDuration = 0.65f;
        [Min(0f)] public float phaseTwoWindupHoldDuration = 0.5f;
        [Min(0f)] public float phaseThreeWindupHoldDuration = 0.38f;
        [HideInInspector]
        public ChargeTelegraphSettings windupPresentation = new ChargeTelegraphSettings();

        public GameObject GetWindupEffectPrefab(int phase)
        {
            var phasePrefab = phase <= 1 ? phaseOneWindupEffectPrefab
                : phase == 2 ? phaseTwoWindupEffectPrefab
                : phaseThreeWindupEffectPrefab;
            return phasePrefab ? phasePrefab : windupEffectPrefab;
        }

        public float GetWindupHoldDuration(int phase)
        {
            return Mathf.Max(0f, phase <= 1 ? phaseOneWindupHoldDuration
                : phase == 2 ? phaseTwoWindupHoldDuration
                : phaseThreeWindupHoldDuration);
        }

        [Header("受击与击飞表现")]
        public EnemyLaunchSettings launchSettings = new EnemyLaunchSettings();
        public MirrorBossGetUpProtectionSettings getUpProtection = new MirrorBossGetUpProtectionSettings();

        [Header("远程瞬移 Boss（关闭时保持原 Boss 行为）")]
        public bool enableRangedTeleportKit;

        [Header("弓箭")]
        [Min(0.05f)] public float bowDrawDuration = 0.8f;
        [Min(0.01f)] public float bowShotInterval = 0.35f;
        [Min(0f)] public float bowVolleyRecovery = 1.25f;
        [Min(0f)] public float bowReloadRecovery = 1.8f;
        [Min(1)] public int volleysBeforeReload = 2;
        [Min(1)] public int phaseOneArrowCount = 1;
        [Min(1)] public int phaseTwoArrowCount = 2;
        [Min(1)] public int phaseThreeArrowCount = 3;
        [Min(1)] public int bowDamage = 1;
        [Min(0.1f)] public float bowArrowSpeed = 12f;
        [Min(0.1f)] public float bowArrowRange = 18f;
        public Vector2 bowKnockback = new Vector2(1.2f, 0.25f);
        public Vector2 bowSpawnOffset = new Vector2(0.65f, 0.9f);

        [Header("瞬移")]
        [Min(0.1f)] public float teleportCooldown = 6f;
        [Min(0.05f)] public float teleportWindup = 0.38f;
        [Min(0.01f)] public float teleportHiddenDuration = 0.14f;
        [Min(0.05f)] public float teleportAppearRecovery = 0.3f;
        [Min(0.1f)] public float teleportFlankDistance = 2.1f;
        [Min(0.1f)] public float teleportArenaPadding = 1f;
        [Min(0.1f)] public float closeEscapeDistance = 2.2f;
        [Range(0f, 1f)] public float phaseOneTeleportAttackChance = 0.2f;
        [Range(0f, 1f)] public float phaseTwoTeleportAttackChance = 0.32f;
        [Range(0f, 1f)] public float phaseThreeTeleportAttackChance = 0.45f;

        [Header("召唤")]
        public GameObject minionPrefab;
        [Min(0.1f)] public float summonCooldown = 11f;
        [Min(0.05f)] public float summonWindup = 1.25f;
        [Min(0f)] public float summonRecovery = 0.45f;
        [Range(0f, 1f)] public float summonDecisionChance = 0.3f;
        [Min(0)] public int phaseOneMaxMinions = 1;
        [Min(0)] public int phaseTwoMaxMinions = 2;
        [Min(0)] public int phaseThreeMaxMinions = 2;
        [Min(0)] public int totalSummonLimit = 4;
        [Min(0.1f)] public float minionSpawnHorizontalOffset = 2.5f;

        [Header("近身破绽")]
        [Min(0f)] public float closeDefenseRecovery = 1f;

        [Header("Heavy Slash")]
        [Range(0f, 1f)] public float heavySlashChance = 0.25f;
        [Min(0f)] public float heavySlashCooldown = 5f;
        [Min(0f)] public float heavySlashRecovery = 1.1f;
        public MirrorBossComboStepV2 heavySlash = new MirrorBossComboStepV2
        {
            animationState = "ComboAttackD",
            activeStart = 0.36f,
            activeEnd = 0.6f,
            playbackSpeed = 0.85f,
            gapAfter = 0f,
            damage = 1,
            hitboxOffset = new Vector2(0.95f, 0f),
            hitboxSize = new Vector2(1.55f, 1.15f),
            knockback = new Vector2(7f, 3f),
            hitStop = 0.1f,
            interruptPower = 3,
            poiseDamage = 4f,
            playerHitReaction = HitReactionType.Knockdown,
            breaksSuperArmor = true,
            advanceSpeed = 0f,
            windupFrame = 2,
            windupHoldDuration = 0.8f,
            windupEffectOffset = new Vector2(0.7f, 0.9f),
            allowFeint = false
        };

        [Header("Phase 1: A-B")]
        public MirrorBossComboStepV2[] phaseOneCombo =
        {
            Step("ComboAttackA", 0.3f, 0.52f, 1f, 0.12f),
            Step("ComboAttackB", 0.31f, 0.54f, 0.9f, 0f)
        };

        [Header("Phase 2: A-B-delayed D")]
        public MirrorBossComboStepV2[] phaseTwoCombo =
        {
            Step("ComboAttackA", 0.3f, 0.52f, 1f, 0.1f),
            Step("ComboAttackB", 0.31f, 0.54f, 0.9f, 0.42f),
            Step("ComboAttackD", 0.36f, 0.6f, 0.82f, 0f)
        };

        [Header("Phase 3: A-B-C-delayed D")]
        public MirrorBossComboStepV2[] phaseThreeCombo =
        {
            Step("ComboAttackA", 0.3f, 0.52f, 1.05f, 0.08f),
            Step("ComboAttackB", 0.31f, 0.54f, 1f, 0.08f),
            Step("ComboAttackC", 0.33f, 0.56f, 0.95f, 0.35f),
            Step("ComboAttackD", 0.36f, 0.6f, 0.88f, 0f)
        };

        void OnValidate()
        {
            EnsureComboKeys(phaseOneCombo);
            EnsureComboKeys(phaseTwoCombo);
            EnsureComboKeys(phaseThreeCombo);
            heavySlash?.EnsureHitboxKeys();
        }

        static void EnsureComboKeys(MirrorBossComboStepV2[] combo)
        {
            if (combo == null) return;
            foreach (var step in combo) step?.EnsureHitboxKeys();
        }
        static MirrorBossComboStepV2 Step(string state, float start, float end, float speed, float gap)
        {
            return new MirrorBossComboStepV2
            {
                animationState = state,
                activeStart = start,
                activeEnd = end,
                playbackSpeed = speed,
                gapAfter = gap
            };
        }
    }
}
