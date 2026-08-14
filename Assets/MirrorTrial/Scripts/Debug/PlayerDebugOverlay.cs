using UnityEngine;

namespace MirrorTrial.Player
{
    public class PlayerDebugOverlay : MonoBehaviour
    {
        PlayerInputReader input;
        PlayerMotor motor;
        PlayerStateMachine stateMachine;
        PlayerWeaponController weapons;
        PlayerBowCombat bowCombat;

        void Awake()
        {
            input = GetComponent<PlayerInputReader>();
            motor = GetComponent<PlayerMotor>();
            stateMachine = GetComponent<PlayerStateMachine>();
            weapons = GetComponent<PlayerWeaponController>();
            bowCombat = GetComponent<PlayerBowCombat>();

            if (input == null)
                input = FindObjectOfType<PlayerInputReader>();
            if (motor == null)
                motor = FindObjectOfType<PlayerMotor>();
            if (stateMachine == null)
                stateMachine = FindObjectOfType<PlayerStateMachine>();
            if (weapons == null)
                weapons = FindObjectOfType<PlayerWeaponController>();
            if (bowCombat == null)
                bowCombat = FindObjectOfType<PlayerBowCombat>();
        }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 320, 420));
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
                GUILayout.Label($"CurrentWeapon: {(weapons ? weapons.CurrentWeapon.ToString() : "NOT FOUND")}");
                GUILayout.Label($"WeaponSlot: {(weapons ? weapons.ActiveSlotIndex + 1 : 0)}");
                GUILayout.Label($"BowBusy: {(bowCombat && bowCombat.IsBusy)}");
            }

            GUILayout.Space(10);

            if (motor == null)
            {
                GUILayout.Label("<color=red>PlayerMotor NOT FOUND</color>");
            }
            else
            {
                GUILayout.Label($"IsGrounded: {motor.IsGrounded}");
                GUILayout.Label($"State: {(stateMachine ? stateMachine.CurrentState.ToString() : "NOT FOUND")}");
                GUILayout.Label($"Velocity: {motor.Velocity}");
                GUILayout.Label($"MoveX (motor): {motor.MoveX:F2}");
                GUILayout.Label($"MovementLocked: {motor.MovementLocked}");
                GUILayout.Label($"Position: {motor.transform.position}");
                GUILayout.Label($"TimeScale: {Time.timeScale:F2}");
            }

            GUILayout.EndArea();
        }
    }
}
