using System;
using System.Collections.Generic;
using UnityEngine;

namespace MirrorTrial.Player
{
    public enum PlayerInputCommand
    {
        PrimaryAttack,
        SecondaryAttack,
        WeaponSkill,
        MobilitySkill,
        WeaponSlot1,
        WeaponSlot2,
        WeaponSlot3,
        WeaponSlot4,
        Dodge,
        Recover
    }

    [Serializable]
    public sealed class PlayerInputBinding
    {
        public PlayerInputCommand command;
        public string buttonName;
        public KeyCode key;

        public PlayerInputBinding()
        {
        }

        public PlayerInputBinding(PlayerInputCommand command, string buttonName, KeyCode key)
        {
            this.command = command;
            this.buttonName = buttonName;
            this.key = key;
        }
    }

    [DefaultExecutionOrder(-90)]
    public class PlayerInputReader : MonoBehaviour
    {
        [Header("Axes")]
        [SerializeField] string horizontalAxis = "Horizontal";
        [SerializeField] string verticalAxis = "Vertical";

        [Header("Buttons")]
        [SerializeField] string jumpButton = "Jump";

        [Header("Action Bindings")]
        [SerializeField] List<PlayerInputBinding> bindings = CreateDefaultBindings();
        [SerializeField, HideInInspector] int bindingSchemaVersion;

        readonly Dictionary<PlayerInputCommand, bool> pressed = new Dictionary<PlayerInputCommand, bool>();
        readonly Dictionary<PlayerInputCommand, bool> released = new Dictionary<PlayerInputCommand, bool>();
        readonly Dictionary<PlayerInputCommand, bool> held = new Dictionary<PlayerInputCommand, bool>();
        readonly HashSet<PlayerInputCommand> previewHeld = new HashSet<PlayerInputCommand>();
        readonly HashSet<PlayerInputCommand> previewPressed = new HashSet<PlayerInputCommand>();
        readonly HashSet<PlayerInputCommand> previewReleased = new HashSet<PlayerInputCommand>();

        public bool InputEnabled { get; set; } = true;
        public float MoveX { get; private set; }
        public float MoveY { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool JumpReleased { get; private set; }
        public bool AttackPressed => WasPressed(PlayerInputCommand.PrimaryAttack);
        public bool SecondaryAttackPressed => WasPressed(PlayerInputCommand.SecondaryAttack);
        public bool WeaponSkillPressed => WasPressed(PlayerInputCommand.WeaponSkill);
        public bool MobilitySkillPressed => WasPressed(PlayerInputCommand.MobilitySkill);
        public bool MirrorBladePressed => WeaponSkillPressed;
        public bool EchoDashPressed => MobilitySkillPressed;
        public bool DodgePressed => WasPressed(PlayerInputCommand.Dodge);
        public bool RecoverPressed => WasPressed(PlayerInputCommand.Recover);

        public IList<PlayerInputBinding> Bindings { get { return bindings; } }

        void OnValidate()
        {
            EnsureBindings();
        }

        void Awake()
        {
            EnsureBindings();
        }

        void Update()
        {
            if (!InputEnabled)
            {
                MoveX = 0f;
                MoveY = 0f;
                JumpPressed = false;
                JumpReleased = false;
                ClearPressed();
                return;
            }

            MoveX = Input.GetAxisRaw(horizontalAxis);
            MoveY = Input.GetAxisRaw(verticalAxis);
            JumpPressed = Input.GetButtonDown(jumpButton);
            JumpReleased = Input.GetButtonUp(jumpButton);

            ClearPressed();
            for (var i = 0; i < bindings.Count; i++)
            {
                var binding = bindings[i];
                if (binding == null)
                    continue;

                var isPressed = false;
                var isReleased = false;
                var isHeld = false;
                if (!string.IsNullOrEmpty(binding.buttonName))
                {
                    isPressed |= Input.GetButtonDown(binding.buttonName);
                    isReleased |= Input.GetButtonUp(binding.buttonName);
                    isHeld |= Input.GetButton(binding.buttonName);
                }
                if (binding.key != KeyCode.None)
                {
                    isPressed |= Input.GetKeyDown(binding.key);
                    isReleased |= Input.GetKeyUp(binding.key);
                    isHeld |= Input.GetKey(binding.key);
                }
                pressed[binding.command] = isPressed;
                released[binding.command] = isReleased;
                held[binding.command] = isHeld;
            }
            ApplyPreviewInput();
        }

        public void PreviewTap(PlayerInputCommand command)
        {
            previewPressed.Add(command);
            previewReleased.Add(command);
            previewHeld.Remove(command);
        }

        public void PreviewPress(PlayerInputCommand command)
        {
            if (previewHeld.Add(command))
                previewPressed.Add(command);
            previewReleased.Remove(command);
        }

        public void PreviewRelease(PlayerInputCommand command)
        {
            if (previewHeld.Remove(command))
                previewReleased.Add(command);
        }

        public void ClearPreviewInput()
        {
            foreach (var command in previewHeld)
                previewReleased.Add(command);
            previewHeld.Clear();
            previewPressed.Clear();
        }

        void ApplyPreviewInput()
        {
            foreach (PlayerInputCommand command in Enum.GetValues(typeof(PlayerInputCommand)))
            {
                if (previewPressed.Remove(command)) pressed[command] = true;
                if (previewReleased.Remove(command)) released[command] = true;
                if (previewHeld.Contains(command)) held[command] = true;
            }
        }
        public bool WasPressed(PlayerInputCommand command)
        {
            bool value;
            return pressed.TryGetValue(command, out value) && value;
        }

        public bool WasReleased(PlayerInputCommand command)
        {
            bool value;
            return released.TryGetValue(command, out value) && value;
        }

        public bool IsHeld(PlayerInputCommand command)
        {
            bool value;
            return held.TryGetValue(command, out value) && value;
        }

        void ClearPressed()
        {
            foreach (PlayerInputCommand command in Enum.GetValues(typeof(PlayerInputCommand)))
            {
                pressed[command] = false;
                released[command] = false;
                held[command] = false;
            }
        }

        void EnsureBindings()
        {
            if (bindingSchemaVersion < 2)
            {
                bindings = CreateDefaultBindings();
                bindingSchemaVersion = 5;
                return;
            }
            if (bindings == null)
                bindings = new List<PlayerInputBinding>();

            if (bindingSchemaVersion < 3)
            {
                for (var i = 0; i < bindings.Count; i++)
                {
                    var binding = bindings[i];
                    if (binding != null && binding.command == PlayerInputCommand.MobilitySkill && binding.key == KeyCode.LeftShift)
                        binding.key = KeyCode.Q;
                }
            }

            if (bindingSchemaVersion < 4)
                RemoveBindingsCreatedByDodgePopupBug();

            var defaults = CreateDefaultBindings();
            for (var i = 0; i < defaults.Count; i++)
            {
                var exists = false;
                for (var j = 0; j < bindings.Count; j++)
                {
                    if (bindings[j] != null && bindings[j].command == defaults[i].command)
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists)
                    bindings.Add(defaults[i]);
            }
            bindingSchemaVersion = 5;
        }

        void RemoveBindingsCreatedByDodgePopupBug()
        {
            var seen = new HashSet<string>();
            for (var i = bindings.Count - 1; i >= 0; i--)
            {
                var binding = bindings[i];
                if (binding == null)
                {
                    bindings.RemoveAt(i);
                    continue;
                }

                var isBuggedDodgeRow = binding.command == PlayerInputCommand.PrimaryAttack &&
                    string.IsNullOrEmpty(binding.buttonName) && binding.key == KeyCode.LeftShift;
                var signature = ((int)binding.command) + "|" + (binding.buttonName ?? string.Empty) + "|" + (int)binding.key;
                if (isBuggedDodgeRow || !seen.Add(signature))
                    bindings.RemoveAt(i);
            }
        }

        static List<PlayerInputBinding> CreateDefaultBindings()
        {
            return new List<PlayerInputBinding>
            {
                new PlayerInputBinding(PlayerInputCommand.PrimaryAttack, "Fire1", KeyCode.J),
                new PlayerInputBinding(PlayerInputCommand.SecondaryAttack, "Fire2", KeyCode.K),
                new PlayerInputBinding(PlayerInputCommand.WeaponSkill, "Fire3", KeyCode.L),
                new PlayerInputBinding(PlayerInputCommand.MobilitySkill, string.Empty, KeyCode.Q),
                new PlayerInputBinding(PlayerInputCommand.WeaponSlot1, string.Empty, KeyCode.Alpha1),
                new PlayerInputBinding(PlayerInputCommand.WeaponSlot2, string.Empty, KeyCode.Alpha2),
                new PlayerInputBinding(PlayerInputCommand.WeaponSlot3, string.Empty, KeyCode.Alpha3),
                new PlayerInputBinding(PlayerInputCommand.WeaponSlot4, string.Empty, KeyCode.Alpha4),
                new PlayerInputBinding(PlayerInputCommand.Dodge, string.Empty, KeyCode.LeftShift),
                new PlayerInputBinding(PlayerInputCommand.Recover, string.Empty, KeyCode.G)
            };
        }
    }
}
