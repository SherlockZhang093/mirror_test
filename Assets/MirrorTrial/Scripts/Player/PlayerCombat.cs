using System;
using System.Collections;
using System.Collections.Generic;
using MirrorTrial.Combat;
using UnityEngine;

namespace MirrorTrial.Player
{
    public enum PlayerMoveCategory
    {
        Basic,
        Sword,
        Punch,
        Kick,
        Bow,
        Skill
    }

    public enum PlayerComboCategory
    {
        Opener,
        Chain,
        Finisher,
        Launcher,
        Guard,
        Special
    }

    public enum AttackHitboxInterpolation
    {
        Step,
        Linear
    }

    [Serializable]
    public sealed class PlayerAttackHitboxKey
    {
        public int frame;
        public bool enabled = true;
        public Vector2 offset = new Vector2(0.65f, 0f);
        public Vector2 size = new Vector2(1f, 0.8f);
        public AttackHitboxInterpolation interpolation = AttackHitboxInterpolation.Step;
    }

    [Serializable]
    public sealed class PlayerComboStep
    {
        public string name = "Attack";
        public PlayerMoveCategory moveCategory = PlayerMoveCategory.Basic;
        public PlayerComboCategory comboCategory = PlayerComboCategory.Chain;
        public PlayerInputCommand input = PlayerInputCommand.PrimaryAttack;
        public AnimationClip animationClip;
        [HideInInspector] public PlayerActionState animationState = PlayerActionState.Attack;
        public int animationFrameRate = 12;
        public int animationFrameCount = 8;
        public bool mirrorHitboxByFacing = true;
        public List<PlayerAttackHitboxKey> hitboxKeys = new List<PlayerAttackHitboxKey>();
        public SkillAttackType attackType = SkillAttackType.Normal;
        public HitFlashType hitFlashType = HitFlashType.White;
        public bool enableTargetReaction = true;
        public HitReactionType targetReaction = HitReactionType.LightHurt;
        public bool useCustomKnockback;
        public Vector2 customKnockback = new Vector2(4f, 1f);
        [Min(0)] public int interruptPower = 1;
        [Min(0f)] public float poiseDamage = 1f;
        public bool breaksSuperArmor;
        public SkillHitFeedbackSettings hitFeedback = new SkillHitFeedbackSettings();

        public float startup = 0.08f;
        public float activeTime = 0.08f;
        public float recovery = 0.16f;
        public float comboWindowStart = 0.10f;
        public float comboWindowEnd = 0.28f;
        public float damageMultiplier = 1f;
        public float knockbackMultiplier = 1f;
        public bool lockMovement = true;
        public PlayerBodyState bodyState = PlayerBodyState.Normal;
        public float bodyStateStart;
        public float bodyStateEnd;
    }

    [Serializable]
    public sealed class PlayerComboSet
    {
        public string name = "Default Combo";
        public PlayerWeaponType weaponType = PlayerWeaponType.Sword;
        public List<PlayerComboStep> steps = new List<PlayerComboStep>();
    }


    [Serializable]
    public sealed class PlayerAttackReactionSettings
    {
        public bool enabled = true;
        [Min(0f)] public float normalRecoilSpeed = 0.35f;
        [Min(0f)] public float normalRecoilDuration = 0.025f;
        [Min(0f)] public float heavyRecoilSpeed = 1.1f;
        [Min(0f)] public float heavyRecoilDuration = 0.055f;

        public void Resolve(SkillAttackType attackType, out float speed, out float duration)
        {
            speed = attackType == SkillAttackType.Heavy ? heavyRecoilSpeed : normalRecoilSpeed;
            duration = attackType == SkillAttackType.Heavy ? heavyRecoilDuration : normalRecoilDuration;
        }
    }
    [RequireComponent(typeof(PlayerInputReader), typeof(PlayerTuning), typeof(PlayerMotor))]
    [RequireComponent(typeof(PlayerAnimationDriver))]
    public partial class PlayerCombat : MonoBehaviour, IInterruptiblePlayerAction
    {
        [SerializeField] Hitbox attackHitbox;
        [SerializeField] List<PlayerComboSet> comboSets = new List<PlayerComboSet>();
        [SerializeField] int activeComboSetIndex;
        [SerializeField, HideInInspector] bool punchComboCreated;
        [SerializeField, HideInInspector] List<PlayerComboStep> combo = CreateDefaultCombo();
        [SerializeField, HideInInspector] int hitFeedbackSchemaVersion;
        [SerializeField] AnimationClip swordGuardClip;
        [SerializeField] AnimationClip swordGuardImpactClip;
        [SerializeField] PlayerAttackReactionSettings attackReaction = new PlayerAttackReactionSettings();

        PlayerInputReader input;
        PlayerTuning tuning;
        PlayerMotor motor;
        PlayerAnimationDriver animationDriver;
        PlayerWeaponController weapons;
        PlayerBodyStateController bodyStateController;
        PlayerStateMachine stateMachine;
        PlayerDamageReceiver damageReceiver;

        Coroutine attackRoutine;
        Hitbox activeHitbox;
        bool queuedNextComboStep;
        bool acceptingComboHitConfirm;
        bool currentComboMoveHitConfirmed;
        bool dodgeCancelAvailable;
        bool guarding;
        Coroutine guardImpactRoutine;

        public bool IsAttacking { get { return attackRoutine != null || guarding; } }
        public bool CanDodgeCancel => attackRoutine != null && dodgeCancelAvailable;
        public bool IsGuarding { get { return guarding; } }
        public event Action<PlayerMoveCategory, SkillAttackType> AttackActivated;
        public event Action Blocked;
        public int ActiveComboSetIndex { get { return activeComboSetIndex; } set { activeComboSetIndex = Mathf.Clamp(value, 0, Mathf.Max(0, comboSets.Count - 1)); } }
        public IList<PlayerComboSet> ComboSets { get { return comboSets; } }
        public IList<PlayerComboStep> Combo { get { return ActiveCombo; } }
        List<PlayerComboStep> ActiveCombo
        {
            get
            {
                EnsureCombo();
                if (!weapons) weapons = GetComponent<PlayerWeaponController>();
                var weapon = weapons ? weapons.CurrentWeapon : PlayerWeaponType.Unarmed;
                for (var i = 0; i < comboSets.Count; i++)
                    if (comboSets[i] != null && comboSets[i].weaponType == weapon)
                        return comboSets[i].steps;
                return comboSets[Mathf.Clamp(activeComboSetIndex, 0, comboSets.Count - 1)].steps;
            }
        }

        void OnValidate()
        {
            EnsureCombo();
            EnsureMoveComboData();
        }

        public void EnsureComboData()
        {
            EnsureCombo();
            EnsureMoveComboData();
        }

        void Awake()
        {
            input = GetComponent<PlayerInputReader>();
            tuning = GetComponent<PlayerTuning>();
            motor = GetComponent<PlayerMotor>();
            animationDriver = GetComponent<PlayerAnimationDriver>();
            weapons = GetComponent<PlayerWeaponController>();
            bodyStateController = GetComponent<PlayerBodyStateController>();
            stateMachine = GetComponent<PlayerStateMachine>();
            damageReceiver = GetComponent<PlayerDamageReceiver>();
            EnsureCombo();
            EnsureMoveComboData();

            if (attackHitbox)
                attackHitbox.SetActive(false);
        }

        void Update()
        {
            if (!weapons) weapons = GetComponent<PlayerWeaponController>();
            var weapon = weapons ? weapons.CurrentWeapon : PlayerWeaponType.Unarmed;
            if (weapon == PlayerWeaponType.Bow || weapon == PlayerWeaponType.Reserved)
                return;

            if (weapon == PlayerWeaponType.Sword)
            {
                if (!guarding && attackRoutine == null && input.IsHeld(PlayerInputCommand.SecondaryAttack))
                    BeginGuard();
                else if (guarding && !input.IsHeld(PlayerInputCommand.SecondaryAttack))
                    EndGuard();
                if (guarding)
                    return;
            }

            var activeCombo = ActiveCombo;
            if (HasGraphForWeapon(weapon))
            {
                UpdateGraphCombat(weapon);
                return;
            }
            if (activeCombo.Count == 0)
                return;
            if (weapon == PlayerWeaponType.Sword && input.WasPressed(PlayerInputCommand.PrimaryAttack) && attackRoutine == null)
            {
                CancelHurtInvincibilityForPrimaryAttack(PlayerInputCommand.PrimaryAttack);
                attackRoutine = StartCoroutine(ComboRoutine());
                return;
            }
            if (weapon == PlayerWeaponType.Unarmed && input.WasPressed(PlayerInputCommand.SecondaryAttack) && attackRoutine == null)
            {
                attackRoutine = StartCoroutine(SingleStepRoutine(activeCombo[activeCombo.Count - 1]));
                return;
            }
            if (input.WasPressed(activeCombo[0].input) && attackRoutine == null)
            {
                CancelHurtInvincibilityForPrimaryAttack(activeCombo[0].input);
                attackRoutine = StartCoroutine(ComboRoutine());
            }
        }

        void CancelHurtInvincibilityForPrimaryAttack(PlayerInputCommand attackInput)
        {
            if (attackInput == PlayerInputCommand.PrimaryAttack && damageReceiver)
                damageReceiver.CancelHurtInvincibilityForAttack();
        }

        void BeginGuard()
        {
            guarding = true;
            motor.MovementLocked = true;
            animationDriver.ForceState(PlayerActionState.Attack);
            if (swordGuardClip) animationDriver.PlayActionClip(swordGuardClip, swordGuardClip.length);
            else animationDriver.ForceState(PlayerActionState.SwordGuard);
        }

        void EndGuard()
        {
            guarding = false;
            motor.MovementLocked = false;
            animationDriver.StopActionClip();
            animationDriver.ClearForcedState(PlayerActionState.Attack);
            animationDriver.ClearForcedState(PlayerActionState.SwordGuard);
            animationDriver.ClearForcedState(PlayerActionState.SwordGuardImpact);
        }

        public bool TryBlockIncomingHit()
        {
            if (!guarding)
                return false;
            if (guardImpactRoutine != null)
                StopCoroutine(guardImpactRoutine);
            guardImpactRoutine = StartCoroutine(GuardImpactRoutine());
            Blocked?.Invoke();
            return true;
        }

        IEnumerator GuardImpactRoutine()
        {
            if (swordGuardImpactClip) animationDriver.PlayActionClip(swordGuardImpactClip, 0.12f);
            else animationDriver.ForceState(PlayerActionState.SwordGuardImpact);
            yield return new WaitForSeconds(0.12f);
            animationDriver.StopActionClip();
            animationDriver.ClearForcedState(PlayerActionState.SwordGuardImpact);
            if (guarding)
            {
                if (swordGuardClip) animationDriver.PlayActionClip(swordGuardClip, swordGuardClip.length);
                else animationDriver.ForceState(PlayerActionState.SwordGuard);
            }
            guardImpactRoutine = null;
        }

        IEnumerator SingleStepRoutine(PlayerComboStep step)
        {
            yield return ComboStepRoutine(step, step.input);
            DeactivateActiveHitbox();
            motor.MovementLocked = false;
            attackRoutine = null;
        }

        IEnumerator ComboRoutine()
        {
            var activeCombo = ActiveCombo;
            var stepIndex = 0;
            while (stepIndex < activeCombo.Count)
            {
                queuedNextComboStep = false;
                var nextInput = stepIndex < activeCombo.Count - 1 ? activeCombo[stepIndex + 1].input : activeCombo[stepIndex].input;
                yield return ComboStepRoutine(activeCombo[stepIndex], nextInput);

                if (!queuedNextComboStep || stepIndex >= activeCombo.Count - 1)
                    break;

                stepIndex++;
            }

            DeactivateActiveHitbox();
            motor.MovementLocked = false;
            attackRoutine = null;
        }

        IEnumerator ComboStepRoutine(PlayerComboStep step, PlayerInputCommand nextInput)
        {
            var combat = tuning.combat;
            var elapsed = 0f;
            var totalDuration = Mathf.Max(0f, step.startup) + Mathf.Max(0f, step.activeTime) + Mathf.Max(0f, step.recovery);
            var attackCuePlayed = false;
            dodgeCancelAvailable = false;

            if (step.lockMovement)
                motor.MovementLocked = true;
            animationDriver.ForceState(PlayerActionState.Attack);
            if (step.animationClip)
                animationDriver.PlayActionClip(step.animationClip, totalDuration);
            else
                animationDriver.ForceState(step.animationState);

            while (elapsed < totalDuration)
            {
                if (!attackCuePlayed && elapsed >= Mathf.Max(0f, step.startup))
                {
                    attackCuePlayed = true;
                    AttackActivated?.Invoke(step.moveCategory, step.attackType);
                }

                UpdateBodyState(step, elapsed);
                ApplyHitboxFrame(step, combat, elapsed);
                dodgeCancelAvailable = elapsed >= Mathf.Max(0f, step.startup) + Mathf.Max(0f, step.activeTime);

                if (elapsed >= step.comboWindowStart && elapsed <= step.comboWindowEnd && input.WasPressed(nextInput))
                    queuedNextComboStep = true;

                elapsed += Time.deltaTime;
                yield return null;
            }

            dodgeCancelAvailable = false;
            DeactivateActiveHitbox();
            if (bodyStateController)
                bodyStateController.ClearBodyState(this);
            if (step.animationClip)
            {
                animationDriver.StopActionClip();
                animationDriver.ClearForcedState(PlayerActionState.Attack);
            }
            else
                animationDriver.ClearForcedState(step.animationState);
        }

        void ApplyHitboxFrame(PlayerComboStep step, PlayerTuning.CombatTuning combat, float elapsed)
        {
            if (!attackHitbox)
                return;

            PlayerAttackHitboxKey key;
            var frame = Mathf.FloorToInt(Mathf.Max(0f, elapsed) * Mathf.Max(1, step.animationFrameRate));
            if (!TryEvaluateHitboxKey(step, frame, out key) || !key.enabled)
            {
                DeactivateActiveHitbox();
                return;
            }

            var direction = motor.FacingRight ? Vector2.right : Vector2.left;
            ApplyHitboxShape(attackHitbox, key.offset, key.size, step.mirrorHitboxByFacing, direction);

            var damage = Mathf.RoundToInt(combat.attackDamage * Mathf.Max(0f, step.damageMultiplier));
            var reaction = step.enableTargetReaction ? step.targetReaction : HitReactionType.None;
            var knockback = Vector2.zero;
            if (step.enableTargetReaction)
            {
                knockback = step.useCustomKnockback
                    ? step.customKnockback
                    : combat.attackKnockback * Mathf.Max(0f, step.knockbackMultiplier);
                knockback.x = Mathf.Abs(knockback.x) * direction.x;
            }

            var feedback = step.hitFeedback != null
                ? step.hitFeedback.BuildRequest(step.attackType)
                : default(HitFeedbackRequest);
            attackHitbox.Configure(new DamagePayload(gameObject, damage, knockback, direction,
                feedback.requestHitStop ? feedback.hitStopDuration : 0f,
                step.enableTargetReaction ? step.interruptPower : 0,
                step.enableTargetReaction ? step.poiseDamage : 0f,
                reaction, step.enableTargetReaction && step.breaksSuperArmor,
                step.attackType, step.hitFlashType, feedback));
            activeHitbox = attackHitbox;
            activeHitbox.SetActive(true);
        }

        void OnAttackHitConfirmed(DamagePayload payload)
        {
            if (!IsAttacking || !motor || payload.source != gameObject || attackReaction == null || !attackReaction.enabled)
                return;
            float speed;
            float duration;
            attackReaction.Resolve(payload.attackType, out speed, out duration);
            motor.ApplyForcedVelocity(-payload.direction * speed, duration);
        }

        void OnAttackConnected(GameObject target)
        {
            if (acceptingComboHitConfirm && target)
                currentComboMoveHitConfirmed = true;
        }

        void UpdateBodyState(PlayerComboStep step, float elapsed)
        {
            if (!bodyStateController)
                return;

            var active = step.bodyState != PlayerBodyState.Normal
                && step.bodyStateEnd > step.bodyStateStart
                && elapsed >= step.bodyStateStart
                && elapsed < step.bodyStateEnd;
            if (active)
                bodyStateController.SetBodyState(this, step.bodyState);
            else
                bodyStateController.ClearBodyState(this);
        }

        static bool TryEvaluateHitboxKey(PlayerComboStep step, int frame, out PlayerAttackHitboxKey result)
        {
            result = null;
            if (step.hitboxKeys == null || step.hitboxKeys.Count == 0)
                return false;

            PlayerAttackHitboxKey previous = null;
            PlayerAttackHitboxKey next = null;
            for (var i = 0; i < step.hitboxKeys.Count; i++)
            {
                var key = step.hitboxKeys[i];
                if (key == null)
                    continue;
                if (key.frame <= frame && (previous == null || key.frame >= previous.frame))
                    previous = key;
                if (key.frame > frame && (next == null || key.frame < next.frame))
                    next = key;
            }

            if (previous == null)
                return false;

            result = previous;
            if (next == null || previous.interpolation != AttackHitboxInterpolation.Linear || !previous.enabled || !next.enabled || next.frame <= previous.frame)
                return true;

            var t = Mathf.InverseLerp(previous.frame, next.frame, frame);
            result = new PlayerAttackHitboxKey
            {
                frame = frame,
                enabled = true,
                offset = Vector2.Lerp(previous.offset, next.offset, t),
                size = Vector2.Lerp(previous.size, next.size, t),
                interpolation = previous.interpolation
            };
            return true;
        }

        void ApplyHitboxShape(Hitbox hitbox, Vector2 offset, Vector2 size, bool mirrorByFacing, Vector2 direction)
        {
            var hitboxTransform = hitbox.transform;
            var localPosition = (Vector3)offset;
            if (mirrorByFacing)
                localPosition.x = Mathf.Abs(localPosition.x) * direction.x;
            hitboxTransform.localPosition = localPosition;

            size = new Vector2(Mathf.Max(0.01f, size.x), Mathf.Max(0.01f, size.y));
            var box = hitbox.GetComponent<BoxCollider2D>();
            if (box)
            {
                box.size = size;
                box.offset = Vector2.zero;
                return;
            }

            var capsule = hitbox.GetComponent<CapsuleCollider2D>();
            if (capsule)
            {
                capsule.size = size;
                capsule.offset = Vector2.zero;
                return;
            }

            var circle = hitbox.GetComponent<CircleCollider2D>();
            if (circle)
            {
                circle.radius = Mathf.Max(size.x, size.y) * 0.5f;
                circle.offset = Vector2.zero;
            }
        }

        void DeactivateActiveHitbox()
        {
            if (activeHitbox)
                activeHitbox.SetActive(false);
            activeHitbox = null;
            if (attackHitbox)
                attackHitbox.SetActive(false);
        }

        public bool TryCancelForDodge()
        {
            if (!CanDodgeCancel)
                return false;
            CancelCurrentAction(PlayerActionCancelReason.Dodge);
            return true;
        }

        public void CancelCurrentAction(PlayerActionCancelReason reason)
        {
            actionVersion++;
            dodgeCancelAvailable = false;
            runtimeCurrentDecisionId = string.Empty;
            runtimeDecisionProgress = 0f;
            CancelChargePresentation(reason == PlayerActionCancelReason.Hit
                ? ChargeTelegraphEndReason.Interrupted
                : ChargeTelegraphEndReason.Cancelled);
            if (attackRoutine != null)
            {
                StopCoroutine(attackRoutine);
                attackRoutine = null;
            }
            if (guardImpactRoutine != null)
            {
                StopCoroutine(guardImpactRoutine);
                guardImpactRoutine = null;
            }

            queuedNextComboStep = false;
            acceptingComboHitConfirm = false;
            currentComboMoveHitConfirmed = false;
            guarding = false;
            if (bodyStateController)
                bodyStateController.ClearBodyState(this);
            DeactivateActiveHitbox();
            motor.MovementLocked = false;
            animationDriver.StopActionClip();
            animationDriver.ClearForcedState(PlayerActionState.Attack);
            animationDriver.ClearForcedState(PlayerActionState.SwordGuard);
            animationDriver.ClearForcedState(PlayerActionState.SwordGuardImpact);
        }

        void OnDisable()
        {
            CancelCurrentAction(PlayerActionCancelReason.Hit);
        }


        void EnsureCombo()
        {
            if (comboSets == null)
                comboSets = new List<PlayerComboSet>();
            if (comboSets.Count == 0)
            {
                var migratedSteps = combo != null && combo.Count > 0 ? combo : CreateDefaultCombo();
                comboSets.Add(new PlayerComboSet { name = "\u5251\u672f\u56db\u8fde", weaponType = PlayerWeaponType.Sword, steps = migratedSteps });
                combo = new List<PlayerComboStep>();
            }
            if (!punchComboCreated)
            {
                comboSets.Add(CreatePunchComboSet());
                punchComboCreated = true;
            }
            activeComboSetIndex = Mathf.Clamp(activeComboSetIndex, 0, comboSets.Count - 1);
            for (var setIndex = 0; setIndex < comboSets.Count; setIndex++)
            {
                var set = comboSets[setIndex];
                if (set == null)
                {
                    set = new PlayerComboSet { name = "Combo " + (setIndex + 1) };
                    comboSets[setIndex] = set;
                }
                if (set.steps == null)
                    set.steps = new List<PlayerComboStep>();
                for (var i = 0; i < set.steps.Count; i++)
                {
                    EnsureHitboxKeys(set.steps[i]);
                    if (set.steps[i].hitFeedback == null)
                        set.steps[i].hitFeedback = new SkillHitFeedbackSettings();
                }
            }
            MigrateHitFeedbackDefaults();
        }

        void MigrateHitFeedbackDefaults()
        {
            if (hitFeedbackSchemaVersion >= 1) return;
            foreach (var set in comboSets)
            {
                if (set == null || set.steps == null) continue;
                foreach (var step in set.steps)
                {
                    if (step == null || step.comboCategory != PlayerComboCategory.Finisher) continue;
                    step.attackType = SkillAttackType.Heavy;
                    step.enableTargetReaction = true;
                    step.targetReaction = HitReactionType.Launch;
                    step.useCustomKnockback = true;
                    step.customKnockback = set.weaponType == PlayerWeaponType.Unarmed
                        ? new Vector2(5.5f, 3.2f)
                        : new Vector2(6f, 3.4f);
                }
            }
            hitFeedbackSchemaVersion = 1;
        }
        static void EnsureHitboxKeys(PlayerComboStep step)
        {
            if (step.hitboxKeys == null)
                step.hitboxKeys = new List<PlayerAttackHitboxKey>();
            if (step.hitboxKeys.Count == 0)
            {
                step.hitboxKeys.Add(new PlayerAttackHitboxKey { frame = 0, enabled = false });
                step.hitboxKeys.Add(new PlayerAttackHitboxKey { frame = Mathf.Max(1, Mathf.RoundToInt(step.startup * Mathf.Max(1, step.animationFrameRate))), enabled = true });
                step.hitboxKeys.Add(new PlayerAttackHitboxKey { frame = Mathf.Max(2, Mathf.RoundToInt((step.startup + step.activeTime) * Mathf.Max(1, step.animationFrameRate))), enabled = false });
            }
            step.hitboxKeys.Sort((a, b) => a.frame.CompareTo(b.frame));
        }

        static PlayerComboSet CreatePunchComboSet()
        {
            return new PlayerComboSet
            {
                name = "\u62f3\u51fb\u4e09\u8fde",
                weaponType = PlayerWeaponType.Unarmed,
                steps = new List<PlayerComboStep>
                {
                    new PlayerComboStep
                    {
                        name = "\u76f4\u62f3\u8d77\u624b",
                        moveCategory = PlayerMoveCategory.Punch,
                        comboCategory = PlayerComboCategory.Opener,
                        input = PlayerInputCommand.PrimaryAttack,
                        animationState = PlayerActionState.PunchA,
                        startup = 0.05f, activeTime = 0.08f, recovery = 0.12f,
                        comboWindowStart = 0.10f, comboWindowEnd = 0.22f,
                        damageMultiplier = 0.9f, knockbackMultiplier = 0.75f,
                        hitboxKeys = new List<PlayerAttackHitboxKey>
                        {
                            new PlayerAttackHitboxKey { frame = 0, enabled = false },
                            new PlayerAttackHitboxKey { frame = 1, enabled = true, offset = new Vector2(0.52f, 0.08f), size = new Vector2(0.72f, 0.52f), interpolation = AttackHitboxInterpolation.Linear },
                            new PlayerAttackHitboxKey { frame = 3, enabled = false }
                        }
                    },
                    new PlayerComboStep
                    {
                        name = "\u6446\u62f3\u8854\u63a5",
                        moveCategory = PlayerMoveCategory.Punch,
                        comboCategory = PlayerComboCategory.Chain,
                        input = PlayerInputCommand.PrimaryAttack,
                        animationState = PlayerActionState.PunchB,
                        startup = 0.06f, activeTime = 0.08f, recovery = 0.11f,
                        comboWindowStart = 0.10f, comboWindowEnd = 0.22f,
                        damageMultiplier = 1f, knockbackMultiplier = 0.9f,
                        hitboxKeys = new List<PlayerAttackHitboxKey>
                        {
                            new PlayerAttackHitboxKey { frame = 0, enabled = false },
                            new PlayerAttackHitboxKey { frame = 1, enabled = true, offset = new Vector2(0.58f, 0.1f), size = new Vector2(0.82f, 0.58f), interpolation = AttackHitboxInterpolation.Linear },
                            new PlayerAttackHitboxKey { frame = 3, enabled = false }
                        }
                    },
                    new PlayerComboStep
                    {
                        name = "\u91cd\u62f3\u7ec8\u7ed3",
                        moveCategory = PlayerMoveCategory.Punch,
                        comboCategory = PlayerComboCategory.Finisher,
                        attackType = SkillAttackType.Heavy,
                        targetReaction = HitReactionType.Launch,
                        useCustomKnockback = true,
                        customKnockback = new Vector2(5.5f, 3.2f),
                        input = PlayerInputCommand.PrimaryAttack,
                        animationState = PlayerActionState.PunchC,
                        startup = 0.08f, activeTime = 0.1f, recovery = 0.18f,
                        comboWindowStart = 0.12f, comboWindowEnd = 0.24f,
                        damageMultiplier = 1.3f, knockbackMultiplier = 1.35f,
                        hitboxKeys = new List<PlayerAttackHitboxKey>
                        {
                            new PlayerAttackHitboxKey { frame = 0, enabled = false },
                            new PlayerAttackHitboxKey { frame = 2, enabled = true, offset = new Vector2(0.64f, 0.06f), size = new Vector2(0.95f, 0.64f), interpolation = AttackHitboxInterpolation.Linear },
                            new PlayerAttackHitboxKey { frame = 4, enabled = false }
                        }
                    }
                }
            };
        }

        static List<PlayerComboStep> CreateDefaultCombo()
        {
            return new List<PlayerComboStep>
            {
                new PlayerComboStep { name = "Combo A", moveCategory = PlayerMoveCategory.Sword, comboCategory = PlayerComboCategory.Opener, animationState = PlayerActionState.ComboAttackA, hitboxKeys = new List<PlayerAttackHitboxKey> { new PlayerAttackHitboxKey { frame = 0, enabled = false }, new PlayerAttackHitboxKey { frame = 1, enabled = true, offset = new Vector2(0.65f, 0f), size = new Vector2(1f, 0.8f), interpolation = AttackHitboxInterpolation.Linear }, new PlayerAttackHitboxKey { frame = 4, enabled = false } } },
                new PlayerComboStep { name = "Combo B", moveCategory = PlayerMoveCategory.Sword, comboCategory = PlayerComboCategory.Chain, animationState = PlayerActionState.ComboAttackB, hitboxKeys = new List<PlayerAttackHitboxKey> { new PlayerAttackHitboxKey { frame = 0, enabled = false }, new PlayerAttackHitboxKey { frame = 1, enabled = true, offset = new Vector2(0.72f, 0f), size = new Vector2(1.1f, 0.85f), interpolation = AttackHitboxInterpolation.Linear }, new PlayerAttackHitboxKey { frame = 4, enabled = false } }, damageMultiplier = 1.1f },
                new PlayerComboStep { name = "Combo C", moveCategory = PlayerMoveCategory.Sword, comboCategory = PlayerComboCategory.Chain, animationState = PlayerActionState.ComboAttackC, hitboxKeys = new List<PlayerAttackHitboxKey> { new PlayerAttackHitboxKey { frame = 0, enabled = false }, new PlayerAttackHitboxKey { frame = 1, enabled = true, offset = new Vector2(0.78f, 0.04f), size = new Vector2(1.15f, 0.9f), interpolation = AttackHitboxInterpolation.Linear }, new PlayerAttackHitboxKey { frame = 4, enabled = false } }, damageMultiplier = 1.2f },
                new PlayerComboStep { name = "Combo D", moveCategory = PlayerMoveCategory.Sword, comboCategory = PlayerComboCategory.Finisher, attackType = SkillAttackType.Heavy, targetReaction = HitReactionType.Launch, useCustomKnockback = true, customKnockback = new Vector2(6f, 3.4f), animationState = PlayerActionState.ComboAttackD, hitboxKeys = new List<PlayerAttackHitboxKey> { new PlayerAttackHitboxKey { frame = 0, enabled = false }, new PlayerAttackHitboxKey { frame = 1, enabled = true, offset = new Vector2(0.86f, 0.06f), size = new Vector2(1.25f, 0.95f), interpolation = AttackHitboxInterpolation.Linear }, new PlayerAttackHitboxKey { frame = 4, enabled = false } }, damageMultiplier = 1.35f }
            };
        }
    }
}
