using System;
using System.Collections;
using MirrorTrial.Abilities;
using MirrorTrial.Combat;
using MirrorTrial.Enemies;
using MirrorTrial.Player;
using UnityEngine;

namespace MirrorTrial.Boss
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(Hurtbox))]
    public sealed class MirrorBossActorV2 : MonoBehaviour
    {
        public enum State
        {
            Dormant, Intro, Approach, Windup, Combo, Shooting, TeleportWindup, TeleportHidden,
            TeleportRecover, Summoning, Recovery, PhaseChange, Dead, Launch
        }

        [SerializeField] MirrorBossSimpleProfile profile;
        [SerializeField] Hitbox swordHitbox;
        [SerializeField] BoxCollider2D swordCollider;
        [SerializeField] SpriteRenderer sprite;
        [SerializeField] Color phaseOneTint = new Color(0.35f, 0.58f, 1f, 1f);
        [SerializeField] Color phaseTwoTint = new Color(0.48f, 0.38f, 1f, 1f);
        [SerializeField] Color phaseThreeTint = new Color(0.88f, 0.3f, 0.62f, 1f);
        [SerializeField, Tooltip("由 NodeCanvas Behaviour Tree 负责决策。")] bool behaviourTreeControlled = true;

        Rigidbody2D body;
        Collider2D bodyCollider;
        Hurtbox hurtbox;
        EnemyLaunchController2D launchController;
        MirrorBossMinionController minionController;
        Animator animator;
        ChargeTelegraphPresentation windupPresentation;
        readonly ChargeTelegraphPresentation[] phaseWindupPresentations = new ChargeTelegraphPresentation[3];
        Transform target;
        Coroutine action;
        int hitPoints;
        int phase = 1;
        bool facingRight = true;
        bool activated;
        bool introCompleted;
        int requestedAnimation;
        float stunnedUntil;
        float arenaLeft = float.NegativeInfinity;
        float arenaRight = float.PositiveInfinity;
        int feintsUsedThisCombo;
        float nextTeleportAt;
        float nextSummonAt;
        int completedVolleys;
        bool tacticalDecisionValid;
        MirrorBossTacticalAction tacticalDecision;
        MirrorBossTacticalAction lastTacticalAction = (MirrorBossTacticalAction)(-1);
        float nextHeavySlashAt;
        bool lastAttackWasHeavySlash;
        bool meleeAttackDecisionValid;
        bool meleeHeavySlashSelected;

        public State CurrentState { get; private set; } = State.Dormant;
        public int CurrentHitPoints => hitPoints;
        public int MaxHitPoints => profile ? profile.maxHitPoints : 1;
        public int Phase => phase;
        public bool IsActivated => activated;
        public bool IntroCompleted => introCompleted;
        public bool IsStunned => CurrentState != State.Dead && ((launchController && launchController.IsLaunching) || Time.time < stunnedUntil);
        public float DistanceToTarget => target ? Mathf.Abs(target.position.x - transform.position.x) : float.PositiveInfinity;
        public float AttackRange => profile ? profile.attackRange : 0f;
        public float CurrentWindupProgress { get; private set; }
        public bool AttackHitboxActive { get; private set; }
        public string CurrentAttackAnimation { get; private set; }
        public float CurrentAttackProgress { get; private set; }
        public bool IsFeinting { get; private set; }
        public bool UsesRangedTeleportKit => profile && profile.enableRangedTeleportKit;
        public bool LastAttackWasHeavySlash => lastAttackWasHeavySlash;
        public float HeavySlashRecovery => profile ? profile.heavySlashRecovery : 1.1f;
        public bool IsActionRunning => action != null;
        public MirrorBossTacticalAction TacticalDecision => tacticalDecision;
        public int AliveMinionCount => minionController ? minionController.AliveCount : 0;
        public string DisplayName => profile ? profile.displayName : "镜中行刑者";
        public event Action<MirrorBossActorV2> Activated;
        public event Action<MirrorBossActorV2, int, int> HealthChanged;
        public event Action<MirrorBossActorV2, int> PhaseChanged;
        public event Action<MirrorBossActorV2> Defeated;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
            hurtbox = GetComponent<Hurtbox>();
            animator = GetComponentInChildren<Animator>(true);
            var windupVisual = sprite ? sprite : GetComponentInChildren<SpriteRenderer>(true);
            for (var phaseIndex = 0; phaseIndex < phaseWindupPresentations.Length; phaseIndex++)
            {
                var effectPrefab = profile ? profile.GetWindupEffectPrefab(phaseIndex + 1) : null;
                if (effectPrefab && effectPrefab.GetComponent<ChargeTelegraphPresentation>())
                {
                    var effectObject = Instantiate(effectPrefab, transform);
                    effectObject.transform.localPosition = Vector3.zero;
                    phaseWindupPresentations[phaseIndex] = effectObject.GetComponent<ChargeTelegraphPresentation>();
                    phaseWindupPresentations[phaseIndex].Bind(windupVisual);
                }
                else
                    phaseWindupPresentations[phaseIndex] = ChargeTelegraphPresentation.Ensure(gameObject, windupVisual);
            }
            windupPresentation = phaseWindupPresentations[0];
            launchController = GetComponent<EnemyLaunchController2D>();
            if (!launchController) launchController = gameObject.AddComponent<EnemyLaunchController2D>();
            launchController.Configure(profile ? profile.launchSettings : null);
            if (!GetComponent<EnemyLaunchAnimationPresenter>()) gameObject.AddComponent<EnemyLaunchAnimationPresenter>();
            if (!GetComponent<EnemyLaunchVfxPresenter>()) gameObject.AddComponent<EnemyLaunchVfxPresenter>();
            launchController.LaunchCompleted += OnLaunchCompleted;
            if (!sprite) sprite = GetComponentInChildren<SpriteRenderer>(true);
            if (!swordHitbox) swordHitbox = GetComponentInChildren<Hitbox>(true);
            if (!swordCollider && swordHitbox) swordCollider = swordHitbox.GetComponent<BoxCollider2D>();
            if (profile && profile.enableRangedTeleportKit)
            {
                minionController = GetComponent<MirrorBossMinionController>();
                if (!minionController) minionController = gameObject.AddComponent<MirrorBossMinionController>();
            }
            body.freezeRotation = true;
            hitPoints = MaxHitPoints;
            SetHitbox(false);
            ApplyPhaseTint();
        }

        void Update()
        {
            if (launchController && launchController.IsLaunching) return;
            if (behaviourTreeControlled) return;
            if (!profile || CurrentState != State.Approach || !target) return;
            if (profile.enableRangedTeleportKit)
            {
                BTRefreshTacticalDecision();
                BTBeginTacticalDecision();
                return;
            }
            var delta = target.position.x - transform.position.x;
            Face(delta);
            if (Mathf.Abs(delta) > profile.attackRange)
            {
                Move(Mathf.Sign(delta));
                return;
            }
            StopHorizontal();
            action = StartCoroutine(ComboRoutine());
        }

        void FixedUpdate()
        {
            if (launchController && launchController.IsLaunching) return;
            if (float.IsInfinity(arenaLeft) || float.IsInfinity(arenaRight)) return;
            var p = body.position;
            p.x = Mathf.Clamp(p.x, arenaLeft, arenaRight);
            body.position = p;
        }

        public bool BTTickApproach()
        {
            if (!profile || !target || CurrentState == State.Dead || CurrentState == State.PhaseChange || CurrentState == State.Launch) return false;
            CurrentState = State.Approach;
            var delta = target.position.x - transform.position.x;
            Face(delta);
            if (Mathf.Abs(delta) > profile.attackRange)
            {
                Move(Mathf.Sign(delta));
                return false;
            }
            StopHorizontal();
            Play("SwordIdle", 0.06f);
            return true;
        }

        public void BTBeginIntro()
        {
            if (CurrentState == State.Dead) return;
            CurrentState = State.Intro;
            StopHorizontal();
            Play(UsesRangedTeleportKit ? "BowAim" : "SwordIdle");
        }

        public void BTFinishIntro()
        {
            introCompleted = true;
            if (CurrentState != State.Dead && CurrentState != State.PhaseChange) CurrentState = State.Approach;
        }

        public bool BTBeginCombo()
        {
            if (!profile || !target || CurrentState == State.Dead || CurrentState == State.PhaseChange || CurrentState == State.Launch || action != null) return false;
            ConsumeMeleeAttackDecision(false);
            action = StartCoroutine(ComboRoutine());
            return true;
        }

        public bool BTShouldUseHeavySlash()
        {
            if (!profile || profile.enableRangedTeleportKit || !target || action != null ||
                CurrentState == State.Dead || CurrentState == State.PhaseChange || CurrentState == State.Launch ||
                DistanceToTarget > profile.attackRange || Time.time < nextHeavySlashAt || lastAttackWasHeavySlash)
                return false;
            if (!meleeAttackDecisionValid)
            {
                meleeHeavySlashSelected = UnityEngine.Random.value < profile.heavySlashChance;
                meleeAttackDecisionValid = true;
            }
            return meleeHeavySlashSelected;
        }

        public bool BTBeginHeavySlash()
        {
            if (!BTShouldUseHeavySlash()) return false;
            ConsumeMeleeAttackDecision(true);
            action = StartCoroutine(HeavySlashRoutine());
            return true;
        }

        void ConsumeMeleeAttackDecision(bool usedHeavySlash)
        {
            meleeAttackDecisionValid = false;
            meleeHeavySlashSelected = false;
            lastAttackWasHeavySlash = usedHeavySlash;
            if (usedHeavySlash)
                nextHeavySlashAt = Time.time + Mathf.Max(0f, profile.heavySlashCooldown);
        }

        public void BTBeginRecovery()
        {
            if (CurrentState == State.Dead || CurrentState == State.PhaseChange || CurrentState == State.Launch) return;
            CurrentState = State.Recovery;
            SetHitbox(false);
            StopHorizontal();
            Play("SwordIdle", 0.08f);
        }

        public void BTTickRecoveryApproach()
        {
            if (!profile || !target || CurrentState != State.Recovery || CurrentState == State.Launch) return;
            SetHitbox(false);
            var delta = target.position.x - transform.position.x;
            Face(delta);
            if (Mathf.Abs(delta) > profile.attackRange)
                Move(Mathf.Sign(delta), profile.cooldownMoveSpeedMultiplier);
            else
            {
                StopHorizontal();
                Play("SwordIdle", 0.08f);
            }
        }
        public void BTFinishRecovery()
        {
            if (CurrentState == State.Recovery) CurrentState = State.Approach;
        }

        public void BTStopHorizontal()
        {
            if (CurrentState != State.Launch) StopHorizontal();
        }

        public void BTPlayStunned()
        {
            if (CurrentState == State.Dead || CurrentState == State.Launch) return;
            SetHitbox(false);
            StopHorizontal();
            Play("HitDamage", 0.03f);
        }
        public void ConfigureArena(float left, float right)
        {
            arenaLeft = Mathf.Min(left, right);
            arenaRight = Mathf.Max(left, right);
            if (minionController) minionController.Configure(profile, arenaLeft, arenaRight);
        }

        public void Activate(Transform playerTarget)
        {
            if (!profile || !playerTarget || activated) return;
            target = playerTarget;
            if (minionController) minionController.Configure(profile, arenaLeft, arenaRight);
            activated = true;
            Activated?.Invoke(this);
            if (behaviourTreeControlled) return;
            CurrentState = State.Intro;
            action = StartCoroutine(IntroRoutine());
        }

        public void BTRefreshTacticalDecision()
        {
            if (tacticalDecisionValid || !UsesRangedTeleportKit || !target || action != null) return;

            var canTeleport = Time.time >= nextTeleportAt;
            if (DistanceToTarget <= profile.closeEscapeDistance)
            {
                tacticalDecision = canTeleport
                    ? MirrorBossTacticalAction.TeleportRetreat
                    : MirrorBossTacticalAction.CloseDefense;
                tacticalDecisionValid = true;
                return;
            }

            var canSummon = Time.time >= nextSummonAt && minionController && minionController.CanSummon(phase) &&
                            lastTacticalAction != MirrorBossTacticalAction.Summon;
            if (canSummon && UnityEngine.Random.value < profile.summonDecisionChance)
                tacticalDecision = MirrorBossTacticalAction.Summon;
            else
            {
                var teleportChance = phase == 1 ? profile.phaseOneTeleportAttackChance
                    : phase == 2 ? profile.phaseTwoTeleportAttackChance : profile.phaseThreeTeleportAttackChance;
                tacticalDecision = canTeleport && lastTacticalAction != MirrorBossTacticalAction.TeleportAttack &&
                                   UnityEngine.Random.value < teleportChance
                    ? MirrorBossTacticalAction.TeleportAttack
                    : MirrorBossTacticalAction.Shoot;
            }
            tacticalDecisionValid = true;
        }

        public bool BTDecisionIs(MirrorBossTacticalAction expected)
        {
            BTRefreshTacticalDecision();
            return tacticalDecisionValid && tacticalDecision == expected;
        }

        public bool BTBeginTacticalDecision()
        {
            BTRefreshTacticalDecision();
            if (!tacticalDecisionValid || action != null || CurrentState == State.Dead ||
                CurrentState == State.PhaseChange || CurrentState == State.Launch) return false;

            var selected = tacticalDecision;
            tacticalDecisionValid = false;
            lastTacticalAction = selected;
            switch (selected)
            {
                case MirrorBossTacticalAction.TeleportAttack:
                    action = StartCoroutine(TeleportRoutine(true));
                    break;
                case MirrorBossTacticalAction.TeleportRetreat:
                    action = StartCoroutine(TeleportRoutine(false));
                    break;
                case MirrorBossTacticalAction.Summon:
                    action = StartCoroutine(SummonRoutine());
                    break;
                case MirrorBossTacticalAction.CloseDefense:
                    action = StartCoroutine(ComboRoutine());
                    break;
                default:
                    action = StartCoroutine(ShootRoutine());
                    break;
            }
            return true;
        }

        IEnumerator ShootRoutine()
        {
            CurrentState = State.Shooting;
            SetHitbox(false);
            StopHorizontal();
            var arrows = phase == 1 ? profile.phaseOneArrowCount
                : phase == 2 ? profile.phaseTwoArrowCount : profile.phaseThreeArrowCount;
            yield return FireVolley(arrows);
            if (CurrentState == State.Dead || CurrentState == State.PhaseChange || CurrentState == State.Launch)
                yield break;

            completedVolleys++;
            CurrentState = State.Recovery;
            Play("BowAim", 0.06f);
            var reload = completedVolleys >= Mathf.Max(1, profile.volleysBeforeReload);
            if (reload) completedVolleys = 0;
            yield return new WaitForSeconds(reload ? profile.bowReloadRecovery : profile.bowVolleyRecovery);
            FinishRangedAction();
        }

        IEnumerator FireVolley(int arrowCount)
        {
            for (var i = 0; i < Mathf.Max(1, arrowCount); i++)
            {
                if (!target || CurrentState == State.Dead || CurrentState == State.PhaseChange || CurrentState == State.Launch)
                {
                    if (windupPresentation)
                        windupPresentation.End(ChargeTelegraphEndReason.Interrupted, facingRight);
                    CurrentWindupProgress = 0f;
                    yield break;
                }
                Face(target.position.x - transform.position.x);
                PlayRestart("BowDraw", 0.04f);
                CurrentWindupProgress = 0f;
                windupPresentation = phaseWindupPresentations[Mathf.Clamp(phase - 1, 0, phaseWindupPresentations.Length - 1)];
                if (windupPresentation)
                    windupPresentation.Begin(profile.GetWindupEffectPrefab(phase) ? null : profile.windupPresentation,
                        profile.bowSpawnOffset, facingRight);
                var drawStartedAt = Time.time;
                while (Time.time - drawStartedAt < profile.bowDrawDuration)
                {
                    if (!target || CurrentState == State.Dead || CurrentState == State.PhaseChange || CurrentState == State.Launch)
                    {
                        if (windupPresentation)
                            windupPresentation.End(ChargeTelegraphEndReason.Interrupted, facingRight);
                        CurrentWindupProgress = 0f;
                        yield break;
                    }
                    CurrentWindupProgress = Mathf.Clamp01((Time.time - drawStartedAt) / profile.bowDrawDuration);
                    if (windupPresentation)
                        windupPresentation.SetProgress(CurrentWindupProgress, facingRight);
                    yield return null;
                }
                if (!target || CurrentState == State.Dead || CurrentState == State.PhaseChange || CurrentState == State.Launch)
                {
                    if (windupPresentation)
                        windupPresentation.End(ChargeTelegraphEndReason.Interrupted, facingRight);
                    CurrentWindupProgress = 0f;
                    yield break;
                }

                var facing = facingRight ? Vector2.right : Vector2.left;
                var spawn = (Vector2)transform.position + new Vector2(profile.bowSpawnOffset.x * facing.x,
                    profile.bowSpawnOffset.y);
                // The archer commits to a flat lane when the draw starts. The arrow never tilts
                // toward the player's current height and cannot look like a homing projectile.
                var direction = facing;
                if (windupPresentation)
                    windupPresentation.End(ChargeTelegraphEndReason.Released, facingRight);
                CurrentWindupProgress = 0f;
                PlayRestart("BowFire", 0.02f);
                var arrow = BowArrowProjectile.Create(spawn);
                arrow.Launch(new DamagePayload(gameObject, profile.bowDamage,
                        new Vector2(direction.x * Mathf.Abs(profile.bowKnockback.x), profile.bowKnockback.y),
                        direction, 0.04f), direction, profile.bowArrowSpeed, profile.bowArrowRange,
                    target.gameObject);
                if (i + 1 < arrowCount) yield return new WaitForSeconds(profile.bowShotInterval);
            }
        }

        IEnumerator TeleportRoutine(bool attackAfterAppear)
        {
            CurrentState = State.TeleportWindup;
            SetHitbox(false);
            StopHorizontal();
            Play("BowFull", 0.05f);
            yield return new WaitForSeconds(profile.teleportWindup);
            if (CurrentState != State.TeleportWindup) yield break;

            CurrentState = State.TeleportHidden;
            SetTeleportHidden(true);
            var destination = FindTeleportDestination(attackAfterAppear);
            body.position = destination;
            yield return new WaitForSeconds(profile.teleportHiddenDuration);
            if (CurrentState != State.TeleportHidden) yield break;

            SetTeleportHidden(false);
            nextTeleportAt = Time.time + profile.teleportCooldown;
            CurrentState = State.TeleportRecover;
            Face(target ? target.position.x - transform.position.x : 1f);
            Play("BowAim", 0.04f);
            yield return new WaitForSeconds(profile.teleportAppearRecovery);
            if (CurrentState != State.TeleportRecover) yield break;

            if (attackAfterAppear)
            {
                CurrentState = State.Shooting;
                yield return FireVolley(phase >= 3 ? 2 : 1);
                if (CurrentState == State.Dead || CurrentState == State.PhaseChange || CurrentState == State.Launch)
                    yield break;
            }
            CurrentState = State.Recovery;
            yield return new WaitForSeconds(profile.bowVolleyRecovery);
            FinishRangedAction();
        }

        Vector2 FindTeleportDestination(bool flankPlayer)
        {
            var currentY = body.position.y;
            if (float.IsInfinity(arenaLeft) || float.IsInfinity(arenaRight) || !target)
                return body.position + new Vector2((facingRight ? -1f : 1f) * profile.teleportFlankDistance, 0f);

            var min = arenaLeft + profile.teleportArenaPadding;
            var max = arenaRight - profile.teleportArenaPadding;
            float x;
            if (flankPlayer)
            {
                var sideAwayFromBoss = transform.position.x <= target.position.x ? 1f : -1f;
                x = target.position.x + sideAwayFromBoss * profile.teleportFlankDistance;
            }
            else
            {
                var leftDistance = Mathf.Abs(target.position.x - min);
                var rightDistance = Mathf.Abs(max - target.position.x);
                x = leftDistance >= rightDistance ? min : max;
            }
            return new Vector2(Mathf.Clamp(x, min, max), currentY);
        }

        IEnumerator SummonRoutine()
        {
            CurrentState = State.Summoning;
            SetHitbox(false);
            StopHorizontal();
            Face(target ? target.position.x - transform.position.x : 1f);
            Play("BowFull", 0.06f);
            nextSummonAt = Time.time + profile.summonCooldown;
            yield return new WaitForSeconds(profile.summonWindup);
            if (CurrentState != State.Summoning) yield break;
            if (minionController) minionController.SummonToCurrentLimit(phase);
            CurrentState = State.Recovery;
            Play("BowAim", 0.06f);
            yield return new WaitForSeconds(profile.summonRecovery);
            FinishRangedAction();
        }

        void FinishRangedAction()
        {
            if (CurrentState == State.Dead || CurrentState == State.PhaseChange || CurrentState == State.Launch) return;
            CurrentState = State.Approach;
            action = null;
            tacticalDecisionValid = false;
            Play("BowAim", 0.06f);
        }

        void SetTeleportHidden(bool hidden)
        {
            SetHitbox(false);
            if (hurtbox) hurtbox.enabled = !hidden;
            if (sprite) sprite.enabled = !hidden;
            if (body) body.simulated = !hidden;
        }

        IEnumerator IntroRoutine()
        {
            StopHorizontal();
            Play(UsesRangedTeleportKit ? "BowAim" : "SwordIdle");
            yield return new WaitForSeconds(profile.introDuration);
            CurrentState = State.Approach;
            action = null;
        }

        IEnumerator ComboRoutine()
        {
            CurrentState = State.Combo;
            feintsUsedThisCombo = 0;
            IsFeinting = false;
            SetHitbox(false);
            StopHorizontal();
            Face(target.position.x - transform.position.x);
            var combo = phase == 1 ? profile.phaseOneCombo : phase == 2 ? profile.phaseTwoCombo : profile.phaseThreeCombo;
            foreach (var step in combo)
            {
                if (CurrentState != State.Combo) yield break;
                yield return PlayComboStep(step);
                if (step.gapAfter > 0f) yield return new WaitForSeconds(step.gapAfter);
            }
            SetHitbox(false);
            animator.speed = 1f;
            CurrentState = State.Recovery;
            Play("SwordIdle", 0.08f);
            if (UsesRangedTeleportKit)
            {
                yield return new WaitForSeconds(profile.closeDefenseRecovery);
                FinishRangedAction();
                yield break;
            }
            action = null;
            if (behaviourTreeControlled) yield break;
            var range = phase == 1 ? profile.phaseOneRecovery : phase == 2 ? profile.phaseTwoRecovery : profile.phaseThreeRecovery;
            yield return new WaitForSeconds(UnityEngine.Random.Range(range.x, range.y));
            if (CurrentState == State.Recovery) CurrentState = State.Approach;
            action = null;
        }
        IEnumerator HeavySlashRoutine()
        {
            CurrentState = State.Combo;
            feintsUsedThisCombo = 0;
            IsFeinting = false;
            SetHitbox(false);
            StopHorizontal();
            Face(target.position.x - transform.position.x);
            yield return PlayComboStep(profile.heavySlash);
            if (CurrentState != State.Combo)
                yield break;

            SetHitbox(false);
            animator.speed = 1f;
            CurrentState = State.Recovery;
            Play("SwordIdle", 0.08f);
            action = null;
        }

        IEnumerator PlayComboStep(MirrorBossComboStepV2 step)
        {
            SetHitbox(false);
            animator.speed = step.playbackSpeed;
            var hash = Animator.StringToHash(step.animationState);
            requestedAnimation = hash;
            CurrentAttackAnimation = step.animationState;
            CurrentAttackProgress = 0f;
            animator.CrossFade(hash, 0.03f, 0, 0f);

            var entryTimeout = 0.5f;
            while (entryTimeout > 0f)
            {
                var info = animator.GetCurrentAnimatorStateInfo(0);
                if (info.shortNameHash == hash) break;
                entryTimeout -= Time.deltaTime;
                yield return null;
            }

            step.EnsureHitboxKeys();
            var windupHoldDuration = step.windupHoldDuration > 0f
                ? step.windupHoldDuration
                : profile.GetWindupHoldDuration(phase);
            var configuredWindupFrame = Mathf.Clamp(step.windupFrame, 0, step.animationFrameCount - 1);
            var firstActiveFrame = FindFirstActiveFrame(step);
            var windupConsumed = step.windupFrame < 0 || windupHoldDuration <= 0f || configuredWindupFrame >= firstActiveFrame;
            var windupEndTime = 0f;
            var performFeint = !windupConsumed && ShouldPerformFeint(step);
            while (CurrentState == State.Combo || CurrentState == State.Windup)
            {
                var info = animator.GetCurrentAnimatorStateInfo(0);
                if (info.shortNameHash != hash)
                {
                    SetHitbox(false);
                    yield return null;
                    continue;
                }

                var progress = info.normalizedTime;
                CurrentAttackProgress = progress;
                var frame = Mathf.FloorToInt(Mathf.Clamp01(progress) * Mathf.Max(1, step.animationFrameCount - 1));

                if (!windupConsumed && frame >= configuredWindupFrame)
                {
                    if (CurrentState != State.Windup)
                    {
                        CurrentState = State.Windup;
                        SetHitbox(false);
                        StopHorizontal();
                        animator.speed = 0f;
                        windupEndTime = Time.time + windupHoldDuration;
                        windupPresentation = phaseWindupPresentations[Mathf.Clamp(phase - 1, 0, phaseWindupPresentations.Length - 1)];
                        if (windupPresentation)
                        {
                            windupPresentation.Begin(profile.GetWindupEffectPrefab(phase) ? null : profile.windupPresentation,
                                step.windupEffectOffset, facingRight, null, step.windupEffectAngle);
                            if (performFeint) windupPresentation.SetProgress(0.55f, facingRight);
                        }
                    }

                    if (performFeint)
                    {
                        IsFeinting = true;
                        feintsUsedThisCombo++;
                        yield return new WaitForSeconds(Mathf.Min(profile.feintHoldDuration, windupHoldDuration));
                        if (CurrentState != State.Windup) yield break;

                        if (windupPresentation)
                            windupPresentation.End(ChargeTelegraphEndReason.Feinted, facingRight);
                        animator.speed = 1f;
                        Play("SwordIdle", 0.04f);
                        yield return new WaitForSeconds(profile.feintResetDuration);
                        if (CurrentState != State.Windup) yield break;

                        Face(target.position.x - transform.position.x);
                        IsFeinting = false;
                        performFeint = false;
                        CurrentState = State.Combo;
                        animator.speed = step.playbackSpeed;
                        requestedAnimation = hash;
                        animator.CrossFade(hash, 0.04f, 0, 0f);
                        continue;
                    }

                    CurrentWindupProgress = windupHoldDuration <= 0f
                        ? 1f
                        : Mathf.Clamp01(1f - (windupEndTime - Time.time) / windupHoldDuration);
                    if (windupPresentation)
                        windupPresentation.SetProgress(CurrentWindupProgress, facingRight);
                    if (Time.time < windupEndTime)
                    {
                        yield return null;
                        continue;
                    }

                    windupConsumed = true;
                    if (windupPresentation)
                        windupPresentation.End(ChargeTelegraphEndReason.Released, facingRight);
                    CurrentWindupProgress = 0f;
                    CurrentState = State.Combo;
                    animator.speed = step.playbackSpeed;
                    continue;
                }

                PlayerAttackHitboxKey key;
                if (TryEvaluateHitboxKey(step, frame, out key) && key.enabled)
                {
                    ConfigureHitbox(step, key.offset, key.size);
                    SetHitbox(true);
                }
                else
                {
                    SetHitbox(false);
                }

                if (progress >= 1f) break;
                if (AttackHitboxActive && step.advanceSpeed > 0f)
                    body.velocity = new Vector2((facingRight ? 1f : -1f) * step.advanceSpeed, body.velocity.y);
                else
                    StopHorizontal();
                yield return null;
            }
            SetHitbox(false);
            CurrentAttackAnimation = null;
            CurrentAttackProgress = 0f;
            StopHorizontal();
        }

        bool ShouldPerformFeint(MirrorBossComboStepV2 step)
        {
            if (!profile || step == null || !step.allowFeint ||
                feintsUsedThisCombo >= Mathf.Max(1, profile.maxFeintsPerCombo)) return false;
            var phaseChance = phase == 1
                ? profile.phaseOneFeintChance
                : phase == 2 ? profile.phaseTwoFeintChance : profile.phaseThreeFeintChance;
            var chance = step.feintChanceOverride >= 0f ? step.feintChanceOverride : phaseChance;
            return chance > 0f && UnityEngine.Random.value < chance;
        }

        void ConfigureHitbox(MirrorBossComboStepV2 step, Vector2 offset, Vector2 size)
        {
            if (!swordHitbox) return;
            var direction = facingRight ? Vector2.right : Vector2.left;
            swordHitbox.Configure(new DamagePayload(gameObject, step.damage,
                new Vector2(direction.x * step.knockback.x, step.knockback.y), direction, step.hitStop,
                step.interruptPower, step.poiseDamage, step.playerHitReaction, step.breaksSuperArmor));
            if (!swordCollider) return;
            if (step.mirrorHitboxByFacing) offset.x = Mathf.Abs(offset.x) * direction.x;
            swordCollider.offset = offset;
            swordCollider.size = new Vector2(Mathf.Max(0.01f, size.x), Mathf.Max(0.01f, size.y));
        }

        static int FindFirstActiveFrame(MirrorBossComboStepV2 step)
        {
            for (var frame = 0; frame < Mathf.Max(1, step.animationFrameCount); frame++)
            {
                PlayerAttackHitboxKey key;
                if (TryEvaluateHitboxKey(step, frame, out key) && key.enabled) return frame;
            }
            return int.MaxValue;
        }
        static bool TryEvaluateHitboxKey(MirrorBossComboStepV2 step, int frame, out PlayerAttackHitboxKey result)
        {
            result = null;
            if (step.hitboxKeys == null || step.hitboxKeys.Count == 0) return false;
            PlayerAttackHitboxKey previous = null;
            PlayerAttackHitboxKey next = null;
            foreach (var key in step.hitboxKeys)
            {
                if (key == null) continue;
                if (key.frame <= frame && (previous == null || key.frame >= previous.frame)) previous = key;
                if (key.frame > frame && (next == null || key.frame < next.frame)) next = key;
            }
            if (previous == null) return false;
            result = previous;
            if (next == null || previous.interpolation != AttackHitboxInterpolation.Linear ||
                !previous.enabled || !next.enabled || next.frame <= previous.frame) return true;
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

        void OnDamagePayloadReceived(DamagePayload payload)
        {
            if (CurrentState == State.Dead) return;
            hitPoints = Mathf.Max(0, hitPoints - Mathf.Max(0, payload.damage));
            HealthChanged?.Invoke(this, hitPoints, MaxHitPoints);
            if (hitPoints <= 0) { Die(); return; }

            // Launch, landing and get-up own their complete reaction window. Further hits
            // still deal damage, but cannot restart launch, alter velocity or interrupt recovery.
            if (launchController && launchController.IsLaunching)
                return;

            if (payload.reaction == HitReactionType.Launch)
            {
                BeginLaunch(payload.knockback);
                return;
            }

            if (CurrentState == State.PhaseChange) return;

            var ratio = hitPoints / (float)MaxHitPoints;
            if (phase == 1 && ratio <= profile.phaseTwoAt) { BeginPhase(2); return; }
            if (phase == 2 && ratio <= profile.phaseThreeAt) { BeginPhase(3); return; }

            if (payload.breaksSuperArmor &&
                (CurrentState == State.TeleportWindup || CurrentState == State.TeleportRecover || CurrentState == State.Summoning))
            {
                CancelAction();
                CurrentState = State.Approach;
                stunnedUntil = Time.time + 0.35f;
                Play("HitDamage");
                return;
            }

            if (CurrentState != State.Combo && CurrentState != State.Windup && CurrentState != State.Shooting &&
                CurrentState != State.TeleportWindup && CurrentState != State.TeleportHidden && CurrentState != State.Summoning)
            {
                stunnedUntil = Time.time + 0.28f;
                body.velocity = payload.knockback * 0.25f;
                Play("HitDamage");
            }
        }

        void BeginLaunch(Vector2 velocity)
        {
            CancelAction();
            CurrentState = State.Launch;
            launchController.Configure(profile ? profile.launchSettings : null);
            launchController.BeginLaunch(velocity);
        }

        void OnLaunchCompleted()
        {
            if (CurrentState != State.Launch) return;
            var ratio = hitPoints / (float)MaxHitPoints;
            if (phase == 1 && ratio <= profile.phaseTwoAt) { BeginPhase(2); return; }
            if (phase == 2 && ratio <= profile.phaseThreeAt) { BeginPhase(3); return; }
            stunnedUntil = 0f;
            CurrentState = State.Approach;
            Play(UsesRangedTeleportKit ? "BowAim" : "SwordIdle", 0.08f);
        }

        void OnDestroy()
        {
            if (windupPresentation)
                windupPresentation.End(ChargeTelegraphEndReason.Cancelled, facingRight);
            if (launchController) launchController.LaunchCompleted -= OnLaunchCompleted;
        }
        void BeginPhase(int nextPhase)
        {
            CancelAction();
            action = StartCoroutine(PhaseRoutine(nextPhase));
        }

        IEnumerator PhaseRoutine(int nextPhase)
        {
            CurrentState = State.PhaseChange;
            phase = nextPhase;
            ApplyPhaseTint();
            PhaseChanged?.Invoke(this, phase);
            tacticalDecisionValid = false;
            Play(UsesRangedTeleportKit ? "BowFull" : "SwordGuard", 0.08f);
            yield return new WaitForSeconds(profile.phaseTransitionDuration);
            Play(UsesRangedTeleportKit ? "BowAim" : "SwordIdle", 0.08f);
            CurrentState = State.Approach;
            action = null;
        }

        void Die()
        {
            CancelAction();
            CurrentState = State.Dead;
            if (minionController) minionController.ClearAll();
            if (hurtbox) hurtbox.enabled = false;
            Play("Die", 0.05f);
            Defeated?.Invoke(this);
        }

        void CancelAction()
        {
            if (windupPresentation && windupPresentation.IsActive)
                windupPresentation.End(ChargeTelegraphEndReason.Interrupted, facingRight);
            if (action != null) StopCoroutine(action);
            action = null;
            CurrentWindupProgress = 0f;
            IsFeinting = false;
            SetHitbox(false);
            SetTeleportHidden(false);
            if (animator) animator.speed = 1f;
            StopHorizontal();
        }

        void Move(float direction, float speedMultiplier = 1f)
        {
            var desired = direction * profile.moveSpeed * Mathf.Clamp01(speedMultiplier);
            body.velocity = new Vector2(Mathf.MoveTowards(body.velocity.x, desired, profile.acceleration * Time.deltaTime), body.velocity.y);
            Play("SwordWalk", 0.08f);
        }

        void Face(float delta)
        {
            if (Mathf.Abs(delta) < 0.05f) return;
            facingRight = delta > 0f;
            if (sprite) sprite.flipX = !facingRight;
        }

        void StopHorizontal() => body.velocity = new Vector2(0f, body.velocity.y);
        void SetHitbox(bool value)
        {
            AttackHitboxActive = value;
            if (swordHitbox) swordHitbox.SetActive(value);
        }

        void Play(string state, float transition = 0.04f)
        {
            if (!animator) return;
            var hash = Animator.StringToHash(state);
            var current = animator.GetCurrentAnimatorStateInfo(0);
            if (current.shortNameHash == hash && !animator.IsInTransition(0))
            {
                requestedAnimation = hash;
                return;
            }
            if (requestedAnimation == hash && animator.IsInTransition(0)) return;
            requestedAnimation = hash;
            animator.CrossFade(hash, transition);
        }

        void PlayRestart(string state, float transition = 0.04f)
        {
            if (!animator) return;
            var hash = Animator.StringToHash(state);
            requestedAnimation = hash;
            animator.CrossFade(hash, transition, 0, 0f);
        }

        void ApplyPhaseTint()
        {
            if (sprite) sprite.color = phase == 1 ? phaseOneTint : phase == 2 ? phaseTwoTint : phaseThreeTint;
        }
    }
}
