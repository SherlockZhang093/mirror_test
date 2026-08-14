using UnityEngine;

namespace MirrorTrial.Boss
{
    [DisallowMultipleComponent]
    public sealed class MirrorBossAttackDebugGizmo : MonoBehaviour
    {
        [SerializeField] bool drawOnlyWhenSelected = true;
        MirrorBossActorV2 actor;
        BoxCollider2D attackCollider;

        void Awake()
        {
            actor = GetComponent<MirrorBossActorV2>();
            var hitbox = GetComponentInChildren<MirrorTrial.Combat.Hitbox>(true);
            attackCollider = hitbox ? hitbox.GetComponent<BoxCollider2D>() : null;
        }

        void OnDrawGizmos() { if (!drawOnlyWhenSelected) Draw(); }
        void OnDrawGizmosSelected() { if (drawOnlyWhenSelected) Draw(); }

        void Draw()
        {
            if (!actor) actor = GetComponent<MirrorBossActorV2>();
            if (!attackCollider)
            {
                var hitbox = GetComponentInChildren<MirrorTrial.Combat.Hitbox>(true);
                attackCollider = hitbox ? hitbox.GetComponent<BoxCollider2D>() : null;
            }
            if (!attackCollider) return;
            Gizmos.color = attackCollider.enabled ? new Color(1f, 0.15f, 0.1f, 0.9f) : new Color(0.45f, 0.45f, 0.45f, 0.35f);
            var matrix = Gizmos.matrix;
            Gizmos.matrix = attackCollider.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(attackCollider.offset, attackCollider.size);
            Gizmos.matrix = matrix;
        }
    }
}
