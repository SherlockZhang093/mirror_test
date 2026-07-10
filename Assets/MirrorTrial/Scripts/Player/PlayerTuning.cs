using System;
using UnityEngine;

namespace MirrorTrial.Player
{
    public class PlayerTuning : MonoBehaviour
    {
        public MovementTuning movement = new MovementTuning();
        public CombatTuning combat = new CombatTuning();
        public HurtTuning hurt = new HurtTuning();
        public AbilityTuning abilities = new AbilityTuning();

        [Serializable]
        public class MovementTuning
        {
            public float moveSpeed = 5f;
            public float acceleration = 30f;
            public float deceleration = 20f;
            public float airAcceleration = 30f;
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
            public float invincibleTime = 0.75f;
            public Vector2 knockback = new Vector2(4f, 2.5f);
            public float hurtHitStop = 0.05f;
        }

        [Serializable]
        public class AbilityTuning
        {
            public bool mirrorBladeUnlocked;
            public int mirrorBladeDamage = 15;
            public float mirrorBladeStartup = 0.15f;
            public float mirrorBladeRecovery = 0.20f;
            public float mirrorBladeCooldown = 1.50f;
            public float mirrorBladeSpeed = 12f;
            public float mirrorBladeRange = 10f;
            public Vector2 mirrorBladeKnockback = new Vector2(2.5f, 0.5f);
            public float mirrorBladeHitStop = 0.04f;

            public bool echoDashUnlocked;
            public float echoDashDistance = 5f;
            public float echoDashDuration = 0.18f;
            public float echoDashInvincibleTime = 0.15f;
            public float echoDashCooldown = 3f;
        }
    }
}
