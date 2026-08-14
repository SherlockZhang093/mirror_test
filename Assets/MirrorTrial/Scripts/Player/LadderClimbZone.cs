using UnityEngine;

namespace MirrorTrial.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class LadderClimbZone : MonoBehaviour
    {
        [Header("Exits")]
        [SerializeField] Transform topExit;
        [SerializeField] Transform topExitLeft;
        [SerializeField] Transform topExitRight;

        [Header("Bounds")]
        [SerializeField] float topInset = 0.15f;
        [SerializeField] float bottomInset = 0.1f;
        [SerializeField, Min(0f)] float topClimbOffset = 0.55f;
        [SerializeField, Min(0f)] float entryTolerance = 0.25f;

        [Header("Top Support")]
        [SerializeField] bool createTopSupport;
        [SerializeField] string topSupportLayerName = "Ground";
        [SerializeField, Min(0.1f)] float topSupportWidth = 1.2f;
        [SerializeField, Min(0.02f)] float topSupportThickness = 0.2f;

        BoxCollider2D trigger;
        BoxCollider2D topSupport;
        bool topEntryArmed;

        public float CenterX => trigger ? trigger.bounds.center.x : transform.position.x;
        public float BottomY => trigger ? trigger.bounds.min.y + bottomInset : transform.position.y;
        public float TopY => topExit
            ? topExit.position.y - topClimbOffset
            : (trigger ? trigger.bounds.max.y - topInset : transform.position.y);
        public Vector2 TopExitPosition => topExit ? (Vector2)topExit.position : new Vector2(CenterX, TopY + 0.85f);
        public Collider2D TopSupportCollider => topSupport;

        public bool CanEnterFromBottom(float playerRootY) =>
            playerRootY <= BottomY + entryTolerance;

        public bool CanEnterFromTop(float playerRootY) =>
            playerRootY >= TopY - entryTolerance;

        void Awake()
        {
            ConfigureTrigger();
            EnsureTopSupport();
        }

        void OnValidate()
        {
            ConfigureTrigger();
        }

        void Reset()
        {
            ConfigureTrigger();
            trigger.size = new Vector2(0.9f, 5f);
        }

        void ConfigureTrigger()
        {
            trigger = GetComponent<BoxCollider2D>();
            if (!trigger)
                return;

            trigger.isTrigger = true;
            trigger.offset = Vector2.zero;
        }

        public Vector2 GetTopExitPosition(float direction)
        {
            var selectedExit = direction < 0f ? topExitLeft : topExitRight;
            if (selectedExit)
                return selectedExit.position;

            var basePosition = TopExitPosition;
            var supportWorldHalfWidth =
                topSupportWidth * Mathf.Abs(transform.lossyScale.x) * 0.5f;
            var fallbackOffset = Mathf.Max(supportWorldHalfWidth, trigger.bounds.extents.x) + 0.35f;
            return new Vector2(CenterX + Mathf.Sign(direction) * fallbackOffset, basePosition.y);
        }

        void EnsureTopSupport()
        {
            if (!createTopSupport)
                return;

            var supportTransform = transform.Find("LadderTopSupport");
            if (!supportTransform)
            {
                var supportObject = new GameObject("LadderTopSupport");
                supportTransform = supportObject.transform;
                supportTransform.SetParent(transform, false);
            }

            var supportLayer = LayerMask.NameToLayer(topSupportLayerName);
            supportTransform.gameObject.layer = supportLayer >= 0
                ? supportLayer
                : gameObject.layer;

            topSupport = supportTransform.GetComponent<BoxCollider2D>();
            if (!topSupport)
                topSupport = supportTransform.gameObject.AddComponent<BoxCollider2D>();

            supportTransform.position = new Vector2(
                CenterX,
                TopExitPosition.y - topSupportThickness * 0.5f);
            topSupport.isTrigger = false;
            topSupport.size = new Vector2(
                Mathf.Max(topSupportWidth, trigger.size.x),
                topSupportThickness);
        }

        void OnTriggerStay2D(Collider2D other)
        {
            var traversal = other.GetComponentInParent<PlayerTraversalController>();
            var input = other.GetComponentInParent<PlayerInputReader>();
            if (!traversal || !input || !input.InputEnabled)
                return;

            var playerRootY = traversal.transform.position.y;
            if (CanEnterFromTop(playerRootY))
            {
                if (input.MoveY >= -0.25f)
                    topEntryArmed = true;
                else if (topEntryArmed)
                {
                    topEntryArmed = false;
                    traversal.TryEnterLadder(this, -1f);
                }
            }
            else if (input.MoveY > 0.25f && CanEnterFromBottom(playerRootY))
                traversal.TryEnterLadder(this, 1f);
        }

        void OnTriggerExit2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerTraversalController>())
                topEntryArmed = false;
        }
    }
}
