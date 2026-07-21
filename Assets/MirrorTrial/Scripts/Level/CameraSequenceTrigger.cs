using System;
using UnityEngine;

namespace MirrorTrial.Level
{
    public sealed class CameraSequenceTrigger : MonoBehaviour
    {
        [SerializeField] CameraShotTarget target;
        [SerializeField] CameraShotPreset preset;
        [SerializeField] bool playOnce = true;
        [SerializeField] bool playOnEnable;

        bool played;

        public event Action Completed;

        void OnEnable()
        {
            if (playOnEnable) Play();
        }

        public void Play()
        {
            if (playOnce && played) return;
            if (!target || !preset)
            {
                Completed?.Invoke();
                return;
            }

            played = true;
            CameraDirector.Ensure().PlayShot(target.FocusPoint, preset, () => Completed?.Invoke());
        }

        public void ResetTrigger()
        {
            played = false;
        }
    }
}
