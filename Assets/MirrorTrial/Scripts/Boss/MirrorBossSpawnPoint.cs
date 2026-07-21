using UnityEngine;

namespace MirrorTrial.Boss
{
    public sealed class MirrorBossSpawnPoint : MonoBehaviour
    {
#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.95f);
            Gizmos.DrawWireSphere(transform.position, 0.35f);
            Gizmos.DrawLine(transform.position + Vector3.left * 0.55f, transform.position + Vector3.right * 0.55f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 1.4f);
        }
#endif
    }
}
