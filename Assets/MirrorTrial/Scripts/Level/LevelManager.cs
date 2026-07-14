using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MirrorTrial.Level
{
    public class LevelManager : MonoBehaviour
    {
        [Header("关卡信息")]
        [ChineseLabel("关卡ID")] [SerializeField] string levelId = "Level_Reality_01";
        [ChineseLabel("关卡显示名")] [SerializeField] string levelDisplayName = "第一关";
        [ChineseLabel("玩家出生点")] [SerializeField] Transform playerSpawn;

        [Header("场景根节点")]
        [ChineseLabel("地形根节点")] [SerializeField] Transform geometryRoot;
        [ChineseLabel("玩法根节点")] [SerializeField] Transform gameplayRoot;
        [ChineseLabel("运行时根节点")] [SerializeField] Transform runtimeRoot;

        [Header("运行时集合")]
        [ChineseLabel("段落")] [SerializeField] List<LevelSegment> segments = new List<LevelSegment>();
        [ChineseLabel("触发器")] [SerializeField] List<LevelTrigger> triggers = new List<LevelTrigger>();
        [ChineseLabel("战斗区")] [SerializeField] List<CombatEncounter> encounters = new List<CombatEncounter>();
        [ChineseLabel("镜子门")] [SerializeField] List<MirrorGate> mirrorGates = new List<MirrorGate>();
        [ChineseLabel("门")] [SerializeField] List<AreaGate> gates = new List<AreaGate>();
        [ChineseLabel("刷怪点")] [SerializeField] List<SpawnPoint> spawnPoints = new List<SpawnPoint>();

        public string LevelId => levelId;
        public string LevelDisplayName => levelDisplayName;
        public Transform PlayerSpawn => playerSpawn;
        public Transform GeometryRoot => geometryRoot;
        public Transform GameplayRoot => gameplayRoot;
        public Transform RuntimeRoot => runtimeRoot;

        public IReadOnlyList<LevelSegment> Segments => segments;
        public IReadOnlyList<LevelTrigger> Triggers => triggers;
        public IReadOnlyList<CombatEncounter> Encounters => encounters;
        public IReadOnlyList<MirrorGate> MirrorGates => mirrorGates;
        public IReadOnlyList<AreaGate> Gates => gates;
        public IReadOnlyList<SpawnPoint> SpawnPoints => spawnPoints;

        [ChineseLabel("启动时自动收集")] public bool AutoCollectOnAwake = true;
        [SerializeField] bool setupDefaultCameraFollow = true;

        void Awake()
        {
            EnsureRoots();
            if (AutoCollectOnAwake)
                CollectAll();
            SetupDefaultCameraFollow();
        }

        void SetupDefaultCameraFollow()
        {
            if (!setupDefaultCameraFollow || !playerSpawn || !Camera.main) return;

            var follow = Camera.main.GetComponent<CameraFollow2D>();
            if (!follow)
                follow = Camera.main.gameObject.AddComponent<CameraFollow2D>();
            follow.SetTarget(playerSpawn);
            ConfigureCameraBounds(follow);
        }

        void ConfigureCameraBounds(CameraFollow2D follow)
        {
            var bounds = ComputeSceneBounds();
            if (bounds.minX <= float.MinValue || bounds.maxX >= float.MaxValue || bounds.minX > bounds.maxX)
                return;

            follow.SetHorizontalBounds(bounds.minX, bounds.maxX);
        }

        (float minX, float maxX) ComputeSceneBounds()
        {
            var colliders = geometryRoot
                ? geometryRoot.GetComponentsInChildren<Collider2D>(true)
                : FindObjectsOfType<Collider2D>();

            var boundsList = colliders
                .Where(c => c && !c.isTrigger)
                .Select(c => c.bounds)
                .ToList();

            if (boundsList.Count == 0) return (float.MinValue, float.MaxValue);

            var minX = boundsList.Min(b => b.min.x);
            var maxX = boundsList.Max(b => b.max.x);
            return (minX, maxX);
        }



        public void CollectAll()
        {
            segments = FindAll<LevelSegment>(gameplayRoot).ToList();
            triggers = FindAll<LevelTrigger>(gameplayRoot).ToList();
            encounters = FindAll<CombatEncounter>(gameplayRoot).ToList();
            mirrorGates = FindAll<MirrorGate>(gameplayRoot).ToList();
            gates = FindAll<AreaGate>(gameplayRoot).ToList();
            spawnPoints = FindAll<SpawnPoint>(gameplayRoot).ToList();
        }

        static IEnumerable<T> FindAll<T>(Transform root) where T : Component
        {
            if (root)
                return root.GetComponentsInChildren<T>(true);
            return FindObjectsOfType<T>();
        }

        void EnsureRoots()
        {
            if (!geometryRoot) geometryRoot = transform.Find("地形");
            if (!geometryRoot) geometryRoot = CreateRoot("地形");

            if (!gameplayRoot) gameplayRoot = transform.Find("玩法");
            if (!gameplayRoot) gameplayRoot = CreateRoot("玩法");

            if (!runtimeRoot) runtimeRoot = transform.Find("运行时");
            if (!runtimeRoot) runtimeRoot = CreateRoot("运行时");
        }

        Transform CreateRoot(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        public Transform GetCategoryRoot(string category)
        {
            EnsureRoots();
            var parent = gameplayRoot.Find(category);
            if (!parent)
            {
                var go = new GameObject(category);
                go.transform.SetParent(gameplayRoot);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                parent = go.transform;
            }
            return parent;
        }

#if UNITY_EDITOR
        public T CreateGameplayObject<T>(string category, string name, Vector3 position) where T : Component
        {
            EnsureRoots();
            var parent = GetCategoryRoot(category);
            var go = new GameObject(name);
            UnityEditor.Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            go.transform.SetParent(parent);
            go.transform.position = position;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            if (typeof(T) == typeof(LevelTrigger))
            {
                var col = go.AddComponent<BoxCollider2D>();
                col.isTrigger = true;
            }
            else if (typeof(T) == typeof(CombatEncounter))
            {
                var col = go.AddComponent<BoxCollider2D>();
                col.isTrigger = true;
            }
            else if (typeof(T) == typeof(LevelSegment))
            {
                var col = go.AddComponent<BoxCollider2D>();
                col.isTrigger = true;
            }
            else if (typeof(T) == typeof(MirrorGate))
            {
                var col = go.AddComponent<BoxCollider2D>();
                col.isTrigger = true;
                col.size = new Vector2(1.2f, 2f);
            }
            else if (typeof(T) == typeof(AreaGate))
            {
                var col = go.AddComponent<BoxCollider2D>();
            }

            return go.AddComponent<T>();
        }

        public string GetUniqueName(string baseName)
        {
            var candidates = GetComponentsInChildren<Transform>(true).Select(t => t.name).ToList();
            if (!candidates.Contains(baseName))
                return baseName;

            for (var i = 2; i <= 99; i++)
            {
                var candidate = $"{baseName}_{i:00}";
                if (!candidates.Contains(candidate))
                    return candidate;
            }
            return $"{baseName}_{System.Guid.NewGuid().ToString("N").Substring(0, 4)}";
        }
#endif

        void OnDrawGizmos()
        {
            if (!playerSpawn) return;
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(playerSpawn.position, 0.2f);
            Gizmos.DrawLine(playerSpawn.position, playerSpawn.position + Vector3.up * 1f);
        }
    }
}
