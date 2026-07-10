using UnityEngine;

namespace MirrorTrial.Player
{
    public class PlayerInputReader : MonoBehaviour
    {
        [Header("Axes")]
        [SerializeField] string horizontalAxis = "Horizontal";

        [Header("Buttons")]
        [SerializeField] string jumpButton = "Jump";
        [SerializeField] string attackButton = "Fire1";
        [SerializeField] string mirrorBladeButton = "Fire2";
        [SerializeField] string echoDashButton = "Fire3";

        [Header("Key Overrides (bypass Input Manager)")]
        [SerializeField] KeyCode attackKey = KeyCode.J;
        [SerializeField] KeyCode mirrorBladeKey = KeyCode.K;
        [SerializeField] KeyCode echoDashKey = KeyCode.L;

        public bool InputEnabled { get; set; } = true;
        public float MoveX { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool JumpReleased { get; private set; }
        public bool AttackPressed { get; private set; }
        public bool MirrorBladePressed { get; private set; }
        public bool EchoDashPressed { get; private set; }

        void Start() { }

        void Update()
        {
            if (!InputEnabled)
            {
                MoveX = 0f;
                JumpPressed = false;
                JumpReleased = false;
                AttackPressed = false;
                MirrorBladePressed = false;
                EchoDashPressed = false;
                return;
            }

            MoveX = Input.GetAxisRaw(horizontalAxis);
            JumpPressed = Input.GetButtonDown(jumpButton);
            JumpReleased = Input.GetButtonUp(jumpButton);
            AttackPressed = Input.GetButtonDown(attackButton) || Input.GetKeyDown(attackKey);
            MirrorBladePressed = Input.GetButtonDown(mirrorBladeButton) || Input.GetKeyDown(mirrorBladeKey);
            EchoDashPressed = Input.GetButtonDown(echoDashButton) || Input.GetKeyDown(echoDashKey);
        }
    }
}
