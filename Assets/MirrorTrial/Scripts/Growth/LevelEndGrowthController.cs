using System;
using UnityEngine;

namespace MirrorTrial.Growth
{
    public static class LevelEndGrowthController
    {
        const string PanelResourcePath = "UI/Growth/EndLevelGrowthPanel";
        static bool showing;

        public static bool IsShowing => showing;

        public static bool TryShow(GameObject player, Action continuation)
        {
            if (showing)
            {
                Debug.LogWarning("[LevelEndGrowthController] 关末成长界面已经打开。");
                return false;
            }

            var prefab = Resources.Load<GameObject>(PanelResourcePath);
            if (!prefab)
            {
                Debug.LogError("[LevelEndGrowthController] 找不到 Resources/" + PanelResourcePath + "，跳过关末成长。");
                continuation?.Invoke();
                return true;
            }

            var instance = UnityEngine.Object.Instantiate(prefab);
            var panel = instance.GetComponent<EndLevelGrowthPanel>();
            if (!panel)
            {
                Debug.LogError("[LevelEndGrowthController] 关末成长 Prefab 缺少 EndLevelGrowthPanel。", instance);
                UnityEngine.Object.Destroy(instance);
                continuation?.Invoke();
                return true;
            }

            showing = true;
            panel.Show(player, () =>
            {
                showing = false;
                continuation?.Invoke();
            });
            return true;
        }
    }
}
