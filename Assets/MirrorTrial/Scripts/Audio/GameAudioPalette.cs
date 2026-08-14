using UnityEngine;

namespace MirrorTrial.Audio
{
    [CreateAssetMenu(menuName = "Mirror Trial/Audio/Game Audio Palette", fileName = "GameAudioPalette")]
    public sealed class GameAudioPalette : ScriptableObject
    {
        const string DefaultResourcePath = "Audio/DefaultGameAudioPalette";

        static GameAudioPalette cachedDefault;

        [Header("UI")]
        public AudioClip uiSelect;
        [Range(0f, 1f), InspectorName("选择确认 音量")] public float uiSelectVolume = 1f;
        public AudioClip uiCancel;
        [Range(0f, 1f), InspectorName("取消返回 音量")] public float uiCancelVolume = 1f;
        public AudioClip uiComplete;
        [Range(0f, 1f), InspectorName("完成过关 音量")] public float uiCompleteVolume = 1f;

        [Header("Player Movement")]
        public AudioClip sandFootstep;
        [Range(0f, 1f), InspectorName("沙地脚步 音量")] public float sandFootstepVolume = 1f;
        public AudioClip stoneFootstep;
        [Range(0f, 1f), InspectorName("石地脚步 音量")] public float stoneFootstepVolume = 1f;
        public AudioClip jump;
        [Range(0f, 1f), InspectorName("跳跃 音量")] public float jumpVolume = 1f;
        public AudioClip land;
        [Range(0f, 1f), InspectorName("落地 音量")] public float landVolume = 1f;
        public AudioClip dodge;
        [Range(0f, 1f), InspectorName("闪避 音量")] public float dodgeVolume = 1f;

        [Header("Player Combat")]
        public AudioClip swordSwing;
        [Range(0f, 1f), InspectorName("轻剑挥动 音量")] public float swordSwingVolume = 1f;
        public AudioClip heavySwordSwing;
        [Range(0f, 1f), InspectorName("重剑挥动 音量")] public float heavySwordSwingVolume = 1f;
        public AudioClip swordHit;
        [Range(0f, 1f), InspectorName("剑击肉体 音量")] public float swordHitVolume = 1f;
        public AudioClip guardClash;
        [Range(0f, 1f), InspectorName("格挡碰撞 音量")] public float guardClashVolume = 1f;
        public AudioClip unarmedSwing;
        [Range(0f, 1f), InspectorName("徒手挥动 音量")] public float unarmedSwingVolume = 1f;
        public AudioClip unarmedHit;
        [Range(0f, 1f), InspectorName("徒手命中 音量")] public float unarmedHitVolume = 1f;

        [Header("Bow")]
        public AudioClip bowDraw;
        [Range(0f, 1f), InspectorName("拉弦 音量")] public float bowDrawVolume = 1f;
        public AudioClip bowFire;
        [Range(0f, 1f), InspectorName("发射 音量")] public float bowFireVolume = 1f;
        public AudioClip arrowHit;
        [Range(0f, 1f), InspectorName("箭命中 音量")] public float arrowHitVolume = 1f;

        [Header("Player Reactions")]
        public AudioClip playerHurt;
        [Range(0f, 1f), InspectorName("玩家受伤 音量")] public float playerHurtVolume = 1f;
        public AudioClip playerDeath;
        [Range(0f, 1f), InspectorName("玩家死亡 音量")] public float playerDeathVolume = 1f;

        [Header("Enemy")]
        public AudioClip enemyAttack;
        [Range(0f, 1f), InspectorName("敌人攻击 音量")] public float enemyAttackVolume = 1f;
        public AudioClip enemyHurt;
        [Range(0f, 1f), InspectorName("敌人受伤 音量")] public float enemyHurtVolume = 1f;
        public AudioClip enemyDeath;
        [Range(0f, 1f), InspectorName("敌人死亡 音量")] public float enemyDeathVolume = 1f;

        [Header("Boss And Magic")]
        public AudioClip bossCharge;
        [Range(0f, 1f), InspectorName("Boss 蓄力 音量")] public float bossChargeVolume = 1f;
        public AudioClip mirrorBladeRelease;
        [Range(0f, 1f), InspectorName("镜刃剑气 音量")] public float mirrorBladeReleaseVolume = 1f;
        public AudioClip teleport;
        [Range(0f, 1f), InspectorName("传送 音量")] public float teleportVolume = 1f;
        public AudioClip summon;
        [Range(0f, 1f), InspectorName("召唤 音量")] public float summonVolume = 1f;

        [Header("World")]
        public AudioClip mirrorHit;
        [Range(0f, 1f), InspectorName("镜子裂纹 音量")] public float mirrorHitVolume = 1f;
        public AudioClip mirrorShatter;
        [Range(0f, 1f), InspectorName("镜子破碎 音量")] public float mirrorShatterVolume = 1f;
        public AudioClip pulleyMove;
        [Range(0f, 1f), InspectorName("滑轮运转 音量")] public float pulleyMoveVolume = 1f;
        public AudioClip ropeBreak;
        [Range(0f, 1f), InspectorName("绳索断裂 音量")] public float ropeBreakVolume = 1f;
        public AudioClip platformLand;
        [Range(0f, 1f), InspectorName("平台落地 音量")] public float platformLandVolume = 1f;
        public AudioClip grassHit;
        [Range(0f, 1f), InspectorName("砍草 音量")] public float grassHitVolume = 1f;
        public AudioClip stoneHit;
        [Range(0f, 1f), InspectorName("砍石头 音量")] public float stoneHitVolume = 1f;
        public AudioClip resourcePickup;
        [Range(0f, 1f), InspectorName("资源拾取 音量")] public float resourcePickupVolume = 1f;
        public AudioClip heal;
        [Range(0f, 1f), InspectorName("治疗 音量")] public float healVolume = 1f;

        [Header("Ambience")]
        public AudioClip ruinsAmbience;
        [Range(0f, 1f), InspectorName("遗迹环境 音量")] public float ruinsAmbienceVolume = 1f;
        public AudioClip mirrorAmbience;
        [Range(0f, 1f), InspectorName("镜世界环境 音量")] public float mirrorAmbienceVolume = 1f;
        public AudioClip jungleAmbience;
        [Range(0f, 1f), InspectorName("雨林水域环境 音量")] public float jungleAmbienceVolume = 1f;

        public float ScaleVolume(float localVolume, float audioVolume)
        {
            return Mathf.Clamp01(localVolume) * Mathf.Clamp01(audioVolume);
        }

        public static float ScaleDefaultVolume(float localVolume, float audioVolume)
        {
            var palette = LoadDefault();
            return palette ? palette.ScaleVolume(localVolume, audioVolume) : Mathf.Clamp01(localVolume) * Mathf.Clamp01(audioVolume);
        }

        public static GameAudioPalette LoadDefault()
        {
            if (!cachedDefault)
                cachedDefault = Resources.Load<GameAudioPalette>(DefaultResourcePath);
            return cachedDefault;
        }
    }
}
