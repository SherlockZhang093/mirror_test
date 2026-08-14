using UnityEngine;

namespace MirrorTrial.Puzzles
{
    [DisallowMultipleComponent]
    public sealed class WindLightReceiver : MonoBehaviour
    {
        [Header("Light Reception")]
        [SerializeField] Transform receivePoint;
        [SerializeField, Min(0.01f)] float receiveRadius = 0.5f;
        [SerializeField, Min(0.05f)] float chargeDuration = 0.9f;

        [Header("Wind Output")]
        [SerializeField] GameObject airflow;
        [SerializeField] GameObject poweredPedestal;
        [SerializeField] GameObject unpoweredPedestal;

        [Header("Receiver Light")]
        [SerializeField] WindLightReceiverPresentation indicator;

        bool receivingLight;
        bool powered;
        bool airflowShutDown;
        float chargeProgress;

        public Transform BeamPoint => receivePoint ? receivePoint : transform;
        public bool IsPowered => powered;

        void Awake()
        {
            ResetReceiver();
        }

        void Update()
        {
            if (!powered && !receivingLight)
            {
                chargeProgress = 0f;
            }
            else if (!powered)
            {
                chargeProgress = Mathf.Min(chargeDuration, chargeProgress + Time.deltaTime);
                if (chargeProgress >= chargeDuration)
                    SetPowered(true);
            }

            UpdateIndicator();
        }

        public bool TryReceiveBeam(Vector2 origin, Vector2 direction, float maximumDistance,
            out Vector2 receivePosition)
        {
            var center = (Vector2)BeamPoint.position;
            receivePosition = center;
            var centerDistance = Vector2.Dot(center - origin, direction);
            if (centerDistance < 0f || centerDistance > maximumDistance + receiveRadius)
                return false;

            var closest = origin + direction * centerDistance;
            var perpendicularDistance = Vector2.Distance(closest, center);
            if (perpendicularDistance > receiveRadius)
                return false;

            var distanceToEdge = Mathf.Sqrt(
                Mathf.Max(0f, receiveRadius * receiveRadius -
                               perpendicularDistance * perpendicularDistance));
            var entryDistance = Mathf.Max(0f, centerDistance - distanceToEdge);
            if (entryDistance > maximumDistance)
                return false;

            receivePosition = origin + direction * entryDistance;
            return true;
        }

        public void SetLit(bool lit)
        {
            receivingLight = lit;
        }

        public void ShutDownAirflow()
        {
            airflowShutDown = true;
            if (airflow) airflow.SetActive(false);
        }

        void ResetReceiver()
        {
            receivingLight = false;
            chargeProgress = 0f;
            SetPowered(false);
            UpdateIndicator();
        }

        void SetPowered(bool value)
        {
            powered = value;
            if (airflow) airflow.SetActive(value && !airflowShutDown);
            if (poweredPedestal) poweredPedestal.SetActive(value);
            if (unpoweredPedestal) unpoweredPedestal.SetActive(!value);
        }

        void UpdateIndicator()
        {
            if (indicator)
                indicator.SetState(chargeProgress / chargeDuration, receivingLight, powered);
        }

        void OnDisable()
        {
            ResetReceiver();
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);
            Gizmos.DrawWireSphere(BeamPoint.position, receiveRadius);
        }
    }
}
