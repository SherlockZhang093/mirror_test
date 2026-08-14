using System;
using UnityEngine;

namespace MirrorTrial.Player
{
    public enum PlayerWeaponType
    {
        Unarmed,
        Sword,
        Bow,
        Reserved
    }

    [Serializable]
    public sealed class PlayerWeaponSlot
    {
        public string name;
        public PlayerWeaponType weaponType;
        public bool available;

        public PlayerWeaponSlot(string name, PlayerWeaponType weaponType, bool available = false)
        {
            this.name = name;
            this.weaponType = weaponType;
            this.available = available;
        }
    }

    [DefaultExecutionOrder(-80)]
    [RequireComponent(typeof(PlayerInputReader), typeof(PlayerStateMachine))]
    public sealed class PlayerWeaponController : MonoBehaviour
    {
        [SerializeField] PlayerWeaponSlot[] slots = CreateDefaultSlots();
        [SerializeField] int activeSlotIndex;

        PlayerInputReader input;
        PlayerStateMachine stateMachine;
        PlayerCombat combat;
        PlayerAbilityLoadout abilities;
        PlayerBowCombat bowCombat;

        public int ActiveSlotIndex => activeSlotIndex;
        public PlayerWeaponType CurrentWeapon => GetSlot(activeSlotIndex).weaponType;
        public PlayerWeaponSlot[] Slots => slots;
        public event Action<PlayerWeaponType> WeaponChanged;

        void OnValidate()
        {
            EnsureSlots();
            activeSlotIndex = Mathf.Clamp(activeSlotIndex, 0, slots.Length - 1);
        }

        void Awake()
        {
            EnsureSlots();
            ResetToStartingLoadout();
            input = GetComponent<PlayerInputReader>();
            stateMachine = GetComponent<PlayerStateMachine>();
            combat = GetComponent<PlayerCombat>();
            abilities = GetComponent<PlayerAbilityLoadout>();
            bowCombat = GetComponent<PlayerBowCombat>();
        }

        void Update()
        {
            if (input.WasPressed(PlayerInputCommand.WeaponSlot1)) TryEquipSlot(0);
            else if (input.WasPressed(PlayerInputCommand.WeaponSlot2)) TryEquipSlot(1);
            else if (input.WasPressed(PlayerInputCommand.WeaponSlot3)) TryEquipSlot(2);
            else if (input.WasPressed(PlayerInputCommand.WeaponSlot4)) TryEquipSlot(3);
        }

        public bool TryEquipSlot(int slotIndex)
        {
            EnsureSlots();
            if (slotIndex < 0 || slotIndex >= slots.Length || !slots[slotIndex].available)
                return false;
            if (slotIndex == activeSlotIndex)
                return true;
            if (!CanSwitchWeapon())
                return false;
            activeSlotIndex = slotIndex;
            WeaponChanged?.Invoke(CurrentWeapon);
            return true;
        }

        public bool IsWeaponAvailable(PlayerWeaponType weaponType)
        {
            EnsureSlots();
            var slotIndex = FindSlot(weaponType);
            return slotIndex >= 0 && slots[slotIndex].available;
        }

        public void SetWeaponAvailable(PlayerWeaponType weaponType, bool available)
        {
            EnsureSlots();
            var slotIndex = FindSlot(weaponType);
            if (slotIndex < 0) return;

            slots[slotIndex].available = available;
            if (!available && slotIndex == activeSlotIndex)
                ForceEquipWeapon(PlayerWeaponType.Unarmed);
        }

        public bool ForceEquipWeapon(PlayerWeaponType weaponType)
        {
            EnsureSlots();
            var slotIndex = FindSlot(weaponType);
            if (slotIndex < 0 || !slots[slotIndex].available)
                return false;
            if (slotIndex == activeSlotIndex)
                return true;

            activeSlotIndex = slotIndex;
            WeaponChanged?.Invoke(CurrentWeapon);
            return true;
        }

        public void ApplyProgression(bool swordUnlocked, bool bowUnlocked)
        {
            SetWeaponAvailable(PlayerWeaponType.Unarmed, true);
            SetWeaponAvailable(PlayerWeaponType.Sword, swordUnlocked);
            SetWeaponAvailable(PlayerWeaponType.Bow, bowUnlocked);
            SetWeaponAvailable(PlayerWeaponType.Reserved, false);

            var tuning = GetComponent<PlayerTuning>();
            if (tuning)
            {
                tuning.abilities.mirrorBladeUnlocked = swordUnlocked;
                // The former second reward is no longer part of the current
                // fist -> sword -> bow progression.
                tuning.abilities.echoDashUnlocked = false;
            }
        }

        public bool CanSwitchWeapon()
        {
            if (combat && combat.IsAttacking)
                return false;
            if (abilities && abilities.IsBusy)
                return false;
            if (bowCombat && bowCombat.IsBusy)
                return false;
            if (stateMachine && (stateMachine.CurrentState == PlayerActionState.Hurt || stateMachine.CurrentState == PlayerActionState.Dead || stateMachine.CurrentState == PlayerActionState.Dash))
                return false;
            return true;
        }

        PlayerWeaponSlot GetSlot(int index)
        {
            EnsureSlots();
            return slots[Mathf.Clamp(index, 0, slots.Length - 1)];
        }

        int FindSlot(PlayerWeaponType weaponType)
        {
            for (var i = 0; i < slots.Length; i++)
                if (slots[i].weaponType == weaponType)
                    return i;
            return -1;
        }

        void ResetToStartingLoadout()
        {
            ApplyProgression(false, false);
            ForceEquipWeapon(PlayerWeaponType.Unarmed);
        }

        void EnsureSlots()
        {
            if (slots == null || slots.Length != 4)
                slots = CreateDefaultSlots();
            for (var i = 0; i < slots.Length; i++)
                if (slots[i] == null)
                    slots[i] = CreateDefaultSlots()[i];
        }

        static PlayerWeaponSlot[] CreateDefaultSlots()
        {
            return new[]
            {
                new PlayerWeaponSlot("\u7a7a\u624b", PlayerWeaponType.Unarmed, true),
                new PlayerWeaponSlot("\u5251", PlayerWeaponType.Sword),
                new PlayerWeaponSlot("\u5f13", PlayerWeaponType.Bow),
                new PlayerWeaponSlot("\u9884\u7559", PlayerWeaponType.Reserved)
            };
        }
    }
}
