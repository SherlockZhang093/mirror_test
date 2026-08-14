using System.Collections;
using MirrorTrial.Feedback;
using MirrorTrial.Level;
using MirrorTrial.Player;
using UnityEngine;

namespace MirrorTrial.Boss
{
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class MirrorBossBattleAreaV3 : MonoBehaviour
    {
        [SerializeField] MirrorBossActorV2 bossPrefab;
        [SerializeField] MirrorBossSpawnPoint spawnPoint;
        [SerializeField] CombatEncounter encounter;
        [SerializeField, Min(0f)] float clearDelay = 1.5f;

        BoxCollider2D area;
        MirrorBossActorV2 activeBoss;
        bool started;

        void Awake()
        {
            area = GetComponent<BoxCollider2D>();
            area.isTrigger = true;
            if (!encounter) encounter = GetComponent<CombatEncounter>();
            if (encounter) encounter.enabled = false;
        }

        void Start()
        {
            // The persistent player is repositioned into this area during scene loading.
            // That does not reliably produce an OnTriggerEnter2D callback, so start the
            // mirror fight directly when the player already exists in the scene.
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
            if (!bossPrefab)
            {
                Debug.LogError("[MirrorBossBattleAreaV3] Missing MirrorBossActorV2 prefab reference.", this);
                player.InputEnabled = true;
                return;
            }

            started = true;
            var point = spawnPoint ? spawnPoint.transform : transform;
            activeBoss = Instantiate(bossPrefab, point.position, point.rotation, transform);
            activeBoss.ConfigureArena(area.bounds.min.x, area.bounds.max.x);
            activeBoss.Defeated += OnBossDefeated;
            activeBoss.PhaseChanged += OnBossPhaseChanged;
            ScreenFx.Begin(ScreenFxType.BossBattle, this);
            CameraDirector.Ensure().PlayBossIntro(activeBoss.transform, () => activeBoss.Activate(player.transform));
        }

        void OnBossPhaseChanged(MirrorBossActorV2 changedBoss, int phase)
        {
            ScreenFx.Play(ScreenFxType.BossPhasePulse, Mathf.Clamp01(0.65f + phase * 0.12f), source: this);
        }

        void OnBossDefeated(MirrorBossActorV2 defeated)
        {
            defeated.Defeated -= OnBossDefeated;
            defeated.PhaseChanged -= OnBossPhaseChanged;
            ScreenFx.End(ScreenFxType.BossBattle, this);
            StartCoroutine(ClearAfterDelay());
        }

        IEnumerator ClearAfterDelay()
        {
            yield return new WaitForSeconds(clearDelay);
            if (encounter) encounter.ClearEncounter();
        }

        void OnDestroy()
        {
            if (activeBoss)
            {
                activeBoss.Defeated -= OnBossDefeated;
                activeBoss.PhaseChanged -= OnBossPhaseChanged;
            }
            ScreenFx.End(ScreenFxType.BossBattle, this);
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider2D>();
            if (!box) return;
            Gizmos.color = new Color(0.45f, 0.75f, 1f, 0.12f);
            Gizmos.DrawCube(box.bounds.center, box.bounds.size);
            Gizmos.color = new Color(0.55f, 0.85f, 1f, 0.95f);
            Gizmos.DrawWireCube(box.bounds.center, box.bounds.size);
        }
#endif
    }
}

