using MirrorTrial.Level;
using MirrorTrial.Player;
using UnityEngine;

namespace MirrorTrial.Boss
{
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class MirrorBossBattleArea : MonoBehaviour
    {
        [SerializeField] MirrorBossActor bossPrefab;
        [SerializeField] MirrorBossSpawnPoint spawnPoint;
        [SerializeField] CombatEncounter encounter;
        [SerializeField] float clearDelay = 1.5f;

        BoxCollider2D area;
        MirrorBossActor boss;
        bool started;

        void Awake()
        {
            area = GetComponent<BoxCollider2D>();
            area.isTrigger = true;
            if (!encounter) encounter = GetComponent<CombatEncounter>();
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (started) return;
            if (other.CompareTag("Player") || other.GetComponent<PlayerInputReader>()) BeginFight();
        }

        public void BeginFight()
        {
            if (started || !bossPrefab) return;
            started = true;
            var point = spawnPoint ? spawnPoint.transform : transform;
            boss = Instantiate(bossPrefab, point.position, point.rotation, transform);
            boss.ConfigureArena(area.bounds.min.x, area.bounds.max.x);
            boss.Defeated += OnDefeated;
            CameraDirector.Ensure().PlayBossIntro(boss.transform, () =>
            {
                encounter?.StartEncounter();
                boss.Activate();
            });
        }

        void OnDefeated(MirrorBossActor defeated)
        {
            defeated.Defeated -= OnDefeated;
            Invoke(nameof(ClearEncounter), clearDelay);
        }

        void ClearEncounter() => encounter?.ClearEncounter();

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


