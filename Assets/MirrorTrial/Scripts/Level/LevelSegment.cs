using UnityEngine;

namespace MirrorTrial.Level
{
    public class LevelSegment : MonoBehaviour
    {
        [Header("基础")]
        [ChineseLabel("段落ID")] [Tooltip("段落ID")] [SerializeField] string segmentId = "SEG_A";
        [ChineseLabel("显示名")] [Tooltip("显示名")] [SerializeField] string displayName = "Segment A";

        [Header("范围与状态")]
        [ChineseLabel("范围碰撞体")] [Tooltip("范围碰撞体")] [SerializeField] BoxCollider2D boundsCollider;
        [ChineseLabel("开始时启用")] [Tooltip("开始时启用")] [SerializeField] bool startEnabled = true;

        public string SegmentId => segmentId;
        public string DisplayName => displayName;
        public BoxCollider2D BoundsCollider => boundsCollider;

        public bool IsEnabled { get; private set; }

        void Awake()
        {
            EnsureTriggerCollider();
            IsEnabled = startEnabled;
        }

        void Reset()
        {
            EnsureTriggerCollider();
        }

        void OnValidate()
        {
            EnsureTriggerCollider();
        }

        void EnsureTriggerCollider()
        {
            var col = boundsCollider ? boundsCollider : GetComponent<BoxCollider2D>();
            if (!col) return;

            boundsCollider = col;
            col.isTrigger = true;
        }

        public void Enable()
        {
            IsEnabled = true;
            gameObject.SetActive(true);
        }

        public void Disable()
        {
            IsEnabled = false;
            if (Application.isPlaying)
                gameObject.SetActive(false);
        }

        void OnDrawGizmos()
        {
            var col = boundsCollider ? boundsCollider : GetComponent<BoxCollider2D>();
            if (!col) return;

            var c = col.bounds.center;
            var s = Vector2.Scale(col.size, transform.lossyScale);
            Gizmos.color = new Color(0.6f, 0.3f, 1f, 0.08f);
            Gizmos.DrawCube(c, new Vector3(s.x, s.y, 0.02f));
            Gizmos.color = new Color(0.65f, 0.5f, 1f, 0.95f);
            Gizmos.DrawWireCube(c, new Vector3(s.x, s.y, 0.02f));
        }
    }
}
