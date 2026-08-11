using System;
using System.Collections;
using MirrorTrial.Abilities;
using MirrorTrial.Combat;
using UnityEngine;

namespace MirrorTrial.Boss
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(Hurtbox))]
    public sealed class MirrorArcherMountedBoss : MonoBehaviour
    {
        public enum MountedState
        {
            Dormant,
            Intro,
            Acting,
            Recovery,
            Crashing
        }

        // Final art contract. The new art task may replace VisualRoot while retaining these sockets/states.
        public const string VisualRootName = "VisualRoot";
        public const string MountRootName = "MountRoot";
        public const string RiderSocketName = "RiderSocket";
        public const string ProjectileSocketName = "ProjectileSocket";
        public const string ImpactSocketName = "ImpactSocket";

        [SerializeField] MirrorArcherTwoStageProfile profile;
        [SerializeField] Transform visualRoot;
        [SerializeField] Transform projectileSocket;
        [SerializeField] Transform impactSocket;
        [SerializeField] Animator animator;

        Rigidbody2D body;
        Hurtbox hurtbox;
        Collider2D bodyCollider;
        Transform target;
        Rect airBounds;
        Coroutine loop;
        Coroutine crashVisual;
        Coroutine hitVisual;
        bool facingRight = true;
        bool playingHitReaction;
        int hitPoints;
        int rotationIndex;
        string activeAnimationState = "MountedIdle";
        MirrorArcherSkillType? lastSkill;
        MirrorArcherAttackTelegraph activeTelegraph;

        public MountedState CurrentState { get; private set; } = MountedState.Dormant;
        public bool FacingRight => facingRight;
        public int CurrentHitPoints => hitPoints;
        public int MaxHitPoints => profile ? profile.mountedHitPoints : 1;
        public string DisplayName => profile ? profile.mountedDisplayName : "Mounted Archer";
        public event Action<MirrorArcherMountedBoss> Activated;
        public event Action<MirrorArcherMountedBoss, int, int> HealthChanged;
        public event Action<MirrorArcherMountedBoss> MountDefeated;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            hurtbox = GetComponent<Hurtbox>();
            bodyCollider = GetComponent<Collider2D>();
            if (!visualRoot) visualRoot = transform.Find(VisualRootName);
            if (!projectileSocket) projectileSocket = FindDeep(transform, ProjectileSocketName);
            if (!impactSocket) impactSocket = FindDeep(transform, ImpactSocketName);
            if (!animator) animator = GetComponentInChildren<Animator>(true);
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            hitPoints = MaxHitPoints;
        }

        public void Configure(MirrorArcherTwoStageProfile nextProfile, Bounds arenaBounds)
        {
            profile = nextProfile;
            hitPoints = MaxHitPoints;
            var padding = profile ? profile.airPadding : Vector2.zero;
            airBounds = Rect.MinMaxRect(arenaBounds.min.x + padding.x, arenaBounds.min.y + padding.y,
                arenaBounds.max.x - padding.x, arenaBounds.max.y - padding.y);
        }

        public void Activate(Transform playerTarget)
        {
            if (!profile || !playerTarget || CurrentState != MountedState.Dormant) return;
            target = playerTarget;
            IgnorePlayerBodyCollision(playerTarget);
            CurrentState = MountedState.Intro;
            Play("MountedIdle");
            Activated?.Invoke(this);
            loop = StartCoroutine(CombatLoop());
        }

        void IgnorePlayerBodyCollision(Transform playerRoot)
        {
            if (!bodyCollider || !playerRoot) return;

            foreach (Collider2D playerCollider in playerRoot.GetComponentsInChildren<Collider2D>(true))
            {
                if (!playerCollider || playerCollider.isTrigger) continue;
                Physics2D.IgnoreCollision(bodyCollider, playerCollider, true);
            }
        }

        public void PrepareForCrash()
        {
            if (loop != null) StopCoroutine(loop);
            loop = null;
            StopAllCoroutines();
            crashVisual = null;
            hitVisual = null;
            playingHitReaction = false;
            CancelTelegraph();
            CurrentState = MountedState.Crashing;
            if (hurtbox) hurtbox.enabled = false;
            if (bodyCollider) bodyCollider.enabled = false;
            if (body)
            {
                body.velocity = Vector2.zero;
                body.simulated = false;
            }
            Play(profile ? profile.mountedFallState : "MountedFall");
            crashVisual = StartCoroutine(CrashVisualRoutine());
        }

        IEnumerator CrashVisualRoutine()
        {
            yield return new WaitForSeconds(profile ? profile.mountedFallStartDuration : 0.55f);
            if (CurrentState != MountedState.Crashing) yield break;
            Play(profile ? profile.mountedFallLoopState : "MountedFallLoop");
            crashVisual = null;
        }

        public void SetCrashPose(Vector2 position, bool nextFacingRight)
        {
            transform.position = position;
            SetFacing(nextFacingRight);
        }

        public void PlayCrashImpact()
        {
            if (crashVisual != null) StopCoroutine(crashVisual);
            crashVisual = null;
            Play(profile ? profile.mountedImpactState : "MountedImpact");
        }

        IEnumerator CombatLoop()
        {
            yield return new WaitForSeconds(profile.firstActionDelay);
            while (CurrentState != MountedState.Crashing)
            {
                var skill = NextSkill();
                if (skill == null)
                {
                    yield return null;
                    continue;
                }
                CurrentState = MountedState.Acting;
                yield return Execute(skill);
                if (CurrentState == MountedState.Crashing) yield break;
                CurrentState = MountedState.Recovery;
                Play("MountedIdle");
                yield return new WaitForSeconds(skill.recovery);
            }
        }

        MirrorArcherSkillConfig NextSkill()
        {
            if (profile.airSkills == null || profile.airSkills.Count == 0) return null;
            for (var attempt = 0; attempt < profile.airSkills.Count; attempt++)
            {
                var skill = profile.airSkills[rotationIndex % profile.airSkills.Count];
                rotationIndex = (rotationIndex + 1) % profile.airSkills.Count;
                if (skill == null || skill.stage != MirrorArcherCombatStage.Air) continue;
                if (lastSkill.HasValue && lastSkill.Value == skill.type && profile.airSkills.Count > 1) continue;
                lastSkill = skill.type;
                return skill;
            }
            return null;
        }

        IEnumerator Execute(MirrorArcherSkillConfig skill)
        {
            switch (skill.type)
            {
                case MirrorArcherSkillType.LockedShot:
                    yield return Shoot(skill, false, false);
                    break;
                case MirrorArcherSkillType.FanShot:
                    yield return Shoot(skill, true, false);
                    break;
                case MirrorArcherSkillType.GroundArrowRain:
                    yield return Shoot(skill, false, true);
                    break;
                case MirrorArcherSkillType.MountedDive:
                    yield return Dive(skill);
                    break;
                case MirrorArcherSkillType.AirReposition:
                    yield return Reposition(skill);
                    break;
            }
        }

        IEnumerator Shoot(MirrorArcherSkillConfig skill, bool fan, bool rain)
        {
            yield return FaceTargetAnimated();
            if (CurrentState == MountedState.Crashing) yield break;
            Play(skill.animatorState);
            var telegraph = SpawnEffect(skill.windupEffectPrefab, skill.effectOffset);
            activeTelegraph = MirrorArcherAttackTelegraph.Create(
                transform, projectileSocket ? projectileSocket : transform, target, skill, airBounds,
                facingRight, fan, rain);
            var elapsed = 0f;
            var lockedTarget = target ? (Vector2)target.position : (Vector2)transform.position + Vector2.right;
            var locked = false;
            while (elapsed < skill.EffectiveReleaseMoment)
            {
                elapsed += Time.deltaTime;
                if (activeTelegraph)
                    activeTelegraph.SetProgress(elapsed / Mathf.Max(0.01f, skill.EffectiveReleaseMoment));
                if (!locked && elapsed >= skill.lockMoment)
                {
                    locked = true;
                    if (target) lockedTarget = target.position;
                    if (activeTelegraph) activeTelegraph.LockTarget(lockedTarget);
                }
                yield return null;
            }
            if (telegraph) Destroy(telegraph);
            ReleaseTelegraph();
            if (CurrentState == MountedState.Crashing) yield break;

            if (rain)
            {
                var count = Mathf.Max(1, skill.arrowCount);
                for (var i = 0; i < count; i++)
                {
                    var t = count <= 1 ? 0.5f : i / (float)(count - 1);
                    var x = Mathf.Lerp(lockedTarget.x - skill.range * 0.5f, lockedTarget.x + skill.range * 0.5f, t);
                    var spawn = new Vector2(Mathf.Clamp(x, airBounds.xMin, airBounds.xMax), airBounds.yMax);
                    var rainDirection = Quaternion.Euler(0f, 0f, skill.initialAngleOffset) * Vector2.down;
                    SpawnArrow(skill, spawn, rainDirection);
                }
                yield break;
            }

            var origin = projectileSocket ? (Vector2)projectileSocket.position : (Vector2)transform.position + skill.effectOffset;
            var baseDirection = (lockedTarget - origin).normalized;
            if (baseDirection.sqrMagnitude < 0.01f) baseDirection = facingRight ? Vector2.right : Vector2.left;
            var arrows = Mathf.Max(1, skill.arrowCount);
            for (var i = 0; i < arrows; i++)
            {
                var spreadAngle = fan && arrows > 1
                    ? Mathf.Lerp(-skill.arrowAngle * 0.5f, skill.arrowAngle * 0.5f, i / (float)(arrows - 1))
                    : 0f;
                // Treat the configured offset as a local firing angle so the same negative value
                // pitches arrows downward whether the rider is facing left or right.
                var mirroredInitialAngle = skill.initialAngleOffset * (facingRight ? 1f : -1f);
                var angle = mirroredInitialAngle + spreadAngle;
                SpawnArrow(skill, origin, Quaternion.Euler(0f, 0f, angle) * baseDirection);
            }
        }

        IEnumerator Dive(MirrorArcherSkillConfig skill)
        {
            yield return FaceTargetAnimated();
            if (CurrentState == MountedState.Crashing) yield break;
            Play(skill.animatorState);
            var effect = SpawnEffect(skill.windupEffectPrefab, skill.effectOffset);
            activeTelegraph = MirrorArcherAttackTelegraph.Create(
                transform, projectileSocket ? projectileSocket : transform, target, skill, airBounds, facingRight);
            var windupElapsed = 0f;
            var targetLocked = false;
            while (windupElapsed < skill.windup && CurrentState != MountedState.Crashing)
            {
                windupElapsed += Time.deltaTime;
                if (activeTelegraph)
                    activeTelegraph.SetProgress(windupElapsed / Mathf.Max(0.01f, skill.windup));
                if (!targetLocked && windupElapsed >= skill.lockMoment)
                {
                    targetLocked = true;
                    if (activeTelegraph && target)
                        activeTelegraph.LockTarget(target.position);
                }
                yield return null;
            }
            if (effect) Destroy(effect);
            ReleaseTelegraph();
            if (!target || CurrentState == MountedState.Crashing) yield break;

            var start = (Vector2)transform.position;
            var diveEnd = ClampToAir((Vector2)target.position +
                new Vector2(facingRight ? -1.2f : 1.2f, 0.8f));
            // The authored dive pose must end at the trajectory's lowest point.
            // Keeping the Bezier control at that same height makes the descent
            // monotonic instead of dipping below the endpoint and rising again
            // while the bird is still shown with folded dive wings.
            diveEnd.y = Mathf.Min(start.y, diveEnd.y);
            var control = new Vector2((start.x + diveEnd.x) * 0.5f, diveEnd.y);
            yield return MoveCurve(start, control, diveEnd, skill.movementDuration);
            DamageTargetInRadius(skill, diveEnd);

            // The dive action is complete at the lowest point. Open the wings
            // before the upward retreat instead of carrying the dive pose uphill.
            Play("MountedDiveRecover");

            // Preserve the dive's horizontal momentum after passing the target:
            // right-facing dives exit to the right, left-facing dives to the left.
            var retreat = ClampToAir(diveEnd + new Vector2(
                facingRight ? Mathf.Abs(skill.movementOffset.x) : -Mathf.Abs(skill.movementOffset.x),
                Mathf.Abs(skill.movementOffset.y)));
            yield return MoveLinear(diveEnd, retreat, skill.movementDuration * 0.7f);
        }

        IEnumerator Reposition(MirrorArcherSkillConfig skill)
        {
            Play(skill.animatorState);
            activeTelegraph = MirrorArcherAttackTelegraph.Create(
                transform, projectileSocket ? projectileSocket : transform, target, skill, airBounds, facingRight);
            var windupElapsed = 0f;
            while (windupElapsed < skill.windup && CurrentState != MountedState.Crashing)
            {
                windupElapsed += Time.deltaTime;
                if (activeTelegraph)
                    activeTelegraph.SetProgress(windupElapsed / Mathf.Max(0.01f, skill.windup));
                yield return null;
            }
            ReleaseTelegraph();
            if (CurrentState == MountedState.Crashing) yield break;
            var start = (Vector2)transform.position;
            var side = target && transform.position.x < target.position.x ? -1f : 1f;
            var targetBase = target ? (Vector2)target.position : start;
            var end = ClampToAir(targetBase + new Vector2(side * Mathf.Abs(skill.movementOffset.x), Mathf.Abs(skill.movementOffset.y)));
            yield return MoveLinear(start, end, skill.movementDuration);
            yield return FaceTargetAnimated();
        }

        IEnumerator MoveCurve(Vector2 start, Vector2 control, Vector2 end, float duration)
        {
            var elapsed = 0f;
            var fixedWait = new WaitForFixedUpdate();
            while (elapsed < duration && CurrentState != MountedState.Crashing)
            {
                yield return fixedWait;
                if (CurrentState == MountedState.Crashing) yield break;
                elapsed += Time.fixedDeltaTime;
                var t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
                var a = Vector2.Lerp(start, control, t);
                var b = Vector2.Lerp(control, end, t);
                body.MovePosition(Vector2.Lerp(a, b, t));
            }
        }

        IEnumerator MoveLinear(Vector2 start, Vector2 end, float duration)
        {
            var elapsed = 0f;
            var fixedWait = new WaitForFixedUpdate();
            while (elapsed < duration && CurrentState != MountedState.Crashing)
            {
                yield return fixedWait;
                if (CurrentState == MountedState.Crashing) yield break;
                elapsed += Time.fixedDeltaTime;
                body.MovePosition(Vector2.Lerp(start, end, Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration))));
            }
        }

        void SpawnArrow(MirrorArcherSkillConfig skill, Vector2 origin, Vector2 direction)
        {
            var arrow = BowArrowProjectile.Create(skill.projectilePrefab, origin);
            arrow.Launch(new DamagePayload(gameObject, skill.damage,
                    new Vector2(direction.x * Mathf.Abs(skill.knockback.x), Mathf.Abs(skill.knockback.y)), direction, 0.04f),
                direction, skill.arrowSpeed, skill.arrowRange, target ? target.gameObject : null);
        }

        void DamageTargetInRadius(MirrorArcherSkillConfig skill, Vector2 center)
        {
            if (!target || Vector2.Distance(target.position, center) > skill.effectRadius) return;
            var targetHurtbox = target.GetComponentInChildren<Hurtbox>();
            if (!targetHurtbox) return;
            var direction = ((Vector2)target.position - center).normalized;
            if (direction.sqrMagnitude < 0.01f) direction = facingRight ? Vector2.right : Vector2.left;
            targetHurtbox.ReceiveHit(new DamagePayload(gameObject, skill.damage,
                new Vector2(direction.x * Mathf.Abs(skill.knockback.x), Mathf.Abs(skill.knockback.y)), direction, 0.08f));
        }

        GameObject SpawnEffect(GameObject prefab, Vector2 offset)
        {
            if (!prefab) return null;
            var signedOffset = offset;
            signedOffset.x *= facingRight ? 1f : -1f;
            var parent = projectileSocket ? projectileSocket : transform;
            return Instantiate(prefab, (Vector2)parent.position + signedOffset, Quaternion.identity, transform);
        }

        void OnDamagePayloadReceived(DamagePayload payload)
        {
            if (CurrentState == MountedState.Crashing || hitPoints <= 0) return;
            var beforeDamage = hitPoints;
            var requestedDamage = Mathf.Max(0, payload.damage);
            hitPoints = Mathf.Max(0, hitPoints - requestedDamage);
            var actualDamage = beforeDamage - hitPoints;
            if (actualDamage > 0)
                DamageDealtEvents.RaisePlayerDamageDealt(new DamageDealtResult(payload.source, gameObject, requestedDamage, actualDamage));
            HealthChanged?.Invoke(this, hitPoints, MaxHitPoints);
            if (hitPoints > 0)
            {
                PlayHitReaction();
                return;
            }
            if (loop != null) StopCoroutine(loop);
            loop = null;
            if (hurtbox) hurtbox.enabled = false;
            CancelTelegraph();
            MountDefeated?.Invoke(this);
        }

        void PlayHitReaction()
        {
            if (!animator || CurrentState == MountedState.Crashing) return;
            if (hitVisual != null) StopCoroutine(hitVisual);
            hitVisual = StartCoroutine(HitVisualRoutine());
        }

        IEnumerator HitVisualRoutine()
        {
            playingHitReaction = true;
            PlaySpriteState("MountedHit");
            yield return new WaitForSeconds(0.28f);
            playingHitReaction = false;
            hitVisual = null;
            if (animator && CurrentState != MountedState.Crashing && hitPoints > 0)
                PlaySpriteState(activeAnimationState);
        }

        void ReleaseTelegraph()
        {
            if (!activeTelegraph) return;
            activeTelegraph.Release();
            activeTelegraph = null;
        }

        void CancelTelegraph()
        {
            if (!activeTelegraph) return;
            activeTelegraph.Cancel();
            activeTelegraph = null;
        }

        Vector2 ClampToAir(Vector2 value)
        {
            return new Vector2(Mathf.Clamp(value.x, airBounds.xMin, airBounds.xMax),
                Mathf.Clamp(value.y, airBounds.yMin, airBounds.yMax));
        }

        IEnumerator FaceTargetAnimated()
        {
            if (!target) yield break;
            bool nextFacingRight = target.position.x >= transform.position.x;
            if (nextFacingRight == facingRight) yield break;

            // The sheet is authored right-to-left. Mirroring the current visual
            // root automatically turns it left-to-right when starting on the
            // other side; flip the root only after the turn has completed.
            Play("MountedTurn");
            float elapsed = 0f;
            const float turnDuration = 8f / 16f;
            while (elapsed < turnDuration && CurrentState != MountedState.Crashing)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (CurrentState != MountedState.Crashing) SetFacing(nextFacingRight);
        }

        void SetFacing(bool right)
        {
            facingRight = right;
            if (!visualRoot) return;
            var scale = visualRoot.localScale;
            scale.x = Mathf.Abs(scale.x) * (right ? 1f : -1f);
            visualRoot.localScale = scale;
        }

        void Play(string state)
        {
            if (string.IsNullOrEmpty(state)) return;
            activeAnimationState = state;
            if (animator && !playingHitReaction) PlaySpriteState(state);
        }

        void PlaySpriteState(string state)
        {
            if (!animator || string.IsNullOrEmpty(state)) return;
            int stateHash = Animator.StringToHash(state);
            if (!animator.HasState(0, stateHash))
            {
                Debug.LogWarning("Missing mounted-bird animation state: " + state, this);
                return;
            }

            // Sprite object references are discrete values. Cross-fading them can
            // evaluate the default (null) reference between states for one or more
            // frames, making the whole mount disappear. Switch sprite states at an
            // exact frame boundary instead of blending them like skeletal curves.
            animator.Play(stateHash, 0, 0f);
        }

        static Transform FindDeep(Transform root, string childName)
        {
            if (!root) return null;
            foreach (Transform child in root)
            {
                if (child.name == childName) return child;
                var nested = FindDeep(child, childName);
                if (nested) return nested;
            }
            return null;
        }
    }
}
