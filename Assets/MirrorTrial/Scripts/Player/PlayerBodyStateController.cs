using System.Collections.Generic;
using UnityEngine;

namespace MirrorTrial.Player
{
    [DisallowMultipleComponent]
    public sealed class PlayerBodyStateController : MonoBehaviour
    {
        [SerializeField] PlayerBodyState baseState = PlayerBodyState.Normal;
        [SerializeField, Min(0.01f)] float maxPoise = 3f;
        [SerializeField, Min(0f)] float recoveryDelay = 0.75f;
        [SerializeField, Min(0f)] float recoveryPerSecond = 2f;

        readonly Dictionary<object, PlayerBodyState> modifiers = new Dictionary<object, PlayerBodyState>();
        float currentPoise;
        float lastPoiseDamageTime = float.NegativeInfinity;

        public PlayerBodyState CurrentState { get; private set; }
        public float CurrentPoise => currentPoise;
        public float MaxPoise => maxPoise;

        void Awake()
        {
            currentPoise = maxPoise;
            RecalculateState();
        }

        void Update()
        {
            if (currentPoise >= maxPoise || Time.time < lastPoiseDamageTime + recoveryDelay)
                return;
            currentPoise = Mathf.Min(maxPoise, currentPoise + recoveryPerSecond * Time.deltaTime);
        }

        public void SetBodyState(object source, PlayerBodyState state)
        {
            if (source == null) return;
            if (state == PlayerBodyState.Normal) modifiers.Remove(source);
            else modifiers[source] = state;
            RecalculateState();
        }

        public void ClearBodyState(object source)
        {
            if (source != null && modifiers.Remove(source)) RecalculateState();
        }

        public void ClearAllModifiers()
        {
            modifiers.Clear();
            RecalculateState();
        }

        public bool ApplyPoiseDamage(float amount)
        {
            if (amount <= 0f) return false;
            lastPoiseDamageTime = Time.time;
            currentPoise = Mathf.Max(0f, currentPoise - amount);
            var broken = currentPoise <= 0f;
            if (broken) currentPoise = maxPoise;
            return broken;
        }

        public void ResetPoise() => currentPoise = maxPoise;

        void RecalculateState()
        {
            var resolved = baseState;
            foreach (var state in modifiers.Values)
                if (GetPriority(state) > GetPriority(resolved)) resolved = state;
            CurrentState = resolved;
        }

        static int GetPriority(PlayerBodyState state)
        {
            switch (state)
            {
                case PlayerBodyState.Invincible: return 4;
                case PlayerBodyState.DiamondBody: return 3;
                case PlayerBodyState.SuperArmor: return 2;
                case PlayerBodyState.HardBody: return 1;
                default: return 0;
            }
        }
    }
}
