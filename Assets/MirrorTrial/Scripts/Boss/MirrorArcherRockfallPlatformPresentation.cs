using UnityEngine;

namespace MirrorTrial.Boss
{
    /// <summary>
    /// Reversible platform animation and lift state. NodeCanvas advances this presentation so
    /// gameplay timing remains in the behaviour tree.
    /// </summary>
    [RequireComponent(typeof(MirrorBossActorV2), typeof(Rigidbody2D))]
    public sealed class MirrorArcherRockfallPlatformPresentation : MonoBehaviour
    {
        const int Columns = 6;
        const int Rows = 4;
        const int FrameCount = 24;
        const int FrameWidth = 320;
        const int FrameHeight = 360;

        MirrorBossActorV2 actor;
        Rigidbody2D body;
        SpriteRenderer platformRenderer;
        Sprite[] frames;
        AudioClip blockedHitSound;
        Vector2 groundPosition;
        float liftHeight;
        float surfaceOffset;
        float originalGravityScale;
        RigidbodyType2D originalBodyType;
        float blockedFlash;
        bool wardActive;

        public bool WardActive => wardActive;

        public bool BeginRaise(Texture2D sheet, float pixelsPerUnit, float height,
            float platformSurfaceOffset, AudioClip hitSound, Vector2 raiseStartPoint)
        {
            if (wardActive) CancelAndRestore();
            actor = GetComponent<MirrorBossActorV2>();
            body = GetComponent<Rigidbody2D>();
            if (!actor || !body) return false;

            groundPosition = raiseStartPoint;
            liftHeight = Mathf.Max(0.1f, height);
            surfaceOffset = platformSurfaceOffset;
            originalGravityScale = body.gravityScale;
            originalBodyType = body.bodyType;
            blockedHitSound = hitSound;
            body.velocity = Vector2.zero;
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.position = groundPosition;
            actor.SetRockfallInvulnerable(true);
            wardActive = true;

            if (sheet)
            {
                BuildFrames(sheet, Mathf.Max(1f, pixelsPerUnit));
                var visual = new GameObject("RockfallPlatformWardVfx");
                // Keep the platform surface attached to the actor throughout the motion.
                // Previously the VFX spawned at its final raised position while the rigidbody
                // travelled up independently, which made the two visibly drift apart.
                visual.transform.position = groundPosition - Vector2.up * surfaceOffset;
                platformRenderer = visual.AddComponent<SpriteRenderer>();
                var bossRenderer = actor.GetComponentInChildren<SpriteRenderer>(true);
                if (bossRenderer)
                {
                    platformRenderer.sortingLayerID = bossRenderer.sortingLayerID;
                    platformRenderer.sortingOrder = bossRenderer.sortingOrder - 1;
                }
                platformRenderer.sprite = frames[0];
            }
            return true;
        }

        public void TickRaise(float normalized)
        {
            if (!wardActive) return;
            var t = Smooth(normalized);
            body.position = Vector2.Lerp(groundPosition, groundPosition + Vector2.up * liftHeight, t);
            SyncPlatformToBody();
            SetFrame(Mathf.FloorToInt(Mathf.Clamp01(normalized) * (FrameCount - 1)));
        }

        public void HoldRaised()
        {
            if (!wardActive) return;
            body.position = groundPosition + Vector2.up * liftHeight;
            SyncPlatformToBody();
            SetFrame(FrameCount - 1);
        }

        public void TickLower(float normalized)
        {
            if (!wardActive) return;
            var t = Smooth(normalized);
            body.position = Vector2.Lerp(groundPosition + Vector2.up * liftHeight, groundPosition, t);
            SyncPlatformToBody();
            SetFrame((FrameCount - 1) - Mathf.FloorToInt(Mathf.Clamp01(normalized) * (FrameCount - 1)));
        }

        public void FinishLowering()
        {
            if (!wardActive) return;
            RestoreBody();
            DestroyVisual();
            wardActive = false;
        }

        public void CancelAndRestore()
        {
            if (!wardActive) return;
            RestoreBody();
            DestroyVisual();
            wardActive = false;
        }

        public void PlayBlockedHit()
        {
            if (!wardActive) return;
            blockedFlash = 0.12f;
            if (blockedHitSound)
                AudioSource.PlayClipAtPoint(blockedHitSound, transform.position, 0.72f);
        }

        void Update()
        {
            if (!platformRenderer) return;
            if (blockedFlash > 0f)
            {
                blockedFlash -= Time.deltaTime;
                platformRenderer.color = new Color(1f, 0.86f, 0.48f, 1f);
            }
            else
                platformRenderer.color = Color.white;
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
                var rect = new Rect(column * FrameWidth, rowFromBottom * FrameHeight, FrameWidth, FrameHeight);
                frames[index] = Sprite.Create(sheet, rect, new Vector2(0.5f, 0.5f), pixelsPerUnit,
                    0, SpriteMeshType.FullRect);
                frames[index].name = "MirrorArcherPlatformWard_" + index.ToString("00");
            }
        }

        void SetFrame(int index)
        {
            if (platformRenderer && frames != null && frames.Length == FrameCount)
                platformRenderer.sprite = frames[Mathf.Clamp(index, 0, FrameCount - 1)];
        }

        void SyncPlatformToBody()
        {
            if (platformRenderer && body)
                platformRenderer.transform.position = body.position - Vector2.up * surfaceOffset;
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
        }

        void DestroyVisual()
        {
            if (platformRenderer) Destroy(platformRenderer.gameObject);
            platformRenderer = null;
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
            DestroyVisual();
        }

        static float Smooth(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }
    }
}
