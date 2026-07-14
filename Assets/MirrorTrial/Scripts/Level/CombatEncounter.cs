using System.Collections;
using System.Collections.Generic;
using MirrorTrial.Player;
using UnityEngine;

namespace MirrorTrial.Level
{
    [RequireComponent(typeof(BoxCollider2D))]
    public class CombatEncounter : MonoBehaviour
    {
        [Header("基础")]
        [ChineseLabel("战斗ID")] [Tooltip("战斗ID")] [SerializeField] string encounterId = "Combat_A_01";
        [ChineseLabel("玩家进入时自动开始")] [Tooltip("玩家进入时自动开始")] [SerializeField] bool startOnPlayerEnter = true;
        [ChineseLabel("清场条件")] [Tooltip("清场条件")] [SerializeField] CombatClearCondition clearCondition = CombatClearCondition.WavesCompletedAndEnemiesCleared;

        [Header("门与波次")]
        [ChineseLabel("战斗开始时关闭的门")] [Tooltip("战斗开始时关闭的门")] [SerializeField] AreaGate[] lockGates = new AreaGate[0];
        [ChineseLabel("波次配置")] [Tooltip("波次配置")] [SerializeField] WaveDefinition[] waves = new WaveDefinition[0];

        public string EncounterId => encounterId;
        public bool StartOnPlayerEnter => startOnPlayerEnter;
        public CombatClearCondition ClearCondition => clearCondition;
        public IReadOnlyList<AreaGate> LockGates => lockGates;
        public bool HasWaves => waves != null && waves.Length > 0;
        public int WaveCount => waves?.Length ?? 0;
        public IReadOnlyList<WaveDefinition> Waves => waves;
        public bool IsCleared { get; private set; }
        public int AliveEnemyCount => aliveEnemies.Count;

        readonly HashSet<GameObject> aliveEnemies = new HashSet<GameObject>();
        bool encounterStarted;
        bool allWavesCompleted;
        int currentWaveIndex = -1;
        Coroutine waveRoutine;

        public System.Action<CombatEncounter> OnCleared;

        void Awake()
        {
            var col = GetComponent<BoxCollider2D>();
            col.isTrigger = true;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!startOnPlayerEnter || encounterStarted) return;
            if (IsPlayer(other))
                StartEncounter();
        }

        public void StartEncounter()
        {
            if (encounterStarted) return;
            encounterStarted = true;
            allWavesCompleted = !HasWaves;

            foreach (var g in lockGates)
                if (g) g.Close();

            if (HasWaves)
                StartNextWave();
            else
                TryClear();
        }

        public void StartWave(int waveIndex)
        {
            if (encounterStarted || waveIndex < 0 || waveIndex >= WaveCount)
                return;

            currentWaveIndex = waveIndex - 1;
            StartEncounter();
        }

        void StartNextWave()
        {
            currentWaveIndex++;
            if (currentWaveIndex >= waves.Length)
            {
                allWavesCompleted = true;
                waveRoutine = null;
                TryClear();
                return;
            }

            waveRoutine = StartCoroutine(RunWave(waves[currentWaveIndex]));
        }

        IEnumerator RunWave(WaveDefinition wave)
        {
            if (wave != null && wave.spawnEntries != null)
            {
                foreach (var entry in wave.spawnEntries)
                {
                    if (entry == null) continue;
                    if (entry.delay > 0f)
                        yield return new WaitForSeconds(entry.delay);

                    for (var i = 0; i < entry.count; i++)
                    {
                        SpawnEntry(entry);
                        if (i < entry.count - 1 && entry.interval > 0f)
                            yield return new WaitForSeconds(entry.interval);
                    }
                }
            }

            if (wave != null && wave.clearCondition == CombatClearCondition.AllEnemiesDefeated)
                yield return new WaitUntil(() => aliveEnemies.Count == 0);

            if (wave != null && wave.nextWaveDelay > 0f)
                yield return new WaitForSeconds(wave.nextWaveDelay);

            waveRoutine = null;
            StartNextWave();
        }

        void SpawnEntry(WaveSpawnEntry entry)
        {
            var prefab = entry.enemyPrefab ? entry.enemyPrefab : (entry.spawnPoint ? entry.spawnPoint.DefaultEnemyPrefab : null);
            if (!prefab) return;

            var point = entry.spawnPoint ? entry.spawnPoint.transform : transform;
            var rot = entry.spawnPoint ? entry.spawnPoint.SpawnRotation : point.rotation;
            var instance = Instantiate(prefab, point.position, rot);
            aliveEnemies.Add(instance);

            ApplySpawnPointOverrides(entry.spawnPoint, instance);

            var link = instance.GetComponent<SpawnedEnemyLink>() ?? instance.AddComponent<SpawnedEnemyLink>();
            link.Bind(this);
        }

        void ApplySpawnPointOverrides(SpawnPoint spawnPoint, GameObject instance)
        {
            if (!spawnPoint) return;
            var enemyAI = instance.GetComponent<MirrorTrial.Enemies.EnemyAI>();
            if (!enemyAI) return;

            if (spawnPoint.AIProfile != null)
                enemyAI.SetProfile(spawnPoint.AIProfile);
            if (spawnPoint.OverrideHitPoints > 0)
                enemyAI.SetOverrideHitPoints(spawnPoint.OverrideHitPoints);
            if (spawnPoint.OverrideMoveSpeed > 0f)
                enemyAI.SetOverrideMoveSpeed(spawnPoint.OverrideMoveSpeed);
        }

        public void NotifyEnemyDefeated(GameObject enemy)
        {
            if (enemy) aliveEnemies.Remove(enemy);
            TryClear();
        }

        void TryClear()
        {
            if (IsCleared) return;

            var wavesDone = allWavesCompleted;
            var enemiesCleared = aliveEnemies.Count == 0;
            var shouldClear = wavesDone && enemiesCleared;

            if (shouldClear)
                ClearEncounter();
        }

        public void ClearEncounter()
        {
            if (IsCleared) return;
            IsCleared = true;

            foreach (var g in lockGates)
                if (g) g.Open();

            OnCleared?.Invoke(this);
        }

        bool IsPlayer(Collider2D other)
        {
            return other.CompareTag("Player") || other.GetComponent<PlayerInputReader>() != null;
        }

        void OnDrawGizmos()
        {
            var col = GetComponent<BoxCollider2D>();
            if (!col) return;

            var c = col.bounds.center;
            var s = Vector2.Scale(col.size, transform.lossyScale);
            Gizmos.color = new Color(1f, 0.9f, 0.1f, 0.08f);
            Gizmos.DrawCube(c, new Vector3(s.x, s.y, 0.02f));
            Gizmos.color = new Color(1f, 0.9f, 0.1f, 0.95f);
            Gizmos.DrawWireCube(c, new Vector3(s.x, s.y, 0.02f));
        }
    }

    public class SpawnedEnemyLink : MonoBehaviour
    {
        CombatEncounter owner;
        bool notified;

        public void Bind(CombatEncounter encounter)
        {
            owner = encounter;
        }

        void OnDisable()
        {
            NotifyOwner();
        }

        void OnDestroy()
        {
            NotifyOwner();
        }

        void NotifyOwner()
        {
            if (notified || !Application.isPlaying) return;
            notified = true;
            owner?.NotifyEnemyDefeated(gameObject);
        }
    }
}
