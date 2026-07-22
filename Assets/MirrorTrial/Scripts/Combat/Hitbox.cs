using System.Collections.Generic;
using MirrorTrial.Feedback;
using UnityEngine;

namespace MirrorTrial.Combat
{
    [RequireComponent(typeof(Collider2D))]
    public class Hitbox : MonoBehaviour
    {
        [SerializeField] Collider2D hitboxCollider;

        readonly HashSet<int> hitTargets = new HashSet<int>();
        DamagePayload payload;
        bool sharedFeedbackTriggered;

        void Awake()
        {
            if (!hitboxCollider)
                hitboxCollider = GetComponent<Collider2D>();
            hitboxCollider.isTrigger = true;
        }

        public void Configure(DamagePayload nextPayload)
        {
            payload = nextPayload;
        }

        public void SetActive(bool active)
        {
            if (!hitboxCollider) return;
            if (active && !hitboxCollider.enabled)
            {
                hitTargets.Clear();
                sharedFeedbackTriggered = false;
            }
            hitboxCollider.enabled = active;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (payload.source && other.transform.root.gameObject == payload.source)
                return;

            var hurtbox = other.GetComponent<Hurtbox>();
            if (!hurtbox)
                return;

            var targetId = hurtbox.transform.root.GetInstanceID();
            if (!hitTargets.Add(targetId))
                return;

            var contactPoint = ResolveContactPoint(other);
            hurtbox.ReceiveHit(payload);
            PlayEffect(contactPoint);

            if (sharedFeedbackTriggered)
                return;
            sharedFeedbackTriggered = true;

            var feedback = payload.feedback;
            var legacyFeedback = !feedback.requestHitStop && !feedback.requestCamera &&
                !feedback.playEffect && !feedback.playSound && !feedback.notifyAttackerReaction &&
                payload.hitStop > 0f;
            if (feedback.requestHitStop || legacyFeedback)
                HitStopService.Request(feedback.requestHitStop ? feedback.hitStopDuration : payload.hitStop);
            if (feedback.requestCamera)
                CameraFeedbackService.RequestHit(payload.direction, feedback.cameraPower);
            else if (legacyFeedback)
                CameraFeedbackService.RequestHit(payload);
            if (feedback.playSound && feedback.sound)
                AudioSource.PlayClipAtPoint(feedback.sound, contactPoint, feedback.soundVolume);
            if (feedback.notifyAttackerReaction && payload.source)
                payload.source.SendMessage("OnAttackHitConfirmed", payload, SendMessageOptions.DontRequireReceiver);
        }

        Vector2 ResolveContactPoint(Collider2D other)
        {
            var fromAttack = hitboxCollider.ClosestPoint(other.bounds.center);
            var fromTarget = other.ClosestPoint(hitboxCollider.bounds.center);
            return (fromAttack + fromTarget) * 0.5f;
        }

        void PlayEffect(Vector2 contactPoint)
        {
            var feedback = payload.feedback;
            if (!feedback.playEffect || !feedback.effectPrefab)
                return;

            var directionSign = payload.direction.x < 0f ? -1f : 1f;
            var offset = feedback.effectOffset;
            offset.x *= directionSign;
            var effect = Instantiate(feedback.effectPrefab, contactPoint + offset, Quaternion.identity);
            if (feedback.mirrorEffectByDirection)
            {
                var scale = effect.transform.localScale;
                scale.x = Mathf.Abs(scale.x) * directionSign;
                effect.transform.localScale = scale;
            }
            Destroy(effect, feedback.effectLifetime);
        }
    }
}
