using UnityEngine;

namespace MirrorTrial.Feedback
{
    public enum ScreenFxType
    {
        PlayerHit,
        BossBattle,
        BossPhasePulse,
        LowHealth,
        Death
    }

    public enum ScreenFxSignalMode
    {
        Play,
        Begin,
        End,
        Clear
    }

    public readonly struct ScreenFxSignal
    {
        public readonly ScreenFxType Type;
        public readonly ScreenFxSignalMode Mode;
        public readonly float Intensity;
        public readonly float Duration;
        public readonly Vector2 Direction;
        public readonly Object Source;

        public ScreenFxSignal(
            ScreenFxType type,
            ScreenFxSignalMode mode = ScreenFxSignalMode.Play,
            float intensity = -1f,
            float duration = -1f,
            Vector2 direction = default,
            Object source = null)
        {
            Type = type;
            Mode = mode;
            Intensity = intensity;
            Duration = duration;
            Direction = direction;
            Source = source;
        }
    }
}
