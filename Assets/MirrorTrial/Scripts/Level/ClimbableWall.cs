using UnityEngine;

namespace MirrorTrial.Level
{
    /// <summary>
    /// Marks solid Ground geometry as a surface that supports wall clinging and wall jumping.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider2D))]
    public sealed class ClimbableWall : MonoBehaviour
    {
    }
}
