using System;
using System.Collections;
using MirrorTrial.Feedback;
using MirrorTrial.Level;
using MirrorTrial.Player;
using UnityEngine;

namespace MirrorTrial.Boss
{
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class MirrorArcherTwoStageCoordinator : MonoBehaviour
    {
        [SerializeField] MirrorArcherTwoStageProfile profile;
        [SerializeField] MirrorArcherMountedBoss mountedBossPrefab;
        [SerializeField] MirrorBossActorV2 groundBossPrefab;
        [SerializeField] MirrorBossSpawnPoint airSpawnPoint;
        [SerializeField] MirrorBossSpawnPoint groundSpawnPoint;
        [SerializeField] MirrorArcherRockfallArea rockfallArea;
        [SerializeField] CombatEncounter encounter;
        [SerializeField, Min(0f)] float clearDelay = 1.5f;

        BoxCollider2D area;
        Transform playerTarget;
        MirrorArcherMountedBoss activeMountedBoss;
        MirrorBossActorV2 activeGroundBoss;
        bool started;
        bool transitioning;

        public Vector2 LastImpactPosition { get; private set; }
        public bool LastImpactFacingRight { get; private set; }
        public bool GroundPhaseActive => activeGroundBoss && activeGroundBoss.CurrentState != MirrorBossActorV2.State.Dead;
        public event Action<MirrorArcherCombatStage, string, int, int> BossHealthChanged;
        public event Action BossHealthHidden;

        void Awake()
        {
            area = GetComponent<BoxCollider2D>();
            area.isTrigger = true;
            if (!encounter) encounter = GetComponent<CombatEncounter>();
            if (!rockfallArea) rockfallArea = GetComponentInChildren<MirrorArcherRockfallArea>(true);
            if (encounter) encounter.enabled = false;
        }

        void Start()
        {
            var player = FindObjectOfType<PlayerInputReader>();
            if (player) BeginFight(player);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            var player = other.GetComponentInParent<PlayerInputReader>();
            if (player) BeginFight(player);
        }

        void BeginFight(PlayerInputReader player)
        {
            if (started) return;
            if (!profile || !ResolveMountedPrefab() || !ResolveGroundPrefab())
            {
                Debug.LogError("[MirrorArcherTwoStageCoordinator] Profile or stage prefab is missing.", this);
                player.InputEnabled = true;
                return;
            }

            started = true;
            playerTarget = player.transform;
            var point = airSpawnPoint ? airSpawnPoint.transform : transform;
            activeMountedBoss = Instantiate(ResolveMountedPrefab(), point.position, point.rotation, transform);
            var minimumVisibleFlightY = rockfallArea
                ? rockfallArea.WorldBounds.min.y
                : area.bounds.min.y;
            activeMountedBoss.Configure(profile, area.bounds, minimumVisibleFlightY);
            activeMountedBoss.MountDefeated += OnMountDefeated;
            activeMountedBoss.HealthChanged += OnMountedHealthChanged;
            PresentMountedHealth(activeMountedBoss);
            ScreenFx.Begin(ScreenFxType.BossBattle, this);
            CameraDirector.Ensure().PlayBossIntro(activeMountedBoss.transform,
                () => activeMountedBoss.Activate(playerTarget));
        }

        void OnMountDefeated(MirrorArcherMountedBoss mounted)
        {
            if (transitioning || !mounted) return;
            transitioning = true;
            mounted.MountDefeated -= OnMountDefeated;
            mounted.PrepareForCrash();
            StartCoroutine(CrashAndSwap(mounted));
        }

        IEnumerator CrashAndSwap(MirrorArcherMountedBoss mounted)
        {
            var start = (Vector2)mounted.transform.position;
            var groundY = groundSpawnPoint ? groundSpawnPoint.transform.position.y : area.bounds.min.y;
            var targetX = Mathf.Clamp(start.x + profile.crashLandingOffset.x, area.bounds.min.x, area.bounds.max.x);
            LastImpactPosition = new Vector2(targetX, groundY + profile.crashLandingOffset.y);
            LastImpactFacingRight = mounted.FacingRight;

            var elapsed = 0f;
            var duration = Mathf.Max(0.1f, profile.crashDuration);
            while (elapsed < duration && mounted)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var curved = profile.crashCurve == null ? t : Mathf.Clamp01(profile.crashCurve.Evaluate(t));
                var lateral = Mathf.SmoothStep(start.x, LastImpactPosition.x, t);
                mounted.SetCrashPose(new Vector2(lateral, Mathf.Lerp(start.y, LastImpactPosition.y, curved)),
                    LastImpactFacingRight);
                yield return null;
            }

            if (mounted)
            {
                mounted.SetCrashPose(LastImpactPosition, LastImpactFacingRight);
                mounted.PlayCrashImpact();
            }
            ScreenFx.Play(ScreenFxType.BossPhasePulse, 1f, source: this);
            GameObject impact = null;
            if (profile.crashImpactPrefab)
                impact = Instantiate(profile.crashImpactPrefab, LastImpactPosition, Quaternion.identity, transform);
            yield return new WaitForSeconds(profile.impactCoverDuration);

            SpawnGroundBoss();
            if (mounted) Destroy(mounted.gameObject);
            if (impact) Destroy(impact, 1f);
            transitioning = false;
        }

        void SpawnGroundBoss()
        {
            // MirrorBossActorV2 owns facing through SpriteRenderer.flipX. Rotating the prefab root
            // here mirrors the visual and child hitboxes a second time, making movement appear to
            // run away from the player and placing attacks on the wrong side.
            activeGroundBoss = Instantiate(ResolveGroundPrefab(), LastImpactPosition,
                Quaternion.identity, transform);
            activeGroundBoss.ConfigureArena(area.bounds.min.x, area.bounds.max.x);
            activeGroundBoss.Defeated += OnGroundBossDefeated;
            activeGroundBoss.HealthChanged += OnGroundHealthChanged;
            var legacyHud = activeGroundBoss.GetComponent<MirrorBossHudV3>();
            if (legacyHud) legacyHud.enabled = false;
            var controller = activeGroundBoss.GetComponent<MirrorArcherGroundPhaseController>();
            if (!controller) controller = activeGroundBoss.gameObject.AddComponent<MirrorArcherGroundPhaseController>();
            controller.Configure(profile, rockfallArea);
            PresentGroundHealth(activeGroundBoss);
            controller.BeginFromCrash(playerTarget, LastImpactFacingRight);
        }

        void OnMountedHealthChanged(MirrorArcherMountedBoss boss, int current, int maximum)
        {
            BossHealthChanged?.Invoke(MirrorArcherCombatStage.Air, boss.DisplayName, current, maximum);
        }

        void OnGroundHealthChanged(MirrorBossActorV2 boss, int current, int maximum)
        {
            BossHealthChanged?.Invoke(MirrorArcherCombatStage.Ground, boss.DisplayName, current, maximum);
        }

        void PresentMountedHealth(MirrorArcherMountedBoss boss)
        {
            BossHealthChanged?.Invoke(MirrorArcherCombatStage.Air, boss.DisplayName,
                boss.CurrentHitPoints, boss.MaxHitPoints);
        }

        void PresentGroundHealth(MirrorBossActorV2 boss)
        {
            BossHealthChanged?.Invoke(MirrorArcherCombatStage.Ground, boss.DisplayName,
                boss.CurrentHitPoints, boss.MaxHitPoints);
        }

        void OnGroundBossDefeated(MirrorBossActorV2 defeated)
        {
            defeated.Defeated -= OnGroundBossDefeated;
            ScreenFx.End(ScreenFxType.BossBattle, this);
            StartCoroutine(ClearAfterDelay());
        }

        IEnumerator ClearAfterDelay()
        {
            yield return new WaitForSeconds(clearDelay);
            BossHealthHidden?.Invoke();
            if (encounter) encounter.ClearEncounter();
        }

        MirrorArcherMountedBoss ResolveMountedPrefab()
        {
            if (mountedBossPrefab) return mountedBossPrefab;
            return profile && profile.mountedBossPrefab
                ? profile.mountedBossPrefab.GetComponent<MirrorArcherMountedBoss>()
                : null;
        }

        MirrorBossActorV2 ResolveGroundPrefab()
        {
            if (groundBossPrefab) return groundBossPrefab;
            return profile && profile.groundBossPrefab
                ? profile.groundBossPrefab.GetComponent<MirrorBossActorV2>()
                : null;
        }

        void OnDestroy()
        {
            if (activeMountedBoss)
            {
                activeMountedBoss.MountDefeated -= OnMountDefeated;
                activeMountedBoss.HealthChanged -= OnMountedHealthChanged;
            }
            if (activeGroundBoss)
            {
                activeGroundBoss.Defeated -= OnGroundBossDefeated;
                activeGroundBoss.HealthChanged -= OnGroundHealthChanged;
            }
            ScreenFx.End(ScreenFxType.BossBattle, this);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            var box = GetComponent<BoxCollider2D>();
            if (!box) return;
            Gizmos.color = new Color(0.25f, 0.72f, 1f, 0.12f);
            Gizmos.DrawCube(box.bounds.center, box.bounds.size);
            Gizmos.color = new Color(0.35f, 0.82f, 1f, 0.95f);
            Gizmos.DrawWireCube(box.bounds.center, box.bounds.size);
            if (rockfallArea && groundSpawnPoint)
            {
                Gizmos.color = new Color(0.2f, 1f, 0.45f, 0.9f);
                Gizmos.DrawWireSphere(groundSpawnPoint.transform.position, rockfallArea.SafeRadius);
            }
        }
#endif
    }
}
