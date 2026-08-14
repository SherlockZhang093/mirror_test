using UnityEngine;

namespace MirrorTrial.Puzzles
{
    [DisallowMultipleComponent]
    public sealed class WindLightReceiverPresentation : MonoBehaviour
    {
        [SerializeField] PuzzleChargePresentation chargePresentation;

        bool completed;

        public void SetState(float normalizedCharge, bool receivingLight, bool powered)
        {
            if (!chargePresentation) return;

            chargePresentation.SetReceivingLight(receivingLight);
            if (powered)
            {
                if (!completed)
                    chargePresentation.Complete();
                completed = true;
                return;
            }

            if (completed)
                chargePresentation.ResetPresentation();
            completed = false;
            chargePresentation.SetProgress(Mathf.Clamp01(normalizedCharge));
        }

        public void ResetPresentation()
        {
            completed = false;
            if (chargePresentation)
                chargePresentation.ResetPresentation();
        }

        void OnDisable()
        {
            ResetPresentation();
        }
    }
}
