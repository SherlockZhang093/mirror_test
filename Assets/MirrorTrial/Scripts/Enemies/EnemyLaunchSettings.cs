using System;
using MirrorTrial.Level;
using UnityEngine;
using UnityEngine.Serialization;

namespace MirrorTrial.Enemies
{
    [Serializable]
    public sealed class EnemyLaunchSettings
    {
        [FormerlySerializedAs("landingRecovery")]
        [ChineseLabel("起身动画时间"), Min(0f)] public float getUpDuration = 0.6f;
        [ChineseLabel("地面检测距离"), Min(0.01f)] public float groundProbeDistance = 0.08f;
        [ChineseLabel("命中压缩时间"), Min(0f)] public float hitCompressDuration = 0.045f;
        [ChineseLabel("脱离地面动画时间"), Min(0f)] public float takeoffDuration = 0.08f;
        [ChineseLabel("落地滑行速度"), Min(0f)] public float landingSlideSpeed = 2.2f;
        [ChineseLabel("落地滑行时间"), Min(0f)] public float landingSlideDuration = 0.12f;
        [ChineseLabel("倒地停留时间"), Min(0f)] public float knockdownDuration = 0.28f;

        [Header("表现配置")]
        [ChineseLabel("击飞表现 Profile")] public EnemyLaunchPresentationProfile presentation;
    }
}
