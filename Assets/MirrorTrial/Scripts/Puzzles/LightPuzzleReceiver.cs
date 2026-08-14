using System.Collections;
using System.Collections.Generic;
using MirrorTrial.Feedback;
using MirrorTrial.Level;
using MirrorTrial.Player;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Video;

namespace MirrorTrial.Puzzles
{
    [DisallowMultipleComponent]
    public sealed class LightPuzzleReceiver : MonoBehaviour
    {
        [Header("Light Reception")]
        [SerializeField] Transform leftReceivePoint;
        [SerializeField, Min(0.1f)] float chargeDuration = 3f;
        [SerializeField, Min(0.01f)] float receiveRadius = 0.3f;
        [FormerlySerializedAs("minimumRightwardDirection")]
        [SerializeField, Range(0f, 1f)] float minimumHorizontalDirection = 0.01f;
        [SerializeField] bool requireLeftwardDirection;
        [Header("Charge Presentation")]
        [SerializeField] PuzzleChargePresentation chargePresentation;
        [SerializeField] Animator activationAnimator;
        [SerializeField] string activationTrigger = "Activate";
        [SerializeField] VideoClip activationVideoClip;
        [Tooltip("把你后续提供的接收装置动画放到 Animator，并填写上面的 Trigger。")]
        [SerializeField] float activationDelay = 0.65f;
        [SerializeField] SpriteRenderer shrineRenderer;
        [SerializeField] Sprite openedSprite;
        [SerializeField, Range(0f, 1f)] float activationShakePower = 0.45f;
        [Header("Ability Reward")]
        [SerializeField] bool unlockDoubleJump = true;
        [SerializeField] WindLightReceiver windReceiverToStop;
        [SerializeField] AbilityTransferEffect abilityTransferPrefab;
        [SerializeField] string unlockPromptSpeaker = "能力解锁";
        [SerializeField, TextArea(2, 4)] string unlockPromptText = "已获得二段跳能力\n在空中再次按跳跃键即可进行二段跳";
        [SerializeField] StoryTutorialSequence unlockPromptSequence;
        [SerializeField] GameObject lightBlockerVisual;
        [SerializeField] Collider2D lightBlockerCollider;
        [Header("Light Blocker Opening")]
        [SerializeField, Min(0.01f)] float lightBlockerOpenDuration = 1.6f;
        [SerializeField] Vector3 lightBlockerOpenOffset = new Vector3(0f, -2.2f, 0f);
        [SerializeField, Range(0f, 1f)] float lightBlockerOpenShakePower = 0.55f;

        bool activated;
        bool activationCommitted;
        bool receivingValidLight;
        float chargeProgress;
        Coroutine routine;
        VideoPlayer activationVideoPlayer;
        bool videoFinished;
        bool videoFailed;
        bool activationVideoStarted;
        bool activationVideoRunning;
        bool abilityTransferFinished;
        Vector3 lightBlockerClosedPosition;
        SpriteMask lightBlockerMask;
        Sprite lightBlockerMaskSprite;
        Texture2D lightBlockerMaskTexture;

        public Transform BeamPoint => leftReceivePoint ? leftReceivePoint : transform;

        void Awake()
        {
            if (chargePresentation) chargePresentation.ResetPresentation();
            if (!lightBlockerVisual) return;
            lightBlockerClosedPosition = lightBlockerVisual.transform.localPosition;
            lightBlockerVisual.SetActive(true);
            if (!lightBlockerCollider)
                lightBlockerCollider = lightBlockerVisual.GetComponent<Collider2D>();
            if (lightBlockerCollider) lightBlockerCollider.enabled = true;
            CreateLightBlockerMask();
        }

        void CreateLightBlockerMask()
        {
            var blockerRenderer = lightBlockerVisual.GetComponent<SpriteRenderer>();
            if (!blockerRenderer || !blockerRenderer.sprite) return;

            lightBlockerMaskTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "LightBlockerOpeningMaskTexture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            lightBlockerMaskTexture.SetPixel(0, 0, Color.white);
            lightBlockerMaskTexture.Apply();
            lightBlockerMaskSprite = Sprite.Create(
                lightBlockerMaskTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
            lightBlockerMaskSprite.name = "LightBlockerOpeningMaskSprite";

            var maskObject = new GameObject("LightBlockerOpeningMask");
            maskObject.layer = lightBlockerVisual.layer;
            maskObject.transform.SetParent(lightBlockerVisual.transform.parent, false);
            maskObject.transform.localPosition = lightBlockerClosedPosition;
            maskObject.transform.localRotation = lightBlockerVisual.transform.localRotation;

            var blockerSize = blockerRenderer.sprite.bounds.size;
            var blockerScale = lightBlockerVisual.transform.localScale;
            maskObject.transform.localScale = new Vector3(
                blockerSize.x * Mathf.Abs(blockerScale.x),
                blockerSize.y * Mathf.Abs(blockerScale.y),
                1f);

            lightBlockerMask = maskObject.AddComponent<SpriteMask>();
            lightBlockerMask.sprite = lightBlockerMaskSprite;
            lightBlockerMask.alphaCutoff = 0.01f;
            lightBlockerMask.isCustomRangeActive = true;
            lightBlockerMask.frontSortingLayerID = blockerRenderer.sortingLayerID;
            lightBlockerMask.backSortingLayerID = blockerRenderer.sortingLayerID;
            lightBlockerMask.frontSortingOrder = blockerRenderer.sortingOrder + 1;
            lightBlockerMask.backSortingOrder = blockerRenderer.sortingOrder - 1;
            blockerRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        }

        void Update()
        {
            if (activated || activationCommitted) return;

            if (!receivingValidLight)
            {
                if (chargeProgress > 0f) ResetCharge();
                return;
            }

            if (chargeProgress <= 0f)
                BeginActivationVideo();

            chargeProgress = Mathf.Min(chargeDuration, chargeProgress + Time.deltaTime);
            var normalized = Mathf.Clamp01(chargeProgress / Mathf.Max(0.1f, chargeDuration));
            if (chargePresentation) chargePresentation.SetProgress(normalized);

            if (chargeProgress >= chargeDuration)
                CommitActivation();
        }

        public bool TryReceiveBeam(Vector2 origin, Vector2 direction, float maximumDistance,
            out Vector2 receivePoint)
        {
            var center = (Vector2)BeamPoint.position;
            receivePoint = center;
            var invalidDirection = requireLeftwardDirection
                ? direction.x >= -minimumHorizontalDirection
                : direction.x <= minimumHorizontalDirection;
            if (invalidDirection)
                return false;

            var centerDistance = Vector2.Dot(center - origin, direction);
            if (centerDistance < 0f || centerDistance > maximumDistance + receiveRadius)
                return false;

            var closest = origin + direction * centerDistance;
            var perpendicularDistance = Vector2.Distance(closest, center);
            if (perpendicularDistance > receiveRadius)
                return false;

            var distanceToEdge = Mathf.Sqrt(
                Mathf.Max(0f, receiveRadius * receiveRadius -
                               perpendicularDistance * perpendicularDistance));
            var entryDistance = Mathf.Max(0f, centerDistance - distanceToEdge);
            if (entryDistance > maximumDistance)
                return false;

            receivePoint = origin + direction * entryDistance;
            return true;
        }

        void OnDrawGizmosSelected()
        {
            var center = BeamPoint.position;
            Gizmos.color = new Color(1f, 0.62f, 0.12f, 0.9f);
            Gizmos.DrawWireSphere(center, receiveRadius);
        }

        public void SetLit(bool lit)
        {
            if (activated || activationCommitted) return;
            receivingValidLight = lit;
            if (chargePresentation) chargePresentation.SetReceivingLight(lit);
        }

        void CommitActivation()
        {
            if (activationCommitted) return;
            activationCommitted = true;
            receivingValidLight = false;
            if (chargePresentation)
            {
                chargePresentation.SetReceivingLight(false);
                chargePresentation.Complete();
            }
            routine = StartCoroutine(Activate());
        }

        void ResetCharge()
        {
            chargeProgress = 0f;
            StopActivationVideo();
            if (chargePresentation) chargePresentation.ResetPresentation();
        }

        IEnumerator Activate()
        {
            if (activationAnimator && !string.IsNullOrEmpty(activationTrigger))
                activationAnimator.SetTrigger(activationTrigger);
            if (activationShakePower > 0f)
                CameraShakeService.Shake(Vector2.up, activationShakePower);
            if (activationVideoClip && shrineRenderer)
            {
                BeginActivationVideo();
                while (activationVideoRunning)
                    yield return null;
            }
            else
                yield return new WaitForSeconds(activationDelay);

            if (shrineRenderer && openedSprite)
                shrineRenderer.sprite = openedSprite;

            if (unlockDoubleJump)
                yield return PlayAbilityTransfer();

            activated = true;
            routine = null;
            if (unlockDoubleJump) UnlockDoubleJump();
            if (lightBlockerVisual) yield return OpenLightBlocker();
        }

        IEnumerator PlayAbilityTransfer()
        {
            var player = FindObjectOfType<PlayerTuning>();
            if (!abilityTransferPrefab || !player) yield break;

            abilityTransferFinished = false;
            var effect = Instantiate(abilityTransferPrefab, BeamPoint.position,
                Quaternion.identity);
            effect.Begin(player.transform, () => abilityTransferFinished = true);
            while (!abilityTransferFinished && effect)
                yield return null;
        }

        IEnumerator OpenLightBlocker()
        {
            var blockerTransform = lightBlockerVisual.transform;
            var targetPosition = lightBlockerClosedPosition + lightBlockerOpenOffset;
            var elapsed = 0f;
            if (lightBlockerCollider) lightBlockerCollider.enabled = false;
            if (chargePresentation) chargePresentation.PlayDoorOpening();
            if (lightBlockerOpenShakePower > 0f)
                CameraShakeService.Shake(Vector2.down, lightBlockerOpenShakePower);

            while (elapsed < lightBlockerOpenDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var progress = Mathf.Clamp01(elapsed / lightBlockerOpenDuration);
                progress = progress * progress * (3f - 2f * progress);
                blockerTransform.localPosition = Vector3.LerpUnclamped(
                    lightBlockerClosedPosition,
                    targetPosition,
                    progress);
                yield return null;
            }

            blockerTransform.localPosition = targetPosition;
            if (chargePresentation) chargePresentation.StopDoorOpening();
            lightBlockerVisual.SetActive(false);
            if (lightBlockerMask) lightBlockerMask.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (lightBlockerMaskSprite) Destroy(lightBlockerMaskSprite);
            if (lightBlockerMaskTexture) Destroy(lightBlockerMaskTexture);
        }

        IEnumerator PlayActivationVideo()
        {
            activationVideoRunning = true;
            activationVideoPlayer = GetComponent<VideoPlayer>();
            if (!activationVideoPlayer)
                activationVideoPlayer = gameObject.AddComponent<VideoPlayer>();

            activationVideoPlayer.playOnAwake = false;
            activationVideoPlayer.isLooping = false;
            activationVideoPlayer.source = VideoSource.VideoClip;
            activationVideoPlayer.clip = activationVideoClip;
            activationVideoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            activationVideoPlayer.renderMode = VideoRenderMode.MaterialOverride;
            activationVideoPlayer.targetMaterialRenderer = shrineRenderer;
            activationVideoPlayer.targetMaterialProperty = "_MainTex";
            activationVideoPlayer.aspectRatio = VideoAspectRatio.FitInside;

            videoFinished = false;
            videoFailed = false;
            activationVideoPlayer.loopPointReached += OnVideoFinished;
            activationVideoPlayer.errorReceived += OnVideoError;
            activationVideoPlayer.Prepare();

            var prepareDeadline = Time.realtimeSinceStartup + 5f;
            while (!activationVideoPlayer.isPrepared && !videoFailed &&
                   Time.realtimeSinceStartup < prepareDeadline)
                yield return null;

            if (!activationVideoPlayer.isPrepared || videoFailed)
            {
                CleanupVideoPlayer();
                yield return new WaitForSeconds(activationDelay);
                activationVideoRunning = false;
                yield break;
            }

            activationVideoPlayer.Play();
            var playbackDeadline = Time.realtimeSinceStartup +
                                   Mathf.Max(2f, (float)activationVideoClip.length + 2f);
            while (!videoFinished && !videoFailed && Time.realtimeSinceStartup < playbackDeadline)
                yield return null;

            CleanupVideoPlayer();
            activationVideoRunning = false;
        }

        void BeginActivationVideo()
        {
            if (activationVideoStarted || videoFinished || !activationVideoClip || !shrineRenderer) return;
            activationVideoStarted = true;
            StartCoroutine(PlayActivationVideo());
        }

        void StopActivationVideo()
        {
            if (!activationVideoStarted) return;
            activationVideoStarted = false;
            activationVideoRunning = false;
            videoFinished = false;
            videoFailed = false;
            CleanupVideoPlayer();
        }

        void OnVideoFinished(VideoPlayer source) => videoFinished = true;

        void OnVideoError(VideoPlayer source, string message)
        {
            videoFailed = true;
            Debug.LogWarning($"Shrine opening video could not play: {message}", this);
        }

        void CleanupVideoPlayer()
        {
            if (!activationVideoPlayer) return;
            activationVideoPlayer.loopPointReached -= OnVideoFinished;
            activationVideoPlayer.errorReceived -= OnVideoError;
            activationVideoPlayer.Stop();
            activationVideoPlayer.targetMaterialRenderer = null;
            activationVideoStarted = false;
        }

        void UnlockDoubleJump()
        {
            var tuning = FindObjectOfType<PlayerTuning>();
            if (tuning && tuning.abilities != null)
                tuning.abilities.doubleJumpUnlocked = true;

            if (windReceiverToStop)
                windReceiverToStop.ShutDownAirflow();

            if (!unlockPromptSequence)
            {
                var promptObject = new GameObject("DoubleJumpUnlockPrompt");
                promptObject.transform.SetParent(transform, false);
                unlockPromptSequence = promptObject.AddComponent<StoryTutorialSequence>();
                unlockPromptSequence.Configure(
                    "DoubleJump_Ability_Unlocked",
                    null,
                    new List<StoryTutorialStep>
                    {
                        new StoryTutorialStep
                        {
                            type = StoryTutorialStepType.SystemMessage,
                            speaker = unlockPromptSpeaker,
                            text = unlockPromptText
                        }
                    },
                    unlockPromptSpeaker);
            }

            unlockPromptSequence.Play();
        }
    }
}
