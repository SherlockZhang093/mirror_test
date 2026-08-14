using MirrorTrial.Player;
using UnityEngine;

namespace MirrorTrial.Boss
{
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class MirrorBossArena : MonoBehaviour
    {
        [SerializeField] MirrorBossController bossPrefab;
        [SerializeField] Transform bossSpawnPoint;
        [SerializeField] bool spawnOnAwake = true;
        [SerializeField] bool startWhenPlayerEnters = true;
        [SerializeField] MirrorBossEncounter encounter;

        BoxCollider2D trigger;
        MirrorBossController boss;
        bool started;

        public Bounds ArenaBounds => trigger ? trigger.bounds : new Bounds(transform.position, Vector3.zero);

        void Awake()
        {
            trigger = GetComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            if (!encounter) encounter = GetComponent<MirrorBossEncounter>();
            if (spawnOnAwake) EnsureBoss();
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!startWhenPlayerEnters || started) return;
            if (other.CompareTag("Player") || other.GetComponent<PlayerInputReader>())
                BeginFight();
        }

        public void BeginFight()
        {
            if (started) return;
            started = true;
            EnsureBoss();
            if (encounter) encounter.BeginBossFight();
            else boss?.Activate();
        }

        void EnsureBoss()
        {
            if (boss || !bossPrefab) return;
            var point = bossSpawnPoint ? bossSpawnPoint : transform;
            boss = Instantiate(bossPrefab, point.position, point.rotation, transform);
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            var box = GetComponent<BoxCollider2D>();
            if (!box) return;
            var bounds = box.bounds;
            Gizmos.color = new Color(0.5f, 0.75f, 1f, 0.12f);
            Gizmos.DrawCube(bounds.center, bounds.size);
            Gizmos.color = new Color(0.55f, 0.85f, 1f, 0.95f);
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }
#endif
    }
}
