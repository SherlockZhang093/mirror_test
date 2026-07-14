using MirrorTrial.Level;
using UnityEngine;

namespace MirrorTrial.Enemies
{
    [CreateAssetMenu(fileName = "EnemyAIProfile", menuName = "Mirror Trial/敌人AI配置", order = 0)]
    public class EnemyAIProfile : ScriptableObject
    {
        [Header("基础属性")]
        [ChineseLabel("最大生命值")] [Tooltip("最大生命值")] public int maxHitPoints = 2;
        [ChineseLabel("移动速度")] [Tooltip("移动速度")] public float moveSpeed = 2.5f;
        [ChineseLabel("朝向跟随移动")] [Tooltip("面向方向是否跟随移动方向翻转")] public bool flipVisualByVelocity = true;

        [Header("侦测与追击")]
        [ChineseLabel("侦测范围")] [Tooltip("发现玩家的距离")] public float detectionRange = 8f;
        [ChineseLabel("脱战距离")] [Tooltip("超出此距离将丢失目标")] public float loseInterestRange = 12f;
        [ChineseLabel("停止距离")] [Tooltip("追击停止距离（近战=攻击范围，远程=射程）")] public float stopDistance = 1.2f;
        [ChineseLabel("前方侦测")] [Tooltip("是否只在正前方锥形区域内侦测玩家")] public bool useForwardDetection = false;
        [ChineseLabel("侦测半角")] [Tooltip("前方侦测的角度半角（度）")] public float forwardDetectionAngle = 90f;
        [ChineseLabel("巡逻等待")] [Tooltip("到达巡逻点后停留时间（秒）")] public float patrolWaitTime = 1f;
        [ChineseLabel("启用巡逻")] [Tooltip("是否启用巡逻行为")] public bool enablePatrol = false;

        [Header("攻击")]
        [ChineseLabel("攻击前摇")] [Tooltip("攻击动作开始到判定生效的时间（秒）")] public float attackWindup = 0.35f;
        [ChineseLabel("攻击冷却")] [Tooltip("两次攻击之间的最小间隔（秒）")] public float attackCooldown = 1.2f;
        [ChineseLabel("攻击范围")] [Tooltip("攻击判定距离")] public float attackRange = 1.2f;
        [ChineseLabel("攻击伤害")] [Tooltip("每次攻击造成的伤害值")] public int attackDamage = 1;
        [ChineseLabel("伤害延迟")] [Tooltip("攻击触发后伤害实际生效的延迟（秒）")] public float damageDelay = 0.1f;
        [ChineseLabel("投射体预制体")] [Tooltip("远程攻击使用的投射体预制体（为空则为近战）")] public GameObject projectilePrefab;
        [ChineseLabel("投射体偏移")] [Tooltip("投射体发射位置相对角色的偏移")] public Vector2 projectileSpawnOffset = new Vector2(0.4f, 0.2f);
        [ChineseLabel("攻击时锁定移动")] [Tooltip("攻击过程中是否停止移动")] public bool lockMovementWhileAttacking = true;

        [Header("受击与死亡")]
        [ChineseLabel("受击硬直")] [Tooltip("受击后硬直时间（秒）")] public float hurtStun = 0.25f;
        [ChineseLabel("击退力度")] [Tooltip("受击时的击退力")] public float knockbackForce = 4f;
        [ChineseLabel("死亡延迟")] [Tooltip("死亡后延迟多久销毁（秒）")] public float deathFadeDelay = 0.3f;

        [Header("移动限制")]
        [ChineseLabel("悬崖检测距离")] [Tooltip("检测前方地面的距离，0=不检测")] public float ledgeCheckDistance = 0.5f;
        [ChineseLabel("墙壁检测距离")] [Tooltip("检测前方墙壁的距离，0=不检测")] public float wallCheckDistance = 0.3f;
        [ChineseLabel("悬崖处转身")] [Tooltip("到达悬崖边缘时是否自动转身")] public bool canTurnAtLedge = true;

        public bool IsRanged => projectilePrefab != null;
    }
}
