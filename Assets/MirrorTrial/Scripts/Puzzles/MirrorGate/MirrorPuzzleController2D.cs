using System.Collections;
using UnityEngine;

namespace MirrorTrial.Puzzles.MirrorGate
{
    [DisallowMultipleComponent]
    public sealed class MirrorPuzzleController2D : MonoBehaviour
    {
        [SerializeField] FixedLightSource2D lightSource;
        [SerializeField] RotatingMirror2D mirror;
        [SerializeField] MirrorLightReceiver2D receiver;
        [SerializeField] StoneGate2D gate;
        [SerializeField] LineRenderer reflectedBeam;
        [SerializeField, Min(0)] int correctMirrorState = 1;
        [SerializeField] bool keepSolvedPermanently = true;
        [SerializeField] LayerMask obstacleMask;
        [SerializeField, Min(1f)] float maxReflectedDistance = 20f;
        [SerializeField, Min(0.01f)] float receiverHitRadius = 0.35f;
        [SerializeField, Min(0.001f)] float rayStartOffset = 0.02f;
        [SerializeField] Color reflectedBeamColor = new Color(0.2f, 1f, 1f, 0.95f);
        [SerializeField, Min(0.005f)] float reflectedBeamWidth = 0.06f;
        [SerializeField] string sortingLayerName = "Default";
        [SerializeField] int sortingOrder = 21;
        [SerializeField, Min(0f)] float gateOpenDelay = 0.2f;
        [SerializeField] SpriteRenderer[] linkRunes;
        [SerializeField] Color runeInactiveColor = new Color(0.12f, 0.24f, 0.28f, 0.65f);
        [SerializeField] Color runeActiveColor = new Color(0.2f, 1f, 1f, 1f);
        [SerializeField, Min(0f)] float runeStepDelay = 0.06f;

        readonly RaycastHit2D[] hits = new RaycastHit2D[12];
        bool isSolved;
        bool solving;
        Coroutine solveRoutine;

        public bool IsSolved => isSolved;
        public int CorrectMirrorState => correctMirrorState;

        void Awake()
        {
            ConfigureLine();
            SetRunes(false);
            if (reflectedBeam) reflectedBeam.enabled = false;
        }

        void OnEnable()
        {
            if (mirror) mirror.StateChanged += OnMirrorStateChanged;
        }

        void Start() => EvaluatePuzzle();

        void LateUpdate() => EvaluatePuzzle();

        void OnDisable()
        {
            if (mirror) mirror.StateChanged -= OnMirrorStateChanged;
        }

        void OnMirrorStateChanged(int state) => EvaluatePuzzle();

        public void EvaluatePuzzle()
        {
            if (isSolved && keepSolvedPermanently) return;
            if (!mirror || !receiver || !lightSource || !gate)
            {
                HideReflectedBeam();
                return;
            }

            var incomingClear = lightSource.RefreshBeam();
            var endpoint = (Vector2)mirror.BeamPoint.position;
            var receiverHit = false;
            var reflected = incomingClear && TryTraceReflectedBeam(out endpoint, out receiverHit);
            if (reflected)
                ShowReflectedBeam(mirror.BeamPoint.position, endpoint);
            else
                HideReflectedBeam();

            var stableCorrectHit = receiverHit && !mirror.IsRotating &&
                                   mirror.CurrentState == correctMirrorState;
            if (stableCorrectHit)
            {
                if (!solving && !isSolved) solveRoutine = StartCoroutine(SolveRoutine());
            }
            else if (!isSolved)
            {
                if (solveRoutine != null) StopCoroutine(solveRoutine);
                solveRoutine = null;
                solving = false;
                receiver.Deactivate();
                SetRunes(false);
            }
        }

        bool TryTraceReflectedBeam(out Vector2 endpoint, out bool receiverHit)
        {
            var start = (Vector2)mirror.BeamPoint.position;
            var incident = start - (Vector2)lightSource.BeamStartPoint.position;
            endpoint = start;
            receiverHit = false;
            if (incident.sqrMagnitude <= 0.0001f) return false;

            var normal = mirror.ReflectionNormal;
            if (normal.sqrMagnitude <= 0.0001f) return false;
            incident.Normalize();
            normal.Normalize();
            if (Vector2.Dot(incident, normal) > 0f) normal = -normal;
            var direction = Vector2.Reflect(incident, normal).normalized;
            if (direction.sqrMagnitude <= 0.0001f) return false;

            var maximumDistance = maxReflectedDistance;
            var castStart = start + direction * rayStartOffset;
            var count = Physics2D.RaycastNonAlloc(castStart, direction, hits,
                Mathf.Max(0f, maxReflectedDistance - rayStartOffset), obstacleMask);
            for (var i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (!hit.collider || hit.collider.transform.IsChildOf(mirror.transform) ||
                    hit.collider.transform.IsChildOf(receiver.transform) || hit.collider.isTrigger)
                    continue;
                maximumDistance = Mathf.Min(maximumDistance, hit.distance + rayStartOffset);
            }

            receiverHit = TryHitReceiver(start, direction, maximumDistance);
            endpoint = receiverHit
                ? (Vector2)receiver.ReceiverPoint.position
                : start + direction * maximumDistance;
            return true;
        }

        bool TryHitReceiver(Vector2 start, Vector2 direction, float maximumDistance)
        {
            var toReceiver = (Vector2)receiver.ReceiverPoint.position - start;
            var distanceAlongRay = Vector2.Dot(toReceiver, direction);
            if (distanceAlongRay <= rayStartOffset || distanceAlongRay > maximumDistance) return false;

            var closestPoint = start + direction * distanceAlongRay;
            return ((Vector2)receiver.ReceiverPoint.position - closestPoint).sqrMagnitude <=
                   receiverHitRadius * receiverHitRadius;
        }

        IEnumerator SolveRoutine()
        {
            solving = true;
            receiver.Activate();
            if (linkRunes != null)
            {
                for (var i = 0; i < linkRunes.Length; i++)
                {
                    if (linkRunes[i]) linkRunes[i].color = runeActiveColor;
                    if (runeStepDelay > 0f) yield return new WaitForSecondsRealtime(runeStepDelay);
                }
            }
            if (gateOpenDelay > 0f) yield return new WaitForSecondsRealtime(gateOpenDelay);
            gate.Open();
            isSolved = true;
            solving = false;
            solveRoutine = null;
            if (keepSolvedPermanently) mirror.LockSolved();
        }

        void ConfigureLine()
        {
            if (!reflectedBeam) return;
            reflectedBeam.useWorldSpace = true;
            reflectedBeam.positionCount = 2;
            reflectedBeam.startWidth = reflectedBeam.endWidth = reflectedBeamWidth;
            reflectedBeam.startColor = reflectedBeam.endColor = reflectedBeamColor;
            reflectedBeam.sortingLayerName = sortingLayerName;
            reflectedBeam.sortingOrder = sortingOrder;
        }

        void ShowReflectedBeam(Vector3 start, Vector2 end)
        {
            if (!reflectedBeam) return;
            reflectedBeam.SetPosition(0, start);
            reflectedBeam.SetPosition(1, end);
            reflectedBeam.enabled = true;
        }

        void HideReflectedBeam()
        {
            if (reflectedBeam) reflectedBeam.enabled = false;
        }

        void SetRunes(bool active)
        {
            if (linkRunes == null) return;
            foreach (var rune in linkRunes)
                if (rune) rune.color = active ? runeActiveColor : runeInactiveColor;
        }

        void OnDrawGizmosSelected()
        {
            if (!receiver) return;
            Gizmos.color = new Color(0.2f, 1f, 1f, 0.7f);
            Gizmos.DrawWireSphere(receiver.ReceiverPoint.position, receiverHitRadius);
        }
    }
}
