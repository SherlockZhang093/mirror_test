using UnityEngine;

namespace MirrorTrial.Player
{
    /// <summary>
    /// Temporary debug overlay. Shows input state + motor state on screen.
    /// Attach to the Player or any object in the scene.
    /// Delete this script once debugging is done.
    /// </summary>
    public class PlayerDebugOverlay : MonoBehaviour
    {
        PlayerInputReader input;
        PlayerMotor motor;

        void Awake()
        {
            // Try to find on self first, then search scene
            input = GetComponent<PlayerInputReader>();
            motor = GetComponent<PlayerMotor>();

            if (input == null)
                input = FindObjectOfType<PlayerInputReader>();
            if (motor == null)
                motor = FindObjectOfType<PlayerMotor>();
        }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 400));
            GUILayout.Label("<size=14><b>== Player Debug ==</b></size>");

            if (input == null)
            {
                GUILayout.Label("<color=red>PlayerInputReader NOT FOUND</color>");
            }
            else
            {
                GUILayout.Label($"InputEnabled: {input.InputEnabled}");
                GUILayout.Label($"MoveX: {input.MoveX:F2}");
                GUILayout.Label($"JumpPressed: {input.JumpPressed}");
                GUILayout.Label($"AttackPressed: {input.AttackPressed}");
                GUILayout.Label($"MirrorBlade: {input.MirrorBladePressed}");
                GUILayout.Label($"EchoDash: {input.EchoDashPressed}");
            }

            GUILayout.Space(10);

            if (motor == null)
            {
                GUILayout.Label("<color=red>PlayerMotor NOT FOUND</color>");
            }
            else
            {
                GUILayout.Label($"IsGrounded: {motor.IsGrounded}");
                GUILayout.Label($"Velocity: {motor.Velocity}");
                GUILayout.Label($"MoveX (motor): {motor.MoveX:F2}");
                GUILayout.Label($"MovementLocked: {motor.MovementLocked}");
                GUILayout.Label($"Position: {motor.transform.position}");
            }

            GUILayout.EndArea();
        }
    }
}
