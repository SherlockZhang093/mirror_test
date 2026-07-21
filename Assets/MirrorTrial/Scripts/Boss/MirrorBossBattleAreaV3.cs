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

        BoxCollider2D area;
        bool started;

        void Awake()
        {
            area = GetComponent<BoxCollider2D>();
            area.isTrigger = true;
            if (!encounter) encounter = GetComponent<CombatEncounter>();
            if (encounter) encounter.enabled = false;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (started) return;
            var player = other.GetComponentInParent<PlayerInputReader>();
            if (!player) return;
            started = true;
            var point = spawnPoint ? spawnPoint.transform : transform;
            var boss = Instantiate(bossPrefab, point.position, point.rotation, transform);
            boss.ConfigureArena(area.bounds.min.x, area.bounds.max.x);
            CameraDirector.Ensure().PlayBossIntro(boss.transform, () => boss.Activate(player.transform));
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

