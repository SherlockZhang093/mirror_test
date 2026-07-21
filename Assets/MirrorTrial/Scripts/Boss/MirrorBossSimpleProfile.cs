using System;
using System.Collections.Generic;
using MirrorTrial.Combat;
using MirrorTrial.Player;
using UnityEngine;

namespace MirrorTrial.Boss
{
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
