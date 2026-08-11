using UnityEngine;

namespace MirrorTrial.Growth
{
    [CreateAssetMenu(menuName = "Mirror Trial/成长/成长卡配置", fileName = "GrowthCardDefinition")]
    public sealed class GrowthCardDefinition : ScriptableObject
    {
        [SerializeField, Min(0)] int essenceCost = 5;
        [SerializeField] GrowthEffectType effectType = GrowthEffectType.MaxHealth;
        [SerializeField, Min(0.01f)] float increaseAmount = 1f;

        public int EssenceCost => essenceCost;
        public GrowthEffectType EffectType => effectType;
        public float IncreaseAmount => increaseAmount;

#if UNITY_EDITOR
        public void Configure(int cost, GrowthEffectType effect, float amount)
        {
            essenceCost = Mathf.Max(0, cost);
            effectType = effect;
            increaseAmount = Mathf.Max(0.01f, amount);
        }
#endif
    }
}
