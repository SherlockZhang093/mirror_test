using System;
using MirrorTrial.Combat;
using UnityEngine;

namespace MirrorTrial.Boss
{
    public enum MirrorBossMove
    {
        MirrorDouble,
        BrokenTriple,
        HeavySlash,
        PursuitSlash
    }

    [Serializable]
    public sealed class MirrorBossAttack
    {
        public MirrorBossMove move;
        public string animationState = "ComboAttackA";
        [Min(0f)] public float tell = 0.45f;
        [Min(0.01f)] public float activeTime = 0.12f;
        [Min(0f)] public float recovery = 0.9f;
        [Min(1)] public int damage = 2;
        public Vector2 hitboxOffset = new Vector2(0.8f, 0.05f);
        public Vector2 hitboxSize = new Vector2(1.3f, 0.9f);
        public Vector2 knockback = new Vector2(5f, 2f);
        [Range(0f, 0.2f)] public float hitStop = 0.06f;
        [Min(0)] public int interruptPower = 1;
        [Min(0f)] public float poiseDamage = 1f;
        public HitReactionType playerHitReaction = HitReactionType.LightHurt;
        public bool breaksSuperArmor;
        [Min(0f)] public float advanceSpeed;
        [Min(0f)] public float advanceDuration;
    }

    [CreateAssetMenu(menuName = "Mirror Trial/Boss/Mirror Boss Profile", fileName = "MirrorBossProfile")]
    public sealed class MirrorBossProfile : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "镜中行刑者";
        [Min(1)] public int maxHitPoints = 30;

        [Header("Movement")]
        [Min(0f)] public float moveSpeed = 2.8f;
        [Min(0f)] public float acceleration = 18f;
        [Min(0f)] public float stopDistance = 1.65f;
        [Min(0f)] public float pursuitDistance = 4.5f;
        public Vector2 arenaXLimits = new Vector2(-12f, 12f);

        [Header("Cadence")]
        public Vector2 duelPause = new Vector2(0.7f, 1.25f);
        [Range(0.1f, 1f)] public float phaseThreePauseMultiplier = 0.8f;
        [Range(0.1f, 1f)] public float phaseThreeRecoveryMultiplier = 0.9f;

        [Header("Phase thresholds")]
        [Range(0f, 1f)] public float phaseTwoAt = 0.7f;
        [Range(0f, 1f)] public float phaseThreeAt = 0.35f;
        [Min(0f)] public float phaseTransitionDuration = 1.6f;

        [Header("Attacks")]
        public MirrorBossAttack doubleFirst = new MirrorBossAttack
        {
            move = MirrorBossMove.MirrorDouble, animationState = "ComboAttackA", tell = 0.42f,
            activeTime = 0.12f, recovery = 0.18f, damage = 2
        };
        public MirrorBossAttack doubleSecond = new MirrorBossAttack
        {
            move = MirrorBossMove.MirrorDouble, animationState = "ComboAttackB", tell = 0.16f,
            activeTime = 0.13f, recovery = 0.95f, damage = 3
        };
        public MirrorBossAttack tripleFirst = new MirrorBossAttack
        {
            move = MirrorBossMove.BrokenTriple, animationState = "ComboAttackA", tell = 0.48f,
            activeTime = 0.11f, recovery = 0.12f, damage = 2
        };
        public MirrorBossAttack tripleSecond = new MirrorBossAttack
        {
            move = MirrorBossMove.BrokenTriple, animationState = "ComboAttackB", tell = 0.12f,
            activeTime = 0.11f, recovery = 0.1f, damage = 2
        };
        public MirrorBossAttack tripleFinisher = new MirrorBossAttack
        {
            move = MirrorBossMove.BrokenTriple, animationState = "ComboAttackD", tell = 0.52f,
            activeTime = 0.14f, recovery = 1.25f, damage = 4,
            hitboxOffset = new Vector2(1.05f, 0.05f), hitboxSize = new Vector2(1.7f, 0.75f),
            interruptPower = 2, poiseDamage = 2f, playerHitReaction = HitReactionType.HeavyHurt
        };
        public MirrorBossAttack heavySlash = new MirrorBossAttack
        {
            move = MirrorBossMove.HeavySlash, animationState = "ComboAttackD", tell = 1.05f,
            activeTime = 0.16f, recovery = 1.55f, damage = 5,
            hitboxOffset = new Vector2(0.95f, 0f), hitboxSize = new Vector2(1.55f, 1.15f),
            knockback = new Vector2(7f, 3f), hitStop = 0.1f,
            interruptPower = 3, poiseDamage = 4f,
            playerHitReaction = HitReactionType.Knockdown, breaksSuperArmor = true
        };
        public MirrorBossAttack pursuitSlash = new MirrorBossAttack
        {
            move = MirrorBossMove.PursuitSlash, animationState = "SwordRunSlash", tell = 0.55f,
            activeTime = 0.13f, recovery = 0.8f, damage = 3,
            hitboxOffset = new Vector2(0.9f, 0f), hitboxSize = new Vector2(1.4f, 0.85f),
            advanceSpeed = 7f, advanceDuration = 0.28f
        };
    }
}
