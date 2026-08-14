using System;
using UnityEngine;

namespace MirrorTrial.Feedback
{
    /// <summary>Gameplay-facing signal bus. Callers never need a manager or overlay reference.</summary>
    public static class ScreenFx
    {
        internal static event Action<ScreenFxSignal> SignalSent;

        public static void Send(ScreenFxSignal signal) => SignalSent?.Invoke(signal);

        public static void Play(ScreenFxType type, float intensity = -1f, float duration = -1f,
            Vector2 direction = default, UnityEngine.Object source = null)
        {
            Send(new ScreenFxSignal(type, ScreenFxSignalMode.Play, intensity, duration, direction, source));
        }

        public static void Begin(ScreenFxType type, UnityEngine.Object source, float intensity = -1f)
        {
            Send(new ScreenFxSignal(type, ScreenFxSignalMode.Begin, intensity, source: source));
        }

        public static void End(ScreenFxType type, UnityEngine.Object source)
        {
            Send(new ScreenFxSignal(type, ScreenFxSignalMode.End, source: source));
        }

        public static void Clear(ScreenFxType type)
        {
            Send(new ScreenFxSignal(type, ScreenFxSignalMode.Clear));
        }
    }
}
