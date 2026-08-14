using MirrorTrial.Level;
using UnityEngine;

namespace MirrorTrial.Feedback
{
    /// <summary>
    /// Global entry point for every gameplay camera shake.
    /// Power is normalized to 0..1; direction controls the impulse direction.
    /// </summary>
    public static class CameraShakeService
    {
        public static void Shake(float power)
        {
            Shake(Vector2.right, power);
        }

        public static void Shake(Vector2 direction, float power)
        {
            CameraDirector.Ensure().Shake(direction, Mathf.Clamp01(power));
        }
    }
}
