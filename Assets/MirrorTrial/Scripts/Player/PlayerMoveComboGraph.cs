using System;
using System.Collections;
using System.Collections.Generic;
using MirrorTrial.Combat;
using UnityEngine;

namespace MirrorTrial.Player
{
    public enum PlayerChargeStage
    {
        None,
        Charging,
        LaunchReady,
        Full
    }

    public enum ComboInputCondition
    {
        [InspectorName("点击")] Press,
        [InspectorName("点按（按下后松开）")] Tap,
        [InspectorName("按住")] Hold,
        [InspectorName("松开")] Release
    }

    [Serializable]
    public sealed class PlayerMoveTemplate
    {
        [HideInInspector] public string id;
        public string name = "新招式";
        public PlayerComboStep move = new PlayerComboStep();
    }

    [Serializable]
    public sealed class PlayerComboMove
    {
        [HideInInspector] public string id;
        public string name = "招式";
        public PlayerComboStep move = new PlayerComboStep();
        public Vector2 graphPosition;
        [HideInInspector] public string moveId;
        [Min(0f)] public float damageMultiplier = 1f;
        [Min(0f)] public float knockbackMultiplier = 1f;
        [Min(0f)] public float poiseMultiplier = 1f;
        [Min(0.05f)] public float animationSpeed = 1f;

        public bool overrideAttackType;
        public SkillAttackType attackType = SkillAttackType.Normal;
        public bool overrideTargetReaction;
        public bool enableTargetReaction = true;
        public HitReactionType targetReaction = HitReactionType.LightHurt;
        public bool useCustomKnockback;
        public Vector2 customKnockback = new Vector2(4f, 1f);

        public bool enableCharge;
        [Min(0)] public int chargeHoldFrame = 1;
        [HideInInspector, Range(0f, 0.95f)] public float chargeHoldNormalizedTime = 0.35f;
        [HideInInspector] public AnimationClip chargeAnimation;
        public bool showChargeEffect = true;
        public GameObject chargeEffectPrefab;
        public Vector2 chargeEffectOffset = new Vector2(0.65f, 0.15f);
        [Min(0.1f)] public float chargeEffectScale = 0.75f;
        public float chargeEffectRotationSpeed = 55f;
        [Min(0f)] public float chargeEffectPulseSpeed = 18f;
        [Range(0f, 1f)] public float chargeEffectPulseAmount = 0.12f;
        [Min(0.01f)] public float chargeEffectStartScale = 0.9f;
        [Min(0.01f)] public float chargeEffectFullScale = 1.8f;
        [Range(0f, 1f)] public float chargeEffectStartAlpha = 0.55f;
        [Range(0f, 1f)] public float chargeEffectFullAlpha = 1f;
        [Range(0f, 2f)] public float chargeEffectParticleIntensity = 1f;
        public ChargeTelegraphSettings chargePresentation = new ChargeTelegraphSettings();
        [Min(0f)] public float minimumChargeTime = 0.18f;
        [Min(0.01f)] public float maximumChargeTime = 0.7f;
        [Range(0f, 1f)] public float launchChargeThreshold = 0.65f;
        public HitReactionType insufficientLaunchReaction = HitReactionType.HeavyHurt;
        [Min(1f)] public float fullChargeDamageMultiplier = 1.5f;
        [Min(1f)] public float fullChargeKnockbackMultiplier = 1.35f;
        public bool autoReleaseAtFullCharge = true;
        [Min(0.05f)] public float fullChargeHoldLimit = 1.5f;
        public GameObject chargeCountdownPrefab;
        public Vector2 chargeCountdownOffset = new Vector2(0f, 1.15f);
    }

    [Serializable]
    public sealed class PlayerComboTransition
    {
        [HideInInspector] public string id;
        [HideInInspector] public string fromInstanceId;
        [HideInInspector] public string toInstanceId;
        public PlayerInputCommand input = PlayerInputCommand.PrimaryAttack;
        public ComboInputCondition condition = ComboInputCondition.Press;
        [Min(0f)] public float windowStart = 0.1f;
        [Min(0f)] public float windowEnd = 0.28f;
        [Min(0.01f)] public float holdThreshold = 0.15f;
        [Tooltip("Only allows this transition after the source move hit a Hurtbox.")]
        public bool requiresHit;
    }

    [Serializable]
    public sealed class PlayerComboInputDecision
    {
        [HideInInspector] public string id;
        public string name = "点按 / 长按";
        public Vector2 graphPosition;
        public bool enabled = true;
        [HideInInspector] public string sourceInstanceId;
        public PlayerInputCommand input = PlayerInputCommand.PrimaryAttack;
        [Min(0)] public int listenStartFrame;
        [Min(0)] public int decisionFrame = 2;
        [Min(1)] public int entryFrameRate = 60;
        [HideInInspector] public string tapTargetInstanceId;
        [HideInInspector] public string holdTargetInstanceId;
        [HideInInspector] public string noInputTargetInstanceId;
        public bool tapRequiresHit;
        public bool holdRequiresHit;
        public bool noInputRequiresHit;
    }

    [Serializable]
    public sealed class PlayerComboGraph
    {
        public string name = "新连招";
        public PlayerWeaponType weaponType = PlayerWeaponType.Unarmed;
        public PlayerInputCommand entryInput = PlayerInputCommand.PrimaryAttack;
        [HideInInspector] public string entryInstanceId;
        [HideInInspector] public string entryDecisionId;
        public List<PlayerComboMove> instances = new List<PlayerComboMove>();
        public List<PlayerComboTransition> transitions = new List<PlayerComboTransition>();
        public List<PlayerComboInputDecision> inputDecisions = new List<PlayerComboInputDecision>();
    }

    public partial class PlayerCombat
    {
        [SerializeField, HideInInspector] List<PlayerMoveTemplate> moveLibrary = new List<PlayerMoveTemplate>();
        [SerializeField] List<PlayerComboGraph> comboGraphs = new List<PlayerComboGraph>();
        [SerializeField, HideInInspector] int moveComboSchemaVersion;

        public IList<PlayerComboGraph> ComboGraphs => comboGraphs;
        string runtimeCurrentMoveId;
        string runtimeCurrentMoveName;
        string runtimeLastTransitionId;
        string runtimeCurrentDecisionId;
        float runtimeDecisionProgress;
        float runtimeChargeSeconds;
        float runtimeChargeNormalized;
        float runtimeDamageMultiplier = 1f;
        PlayerChargeStage runtimeChargeStage;
        int actionVersion;
        ChargeTelegraphPresentation activeChargePresentation;
        bool ownsActiveChargePresentation;
        ChargeCountdownPresentation activeChargeCountdown;
        bool ownsActiveChargeCountdown;
        string runtimePreviewStatus = "\u7b49\u5f85\u8f93\u5165";

        public string RuntimeCurrentMoveId => runtimeCurrentMoveId;
        public string RuntimeCurrentMoveName => runtimeCurrentMoveName;
        public string RuntimeLastTransitionId => runtimeLastTransitionId;
        public string RuntimeCurrentDecisionId => runtimeCurrentDecisionId;
        public float RuntimeDecisionProgress => runtimeDecisionProgress;
        public float RuntimeChargeSeconds => runtimeChargeSeconds;
        public float RuntimeChargeNormalized => runtimeChargeNormalized;
        public float RuntimeDamageMultiplier => runtimeDamageMultiplier;
        public PlayerChargeStage RuntimeChargeStage => runtimeChargeStage;
        public string RuntimePreviewStatus => runtimePreviewStatus;

        public void ClearRuntimePreviewState()
        {
            runtimeCurrentMoveId = string.Empty;
            runtimeCurrentMoveName = string.Empty;
            runtimeLastTransitionId = string.Empty;
            runtimeCurrentDecisionId = string.Empty;
            runtimeDecisionProgress = 0f;
            runtimeChargeSeconds = 0f;
            runtimeChargeNormalized = 0f;
            runtimeDamageMultiplier = 1f;
            runtimeChargeStage = PlayerChargeStage.None;
            runtimePreviewStatus = "\u7b49\u5f85\u8f93\u5165";
        }

        sealed class TransitionRuntimeState
        {
            public PlayerComboTransition transition;
            public bool tracking;
            public float pressedAt;
        }

        sealed class SelectedTransition
        {
            public PlayerComboTransition transition;
            public string targetInstanceId;
            public string runtimeId;
            public float heldDuration;
        }

        bool HasGraphForWeapon(PlayerWeaponType weapon)
        {
            EnsureMoveComboData();
            return FindGraphForWeapon(weapon) != null;
        }

        void UpdateGraphCombat(PlayerWeaponType weapon)
        {
            var graph = FindGraphForWeapon(weapon);
            if (graph == null || graph.instances == null || graph.instances.Count == 0)
                return;
            if (attackRoutine != null)
                return;

            var entryDecision = FindDecision(graph, graph.entryDecisionId);
            if (entryDecision != null && entryDecision.enabled)
            {
                if (input.WasPressed(entryDecision.input))
                    attackRoutine = StartCoroutine(GraphEntryDecisionRoutine(graph, entryDecision));
                return;
            }

            if (input.WasPressed(graph.entryInput))
                attackRoutine = StartCoroutine(GraphComboRoutine(graph));
        }

        PlayerComboGraph FindGraphForWeapon(PlayerWeaponType weapon)
        {
            if (comboGraphs == null) return null;
            for (var i = 0; i < comboGraphs.Count; i++)
                if (comboGraphs[i] != null && comboGraphs[i].weaponType == weapon)
                    return comboGraphs[i];
            return null;
        }

        IEnumerator GraphComboRoutine(PlayerComboGraph graph)
        {
            var version = ++actionVersion;
            yield return StartCoroutine(GraphComboRoutine(graph, graph.entryInstanceId, graph.entryInput, 0f, version));
        }

        IEnumerator GraphComboRoutine(PlayerComboGraph graph, string startInstanceId,
            PlayerInputCommand incomingChargeInput, float incomingChargeTime, int version)
        {
            var current = FindInstance(graph, startInstanceId);
            while (current != null)
            {
                runtimeCurrentMoveId = current.id;
                runtimeCurrentMoveName = current.name;
                runtimePreviewStatus = current.enableCharge ? "\u84c4\u529b\u4e2d" : "\u64ad\u653e\u62db\u5f0f";
                if (current.move == null)
                    break;

                var selection = new SelectedTransition();
                yield return StartCoroutine(GraphMoveRoutine(
                    graph,
                    current,
                    current.move,
                    selection,
                    incomingChargeInput,
                    incomingChargeTime,
                    version));
                if (version != actionVersion)
                    yield break;
                var targetInstanceId = !string.IsNullOrEmpty(selection.targetInstanceId)
                    ? selection.targetInstanceId
                    : selection.transition != null ? selection.transition.toInstanceId : string.Empty;
                if (string.IsNullOrEmpty(targetInstanceId))
                    break;

                runtimeLastTransitionId = !string.IsNullOrEmpty(selection.runtimeId)
                    ? selection.runtimeId
                    : selection.transition.id;
                current = FindInstance(graph, targetInstanceId);
                incomingChargeTime = selection.heldDuration;
                if (selection.transition != null)
                    incomingChargeInput = selection.transition.input;
            }

            FinishGraphAction();
        }

        IEnumerator GraphEntryDecisionRoutine(PlayerComboGraph graph, PlayerComboInputDecision decision)
        {
            var version = ++actionVersion;
            var elapsed = 0f;
            var duration = Mathf.Max(1, decision.decisionFrame) / (float)Mathf.Max(1, decision.entryFrameRate);
            runtimeCurrentDecisionId = decision.id;
            runtimeDecisionProgress = 0f;
            runtimePreviewStatus = "起手判定：松开轻击 / 按住蓄力";

            while (version == actionVersion && elapsed < duration)
            {
                if (input.WasReleased(decision.input))
                {
                    runtimeLastTransitionId = DecisionRuntimeId(decision.id, "tap");
                    runtimeCurrentDecisionId = string.Empty;
                    runtimeDecisionProgress = 1f;
                    if (!string.IsNullOrEmpty(decision.tapTargetInstanceId))
                        yield return StartCoroutine(GraphComboRoutine(graph, decision.tapTargetInstanceId,
                            decision.input, elapsed, version));
                    else
                        FinishGraphAction();
                    yield break;
                }

                elapsed += Time.deltaTime;
                runtimeDecisionProgress = Mathf.Clamp01(elapsed / duration);
                yield return null;
            }

            if (version != actionVersion)
                yield break;

            var branch = input.IsHeld(decision.input) ? "hold" : "tap";
            var target = branch == "hold" ? decision.holdTargetInstanceId : decision.tapTargetInstanceId;
            runtimeLastTransitionId = DecisionRuntimeId(decision.id, branch);
            runtimeCurrentDecisionId = string.Empty;
            runtimeDecisionProgress = 1f;
            if (!string.IsNullOrEmpty(target))
                yield return StartCoroutine(GraphComboRoutine(graph, target, decision.input, elapsed, version));
            else
                FinishGraphAction();
        }

        void FinishGraphAction()
        {
            DeactivateActiveHitbox();
            motor.MovementLocked = false;
            attackRoutine = null;
            runtimeChargeStage = PlayerChargeStage.None;
            runtimeCurrentDecisionId = string.Empty;
            runtimeDecisionProgress = 0f;
            runtimePreviewStatus = "连招结束";
        }

        sealed class ChargeResult
        {
            public bool completed;
            public float normalized;
        }

        IEnumerator ChargeRoutine(PlayerComboMove instance, PlayerInputCommand command, float initialTime,
            ChargeResult result, int version)
        {
            var minimum = Mathf.Max(0f, instance.minimumChargeTime);
            var maximum = Mathf.Max(minimum, instance.maximumChargeTime);
            var elapsed = Mathf.Clamp(initialTime, 0f, maximum);
            var fullChargeElapsed = 0f;
            var playerChargeStart = Resources.Load<AudioClip>("Audio/Player_Charge_Start");
            var playerChargeFull = Resources.Load<AudioClip>("Audio/Player_Charge_Full");
            var visualReadyThreshold = maximum > 0f
                ? Mathf.Clamp01((minimum + Mathf.Clamp01(instance.launchChargeThreshold) * (maximum - minimum)) / maximum)
                : Mathf.Clamp01(instance.launchChargeThreshold);
            if (instance.showChargeEffect)
            {
                var offset = instance.chargeEffectOffset;
                var visual = GetComponentInChildren<SpriteRenderer>(true);
                var presentationPrefab = instance.chargeEffectPrefab
                    ? instance.chargeEffectPrefab.GetComponent<ChargeTelegraphPresentation>()
                    : null;
                if (presentationPrefab)
                {
                    var effectObject = Instantiate(instance.chargeEffectPrefab, transform);
                    effectObject.transform.localPosition = Vector3.zero;
                    activeChargePresentation = effectObject.GetComponent<ChargeTelegraphPresentation>();
                    activeChargePresentation.Bind(visual);
                    ownsActiveChargePresentation = true;
                    activeChargePresentation.Begin(null, offset, motor.FacingRight, null, 0f, visualReadyThreshold,
                        playerChargeStart, playerChargeFull, false);
                }
                else
                {
                    activeChargePresentation = ChargeTelegraphPresentation.Ensure(gameObject, visual);
                    ownsActiveChargePresentation = false;
                    var presentationSettings = instance.chargePresentation ?? new ChargeTelegraphSettings();
                    presentationSettings.pulseSpeed = instance.chargeEffectPulseSpeed;
                    presentationSettings.pulseAmount = instance.chargeEffectPulseAmount;
                    presentationSettings.particleIntensity = instance.chargeEffectParticleIntensity;
                    presentationSettings.rotationSpeed = instance.chargeEffectRotationSpeed;
                    presentationSettings.coreStartScale = instance.chargeEffectScale * instance.chargeEffectStartScale;
                    presentationSettings.coreFullScale = instance.chargeEffectScale * instance.chargeEffectFullScale;
                    var customRenderer = instance.chargeEffectPrefab
                        ? instance.chargeEffectPrefab.GetComponentInChildren<SpriteRenderer>(true)
                        : null;
                    activeChargePresentation.Begin(presentationSettings, offset, motor.FacingRight,
                        customRenderer ? customRenderer.sprite : null, 0f, visualReadyThreshold,
                        playerChargeStart, playerChargeFull, false);
                }
            }

            var countdownPrefab = instance.chargeCountdownPrefab
                ? instance.chargeCountdownPrefab
                : Resources.Load<GameObject>("Effects/PlayerChargeCountdownUI");
            if (countdownPrefab && countdownPrefab.GetComponent<ChargeCountdownPresentation>())
            {
                var countdownObject = Instantiate(countdownPrefab, transform);
                countdownObject.transform.localPosition = Vector3.zero;
                activeChargeCountdown = countdownObject.GetComponent<ChargeCountdownPresentation>();
                ownsActiveChargeCountdown = true;
            }
            else
            {
                activeChargeCountdown = ChargeCountdownPresentation.Ensure(gameObject);
                ownsActiveChargeCountdown = false;
            }
            if (activeChargeCountdown)
                activeChargeCountdown.Begin(instance.chargeCountdownOffset, motor.FacingRight);

            runtimeChargeStage = PlayerChargeStage.Charging;
            while (version == actionVersion && input.IsHeld(command) &&
                   (elapsed < maximum ||
                    (!instance.autoReleaseAtFullCharge &&
                     fullChargeElapsed < Mathf.Max(0.05f, instance.fullChargeHoldLimit))))
            {
                if (elapsed < maximum)
                    elapsed = Mathf.Min(maximum, elapsed + Time.deltaTime);
                else
                    fullChargeElapsed += Time.deltaTime;
                runtimeChargeSeconds = elapsed;
                runtimeChargeNormalized = maximum <= minimum ? 1f : Mathf.Clamp01((elapsed - minimum) / (maximum - minimum));
                runtimeDamageMultiplier = Mathf.Lerp(1f, Mathf.Max(1f, instance.fullChargeDamageMultiplier), runtimeChargeNormalized);
                runtimeChargeStage = ResolveChargeStage(runtimeChargeNormalized, instance.launchChargeThreshold);
                if (activeChargePresentation)
                    activeChargePresentation.SetProgress(maximum > 0f ? elapsed / maximum : 1f, motor.FacingRight);
                if (activeChargeCountdown)
                {
                    var chargingProgress = maximum > 0f ? elapsed / maximum : 1f;
                    var holdRemaining = 1f - fullChargeElapsed / Mathf.Max(0.05f, instance.fullChargeHoldLimit);
                    activeChargeCountdown.SetProgress(
                        elapsed < maximum ? chargingProgress : Mathf.Clamp01(holdRemaining),
                        elapsed >= maximum,
                        motor.FacingRight);
                }
                yield return null;
            }

            if (version != actionVersion)
            {
                result.completed = false;
                yield break;
            }

            if (activeChargePresentation)
            {
                activeChargePresentation.SetProgress(maximum > 0f ? elapsed / maximum : 1f, motor.FacingRight);
                activeChargePresentation.End(ChargeTelegraphEndReason.Released, motor.FacingRight);
                if (ownsActiveChargePresentation)
                    Destroy(activeChargePresentation.gameObject, 0.4f);
                activeChargePresentation = null;
                ownsActiveChargePresentation = false;
            }
            EndChargeCountdown();

            result.completed = true;
            result.normalized = maximum <= minimum
                ? 1f
                : Mathf.Clamp01((elapsed - minimum) / (maximum - minimum));
        }

        static PlayerChargeStage ResolveChargeStage(float normalized, float launchThreshold)
        {
            if (normalized >= 1f) return PlayerChargeStage.Full;
            return normalized >= Mathf.Clamp01(launchThreshold)
                ? PlayerChargeStage.LaunchReady
                : PlayerChargeStage.Charging;
        }

        void CancelChargePresentation(ChargeTelegraphEndReason reason)
        {
            if (activeChargePresentation)
            {
                activeChargePresentation.End(reason, motor && motor.FacingRight);
                if (ownsActiveChargePresentation)
                    Destroy(activeChargePresentation.gameObject, 0.4f);
                activeChargePresentation = null;
                ownsActiveChargePresentation = false;
            }
            EndChargeCountdown();
            runtimeChargeSeconds = 0f;
            runtimeChargeNormalized = 0f;
            runtimeChargeStage = PlayerChargeStage.None;
        }

        void EndChargeCountdown()
        {
            if (!activeChargeCountdown) return;
            activeChargeCountdown.End();
            if (ownsActiveChargeCountdown)
                Destroy(activeChargeCountdown.gameObject, 0.2f);
            activeChargeCountdown = null;
            ownsActiveChargeCountdown = false;
        }

        IEnumerator GraphMoveRoutine(
            PlayerComboGraph graph,
            PlayerComboMove instance,
            PlayerComboStep sourceMove,
            SelectedTransition selection,
            PlayerInputCommand chargeCommand,
            float initialChargeTime,
            int version)
        {
            var combatTuning = tuning.combat;
            var elapsed = 0f;
            var speed = Mathf.Max(0.05f, instance.animationSpeed);
            var move = ResolveMove(sourceMove, instance, 0f);
            var totalDuration = (Mathf.Max(0f, sourceMove.startup) + Mathf.Max(0f, sourceMove.activeTime) + Mathf.Max(0f, sourceMove.recovery)) / speed;
            var chargeFrameRate = sourceMove.animationClip
                ? Mathf.Max(1f, sourceMove.animationClip.frameRate)
                : Mathf.Max(1, sourceMove.animationFrameRate);
            var chargeClipDuration = sourceMove.animationClip
                ? Mathf.Max(0.0001f, sourceMove.animationClip.length)
                : Mathf.Max(0.0001f, (Mathf.Max(1, sourceMove.animationFrameCount) - 1) / chargeFrameRate);
            var maxChargeFrame = Mathf.Max(0, Mathf.FloorToInt(chargeClipDuration * chargeFrameRate));
            var chargeFrame = Mathf.Clamp(instance.chargeHoldFrame, 0, maxChargeFrame);
            var chargeHoldNormalized = Mathf.Clamp01((chargeFrame / chargeFrameRate) / chargeClipDuration);
            var chargeHoldTime = totalDuration * chargeHoldNormalized;
            var chargeHandled = !instance.enableCharge;
            var attackCuePlayed = false;
            var outgoing = BuildTransitionStates(graph, instance.id);
            var inputDecision = FindSourceDecision(graph, instance.id);
            var decisionSawPress = false;
            var decisionSawRelease = false;
            var decisionResolved = inputDecision == null;
            var decisionPressedAt = 0f;
            var sourceFrameRate = Mathf.Max(1, sourceMove.animationFrameRate);
            var finalSourceFrame = Mathf.Max(0, Mathf.FloorToInt(totalDuration * speed * sourceFrameRate));
            var decisionListenFrame = inputDecision != null
                ? Mathf.Clamp(inputDecision.listenStartFrame, 0, finalSourceFrame)
                : 0;
            var decisionResolveFrame = inputDecision != null
                ? Mathf.Clamp(inputDecision.decisionFrame, decisionListenFrame, finalSourceFrame)
                : 0;
            currentComboMoveHitConfirmed = false;
            acceptingComboHitConfirm = true;

            if (move.lockMovement)
                motor.MovementLocked = true;
            animationDriver.ForceState(PlayerActionState.Attack);
            if (move.animationClip)
                animationDriver.PlayActionClip(move.animationClip, totalDuration);
            else
                animationDriver.ForceState(move.animationState);

            while (elapsed < totalDuration)
            {
                if (!chargeHandled && elapsed >= chargeHoldTime)
                {
                    chargeHandled = true;
                    animationDriver.SetActionClipPaused(true);
                    runtimePreviewStatus = "蓄力定格";
                    var chargeResult = new ChargeResult();
                    yield return StartCoroutine(ChargeRoutine(instance, chargeCommand, initialChargeTime, chargeResult, version));
                    if (!chargeResult.completed || version != actionVersion)
                        yield break;
                    animationDriver.SetActionClipPaused(false);
                    move = ResolveMove(sourceMove, instance, chargeResult.normalized);
                    runtimePreviewStatus = "释放重击";
                }

                var moveElapsed = elapsed * speed;
                if (!attackCuePlayed && moveElapsed >= Mathf.Max(0f, move.startup))
                {
                    attackCuePlayed = true;
                    AttackActivated?.Invoke(move.moveCategory, move.attackType);
                }
                UpdateBodyState(move, moveElapsed);
                ApplyHitboxFrame(move, combatTuning, moveElapsed);

                var selectedDecisionBranch = false;
                if (!decisionResolved)
                {
                    var sourceFrame = Mathf.FloorToInt(Mathf.Max(0f, moveElapsed) * sourceFrameRate);
                    if (sourceFrame >= decisionListenFrame && sourceFrame <= decisionResolveFrame)
                    {
                        runtimeCurrentDecisionId = inputDecision.id;
                        runtimeDecisionProgress = decisionResolveFrame <= decisionListenFrame
                            ? 1f
                            : Mathf.InverseLerp(decisionListenFrame, decisionResolveFrame, sourceFrame);
                        runtimePreviewStatus = "监听输入：松开点按 / 保持长按";
                        if (input.WasPressed(inputDecision.input))
                        {
                            decisionSawPress = true;
                            decisionPressedAt = elapsed;
                        }
                        if (decisionSawPress && input.WasReleased(inputDecision.input))
                            decisionSawRelease = true;
                    }

                    if (sourceFrame >= decisionResolveFrame)
                    {
                        decisionResolved = true;
                        var branch = decisionSawPress && input.IsHeld(inputDecision.input)
                            ? "hold"
                            : decisionSawRelease ? "tap" : "none";
                        var target = ResolveDecisionTarget(inputDecision, branch);
                        var requirementMet = DecisionRequirementMet(inputDecision, branch);
                        runtimeCurrentDecisionId = string.Empty;
                        runtimeDecisionProgress = 1f;
                        runtimeLastTransitionId = DecisionRuntimeId(inputDecision.id, branch);
                        if (!requirementMet)
                        {
                            runtimePreviewStatus = "分支未通过：上一招没有命中";
                            target = inputDecision.noInputTargetInstanceId;
                        }
                        else
                        {
                            runtimePreviewStatus = branch == "hold" ? "判定：长按" :
                                branch == "tap" ? "判定：点按" : "判定：无输入";
                        }

                        if (!string.IsNullOrEmpty(target))
                        {
                            selection.targetInstanceId = target;
                            selection.runtimeId = DecisionRuntimeId(inputDecision.id, branch);
                            selection.heldDuration = branch == "hold"
                                ? Mathf.Max(0f, elapsed - decisionPressedAt)
                                : 0f;
                            selectedDecisionBranch = true;
                        }
                    }
                }

                if (selectedDecisionBranch)
                    break;

                if (inputDecision == null)
                {
                    if (selection.transition == null && elapsed > 0f)
                        EvaluateTransitions(outgoing, moveElapsed, selection);
                    else if (selection.transition != null && selection.transition.condition == ComboInputCondition.Hold && input.IsHeld(selection.transition.input))
                        selection.heldDuration += Time.deltaTime;
                }
                elapsed += Time.deltaTime;
                yield return null;
            }

            var decisionElapsed = totalDuration * speed;
            if (inputDecision == null && selection.transition == null)
                ResolveBufferedTransition(outgoing, decisionElapsed, selection);
            acceptingComboHitConfirm = false;

            DeactivateActiveHitbox();
            if (bodyStateController)
                bodyStateController.ClearBodyState(this);
            if (move.animationClip)
            {
                animationDriver.StopActionClip();
                animationDriver.ClearForcedState(PlayerActionState.Attack);
            }
            else
                animationDriver.ClearForcedState(move.animationState);
        }

        List<TransitionRuntimeState> BuildTransitionStates(PlayerComboGraph graph, string instanceId)
        {
            var result = new List<TransitionRuntimeState>();
            if (graph.transitions == null) return result;
            for (var i = 0; i < graph.transitions.Count; i++)
            {
                var transition = graph.transitions[i];
                if (transition != null && transition.fromInstanceId == instanceId)
                    result.Add(new TransitionRuntimeState { transition = transition });
            }
            return result;
        }

        void EvaluateTransitions(List<TransitionRuntimeState> states, float elapsed, SelectedTransition selection)
        {
            for (var i = 0; i < states.Count; i++)
            {
                var state = states[i];
                var link = state.transition;
                var insideWindow = elapsed >= link.windowStart && elapsed <= link.windowEnd;
                if (!state.tracking && !insideWindow)
                    continue;

                if (insideWindow && input.WasPressed(link.input))
                {
                    state.tracking = true;
                    state.pressedAt = elapsed;
                    if (link.condition == ComboInputCondition.Press)
                    {
                        if (TransitionRequirementsMet(link))
                        {
                            selection.transition = link;
                            return;
                        }
                    }
                }

                var heldFor = state.tracking ? Mathf.Max(0f, elapsed - state.pressedAt) : 0f;
                if (link.condition == ComboInputCondition.Tap && state.tracking && input.WasReleased(link.input))
                {
                    if (TransitionRequirementsMet(link))
                    {
                        selection.transition = link;
                        selection.heldDuration = heldFor;
                        return;
                    }
                }
                if (link.condition == ComboInputCondition.Release && insideWindow && input.WasReleased(link.input))
                {
                    if (TransitionRequirementsMet(link))
                    {
                        selection.transition = link;
                        selection.heldDuration = heldFor;
                        return;
                    }
                }
            }
        }

        void ResolveBufferedTransition(List<TransitionRuntimeState> states, float elapsed, SelectedTransition selection)
        {
            for (var i = 0; i < states.Count; i++)
            {
                var state = states[i];
                if (!state.tracking || state.transition.condition != ComboInputCondition.Hold ||
                    !input.IsHeld(state.transition.input) || !TransitionRequirementsMet(state.transition))
                    continue;
                selection.transition = state.transition;
                selection.heldDuration = Mathf.Max(0f, elapsed - state.pressedAt);
                return;
            }

            for (var i = 0; i < states.Count; i++)
            {
                var state = states[i];
                if (!state.tracking || state.transition.condition != ComboInputCondition.Tap ||
                    input.IsHeld(state.transition.input) || !TransitionRequirementsMet(state.transition))
                    continue;
                selection.transition = state.transition;
                selection.heldDuration = Mathf.Max(0f, elapsed - state.pressedAt);
                return;
            }
        }

        bool TransitionRequirementsMet(PlayerComboTransition transition)
        {
            return transition != null && (!transition.requiresHit || currentComboMoveHitConfirmed);
        }

        PlayerComboStep ResolveMove(PlayerComboStep source, PlayerComboMove instance, float chargeNormalized)
        {
            var result = CloneMove(source);
            var chargeDamage = Mathf.Lerp(1f, Mathf.Max(1f, instance.fullChargeDamageMultiplier), chargeNormalized);
            var chargeKnockback = Mathf.Lerp(1f, Mathf.Max(1f, instance.fullChargeKnockbackMultiplier), chargeNormalized);
            result.damageMultiplier *= Mathf.Max(0f, instance.damageMultiplier) * chargeDamage;
            result.knockbackMultiplier *= Mathf.Max(0f, instance.knockbackMultiplier) * chargeKnockback;
            result.poiseDamage *= Mathf.Max(0f, instance.poiseMultiplier);
            if (instance.overrideAttackType)
                result.attackType = instance.attackType;
            if (instance.overrideTargetReaction)
            {
                result.enableTargetReaction = instance.enableTargetReaction;
                result.targetReaction = instance.targetReaction;
                result.useCustomKnockback = instance.useCustomKnockback;
                result.customKnockback = instance.customKnockback;
            }
            if (instance.enableCharge && result.targetReaction == HitReactionType.Launch &&
                chargeNormalized < Mathf.Clamp01(instance.launchChargeThreshold))
            {
                result.targetReaction = instance.insufficientLaunchReaction == HitReactionType.Launch
                    ? HitReactionType.HeavyHurt
                    : instance.insufficientLaunchReaction;
            }
            return result;
        }

        static PlayerComboStep CloneMove(PlayerComboStep source)
        {
            return new PlayerComboStep
            {
                name = source.name,
                moveCategory = source.moveCategory,
                comboCategory = source.comboCategory,
                animationClip = source.animationClip,
                animationState = source.animationState,
                animationFrameRate = source.animationFrameRate,
                animationFrameCount = source.animationFrameCount,
                mirrorHitboxByFacing = source.mirrorHitboxByFacing,
                hitboxKeys = source.hitboxKeys,
                attackType = source.attackType,
                enableTargetReaction = source.enableTargetReaction,
                targetReaction = source.targetReaction,
                useCustomKnockback = source.useCustomKnockback,
                customKnockback = source.customKnockback,
                interruptPower = source.interruptPower,
                poiseDamage = source.poiseDamage,
                breaksSuperArmor = source.breaksSuperArmor,
                hitFeedback = source.hitFeedback,
                startup = source.startup,
                activeTime = source.activeTime,
                recovery = source.recovery,
                damageMultiplier = source.damageMultiplier,
                knockbackMultiplier = source.knockbackMultiplier,
                lockMovement = source.lockMovement,
                bodyState = source.bodyState,
                bodyStateStart = source.bodyStateStart,
                bodyStateEnd = source.bodyStateEnd
            };
        }

        PlayerMoveTemplate FindMoveTemplate(string id)
        {
            if (moveLibrary == null) return null;
            for (var i = 0; i < moveLibrary.Count; i++)
                if (moveLibrary[i] != null && moveLibrary[i].id == id)
                    return moveLibrary[i];
            return null;
        }

        static PlayerComboMove FindInstance(PlayerComboGraph graph, string id)
        {
            if (graph == null || graph.instances == null) return null;
            for (var i = 0; i < graph.instances.Count; i++)
                if (graph.instances[i] != null && graph.instances[i].id == id)
                    return graph.instances[i];
            return null;
        }

        static PlayerComboInputDecision FindDecision(PlayerComboGraph graph, string id)
        {
            if (graph == null || graph.inputDecisions == null || string.IsNullOrEmpty(id)) return null;
            for (var i = 0; i < graph.inputDecisions.Count; i++)
            {
                var decision = graph.inputDecisions[i];
                if (decision != null && decision.id == id)
                    return decision;
            }
            return null;
        }

        static PlayerComboInputDecision FindSourceDecision(PlayerComboGraph graph, string sourceInstanceId)
        {
            if (graph == null || graph.inputDecisions == null) return null;
            for (var i = 0; i < graph.inputDecisions.Count; i++)
            {
                var decision = graph.inputDecisions[i];
                if (decision != null && decision.enabled && decision.sourceInstanceId == sourceInstanceId)
                    return decision;
            }
            return null;
        }

        static string ResolveDecisionTarget(PlayerComboInputDecision decision, string branch)
        {
            if (decision == null) return string.Empty;
            if (branch == "hold") return decision.holdTargetInstanceId;
            if (branch == "tap") return decision.tapTargetInstanceId;
            return decision.noInputTargetInstanceId;
        }

        bool DecisionRequirementMet(PlayerComboInputDecision decision, string branch)
        {
            if (decision == null) return false;
            var requiresHit = branch == "hold" ? decision.holdRequiresHit :
                branch == "tap" ? decision.tapRequiresHit : decision.noInputRequiresHit;
            return !requiresHit || currentComboMoveHitConfirmed;
        }

        static string DecisionRuntimeId(string decisionId, string branch)
        {
            return decisionId + ":" + branch;
        }

        void EnsureMoveComboData()
        {
            if (moveLibrary == null) moveLibrary = new List<PlayerMoveTemplate>();
            if (comboGraphs == null) comboGraphs = new List<PlayerComboGraph>();
            if (moveComboSchemaVersion < 1 && comboSets != null && comboSets.Count > 0)
            {
                MigrateLegacyCombosToGraphs();
                moveComboSchemaVersion = 1;
            }
            if (moveComboSchemaVersion < 2)
            {
                for (var i = 0; i < comboGraphs.Count; i++)
                {
                    var graph = comboGraphs[i];
                    if (graph != null && graph.weaponType == PlayerWeaponType.Unarmed && graph.instances.Count >= 3 &&
                        !graph.transitions.Exists(x => x.condition == ComboInputCondition.Tap))
                        ConfigureDefaultPunchBranch(graph);
                }
                moveComboSchemaVersion = 2;
            }
            if (moveComboSchemaVersion < 3)
            {
                BakeIndependentComboMoves();
                moveComboSchemaVersion = 3;
            }
            if (moveComboSchemaVersion < 4)
            {
                moveLibrary.Clear();
                moveComboSchemaVersion = 4;
            }
            if (moveComboSchemaVersion < 5)
            {
                InitializeComboGraphPositions();
                moveComboSchemaVersion = 5;
            }
            if (moveComboSchemaVersion < 6)
            {
                RequireHitForChargeTransitions();
                moveComboSchemaVersion = 6;
            }
            if (moveComboSchemaVersion < 7)
            {
                MigrateInputDecisionNodes();
                moveComboSchemaVersion = 7;
            }
            EnsureMoveComboIds();
        }

        void MigrateInputDecisionNodes()
        {
            for (var graphIndex = 0; graphIndex < comboGraphs.Count; graphIndex++)
            {
                var graph = comboGraphs[graphIndex];
                if (graph == null || graph.instances == null || graph.instances.Count == 0) continue;
                if (graph.inputDecisions == null) graph.inputDecisions = new List<PlayerComboInputDecision>();
                if (graph.inputDecisions.Count > 0) continue;

                PlayerComboInputDecision firstChargeDecision = null;
                var migratedTransitions = new List<PlayerComboTransition>();
                for (var transitionIndex = 0; transitionIndex < graph.transitions.Count; transitionIndex++)
                {
                    var hold = graph.transitions[transitionIndex];
                    if (hold == null || hold.condition != ComboInputCondition.Hold) continue;
                    var chargeTarget = FindInstance(graph, hold.toInstanceId);
                    var source = FindInstance(graph, hold.fromInstanceId);
                    if (chargeTarget == null || !chargeTarget.enableCharge || source == null) continue;

                    var tap = graph.transitions.Find(candidate => candidate != null &&
                        candidate.fromInstanceId == hold.fromInstanceId &&
                        candidate.input == hold.input && candidate.condition == ComboInputCondition.Tap);
                    var frameRate = source.move != null ? Mathf.Max(1, source.move.animationFrameRate) : 12;
                    var decision = new PlayerComboInputDecision
                    {
                        id = "decision-branch-" + graphIndex + "-" + graph.inputDecisions.Count,
                        name = "连招点按 / 长按",
                        sourceInstanceId = source.id,
                        input = hold.input,
                        listenStartFrame = Mathf.Max(0, Mathf.RoundToInt(hold.windowStart * frameRate)),
                        decisionFrame = Mathf.Max(0, Mathf.RoundToInt(hold.windowEnd * frameRate)),
                        tapTargetInstanceId = tap != null ? tap.toInstanceId : string.Empty,
                        holdTargetInstanceId = hold.toInstanceId,
                        tapRequiresHit = tap != null && tap.requiresHit,
                        holdRequiresHit = hold.requiresHit,
                        graphPosition = new Vector2(source.graphPosition.x + 180f, source.graphPosition.y)
                    };
                    graph.inputDecisions.Add(decision);
                    if (firstChargeDecision == null) firstChargeDecision = decision;
                    migratedTransitions.Add(hold);
                    if (tap != null) migratedTransitions.Add(tap);
                }

                for (var i = 0; i < migratedTransitions.Count; i++)
                    graph.transitions.Remove(migratedTransitions[i]);

                if (firstChargeDecision == null) continue;
                for (var i = 0; i < graph.instances.Count; i++)
                    graph.instances[i].graphPosition += Vector2.right * 190f;
                for (var i = 0; i < graph.inputDecisions.Count; i++)
                {
                    var source = FindInstance(graph, graph.inputDecisions[i].sourceInstanceId);
                    if (source != null)
                        graph.inputDecisions[i].graphPosition = source.graphPosition + Vector2.right * 180f;
                }

                var entry = FindInstance(graph, graph.entryInstanceId);
                var entryDecision = new PlayerComboInputDecision
                {
                    id = "decision-entry-" + graphIndex,
                    name = "起手点按 / 长按",
                    sourceInstanceId = string.Empty,
                    input = graph.entryInput,
                    listenStartFrame = 0,
                    decisionFrame = 2,
                    entryFrameRate = 60,
                    tapTargetInstanceId = graph.entryInstanceId,
                    holdTargetInstanceId = firstChargeDecision.holdTargetInstanceId,
                    holdRequiresHit = false,
                    graphPosition = entry != null
                        ? new Vector2(Mathf.Max(20f, entry.graphPosition.x - 180f), entry.graphPosition.y)
                        : new Vector2(20f, 30f)
                };
                graph.inputDecisions.Insert(0, entryDecision);
                graph.entryDecisionId = entryDecision.id;
            }
        }

        void RequireHitForChargeTransitions()
        {
            for (var graphIndex = 0; graphIndex < comboGraphs.Count; graphIndex++)
            {
                var graph = comboGraphs[graphIndex];
                if (graph == null || graph.transitions == null) continue;
                for (var transitionIndex = 0; transitionIndex < graph.transitions.Count; transitionIndex++)
                {
                    var transition = graph.transitions[transitionIndex];
                    if (transition == null || transition.condition != ComboInputCondition.Hold) continue;
                    var destination = FindInstance(graph, transition.toInstanceId);
                    if (destination != null && destination.enableCharge)
                        transition.requiresHit = true;
                }
            }
        }

        void MigrateLegacyCombosToGraphs()
        {
            moveLibrary.Clear();
            comboGraphs.Clear();
            for (var setIndex = 0; setIndex < comboSets.Count; setIndex++)
            {
                var legacy = comboSets[setIndex];
                if (legacy == null || legacy.steps == null || legacy.steps.Count == 0) continue;
                var graph = new PlayerComboGraph
                {
                    name = legacy.name,
                    weaponType = legacy.weaponType,
                    entryInput = legacy.steps[0].input
                };
                for (var i = 0; i < legacy.steps.Count; i++)
                {
                    var step = legacy.steps[i];
                    if (step == null) continue;
                    var moveId = "legacy-move-" + setIndex + "-" + i;
                    var instanceId = "legacy-instance-" + setIndex + "-" + i;
                    moveLibrary.Add(new PlayerMoveTemplate { id = moveId, name = step.name, move = CloneMove(step) });
                    graph.instances.Add(new PlayerComboMove { id = instanceId, name = step.name, move = CloneMove(step), moveId = moveId });
                    if (string.IsNullOrEmpty(graph.entryInstanceId)) graph.entryInstanceId = instanceId;
                    if (i > 0)
                    {
                        var previous = legacy.steps[i - 1];
                        graph.transitions.Add(new PlayerComboTransition
                        {
                            id = "legacy-link-" + setIndex + "-" + (i - 1),
                            fromInstanceId = "legacy-instance-" + setIndex + "-" + (i - 1),
                            toInstanceId = instanceId,
                            input = step.input,
                            condition = ComboInputCondition.Press,
                            windowStart = previous.comboWindowStart,
                            windowEnd = previous.comboWindowEnd
                        });
                    }
                }
                if (legacy.weaponType == PlayerWeaponType.Unarmed && graph.instances.Count >= 3)
                    ConfigureDefaultPunchBranch(graph);
                comboGraphs.Add(graph);
            }
        }

        static void ConfigureDefaultPunchBranch(PlayerComboGraph graph)
        {
            var source = graph.instances[graph.instances.Count - 2];
            var heavy = graph.instances[graph.instances.Count - 1];
            var heavyLink = graph.transitions.Find(x => x.toInstanceId == heavy.id);
            if (heavyLink == null) return;

            heavy.name = "蓄力重击终结";
            heavy.enableCharge = true;
            heavy.minimumChargeTime = 0.15f;
            heavy.maximumChargeTime = 0.7f;
            heavy.fullChargeDamageMultiplier = 1.5f;
            heavy.fullChargeKnockbackMultiplier = 1.35f;
            heavy.overrideAttackType = true;
            heavy.attackType = SkillAttackType.Heavy;
            heavyLink.condition = ComboInputCondition.Hold;
            heavyLink.holdThreshold = 0.15f;

            var quick = new PlayerComboMove
            {
                id = "legacy-quick-finisher-" + graph.weaponType,
                name = "快速终结",
                moveId = heavy.moveId,
                move = CloneMove(heavy.move),
                damageMultiplier = 0.8f,
                knockbackMultiplier = 0.7f,
                overrideAttackType = true,
                attackType = SkillAttackType.Normal,
                overrideTargetReaction = true,
                enableTargetReaction = true,
                targetReaction = HitReactionType.LightHurt
            };
            graph.instances.Add(quick);
            graph.transitions.Add(new PlayerComboTransition
            {
                id = "legacy-tap-finisher-link-" + graph.weaponType,
                fromInstanceId = source.id,
                toInstanceId = quick.id,
                input = heavyLink.input,
                condition = ComboInputCondition.Tap,
                windowStart = heavyLink.windowStart,
                windowEnd = heavyLink.windowEnd,
                holdThreshold = heavyLink.holdThreshold
            });
        }

        void BakeIndependentComboMoves()
        {
            for (var graphIndex = 0; graphIndex < comboGraphs.Count; graphIndex++)
            {
                var graph = comboGraphs[graphIndex];
                if (graph == null || graph.instances == null) continue;
                for (var i = 0; i < graph.instances.Count; i++)
                {
                    var node = graph.instances[i];
                    if (node == null) continue;
                    var template = FindMoveTemplate(node.moveId);
                    var source = template != null && template.move != null ? template.move : node.move;
                    if (source == null) source = new PlayerComboStep();
                    node.move = ResolveMove(source, node, 0f);
                    node.damageMultiplier = 1f;
                    node.knockbackMultiplier = 1f;
                    node.poiseMultiplier = 1f;
                    node.overrideAttackType = false;
                    node.overrideTargetReaction = false;
                }
            }
            moveLibrary.Clear();
        }

        void InitializeComboGraphPositions()
        {
            for (var graphIndex = 0; graphIndex < comboGraphs.Count; graphIndex++)
            {
                var graph = comboGraphs[graphIndex];
                if (graph == null || graph.instances == null || graph.instances.Count == 0) continue;
                var depth = new int[graph.instances.Count];
                for (var i = 0; i < depth.Length; i++) depth[i] = -1;
                var indexById = new Dictionary<string, int>();
                for (var i = 0; i < graph.instances.Count; i++) indexById[graph.instances[i].id] = i;
                if (indexById.TryGetValue(graph.entryInstanceId, out var entry)) depth[entry] = 0;
                else depth[0] = 0;
                for (var pass = 0; pass < graph.instances.Count; pass++)
                {
                    var changed = false;
                    for (var i = 0; i < graph.transitions.Count; i++)
                    {
                        var link = graph.transitions[i];
                        if (!indexById.TryGetValue(link.fromInstanceId, out var from) || !indexById.TryGetValue(link.toInstanceId, out var to) || depth[from] < 0) continue;
                        var next = depth[from] + 1;
                        if (depth[to] < next) { depth[to] = next; changed = true; }
                    }
                    if (!changed) break;
                }
                var maxDepth = 0;
                for (var i = 0; i < depth.Length; i++) maxDepth = Mathf.Max(maxDepth, depth[i]);
                for (var i = 0; i < depth.Length; i++) if (depth[i] < 0) depth[i] = ++maxDepth;
                var rows = new Dictionary<int, int>();
                for (var i = 0; i < depth.Length; i++) rows[depth[i]] = rows.TryGetValue(depth[i], out var count) ? count + 1 : 1;
                var maxRows = 1;
                foreach (var pair in rows) maxRows = Mathf.Max(maxRows, pair.Value);
                var cursor = new Dictionary<int, int>();
                for (var i = 0; i < graph.instances.Count; i++)
                {
                    var row = cursor.TryGetValue(depth[i], out var current) ? current : 0;
                    cursor[depth[i]] = row + 1;
                    graph.instances[i].graphPosition = new Vector2(35f + depth[i] * 225f, 28f + (row + (maxRows - rows[depth[i]]) * 0.5f) * 105f);
                }
            }
        }

        void EnsureMoveComboIds()
        {
            for (var i = 0; i < moveLibrary.Count; i++)
            {
                if (moveLibrary[i] == null) moveLibrary[i] = new PlayerMoveTemplate();
                if (string.IsNullOrEmpty(moveLibrary[i].id)) moveLibrary[i].id = Guid.NewGuid().ToString("N");
                if (moveLibrary[i].move == null) moveLibrary[i].move = new PlayerComboStep();
                EnsureHitboxKeys(moveLibrary[i].move);
                if (moveLibrary[i].move.hitFeedback == null) moveLibrary[i].move.hitFeedback = new SkillHitFeedbackSettings();
            }
            for (var i = 0; i < comboGraphs.Count; i++)
            {
                var graph = comboGraphs[i];
                if (graph == null) { graph = new PlayerComboGraph(); comboGraphs[i] = graph; }
                if (graph.instances == null) graph.instances = new List<PlayerComboMove>();
                if (graph.transitions == null) graph.transitions = new List<PlayerComboTransition>();
                if (graph.inputDecisions == null) graph.inputDecisions = new List<PlayerComboInputDecision>();
                for (var j = 0; j < graph.instances.Count; j++)
                {
                    if (graph.instances[j] == null) graph.instances[j] = new PlayerComboMove();
                    if (string.IsNullOrEmpty(graph.instances[j].id)) graph.instances[j].id = Guid.NewGuid().ToString("N");
                    if (graph.instances[j].move == null) graph.instances[j].move = new PlayerComboStep();
                    EnsureHitboxKeys(graph.instances[j].move);
                    if (graph.instances[j].move.hitFeedback == null) graph.instances[j].move.hitFeedback = new SkillHitFeedbackSettings();
                }
                for (var j = 0; j < graph.transitions.Count; j++)
                {
                    if (graph.transitions[j] == null) graph.transitions[j] = new PlayerComboTransition();
                    if (string.IsNullOrEmpty(graph.transitions[j].id)) graph.transitions[j].id = Guid.NewGuid().ToString("N");
                }
                for (var j = 0; j < graph.inputDecisions.Count; j++)
                {
                    if (graph.inputDecisions[j] == null) graph.inputDecisions[j] = new PlayerComboInputDecision();
                    if (string.IsNullOrEmpty(graph.inputDecisions[j].id)) graph.inputDecisions[j].id = Guid.NewGuid().ToString("N");
                    graph.inputDecisions[j].decisionFrame = Mathf.Max(graph.inputDecisions[j].listenStartFrame,
                        graph.inputDecisions[j].decisionFrame);
                    graph.inputDecisions[j].entryFrameRate = Mathf.Max(1, graph.inputDecisions[j].entryFrameRate);
                }
                if (string.IsNullOrEmpty(graph.entryInstanceId) && graph.instances.Count > 0)
                    graph.entryInstanceId = graph.instances[0].id;
            }
        }
    }
}
