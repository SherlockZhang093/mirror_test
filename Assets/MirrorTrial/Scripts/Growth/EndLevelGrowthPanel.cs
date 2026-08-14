using System;
using System.Collections;
using System.Collections.Generic;
using MirrorTrial.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MirrorTrial.Growth
{
    public sealed class EndLevelGrowthPanel : MonoBehaviour
    {
        [SerializeField] Transform cardContainer;
        [SerializeField] GameObject[] cardPrefabs;
        [SerializeField] Text balanceText;
        [SerializeField] Text statusText;
        [SerializeField] Button skipButton;
        [SerializeField, Min(0f)] float closeDelay = 0.55f;

        readonly List<GrowthCardView> cards = new List<GrowthCardView>();
        IGrowthEssenceWallet wallet;
        GameObject player;
        PlayerInputReader input;
        Action completed;
        EventSystem createdEventSystem;
        bool previousInputEnabled;
        bool previousCursorVisible;
        CursorLockMode previousCursorLock;
        float previousTimeScale;
        bool opened;
        bool finishing;
        bool completionInvoked;

        public void Show(GameObject targetPlayer, Action onCompleted)
        {
            if (opened)
                return;

            opened = true;
            player = targetPlayer;
            completed = onCompleted;
            previousTimeScale = Time.timeScale;
            previousCursorVisible = Cursor.visible;
            previousCursorLock = Cursor.lockState;
            input = player ? player.GetComponentInChildren<PlayerInputReader>(true) : null;
            if (input)
            {
                previousInputEnabled = input.InputEnabled;
                input.InputEnabled = false;
            }

            Time.timeScale = 0f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            EnsureEventSystem();

            GrowthEssenceWalletLocator.TryResolve(out wallet);
            if (wallet != null)
                wallet.BalanceChanged += OnBalanceChanged;

            if (skipButton)
                skipButton.onClick.AddListener(Skip);

            BuildCards();
            RefreshAll();
        }

        void OnDestroy()
        {
            if (wallet != null)
                wallet.BalanceChanged -= OnBalanceChanged;
            if (skipButton)
                skipButton.onClick.RemoveListener(Skip);

            var callback = !completionInvoked ? completed : null;
            completed = null;
            RestoreGameState();
            callback?.Invoke();
        }

        void BuildCards()
        {
            if (!cardContainer || cardPrefabs == null)
                return;

            if (cardPrefabs.Length > 3)
                Debug.LogWarning("[EndLevelGrowthPanel] 关末成长最多展示 3 张卡，超出的 Prefab 已忽略。", this);

            var cardCount = Mathf.Min(3, cardPrefabs.Length);
            for (var i = 0; i < cardCount; i++)
            {
                if (!cardPrefabs[i])
                    continue;

                var instance = Instantiate(cardPrefabs[i], cardContainer, false);
                var card = instance.GetComponent<GrowthCardView>();
                if (!card)
                {
                    Debug.LogWarning("[EndLevelGrowthPanel] 卡牌 Prefab 缺少 GrowthCardView。", instance);
                    Destroy(instance);
                    continue;
                }

                card.Bind(wallet, player, SelectCard);
                cards.Add(card);
            }
        }

        void SelectCard(GrowthCardView card)
        {
            if (finishing || card == null)
                return;
            if (wallet == null)
            {
                SetStatus("生命精华接口尚未接入");
                return;
            }
            if (!PlayerGrowthEffectApplier.CanApply(player, card.EffectType, card.IncreaseAmount))
            {
                SetStatus("当前玩家无法应用这项提升");
                return;
            }
            if (!wallet.TrySpend(card.EssenceCost))
            {
                RefreshAll();
                SetStatus("生命精华不足");
                return;
            }
            if (!PlayerGrowthEffectApplier.TryApply(player, card.EffectType, card.IncreaseAmount))
            {
                Debug.LogError("[EndLevelGrowthPanel] 精华消费成功但提升应用失败。CanApply 已通过，请检查玩家组件生命周期。", this);
                SetStatus("提升应用失败");
                finishing = true;
                LockCards();
                if (skipButton)
                    skipButton.interactable = false;
                StartCoroutine(CompleteAfterDelay());
                return;
            }

            finishing = true;
            SetStatus("已获得：" + card.EffectDescription);
            LockCards();
            if (skipButton)
                skipButton.interactable = false;
            StartCoroutine(CompleteAfterDelay());
        }

        void Skip()
        {
            if (finishing)
                return;

            finishing = true;
            SetStatus("已保留生命精华");
            LockCards();
            if (skipButton)
                skipButton.interactable = false;
            StartCoroutine(CompleteAfterDelay());
        }

        IEnumerator CompleteAfterDelay()
        {
            if (closeDelay > 0f)
                yield return new WaitForSecondsRealtime(closeDelay);
            Complete();
        }

        void Complete()
        {
            completionInvoked = true;
            var callback = completed;
            completed = null;
            RestoreGameState();
            Destroy(gameObject);
            callback?.Invoke();
        }

        void RefreshAll()
        {
            if (balanceText)
                balanceText.text = wallet != null ? "生命精华  " + wallet.Balance : "生命精华  --";
            if (statusText)
                statusText.text = wallet != null ? "选择一项提升，或保留精华" : "等待生命精华系统接入，可先跳过";
            for (var i = 0; i < cards.Count; i++)
                cards[i].RefreshVisual();
        }

        void LockCards()
        {
            for (var i = 0; i < cards.Count; i++)
                cards[i].SetLocked(true);
        }

        void OnBalanceChanged(int balance)
        {
            RefreshAll();
        }

        void SetStatus(string message)
        {
            if (statusText)
                statusText.text = message;
        }

        void EnsureEventSystem()
        {
            if (EventSystem.current)
                return;

            var eventObject = new GameObject("EndLevelGrowthEventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            createdEventSystem = eventObject.GetComponent<EventSystem>();
        }

        void RestoreGameState()
        {
            if (!opened)
                return;

            opened = false;
            if (input)
                input.InputEnabled = previousInputEnabled;
            Time.timeScale = previousTimeScale;
            Cursor.visible = previousCursorVisible;
            Cursor.lockState = previousCursorLock;
            if (createdEventSystem)
                Destroy(createdEventSystem.gameObject);
        }
    }
}
