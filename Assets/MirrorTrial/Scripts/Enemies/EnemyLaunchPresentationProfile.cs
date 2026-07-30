using MirrorTrial.Level;
using UnityEngine;

namespace MirrorTrial.Enemies
{
    [CreateAssetMenu(fileName = "EnemyLaunchPresentation", menuName = "Mirror Trial/Enemies/Launch Presentation")]
    public sealed class EnemyLaunchPresentationProfile : ScriptableObject
    {
        [Header("动画状态")]
        [ChineseLabel("击飞动画")] public string launchAnimation = "Launch";
        [ChineseLabel("起身动画")] public string getUpAnimation = "LaunchGetUp";

        [Header("特效 Prefab")]
        [ChineseLabel("脱离地面")] public GameObject takeoffPrefab;
        [ChineseLabel("空中残影")] public GameObject airbornePrefab;
        [ChineseLabel("落地冲击")] public GameObject landingPrefab;
        [ChineseLabel("滑行尘迹")] public GameObject slidePrefab;
    }
}
