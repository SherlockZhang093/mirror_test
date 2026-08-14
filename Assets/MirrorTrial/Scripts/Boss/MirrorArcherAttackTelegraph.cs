using UnityEngine;

namespace MirrorTrial.Boss
{
    /// <summary>
    /// Presentation-only warning for the mounted archer. Attack landing points are
    /// frozen when the aim locks, then shown as subtle decorated pixel ripples.
    /// Combat timing, damage and projectile movement remain owned by the boss.
    /// </summary>
    public sealed class MirrorArcherAttackTelegraph : MonoBehaviour
    {
        const int MaxIndicators = 8;
        const float ReleaseFadeDuration = 0.14f;
        const int RippleLayers = 2;
        const int RippleFrameCount = 6;
        const int RippleCanvasWidth = 33;
        const int RippleCanvasHeight = 17;

        static readonly int[] RippleRadiusX = { 4, 6, 8, 10, 12, 15 };
        static readonly int[] RippleRadiusY = { 2, 3, 4, 5, 6, 7 };

        static Sprite[] landingRippleFrames;
        static Sprite pixelDotSprite;
        static Texture2D trajectoryTexture;
        static Material trajectoryMaterial;

        readonly SpriteRenderer[] chargeDots = new SpriteRenderer[3];
        readonly SpriteRenderer[] markerRipples = new SpriteRenderer[MaxIndicators * RippleLayers];
        readonly LineRenderer[] trajectoryLines = new LineRenderer[MaxIndicators];
        readonly Vector2[] lockedLandings = new Vector2[MaxIndicators];
        readonly Vector2[] liveLandings = new Vector2[MaxIndicators];
        readonly Vector2[] trajectoryStarts = new Vector2[MaxIndicators];

        MirrorArcherSkillConfig skill;
        Transform origin;
        Transform target;
        Rect airBounds;
        bool facingRight;
        bool targetLocked;
        Vector2 lockedTarget;
        int lockedLandingCount;
        float progress;
        float releaseFade = -1f;

        public static MirrorArcherAttackTelegraph Create(
            Transform owner,
            Transform attackOrigin,
            Transform playerTarget,
            MirrorArcherSkillConfig config,
            Rect bounds,
            bool pointsRight,
            bool isFan = false,
            bool isRain = false)
        {
            var go = new GameObject("MirrorArcher_" + config.type + "_Telegraph");
            go.transform.position = owner ? owner.position : Vector3.zero;
            var result = go.AddComponent<MirrorArcherAttackTelegraph>();
            result.Initialize(attackOrigin ? attackOrigin : owner, playerTarget, config, bounds, pointsRight);
            return result;
        }

        void Initialize(
            Transform attackOrigin,
            Transform playerTarget,
            MirrorArcherSkillConfig config,
            Rect bounds,
            bool pointsRight)
        {
            origin = attackOrigin;
            target = playerTarget;
            skill = config;
            airBounds = bounds;
            facingRight = pointsRight;

            EnsureRuntimeSprites();
            for (var i = 0; i < chargeDots.Length; i++)
                chargeDots[i] = CreateSpriteRenderer("BowCharge_" + i, pixelDotSprite, 92 + i);
            for (var i = 0; i < MaxIndicators; i++)
            {
                trajectoryLines[i] = CreateTrajectoryRenderer("ArrowTrajectory_" + i);
                for (var layer = 0; layer < RippleLayers; layer++)
                {
                    var index = i * RippleLayers + layer;
                    markerRipples[index] = CreateSpriteRenderer(
                        "FixedLandingRipple_" + i + "_" + layer, landingRippleFrames[0], 86 + layer);
                }
            }
            Redraw();
        }

        public void SetProgress(float normalized)
        {
            progress = Mathf.Clamp01(normalized);
        }

        public void LockTarget(Vector2 worldPoint)
        {
            if (targetLocked) return;
            targetLocked = true;
            lockedTarget = worldPoint;
            lockedLandingCount = BuildLandingPoints(origin ? (Vector2)origin.position : worldPoint,
                lockedTarget, lockedLandings);
        }

        public void Release()
        {
            progress = 1f;
            releaseFade = ReleaseFadeDuration;
        }

        public void Cancel()
        {
            Destroy(gameObject);
        }

        void Update()
        {
            if (!origin || skill == null)
            {
                Destroy(gameObject);
                return;
            }

            if (releaseFade >= 0f)
            {
                releaseFade -= Time.deltaTime;
                if (releaseFade <= 0f)
                {
                    Destroy(gameObject);
                    return;
                }
            }
            Redraw();
        }

        void Redraw()
        {
            DisableAll();
            var fade = releaseFade >= 0f
                ? Mathf.Clamp01(releaseFade / ReleaseFadeDuration)
                : 1f;
            var bow = (Vector2)origin.position;
            DrawBowCharge(bow, fade);

            // Landing warnings only appear once their positions are frozen. This
            // prevents the red circles from sliding after they become readable.
            if (!targetLocked || !IsDamagingSkill(skill.type)) return;
            var count = lockedLandingCount;
            if (count <= 0)
            {
                var aim = target ? (Vector2)target.position : bow + FacingDirection() * 4f;
                count = BuildLandingPoints(bow, aim, liveLandings);
                DrawTrajectories(trajectoryStarts, liveLandings, count, fade);
                DrawLandingMarkers(liveLandings, count, fade);
                return;
            }
            DrawTrajectories(trajectoryStarts, lockedLandings, count, fade);
            DrawLandingMarkers(lockedLandings, count, fade);
        }

        void DrawTrajectories(Vector2[] starts, Vector2[] ends, int count, float fade)
        {
            if (skill.type == MirrorArcherSkillType.MountedDive) return;
            var flowSpeed = Mathf.Lerp(0.85f, 2.4f, progress);
            trajectoryMaterial.mainTextureOffset = new Vector2(-Time.unscaledTime * flowSpeed, 0f);
            var pulse = 0.86f + Mathf.Sin(Time.unscaledTime * Mathf.Lerp(7f, 14f, progress)) * 0.14f;
            var alpha = Mathf.Lerp(0.28f, 0.9f, progress) * pulse * fade;
            for (var i = 0; i < Mathf.Min(count, MaxIndicators); i++)
            {
                var line = trajectoryLines[i];
                line.enabled = true;
                line.SetPosition(0, starts[i]);
                line.SetPosition(1, ends[i]);
                line.textureScale = new Vector2(Mathf.Max(1f, Vector2.Distance(starts[i], ends[i]) / 0.55f), 1f);
                var color = new Color(1f, 0.46f, 0.16f, alpha);
                line.startColor = color;
                line.endColor = new Color(1f, 0.18f, 0.08f, alpha * 0.48f);
            }
        }

        void DrawBowCharge(Vector2 bow, float fade)
        {
            if (skill.type == MirrorArcherSkillType.MountedDive) return;

            Color color;
            var count = 1;
            switch (skill.type)
            {
                case MirrorArcherSkillType.LockedShot:
                    color = new Color(1f, 0.78f, 0.2f, 1f);
                    break;
                case MirrorArcherSkillType.FanShot:
                    color = new Color(1f, 0.38f, 0.12f, 1f);
                    count = 3;
                    break;
                case MirrorArcherSkillType.GroundArrowRain:
                    color = new Color(0.9f, 0.24f, 1f, 1f);
                    count = 3;
                    break;
                default:
                    color = new Color(0.2f, 0.82f, 1f, 1f);
                    break;
            }

            var pulse = 0.78f + Mathf.Sin(Time.unscaledTime * Mathf.Lerp(7f, 13f, progress)) * 0.12f;
            var alpha = Mathf.Lerp(0.45f, 1f, progress) * pulse * fade;
            var forward = FacingDirection();
            for (var i = 0; i < count; i++)
            {
                var renderer = chargeDots[i];
                renderer.enabled = true;
                renderer.color = new Color(color.r, color.g, color.b, alpha);
                var offset = Vector2.zero;
                if (skill.type == MirrorArcherSkillType.FanShot)
                    offset = forward * (i * 0.045f) + Vector2.up * ((i - 1) * 0.055f);
                else if (skill.type == MirrorArcherSkillType.GroundArrowRain)
                    offset = Vector2.up * ((i - 1) * 0.06f);
                renderer.transform.position = bow + offset;
                var scale = Mathf.Lerp(0.8f, 1.28f, progress) * (i == 0 ? 1f : 0.72f);
                renderer.transform.localScale = Vector3.one * scale;
            }
        }

        void DrawLandingMarkers(Vector2[] points, int count, float fade)
        {
            var period = Mathf.Lerp(0.78f, 0.42f, progress);
            var progressAlpha = Mathf.Lerp(0.82f, 1.05f, progress);
            for (var i = 0; i < Mathf.Min(count, MaxIndicators); i++)
            {
                for (var layer = 0; layer < RippleLayers; layer++)
                {
                    var phase = Mathf.Repeat(Time.unscaledTime / period + layer * 0.5f + i * 0.073f, 1f);
                    var frame = Mathf.Min(RippleFrameCount - 1,
                        Mathf.FloorToInt(phase * RippleFrameCount));
                    var envelope = Mathf.Sin(phase * Mathf.PI);
                    // Keep the ripple subdued through its wine-red hue, not by
                    // making it nearly transparent against a dark combat floor.
                    var alpha = Mathf.Lerp(0.24f, 0.56f, envelope) * progressAlpha * fade;
                    if (layer == 1) alpha *= 0.78f;

                    var renderer = markerRipples[i * RippleLayers + layer];
                    renderer.enabled = true;
                    renderer.sprite = landingRippleFrames[frame];
                    renderer.transform.position = points[i] + Vector2.up * 0.035f;
                    var scale = skill.type == MirrorArcherSkillType.MountedDive ? 1.18f : 1f;
                    renderer.transform.localScale = Vector3.one * scale;
                    renderer.color = new Color(0.78f, 0.27f, 0.24f, alpha);
                }
            }
        }

        int BuildLandingPoints(Vector2 bow, Vector2 aim, Vector2[] output)
        {
            switch (skill.type)
            {
                case MirrorArcherSkillType.LockedShot:
                    trajectoryStarts[0] = bow;
                    output[0] = PredictLanding(bow, ApplyInitialAngle(AimDirection(bow, aim)));
                    return 1;

                case MirrorArcherSkillType.FanShot:
                {
                    var arrows = Mathf.Clamp(skill.arrowCount, 1, MaxIndicators);
                    var baseDirection = AimDirection(bow, aim);
                    for (var i = 0; i < arrows; i++)
                    {
                        trajectoryStarts[i] = bow;
                        var spread = arrows <= 1 ? 0f : Mathf.Lerp(-skill.arrowAngle * 0.5f,
                            skill.arrowAngle * 0.5f, i / (float)(arrows - 1));
                        var direction = (Vector2)(Quaternion.Euler(0f, 0f, spread) *
                            ApplyInitialAngle(baseDirection));
                        output[i] = PredictLanding(bow, direction);
                    }
                    return arrows;
                }

                case MirrorArcherSkillType.GroundArrowRain:
                {
                    var count = Mathf.Clamp(skill.arrowCount, 1, MaxIndicators);
                    for (var i = 0; i < count; i++)
                    {
                        var t = count <= 1 ? 0.5f : i / (float)(count - 1);
                        var x = Mathf.Clamp(Mathf.Lerp(aim.x - skill.range * 0.5f,
                            aim.x + skill.range * 0.5f, t), airBounds.xMin, airBounds.xMax);
                        var start = new Vector2(x, airBounds.yMax);
                        var direction = (Vector2)(Quaternion.Euler(0f, 0f, skill.initialAngleOffset) * Vector2.down);
                        trajectoryStarts[i] = start;
                        output[i] = PredictLanding(start, direction);
                    }
                    return count;
                }

                case MirrorArcherSkillType.MountedDive:
                    output[0] = GroundAtX(Mathf.Clamp(aim.x, airBounds.xMin, airBounds.xMax));
                    return 1;

                default:
                    return 0;
            }
        }

        Vector2 AimDirection(Vector2 start, Vector2 aim)
        {
            var direction = (aim - start).normalized;
            return direction.sqrMagnitude < 0.001f ? FacingDirection() : direction;
        }

        Vector2 ApplyInitialAngle(Vector2 direction)
        {
            var mirroredAngle = skill.initialAngleOffset * (facingRight ? 1f : -1f);
            return (Vector2)(Quaternion.Euler(0f, 0f, mirroredAngle) * direction);
        }

        Vector2 PredictLanding(Vector2 start, Vector2 direction)
        {
            var groundMask = LayerMask.GetMask("Ground");
            var hit = Physics2D.Raycast(start, direction, Mathf.Max(2f, skill.arrowRange), groundMask);
            if (hit.collider) return hit.point;

            var groundY = GroundAtX(start.x).y;
            var x = targetLocked ? lockedTarget.x : target ? target.position.x :
                start.x + Mathf.Sign(direction.x) * Mathf.Min(skill.arrowRange, 8f);
            if (direction.y < -0.01f)
            {
                var travel = (groundY - start.y) / direction.y;
                if (travel > 0f) x = start.x + direction.x * travel;
            }
            return GroundAtX(Mathf.Clamp(x, airBounds.xMin, airBounds.xMax));
        }

        Vector2 GroundAtX(float x)
        {
            var top = airBounds.yMax + 3f;
            var distance = Mathf.Max(6f, airBounds.height + 8f);
            var hit = Physics2D.Raycast(new Vector2(x, top), Vector2.down, distance, LayerMask.GetMask("Ground"));
            return hit.collider ? hit.point : new Vector2(x, airBounds.yMin);
        }

        Vector2 FacingDirection()
        {
            return facingRight ? Vector2.right : Vector2.left;
        }

        static bool IsDamagingSkill(MirrorArcherSkillType type)
        {
            return type == MirrorArcherSkillType.LockedShot ||
                   type == MirrorArcherSkillType.FanShot ||
                   type == MirrorArcherSkillType.GroundArrowRain ||
                   type == MirrorArcherSkillType.MountedDive;
        }

        void DisableAll()
        {
            foreach (var renderer in chargeDots) if (renderer) renderer.enabled = false;
            foreach (var renderer in markerRipples) if (renderer) renderer.enabled = false;
            foreach (var line in trajectoryLines) if (line) line.enabled = false;
        }

        LineRenderer CreateTrajectoryRenderer(string objectName)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.textureMode = LineTextureMode.Tile;
            line.alignment = LineAlignment.TransformZ;
            line.startWidth = 0.085f;
            line.endWidth = 0.025f;
            line.numCapVertices = 0;
            line.sortingOrder = 84;
            line.sharedMaterial = trajectoryMaterial;
            line.enabled = false;
            return line;
        }

        SpriteRenderer CreateSpriteRenderer(string objectName, Sprite sprite, int sortingOrder)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            renderer.enabled = false;
            return renderer;
        }

        static void EnsureRuntimeSprites()
        {
            if (!trajectoryTexture)
                trajectoryTexture = CreateTrajectoryTexture();
            if (!trajectoryMaterial)
            {
                trajectoryMaterial = new Material(Shader.Find("Sprites/Default"))
                {
                    name = "MirrorArcher_Trajectory_Material",
                    hideFlags = HideFlags.HideAndDontSave
                };
                trajectoryMaterial.mainTexture = trajectoryTexture;
            }
            if (landingRippleFrames == null || landingRippleFrames.Length != RippleFrameCount)
            {
                landingRippleFrames = new Sprite[RippleFrameCount];
                for (var frame = 0; frame < RippleFrameCount; frame++)
                    landingRippleFrames[frame] = CreateRippleFrame(frame);
            }
            if (!pixelDotSprite)
                pixelDotSprite = CreatePixelSprite("MirrorArcher_PixelDot", 3, 3,
                    (x, y) => x == 1 && y == 1);
        }

        static Texture2D CreateTrajectoryTexture()
        {
            const int width = 16;
            const int height = 4;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "MirrorArcher_FlowingDash_Texture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[width * height];
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var inDash = x <= 7;
                var core = y == 1 || y == 2;
                pixels[y * width + x] = inDash
                    ? new Color32(255, 255, 255, core ? (byte)255 : (byte)96)
                    : new Color32(255, 255, 255, 0);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        static Sprite CreateRippleFrame(int frame)
        {
            var radiusX = RippleRadiusX[frame];
            var radiusY = RippleRadiusY[frame];
            var centerX = RippleCanvasWidth / 2;
            var centerY = RippleCanvasHeight / 2;
            var texture = new Texture2D(RippleCanvasWidth, RippleCanvasHeight, TextureFormat.RGBA32, false)
            {
                name = "MirrorArcher_DecoratedRipple_" + frame + "_Texture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color32[RippleCanvasWidth * RippleCanvasHeight];
            for (var y = 0; y < RippleCanvasHeight; y++)
            for (var x = 0; x < RippleCanvasWidth; x++)
            {
                var dx = x - centerX;
                var dy = y - centerY;
                var ringPixel = IsEllipseOutline(dx, dy, radiusX, radiusY) &&
                                !IsCardinalGap(dx, dy, radiusX, radiusY);
                var shardPixel = IsShardPixel(dx, dy, radiusX, radiusY, frame == 0);
                if (shardPixel)
                    pixels[y * RippleCanvasWidth + x] = new Color32(255, 255, 255, 255);
                else if (ringPixel)
                {
                    pixels[y * RippleCanvasWidth + x] = new Color32(255, 255, 255, 255);
                }
                else
                    pixels[y * RippleCanvasWidth + x] = new Color32(0, 0, 0, 0);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            var sprite = Sprite.Create(texture,
                new Rect(0f, 0f, RippleCanvasWidth, RippleCanvasHeight),
                new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect);
            sprite.name = "MirrorArcher_DecoratedRipple_" + frame;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        static bool IsEllipseOutline(int x, int y, int radiusX, int radiusY)
        {
            var outerX = x / (float)radiusX;
            var outerY = y / (float)radiusY;
            if (outerX * outerX + outerY * outerY > 1f) return false;

            var innerRadiusX = Mathf.Max(1f, radiusX - 1.15f);
            var innerRadiusY = Mathf.Max(0.6f, radiusY - 1.05f);
            var innerX = x / innerRadiusX;
            var innerY = y / innerRadiusY;
            return innerX * innerX + innerY * innerY >= 1f;
        }

        static bool IsCardinalGap(int x, int y, int radiusX, int radiusY)
        {
            var horizontalGap = Mathf.Abs(y) <= 1 && Mathf.Abs(Mathf.Abs(x) - radiusX) <= 1;
            var verticalGap = Mathf.Abs(x) <= 1 && Mathf.Abs(Mathf.Abs(y) - radiusY) <= 1;
            return horizontalGap || verticalGap;
        }

        static bool IsShardPixel(int x, int y, int radiusX, int radiusY, bool compact)
        {
            var size = compact ? 0 : 1;
            return ManhattanDistance(x, y, radiusX, 0) <= size ||
                   ManhattanDistance(x, y, -radiusX, 0) <= size ||
                   ManhattanDistance(x, y, 0, radiusY) <= size ||
                   ManhattanDistance(x, y, 0, -radiusY) <= size;
        }

        static int ManhattanDistance(int x, int y, int targetX, int targetY)
        {
            return Mathf.Abs(x - targetX) + Mathf.Abs(y - targetY);
        }

        static Sprite CreatePixelSprite(string name, int width, int height,
            System.Func<int, int, bool> isOpaque)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name + "_Texture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[width * height];
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                pixels[y * width + x] = isOpaque(x, y)
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(0, 0, 0, 0);
            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            var sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
