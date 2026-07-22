using MirrorTrial.Combat;
using MirrorTrial.Level;
using UnityEngine;

namespace MirrorTrial.Feedback
{
    public static class CameraFeedbackService
    {
        public static void RequestHit(DamagePayload payload)
        {
            RequestHit(payload.direction, EstimatePower(payload));
        }

        public static void RequestHit(Vector2 direction, float power)
        {
            CameraDirector.Ensure().PlayHitFeedback(direction, Mathf.Clamp01(power));
        }

        static float EstimatePower(DamagePayload payload)
        {
            var knockbackPower = Mathf.InverseLerp(2f, 12f, payload.knockback.magnitude);
            var hitStopPower = Mathf.InverseLerp(0.025f, 0.1f, payload.hitStop);
            var damagePower = Mathf.InverseLerp(1f, 4f, payload.damage);
            return Mathf.Clamp01(knockbackPower * 0.45f + hitStopPower * 0.35f + damagePower * 0.2f);
        }
    }
}
