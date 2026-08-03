using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace MirrorTrial.Player
{
    public class PlayerTuning : MonoBehaviour
    {
        public MovementTuning movement = new MovementTuning();
        public CombatTuning combat = new CombatTuning();
        public HurtTuning hurt = new HurtTuning();
        public DodgeTuning dodge = new DodgeTuning();
        public AbilityTuning abilities = new AbilityTuning();

        [Serializable]
        public class MovementTuning
        {
            public float moveSpeed = 5f;
            public float acceleration = 30f;
            public float deceleration = 20f;
            public float airAcceleration = 30f;
            public float airDeceleration = 3f;
            public float jumpSpeed = 7.5f;
            public float baseGravityModifier = 1f;
            public float fallGravityMultiplier = 1.15f;
            [Range(0f, 1f)] public float jumpCutMultiplier = 0.55f;
            public float coyoteTime = 0.08f;
            public float jumpBufferTime = 0.10f;
        }

        [Serializable]
        public class CombatTuning
        {
            public int attackDamage = 8;
            public float attackStartup = 0.08f;
            public float attackActiveTime = 0.10f;
            public float attackRecovery = 0.20f;
            public float attackMoveLock = 0.18f;
            public Vector2 attackKnockback = new Vector2(3f, 1f);
            public float hitStop = 0.04f;
        }

        [Serializable]
        public class HurtTuning
        {
            public float hurtLockTime = 0.25f;
            public float heavyHurtDurationMultiplier = 1.35f;
            public float launchHurtTime = 0.30f;
            public float stunnedTime = 0.70f;
            public float shockLightTime = 0.333f;
            public float shockHeavyTime = 0.45f;
            public float knockdownAnimationTime = 0.60f;
            public float knockdownGroundTime = 0.20f;
            public float getUpTime = 0.70f;
            public float getUpProtectionTime = 0.20f;
            public float knockbackDuration = 0.12f;
            public float invincibleTime = 0.75f;
            public float hurtHitStop = 0.05f;
            [FormerlySerializedAs("hurtFlashTime")]
            public float hurtTintTime = 0.12f;
            public Color hurtTintColor = new Color(1f, 0.18f, 0.18f, 1f);
            public float invincibleBlinkInterval = 0.08f;
        }

        [Serializable]
        public class DodgeTuning
        {
            [Min(0.1f)] public float distance = 3.2f;
            [Min(0.05f)] public float duration = 0.22f;
            [Min(0f)] public float invincibleStart = 0.03f;
            [Min(0f)] public float invincibleDuration = 0.14f;
            [Min(0f)] public float cooldown = 0.28f;
            [Min(0f)] public float inputBufferTime = 0.10f;
            public bool allowAirDodge;
        }

        [Serializable]
        public class AbilityTuning
        {
            public bool mirrorBladeUnlocked = true;
            public int mirrorBladeDamage = 15;
            public float mirrorBladeStartup = 0.15f;
            public float mirrorBladeRecovery = 0.20f;
            public float mirrorBladeCooldown = 1.50f;
            public float mirrorBladeSpeed = 12f;
            public float mirrorBladeRange = 10f;
            public Vector2 mirrorBladeKnockback = new Vector2(2.5f, 0.5f);
            public float mirrorBladeHitStop = 0.04f;

            public bool echoDashUnlocked = true;
            public float echoDashDistance = 5f;
            public float echoDashDuration = 0.18f;
            public float echoDashInvincibleTime = 0.15f;
            public float echoDashCooldown = 3f;

            public float bowMinChargeTime = 0.12f;
            public float bowMaxChargeTime = 0.8f;
            public float bowRecovery = 0.18f;
            public int bowMinDamage = 6;
            public int bowMaxDamage = 18;
            public float bowMinSpeed = 10f;
            public float bowMaxSpeed = 20f;
            public float bowRange = 12f;
            public Vector2 bowKnockback = new Vector2(2.5f, 0.5f);
            public float bowHitStop = 0.04f;
        }
    }
}
