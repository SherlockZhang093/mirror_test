using System;
using UnityEngine;

namespace MirrorTrial.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class EnemyLaunchController2D : MonoBehaviour
    {
        public enum LaunchPhase { None, HitCompress, Rising, Airborne, Falling, Landing, Sliding, Knockdown, Recovering }

        Rigidbody2D body;
        Collider2D bodyCollider;
        EnemyLaunchSettings settings;
        LaunchPhase phase;
        ContactFilter2D groundFilter;
        readonly RaycastHit2D[] groundHits = new RaycastHit2D[4];
        float earliestLandingTime;
        float landingEndsAt;
        float phaseEndsAt;
        float groundContactUntil;
        float launchDirectionSign = 1f;
        Vector2 pendingLaunchVelocity;
        bool applyVelocityOnNextFixedUpdate;

        public bool IsLaunching => phase != LaunchPhase.None;
        public bool IsRecoverySuperArmored => phase == LaunchPhase.Landing || phase == LaunchPhase.Sliding ||
                                               phase == LaunchPhase.Knockdown || phase == LaunchPhase.Recovering;
        public EnemyLaunchSettings Settings => settings;
        public event Action<LaunchPhase, Vector2> PhaseChanged;
        public event Action LaunchCompleted;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            bodyCollider = GetComponent<Collider2D>();
            settings = new EnemyLaunchSettings();
            groundFilter.useTriggers = false;
            groundFilter.useLayerMask = true;
            groundFilter.SetLayerMask(LayerMask.GetMask("Ground"));
        }

        public void Configure(EnemyLaunchSettings nextSettings)
        {
            settings = nextSettings ?? new EnemyLaunchSettings();
        }

        public void BeginLaunch(Vector2 velocity)
        {
            launchDirectionSign = velocity.x < 0f ? -1f : 1f;
            SetPhase(LaunchPhase.HitCompress, velocity);
            phaseEndsAt = Time.time + Mathf.Max(0f, settings.hitCompressDuration);
            groundContactUntil = 0f;
            earliestLandingTime = Time.time + 0.06f;
            landingEndsAt = 0f;
            pendingLaunchVelocity = velocity;
            applyVelocityOnNextFixedUpdate = false;
            body.velocity = Vector2.zero;
            body.WakeUp();
        }

        public void CancelLaunch()
        {
            SetPhase(LaunchPhase.None, Vector2.zero);
            groundContactUntil = 0f;
            applyVelocityOnNextFixedUpdate = false;
        }

        void FixedUpdate()
        {
            if (phase == LaunchPhase.None || phase == LaunchPhase.Landing ||
                phase == LaunchPhase.Knockdown || phase == LaunchPhase.Recovering)
                return;

            if (phase == LaunchPhase.HitCompress)
            {
                body.velocity = Vector2.zero;
                if (Time.time < phaseEndsAt) return;
                applyVelocityOnNextFixedUpdate = true;
                SetPhase(LaunchPhase.Rising, pendingLaunchVelocity);
                phaseEndsAt = Time.time + Mathf.Max(0f, settings.takeoffDuration);
            }

            if (phase == LaunchPhase.Sliding)
            {
                var remaining = Mathf.Clamp01((phaseEndsAt - Time.time) / Mathf.Max(0.01f, settings.landingSlideDuration));
                body.velocity = new Vector2(launchDirectionSign * settings.landingSlideSpeed * remaining, body.velocity.y);
                return;
            }

            if (applyVelocityOnNextFixedUpdate)
            {
                applyVelocityOnNextFixedUpdate = false;
                body.velocity = pendingLaunchVelocity;
                body.WakeUp();
                return;
            }

            var grounded = IsGrounded();

            if (body.velocity.y > 0.05f)
            {
                if (phase == LaunchPhase.Rising && Time.time >= phaseEndsAt)
                    SetPhase(LaunchPhase.Airborne, body.velocity);
                return;
            }

            if (phase != LaunchPhase.Falling)
            {
                SetPhase(LaunchPhase.Falling, body.velocity);
            }

            if (Time.time >= earliestLandingTime && grounded)
                BeginLanding();
        }

        void Update()
        {
            if (phase == LaunchPhase.Landing && Time.time >= landingEndsAt)
            {
                phaseEndsAt = Time.time + Mathf.Max(0f, settings.landingSlideDuration);
                SetPhase(LaunchPhase.Sliding, new Vector2(launchDirectionSign, 0f));
            }
            else if (phase == LaunchPhase.Sliding && Time.time >= phaseEndsAt)
            {
                body.velocity = Vector2.zero;
                phaseEndsAt = Time.time + Mathf.Max(0f, settings.knockdownDuration);
                SetPhase(LaunchPhase.Knockdown, Vector2.zero);
            }
            else if (phase == LaunchPhase.Knockdown && Time.time >= phaseEndsAt)
            {
                phaseEndsAt = Time.time + Mathf.Max(0f, settings.getUpDuration);
                SetPhase(LaunchPhase.Recovering, Vector2.zero);
            }
            else if (phase == LaunchPhase.Recovering && Time.time >= phaseEndsAt)
            {
                SetPhase(LaunchPhase.None, Vector2.zero);
                LaunchCompleted?.Invoke();
            }
        }

        bool IsGrounded()
        {
            if (Time.time <= groundContactUntil)
                return true;
            if (!bodyCollider) return false;
            var distance = Mathf.Max(0.01f, settings.groundProbeDistance);
            var count = bodyCollider.Cast(Vector2.down, groundFilter, groundHits, distance);
            for (var i = 0; i < count; i++)
                if (groundHits[i].normal.y >= 0.55f)
                    return true;
            return false;
        }

        void OnCollisionStay2D(Collision2D collision)
        {
            for (var i = 0; i < collision.contactCount; i++)
            {
                if (collision.GetContact(i).normal.y < 0.55f) continue;
                groundContactUntil = Time.time + Time.fixedDeltaTime * 1.5f;
                return;
            }
        }
        void BeginLanding()
        {
            phase = LaunchPhase.Landing;
            body.velocity = Vector2.zero;
            landingEndsAt = Time.time + 0.06f;
            SetPhase(LaunchPhase.Landing, new Vector2(launchDirectionSign, 0f));
        }

        void SetPhase(LaunchPhase nextPhase, Vector2 velocity)
        {
            phase = nextPhase;
            PhaseChanged?.Invoke(nextPhase, velocity);
        }
    }
}
