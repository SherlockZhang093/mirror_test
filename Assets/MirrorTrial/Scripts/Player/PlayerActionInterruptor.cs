using UnityEngine;

namespace MirrorTrial.Player
{
    public enum PlayerActionCancelReason { Hit, Knockdown, Death }

    public interface IInterruptiblePlayerAction
    {
        void CancelCurrentAction(PlayerActionCancelReason reason);
    }

    [DisallowMultipleComponent]
    public sealed class PlayerActionInterruptor : MonoBehaviour
    {
        PlayerStateMachine stateMachine;
        IInterruptiblePlayerAction[] actions;

        void Awake()
        {
            stateMachine = GetComponent<PlayerStateMachine>();
            var behaviours = GetComponents<MonoBehaviour>();
            var count = 0;
            for (var i = 0; i < behaviours.Length; i++)
                if (behaviours[i] is IInterruptiblePlayerAction) count++;
            actions = new IInterruptiblePlayerAction[count];
            var index = 0;
            for (var i = 0; i < behaviours.Length; i++)
                if (behaviours[i] is IInterruptiblePlayerAction action) actions[index++] = action;
        }

        public void CancelAll(PlayerActionCancelReason reason)
        {
            for (var i = 0; i < actions.Length; i++) actions[i].CancelCurrentAction(reason);
            if (stateMachine) stateMachine.CancelRequestedAction();
        }
    }
}
