using System;
using UnityEngine;

namespace MirrorTrial.Growth
{
    /// <summary>
    /// 关末卡牌系统依赖的最小生命精华接口。
    /// 精华系统实现此接口后，可在 Awake 中调用 GrowthEssenceWalletLocator.Register(this)。
    /// </summary>
    public interface IGrowthEssenceWallet
    {
        int Balance { get; }
        event Action<int> BalanceChanged;
        bool TrySpend(int amount);
    }

    public static class GrowthEssenceWalletLocator
    {
        static IGrowthEssenceWallet current;

        public static void Register(IGrowthEssenceWallet wallet)
        {
            if (wallet != null)
                current = wallet;
        }

        public static void Unregister(IGrowthEssenceWallet wallet)
        {
            if (ReferenceEquals(current, wallet))
                current = null;
        }

        public static bool TryResolve(out IGrowthEssenceWallet wallet)
        {
            if (IsAlive(current))
            {
                wallet = current;
                return true;
            }

            current = null;
            var behaviours = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
            for (var i = 0; i < behaviours.Length; i++)
            {
                var candidate = behaviours[i] as IGrowthEssenceWallet;
                if (candidate == null)
                    continue;

                current = candidate;
                wallet = candidate;
                return true;
            }

            wallet = null;
            return false;
        }

        static bool IsAlive(IGrowthEssenceWallet wallet)
        {
            if (wallet == null)
                return false;

            var unityObject = wallet as UnityEngine.Object;
            if (ReferenceEquals(unityObject, null))
                return true;
            return unityObject;
        }
    }
}
