using UnityEngine;

namespace MirrorTrial.Level
{
    /// <summary>
    /// Prevents the player from grabbing or climbing this ledge.
    /// The collider remains solid and can still be used as normal ground.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class NonClimbableLedge : MonoBehaviour
    {
    }
}
