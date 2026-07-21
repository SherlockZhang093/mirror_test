using MirrorTrial.Player;
using UnityEngine;

namespace MirrorTrial.Boss
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class MirrorBossPlayerCollisionIgnore : MonoBehaviour
    {
        Collider2D bossCollider;
        Collider2D playerCollider;

        void Awake() => bossCollider = GetComponent<Collider2D>();

        void Update()
        {
            if (playerCollider) return;
            var player = FindObjectOfType<PlayerInputReader>();
            playerCollider = player ? player.GetComponent<Collider2D>() : null;
            if (bossCollider && playerCollider) Physics2D.IgnoreCollision(bossCollider, playerCollider, true);
        }

        void OnDestroy()
        {
            if (bossCollider && playerCollider) Physics2D.IgnoreCollision(bossCollider, playerCollider, false);
        }
    }
}
