using MirrorTrial.Enemies;
using UnityEngine;

namespace MirrorTrial.Level
{
    public enum SpawnPointRole
    {
        [InspectorName("近战")] Melee,
        [InspectorName("远程")] Ranged,
        [InspectorName("精英")] Elite
    }

    public class SpawnPoint : MonoBehaviour
    {
        [Header("基础")]
        [ChineseLabel("刷怪点ID")] [Tooltip("刷怪点ID")] [SerializeField] string spawnId = "S01";
        [ChineseLabel("角色类型")] [Tooltip("角色类型")] [SerializeField] SpawnPointRole role = SpawnPointRole.Melee;
        [ChineseLabel("默认敌人预制体")] [Tooltip("默认敌人预制体")] [SerializeField] GameObject defaultEnemyPrefab;

        [Header("AI 配置")]
        [ChineseLabel("敌人 AI 配置")] [Tooltip("生成该点的敌人时使用的 AI 配置；为空则使用敌人预制体上的默认配置")] [SerializeField] EnemyAIProfile aiProfile;
        [ChineseLabel("覆盖血量")] [Tooltip("覆盖 AI 配置中的最大生命值；<=0 表示使用 AI 配置")] [SerializeField] int overrideHitPoints = -1;
        [ChineseLabel("覆盖移速")] [Tooltip("覆盖 AI 配置中的移动速度；<=0 表示使用 AI 配置")] [SerializeField] float overrideMoveSpeed = -1f;

        [Header("朝向与巡逻")]
        [ChineseLabel("出生朝向")] [Tooltip("出生朝向（角度）")] [SerializeField] float facingDegrees;
        [ChineseLabel("巡逻路径")] [Tooltip("巡逻路径节点")] [SerializeField] Transform[] patrolPath = new Transform[0];

        public string SpawnId => spawnId;
        public SpawnPointRole Role => role;
        public GameObject DefaultEnemyPrefab => defaultEnemyPrefab;
        public EnemyAIProfile AIProfile => aiProfile;
        public int OverrideHitPoints => overrideHitPoints;
        public float OverrideMoveSpeed => overrideMoveSpeed;
        public Quaternion SpawnRotation => Quaternion.Euler(0f, 0f, facingDegrees);
        public Vector2 FacingDirection2D
        {
            get
            {
                var r = facingDegrees * Mathf.Deg2Rad;
                return new Vector2(Mathf.Cos(r), Mathf.Sin(r)).normalized;
            }
        }
        public Transform[] PatrolPath => patrolPath;

        public Color GizmoColor
        {
            get
            {
                switch (role)
                {
                    case SpawnPointRole.Ranged: return new Color(0.2f, 0.6f, 1f, 1f);
                    case SpawnPointRole.Elite: return new Color(0.65f, 0.35f, 1f, 1f);
                    default: return new Color(1f, 0.2f, 0.25f, 1f);
                }
            }
        }

        void OnDrawGizmos()
        {
            var pos = transform.position;
            Gizmos.color = GizmoColor;
            Gizmos.DrawSphere(pos, 0.12f);

            var dir = FacingDirection2D;
            var to = pos + (Vector3)(dir * 0.7f);
            Gizmos.DrawLine(pos, to);
            Gizmos.DrawLine(to, to + (Vector3)(Quaternion.Euler(0f, 0f, 150f) * dir * 0.18f));
            Gizmos.DrawLine(to, to + (Vector3)(Quaternion.Euler(0f, 0f, -150f) * dir * 0.18f));

            if (aiProfile != null)
            {
                Gizmos.color = new Color(1f, 1f, 1f, 0.3f);
                Gizmos.DrawWireSphere(pos, aiProfile.detectionRange);
            }
        }
    }
}
