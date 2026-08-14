using UnityEngine;

namespace MirrorTrial.Level
{
    public class AreaGate : MonoBehaviour
    {
        [Header("基础")]
        [ChineseLabel("门ID")] [Tooltip("门ID")] [SerializeField] string gateId = "Gate_01";
        [ChineseLabel("初始开启")] [Tooltip("初始开启")] [SerializeField] bool initialOpen = true;

        [Header("引用")]
        [ChineseLabel("碰撞体")] [Tooltip("碰撞体（关门时启用）")] [SerializeField] Collider2D gateCollider;
        [ChineseLabel("视觉对象")] [Tooltip("视觉对象")] [SerializeField] GameObject visualObject;

        bool isOpen;

        public string GateId => gateId;
        public bool IsOpen => isOpen;

        void Awake()
        {
            isOpen = initialOpen;
            ApplyState();
        }

        public void Open()
        {
            SetState(true);
        }

        public void Close()
        {
            SetState(false);
        }

        public void SetState(bool open)
        {
            isOpen = open;
            ApplyState();
        }

        void ApplyState()
        {
            if (gateCollider)
                gateCollider.enabled = !isOpen;

            if (visualObject)
            {
                visualObject.SetActive(true);
                var sr = visualObject.GetComponent<SpriteRenderer>();
                if (sr)
                    sr.color = isOpen ? new Color(0.2f, 1f, 0.3f, 0.85f) : new Color(1f, 0.25f, 0.25f, 0.9f);
            }
        }

        void OnDrawGizmos()
        {
            var color = isOpen ? new Color(0.2f, 1f, 0.3f, 0.9f) : new Color(1f, 0.25f, 0.25f, 0.9f);
            Gizmos.color = color;
            var pos = transform.position;
            Gizmos.DrawLine(pos + Vector3.up, pos + Vector3.down);
            Gizmos.DrawCube(pos, new Vector3(0.15f, 2f, 0.02f));
        }
    }
}
