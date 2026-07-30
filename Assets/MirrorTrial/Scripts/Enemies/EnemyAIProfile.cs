using MirrorTrial.Combat;
using MirrorTrial.Level;
using UnityEngine;

namespace MirrorTrial.Enemies
{
    [CreateAssetMenu(fileName = "EnemyAIProfile", menuName = "镜像试炼/敌兵/敌兵AI配置", order = 0)]
    public class EnemyAIProfile : ScriptableObject
    {
        [Header("基础属性")]
        [ChineseLabel("最大生命值")] [Tooltip("敌兵能够承受的总伤害。")] public int maxHitPoints = 2;
        [ChineseLabel("移动速度")] [Tooltip("巡逻和追击时的移动速度。")] public float moveSpeed = 2.5f;
        [ChineseLabel("移动时自动转向")] [Tooltip("根据移动方向左右翻转敌兵图片。")]
        public bool flipVisualByVelocity = true;

        [Header("探测、巡逻与追击")]
        [ChineseLabel("默认探测范围")] [Tooltip("关卡中没有单独设置探测矩形时使用的默认距离。")]
        public float detectionRange = 8f;
        [HideInInspector] public float loseInterestRange = 12f;
        [ChineseLabel("开始攻击距离")] [Tooltip("玩家距离敌兵多近时，敌兵停止追击并开始攻击。")]
        public float stopDistance = 1.2f;
        [HideInInspector] public bool useForwardDetection = false;
        [HideInInspector] public float forwardDetectionAngle = 90f;
        [ChineseLabel("巡逻端点等待时间")] [Tooltip("抵达左右巡逻边界后停留多久。")]
        public float patrolWaitTime = 1f;
        [HideInInspector] public bool enablePatrol = false;

        [Header("攻击")]
        [ChineseLabel("攻击前摇")] [Tooltip("开始攻击到伤害生效之间的时间。")]
        public float attackWindup = 0.35f;
        [ChineseLabel("攻击冷却")] [Tooltip("两次攻击之间至少间隔多久。")]
        public float attackCooldown = 1.2f;
        [ChineseLabel("攻击判定距离")] [Tooltip("近战攻击实际能够打到的水平距离。")]
        public float attackRange = 1.2f;
        [ChineseLabel("攻击伤害")] [Tooltip("每次攻击造成的伤害。")]
        public int attackDamage = 1;
        [Min(0)] public int interruptPower = 1;
        [Min(0f)] public float poiseDamage = 1f;
        public HitReactionType playerHitReaction = HitReactionType.LightHurt;
        public bool breaksSuperArmor;
        [ChineseLabel("伤害帧延迟")] [Tooltip("攻击开始后，经过多久执行伤害判定。")]
        public float damageDelay = 0.1f;
        [ChineseLabel("远程子弹预制体")] [Tooltip("留空表示近战敌兵；设置后表示远程敌兵。")]
        public GameObject projectilePrefab;
        [ChineseLabel("子弹生成偏移")] [Tooltip("子弹相对敌兵中心生成的位置。")]
        public Vector2 projectileSpawnOffset = new Vector2(0.4f, 0.2f);
        [ChineseLabel("攻击时停止移动")] [Tooltip("攻击动作期间是否锁定敌兵移动。")]
        public bool lockMovementWhileAttacking = true;

        [Header("受击与死亡")]
        [ChineseLabel("受击硬直时间")] [Tooltip("受击动画至少保持多久。")]
        public float hurtStun = 0.25f;
        [ChineseLabel("受击后退力度")] [Tooltip("敌兵被玩家打中时受到的后退力度。")]
        public float knockbackForce = 4f;
        [ChineseLabel("死亡销毁延迟")] [Tooltip("没有图片组件时，死亡后等待多久销毁。")]
        public float deathFadeDelay = 0.3f;
        [ChineseLabel("死亡闪烁次数")] [Tooltip("销毁前闪烁几次。")]
        public int deathBlinkCount = 2;
        [ChineseLabel("死亡闪烁间隔")] [Tooltip("每次显示或隐藏持续多少秒。")]
        public float deathBlinkInterval = 0.08f;

        [Header("击飞表现")]
        [ChineseLabel("击飞动画与落地表现")]
        public EnemyLaunchSettings launchSettings = new EnemyLaunchSettings();

        [Header("移动限制")]
        [ChineseLabel("悬崖检测距离")] [Tooltip("向前检测地面的距离；设为 0 可关闭。")]
        public float ledgeCheckDistance = 0.5f;
        [ChineseLabel("墙壁检测距离")] [Tooltip("向前检测墙壁的距离；设为 0 可关闭。")]
        public float wallCheckDistance = 0.3f;
        [ChineseLabel("遇到悬崖时转身")] [Tooltip("到达悬崖边缘后是否自动转身。")]
        public bool canTurnAtLedge = true;

        public bool IsRanged => projectilePrefab != null;
    }
}
