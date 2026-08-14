using MirrorTrial.Player;
using UnityEngine;

namespace MirrorTrial.Boss
{
    /// <summary>
    /// Owns the reversible stone-platform presentation and its temporary collision rules.
    /// Frames 0-11 raise/lower the platform. Frames 12-23 retract/restore the stone guardrails
    /// while the platform remains fixed at its highest point.
    /// </summary>
    [RequireComponent(typeof(MirrorBossActorV2), typeof(Rigidbody2D))]
    public sealed class MirrorArcherRockfallPlatformPresentation : MonoBehaviour
    {
        const int Columns = 6;
        const int Rows = 4;
        const int FrameCount = 24;
        const int LiftFrameCount = 12;
        const int FrameWidth = 320;
        const int FrameHeight = 360;
        const string PushWarningVfxResourcePath =
            "Effects/MirrorArcherPlatformPushWarningVfx";

        MirrorBossActorV2 actor;
        Rigidbody2D body;
        Collider2D actorCollider;
        SpriteRenderer platformRenderer;
        SpriteMask groundMask;
        Texture2D maskTexture;
        Sprite maskSprite;
        Sprite[] frames;
        MirrorArcherPlatformPushWarningVfx pushWarningVfx;

        BoxCollider2D protectionBlocker;
        BoxCollider2D topPlatform;

        AudioClip blockedHitSound;
        MirrorArcherRockfallArea rockfallArea;
        Transform playerTarget;
        Rigidbody2D playerBody;
        Collider2D playerCollider;
        PlayerMotor playerMotor;

        Vector2 groundPosition;
        Vector2 raisedPosition;
        Vector2 lockedBodyPosition;
        float liftHeight;
        float feetOffset;
        float originalGravityScale;
        RigidbodyType2D originalBodyType;
        float lowerWarningEndsAt;
        bool wardActive;
        bool attackWindowOpen;
        bool lowerPreparationActive;
        bool lowerReady;

        public bool WardActive => wardActive;
        public bool PositionLocked => wardActive;
        public bool AttackWindowOpen => wardActive && attackWindowOpen;

        public bool BeginRaise(Texture2D sheet, float pixelsPerUnit, float height,
            AudioClip hitSound, Vector2 raiseStartPoint, MirrorArcherRockfallArea area,
            Transform target)
        {
            if (wardActive) CancelAndRestore();

            actor = GetComponent<MirrorBossActorV2>();
            body = GetComponent<Rigidbody2D>();
            rockfallArea = area;
            if (!actor || !body || !rockfallArea || !sheet || !BindCollisionChildren())
                return false;

            actorCollider = FindActorCollider();
            feetOffset = actorCollider ? actorCollider.bounds.min.y - body.position.y : -0.65f;
            playerTarget = target;
            CachePlayerComponents();

            groundPosition = raiseStartPoint;
            liftHeight = Mathf.Max(0.1f, height);
            raisedPosition = groundPosition + Vector2.up * liftHeight;
            lockedBodyPosition = groundPosition;
            originalGravityScale = body.gravityScale;
            originalBodyType = body.bodyType;
            blockedHitSound = hitSound;

            body.velocity = Vector2.zero;
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.position = groundPosition;
            actor.SetRockfallInvulnerable(true);

            wardActive = true;
            attackWindowOpen = false;
            lowerPreparationActive = false;
            lowerReady = false;

            BuildFrames(sheet, Mathf.Max(1f, pixelsPerUnit));
            BuildPlatformVisual();
            ShowCompactWard();
            SetProtectionBlocker(true);
            SetTopPlatform(false);
            SetFrame(0);
            SyncPresentation();
            return true;
        }

        public void TickRaise(float normalized)
        {
            if (!wardActive) return;

            normalized = Mathf.Clamp01(normalized);
            if (normalized < 0.5f)
            {
                var liftProgress = normalized * 2f;
                SetLockedPosition(Vector2.Lerp(groundPosition, raisedPosition, Smooth(liftProgress)));
                SetFrame(Mathf.RoundToInt(liftProgress * (LiftFrameCount - 1)));
            }
            else
            {
                var guardrailProgress = (normalized - 0.5f) * 2f;
                SetLockedPosition(raisedPosition);
                SetFrame(LiftFrameCount + Mathf.RoundToInt(
                    guardrailProgress * (FrameCount - LiftFrameCount - 1)));
            }
        }

        public void HoldRaised()
        {
            if (!wardActive) return;

            SetLockedPosition(raisedPosition);
            SetFrame(FrameCount - 1);
            SetProtectionBlocker(false);
            SetTopPlatform(true);
            lowerPreparationActive = false;
            lowerReady = false;
            attackWindowOpen = true;
            actor.SetRockfallInvulnerable(false);
            StopPushWarning();
        }

        /// <summary>
        /// Gives the player a short warning, then pushes them toward the nearest side without
        /// damage. Lowering may start only after the player has left the protection volume.
        /// </summary>
        public bool PrepareForLowering()
        {
            if (!wardActive) return true;
            SetLockedPosition(raisedPosition);
            SetFrame(FrameCount - 1);

            if (!lowerPreparationActive)
            {
                lowerPreparationActive = true;
                lowerWarningEndsAt = Time.time + rockfallArea.DescentWarningDuration;
                attackWindowOpen = false;
                actor.SetRockfallInvulnerable(true);
                if (pushWarningVfx)
                    pushWarningVfx.PlayWarning(rockfallArea.DescentWarningDuration);
                return false;
            }

            if (Time.time < lowerWarningEndsAt) return false;
            if (IsPlayerInsideProtectionRegion())
            {
                PushPlayerTowardNearestSide();
                return false;
            }

            lowerPreparationActive = false;
            lowerReady = true;
            attackWindowOpen = false;
            actor.SetRockfallInvulnerable(true);
            SetTopPlatform(false);
            SetProtectionBlocker(true);
            HoldExpandedWard();
            return true;
        }

        public void TickLower(float normalized)
        {
            if (!wardActive) return;
            if (!lowerReady) EnterLoweringState();

            normalized = Mathf.Clamp01(normalized);
            if (normalized < 0.5f)
            {
                var guardrailProgress = normalized * 2f;
                SetLockedPosition(raisedPosition);
                SetFrame((FrameCount - 1) - Mathf.RoundToInt(
                    guardrailProgress * (FrameCount - LiftFrameCount - 1)));
            }
            else
            {
                var descendProgress = (normalized - 0.5f) * 2f;
                SetLockedPosition(Vector2.Lerp(raisedPosition, groundPosition, Smooth(descendProgress)));
                SetFrame((LiftFrameCount - 1) - Mathf.RoundToInt(
                    descendProgress * (LiftFrameCount - 1)));
            }
        }

        public void FinishLowering()
        {
            if (!wardActive) return;
            SetLockedPosition(groundPosition);
            RestoreBody();
            DestroyPresentation();
            wardActive = false;
        }

        public void CancelAndRestore()
        {
            if (!wardActive) return;
            RestoreBody();
            DestroyPresentation();
            wardActive = false;
        }

        public void PlayBlockedHit()
        {
            if (!wardActive || attackWindowOpen) return;
            if (pushWarningVfx) pushWarningVfx.PlayBlockedHit();
            if (blockedHitSound)
                AudioSource.PlayClipAtPoint(blockedHitSound, transform.position, 0.72f);
        }

        public void KeepBossLocked()
        {
            if (!wardActive || !body) return;
            body.velocity = Vector2.zero;
            body.position = lockedBodyPosition;
        }

        void LateUpdate()
        {
            if (!wardActive) return;
            KeepBossLocked();
            SyncPresentation();
        }

        Collider2D FindActorCollider()
        {
            var colliders = GetComponents<Collider2D>();
            foreach (var candidate in colliders)
            {
                if (candidate && candidate.enabled && !candidate.isTrigger)
                    return candidate;
            }
            return null;
        }

        void CachePlayerComponents()
        {
            if (!playerTarget)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player) playerTarget = player.transform;
            }
            if (!playerTarget) return;

            playerBody = playerTarget.GetComponent<Rigidbody2D>();
            if (!playerBody) playerBody = playerTarget.GetComponentInParent<Rigidbody2D>();
            playerCollider = playerTarget.GetComponent<Collider2D>();
            if (!playerCollider) playerCollider = playerTarget.GetComponentInParent<Collider2D>();
            playerMotor = playerTarget.GetComponent<PlayerMotor>();
            if (!playerMotor) playerMotor = playerTarget.GetComponentInParent<PlayerMotor>();
        }

        void BuildPlatformVisual()
        {
            var visual = new GameObject("RockfallPlatformWardVfx");
            platformRenderer = visual.AddComponent<SpriteRenderer>();
            var bossRenderer = actor.GetComponentInChildren<SpriteRenderer>(true);
            if (bossRenderer)
            {
                platformRenderer.sortingLayerID = bossRenderer.sortingLayerID;
                platformRenderer.sortingOrder = bossRenderer.sortingOrder - 2;
            }
            platformRenderer.sprite = frames[0];
            BuildGroundMask();
            platformRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            BuildPushWarningVfx();
        }

        void BuildPushWarningVfx()
        {
            var prefab = Resources.Load<GameObject>(PushWarningVfxResourcePath);
            if (!prefab)
            {
                Debug.LogWarning("[MirrorArcherRockfallPlatformPresentation] Missing push " +
                    "warning prefab at Resources/" + PushWarningVfxResourcePath + ".", this);
                return;
            }

            var instance = Instantiate(prefab, platformRenderer.transform, false);
            instance.name = "MirrorArcherPlatformPushWarningVfx";
            pushWarningVfx = instance.GetComponent<MirrorArcherPlatformPushWarningVfx>();
            if (!pushWarningVfx)
            {
                Debug.LogError("[MirrorArcherRockfallPlatformPresentation] Push warning " +
                    "prefab is missing MirrorArcherPlatformPushWarningVfx.", this);
                Destroy(instance);
                return;
            }

            pushWarningVfx.ConfigureSorting(platformRenderer.sortingLayerID,
                platformRenderer.sortingOrder + 2);
            pushWarningVfx.StopImmediate();
        }

        void BuildFrames(Texture2D sheet, float pixelsPerUnit)
        {
            DestroySprites();
            frames = new Sprite[FrameCount];
            for (var index = 0; index < FrameCount; index++)
            {
                var column = index % Columns;
                var sourceRow = index / Columns;
                var rowFromBottom = Rows - 1 - sourceRow;
                var rect = new Rect(column * FrameWidth, rowFromBottom * FrameHeight,
                    FrameWidth, FrameHeight);
                frames[index] = Sprite.Create(sheet, rect, new Vector2(0.5f, 0.5f),
                    pixelsPerUnit, 0, SpriteMeshType.FullRect);
                frames[index].name = "MirrorArcherPlatformWard_" + index.ToString("00");
            }
        }

        bool BindCollisionChildren()
        {
            var root = transform.Find("RockfallPlatformColliders");
            var blocker = root ? root.Find("ProtectionBlocker") : null;
            var platform = root ? root.Find("TopPlatform") : null;
            protectionBlocker = blocker ? blocker.GetComponent<BoxCollider2D>() : null;
            topPlatform = platform ? platform.GetComponent<BoxCollider2D>() : null;

            if (protectionBlocker && topPlatform) return true;
            Debug.LogError("[MirrorArcherRockfallPlatformPresentation] Missing prefab children: " +
                           "RockfallPlatformColliders/ProtectionBlocker or TopPlatform.", this);
            protectionBlocker = null;
            topPlatform = null;
            return false;
        }

        void SetFrame(int index)
        {
            if (platformRenderer && frames != null && frames.Length == FrameCount)
                platformRenderer.sprite = frames[Mathf.Clamp(index, 0, FrameCount - 1)];
        }

        void SetLockedPosition(Vector2 position)
        {
            lockedBodyPosition = position;
            KeepBossLocked();
            SyncPresentation();
        }

        void SyncPresentation()
        {
            SyncPlatformToBody();
        }

        void SyncPlatformToBody()
        {
            if (!platformRenderer || !body) return;

            var bossFeetY = body.position.y + feetOffset;
            var surfaceAnchor = rockfallArea ? rockfallArea.PlatformSurfaceAnchor : -0.3f;
            var alignment = rockfallArea ? rockfallArea.PlatformAlignmentOffset : 0f;
            platformRenderer.transform.position = new Vector3(body.position.x,
                bossFeetY + alignment - surfaceAnchor, platformRenderer.transform.position.z);
            SyncGroundMask();
        }

        bool IsPlayerInsideProtectionRegion()
        {
            if (!playerTarget) CachePlayerComponents();
            if (!playerTarget || !protectionBlocker) return false;

            var scale = protectionBlocker.transform.lossyScale;
            var size = new Vector2(
                protectionBlocker.size.x * Mathf.Abs(scale.x),
                protectionBlocker.size.y * Mathf.Abs(scale.y));
            size.x += rockfallArea.PlayerExitPadding * 2f;
            var center = protectionBlocker.transform.TransformPoint(protectionBlocker.offset);
            var region = new Bounds(center, size);
            return playerCollider ? region.Intersects(playerCollider.bounds) :
                region.Contains(playerTarget.position);
        }

        void PushPlayerTowardNearestSide()
        {
            if (!playerTarget || !protectionBlocker) return;
            var centerX = protectionBlocker.transform.TransformPoint(protectionBlocker.offset).x;
            var direction = playerTarget.position.x < centerX ? -1f : 1f;
            var velocity = new Vector2(direction * rockfallArea.PlayerPushSpeed, 0f);

            if (playerMotor)
            {
                playerMotor.ApplyForcedVelocity(velocity, rockfallArea.PlayerPushDuration);
            }
            else if (playerBody)
            {
                playerBody.velocity = velocity;
            }
            else
            {
                playerTarget.position += Vector3.right *
                    (direction * rockfallArea.PlayerPushSpeed * Time.deltaTime);
            }
        }

        void EnterLoweringState()
        {
            lowerPreparationActive = false;
            lowerReady = true;
            attackWindowOpen = false;
            if (actor) actor.SetRockfallInvulnerable(true);
            SetTopPlatform(false);
            SetProtectionBlocker(true);
            HoldExpandedWard();
        }

        void StopPushWarning()
        {
            if (pushWarningVfx) pushWarningVfx.StopImmediate();
        }

        void ShowCompactWard()
        {
            if (pushWarningVfx) pushWarningVfx.ShowCompactImmediate();
        }

        void HoldExpandedWard()
        {
            if (pushWarningVfx) pushWarningVfx.ShowExpandedImmediate();
        }

        void SetProtectionBlocker(bool active)
        {
            if (protectionBlocker) protectionBlocker.enabled = active;
        }

        void SetTopPlatform(bool active)
        {
            if (topPlatform) topPlatform.enabled = active;
        }

        void BuildGroundMask()
        {
            if (!platformRenderer || !rockfallArea) return;
            maskTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "RockfallPlatformGroundMaskTexture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            maskTexture.SetPixel(0, 0, Color.white);
            maskTexture.Apply();
            maskSprite = Sprite.Create(maskTexture, new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f), 1f);
            maskSprite.name = "RockfallPlatformGroundMaskSprite";

            var maskObject = new GameObject("RockfallPlatformGroundMask");
            groundMask = maskObject.AddComponent<SpriteMask>();
            groundMask.sprite = maskSprite;
            groundMask.alphaCutoff = 0.01f;
            groundMask.isCustomRangeActive = true;
            groundMask.frontSortingLayerID = platformRenderer.sortingLayerID;
            groundMask.backSortingLayerID = platformRenderer.sortingLayerID;
            groundMask.frontSortingOrder = platformRenderer.sortingOrder + 1;
            groundMask.backSortingOrder = platformRenderer.sortingOrder - 1;
            SyncGroundMask();
        }

        void SyncGroundMask()
        {
            if (!groundMask || !rockfallArea) return;
            var bounds = rockfallArea.WorldBounds;
            const float extraHeight = 20f;
            var height = bounds.size.y + liftHeight + extraHeight;
            groundMask.transform.position = new Vector3(bounds.center.x,
                bounds.min.y + height * 0.5f,
                platformRenderer ? platformRenderer.transform.position.z : 0f);
            groundMask.transform.localScale = new Vector3(bounds.size.x + 4f, height, 1f);
        }

        void RestoreBody()
        {
            if (body)
            {
                body.position = groundPosition;
                body.velocity = Vector2.zero;
                body.bodyType = originalBodyType;
                body.gravityScale = originalGravityScale;
            }
            if (actor) actor.SetRockfallInvulnerable(false);
            attackWindowOpen = false;
            lowerPreparationActive = false;
            lowerReady = false;
            StopPushWarning();
        }

        void DestroyPresentation()
        {
            SetProtectionBlocker(false);
            SetTopPlatform(false);
            if (platformRenderer) Destroy(platformRenderer.gameObject);
            if (groundMask) Destroy(groundMask.gameObject);
            platformRenderer = null;
            groundMask = null;
            pushWarningVfx = null;
            protectionBlocker = null;
            topPlatform = null;

            if (maskSprite) Destroy(maskSprite);
            if (maskTexture) Destroy(maskTexture);
            maskSprite = null;
            maskTexture = null;
            DestroySprites();
        }

        void DestroySprites()
        {
            if (frames == null) return;
            foreach (var sprite in frames)
                if (sprite) Destroy(sprite);
            frames = null;
        }

        void OnDestroy()
        {
            if (wardActive) RestoreBody();
            DestroyPresentation();
        }

        static float Smooth(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }
    }
}
