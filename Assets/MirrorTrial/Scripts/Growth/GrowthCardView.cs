using System;
using UnityEngine;
using UnityEngine.UI;

namespace MirrorTrial.Growth
{
    [RequireComponent(typeof(Button))]
    public sealed class GrowthCardView : MonoBehaviour
    {
        [Header("卡牌内容")]
        [SerializeField] GrowthCardDefinition definition;

        [Header("界面引用")]
        [SerializeField] Text titleText;
        [SerializeField] Text amountText;
        [SerializeField] Text effectText;
        [SerializeField] Text costText;
        [SerializeField] Text stateText;
        [SerializeField] Button selectButton;
        [SerializeField] CanvasGroup canvasGroup;

        IGrowthEssenceWallet wallet;
        GameObject player;
        Action<GrowthCardView> selected;
        bool locked;

        public int EssenceCost => definition ? definition.EssenceCost : 0;
        public GrowthEffectType EffectType => definition ? definition.EffectType : GrowthEffectType.MaxHealth;
        public float IncreaseAmount => definition ? definition.IncreaseAmount : 0f;
        public string EffectDescription => PlayerGrowthEffectApplier.FormatEffect(EffectType, IncreaseAmount);

        void Awake()
        {
            if (!selectButton)
                selectButton = GetComponent<Button>();
            if (!canvasGroup)
                canvasGroup = GetComponent<CanvasGroup>();
            selectButton.onClick.AddListener(OnSelected);
            RefreshVisual();
        }

        void OnDestroy()
        {
            if (selectButton)
                selectButton.onClick.RemoveListener(OnSelected);
        }

        public void Bind(IGrowthEssenceWallet essenceWallet, GameObject targetPlayer, Action<GrowthCardView> onSelected)
        {
            wallet = essenceWallet;
            player = targetPlayer;
            selected = onSelected;
            locked = false;
            RefreshVisual();
        }

        public void SetLocked(bool value)
        {
            locked = value;
            RefreshVisual();
        }

        public void RefreshVisual()
        {
            if (titleText)
                titleText.text = PlayerGrowthEffectApplier.FormatCardTitle(EffectType);
            if (amountText)
                amountText.text = PlayerGrowthEffectApplier.FormatCardValue(IncreaseAmount);
            if (effectText)
                effectText.text = titleText && amountText
                    ? PlayerGrowthEffectApplier.FormatCardAttribute(EffectType)
                    : EffectDescription;
            if (costText)
                costText.text = "消耗 " + EssenceCost;

            var hasDefinition = definition;
            var hasWallet = wallet != null;
            var enough = hasWallet && wallet.Balance >= EssenceCost;
            var effectAvailable = hasDefinition && PlayerGrowthEffectApplier.CanApply(player, EffectType, IncreaseAmount);
            var interactable = !locked && hasDefinition && hasWallet && enough && effectAvailable;

            if (selectButton)
                selectButton.interactable = interactable;
            if (canvasGroup)
                canvasGroup.alpha = interactable ? 1f : 0.48f;

            if (!stateText)
                return;
            if (locked)
                stateText.text = "";
            else if (!hasDefinition)
                stateText.text = "缺少成长卡配置";
            else if (!hasWallet)
                stateText.text = "精华接口未接入";
            else if (!effectAvailable)
                stateText.text = "玩家属性不可用";
            else if (!enough)
                stateText.text = "精华不足";
            else
                stateText.text = "";
        }

        void OnSelected()
        {
            if (!locked && selectButton && selectButton.interactable)
                selected?.Invoke(this);
        }
    }
}
