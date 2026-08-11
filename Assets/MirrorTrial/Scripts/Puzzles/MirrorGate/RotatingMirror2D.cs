using System;
using System.Collections;
using MirrorTrial.Player;
using UnityEngine;
using UnityEngine.Serialization;

namespace MirrorTrial.Puzzles.MirrorGate
{
    [DisallowMultipleComponent]
    public sealed class RotatingMirror2D : MonoBehaviour
    {
        [SerializeField] Transform mirrorPivot;
        [SerializeField] Transform beamPoint;
        [SerializeField] float[] presetAngles = { -28f, 0f, 28f };
        [SerializeField, Min(0)] int initialState;
        [Tooltip("Mirror surface direction in the pivot's local 2D space. The reflected ray uses this angle as the real optical surface.")]
        [SerializeField, Range(-180f, 180f)] float reflectionSurfaceLocalAngle = -79.65f;
        [SerializeField, Min(0.01f)] float rotateDuration = 0.15f;
        [SerializeField, Min(0.1f)] float interactionRadius = 1.6f;
        [FormerlySerializedAs("hitCooldown")]
        [SerializeField, Min(0f)] float interactionCooldown = 0.2f;
        [SerializeField] AudioSource audioSource;
        [SerializeField] AudioClip rotateSound;
        [SerializeField, Range(0f, 1f)] float rotateVolume = 0.8f;
        [SerializeField] ParticleSystem rotateEffect;
        [SerializeField, Min(0f)] float hitShakeDistance = 0.06f;

        int currentState;
        float nextAcceptedInteractionTime;
        bool rotating;
        bool puzzleLocked;
        Coroutine rotationRoutine;
        PlayerInputReader nearbyInput;

        public event Action<int> StateChanged;
        public int CurrentState => currentState;
        public bool IsRotating => rotating;
        public Transform BeamPoint => beamPoint ? beamPoint : (mirrorPivot ? mirrorPivot : transform);
        public Vector2 ReflectionSurfaceDirection
        {
            get
            {
                var radians = reflectionSurfaceLocalAngle * Mathf.Deg2Rad;
                var localDirection = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f);
                var worldDirection = mirrorPivot
                    ? mirrorPivot.TransformDirection(localDirection)
                    : transform.TransformDirection(localDirection);
                return ((Vector2)worldDirection).normalized;
            }
        }
        public Vector2 ReflectionNormal
        {
            get
            {
                var surface = ReflectionSurfaceDirection;
                return new Vector2(-surface.y, surface.x);
            }
        }

        void Awake()
        {
            if (!mirrorPivot) mirrorPivot = transform;
            currentState = ClampState(initialState);
            ApplyCurrentAngle();
        }

        void Update()
        {
            if (!nearbyInput)
            {
                var input = FindObjectOfType<PlayerInputReader>();
                if (input && Vector2.Distance(input.transform.position, transform.position) <= interactionRadius)
                    nearbyInput = input;
            }
            else if (Vector2.Distance(nearbyInput.transform.position, transform.position) > interactionRadius)
            {
                nearbyInput = null;
            }

            if (!puzzleLocked && nearbyInput && nearbyInput.InteractPressed)
                RotateNext();
        }

        public void RotateNext()
        {
            if (puzzleLocked || rotating || Time.unscaledTime < nextAcceptedInteractionTime ||
                presetAngles == null || presetAngles.Length == 0)
                return;

            nextAcceptedInteractionTime = Time.unscaledTime + interactionCooldown;
            var nextState = (currentState + 1) % presetAngles.Length;
            if (rotationRoutine != null) StopCoroutine(rotationRoutine);
            rotationRoutine = StartCoroutine(RotateToState(nextState));
        }

        public void LockSolved() => puzzleLocked = true;

        IEnumerator RotateToState(int nextState)
        {
            rotating = true;
            var startAngle = mirrorPivot.localEulerAngles.z;
            var targetAngle = presetAngles[nextState];
            var startPosition = mirrorPivot.localPosition;
            if (audioSource && rotateSound) audioSource.PlayOneShot(rotateSound, rotateVolume);
            if (rotateEffect) rotateEffect.Play();

            var elapsed = 0f;
            while (elapsed < rotateDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / rotateDuration);
                t = t * t * (3f - 2f * t);
                var angle = Mathf.LerpAngle(startAngle, targetAngle, t);
                var shake = Mathf.Sin(t * Mathf.PI * 4f) * (1f - t) * hitShakeDistance;
                mirrorPivot.localRotation = Quaternion.Euler(0f, 0f, angle);
                mirrorPivot.localPosition = startPosition + Vector3.right * shake;
                yield return null;
            }

            mirrorPivot.localPosition = startPosition;
            mirrorPivot.localRotation = Quaternion.Euler(0f, 0f, targetAngle);
            currentState = nextState;
            rotating = false;
            rotationRoutine = null;
            StateChanged?.Invoke(currentState);
        }

        int ClampState(int state)
        {
            return presetAngles == null || presetAngles.Length == 0
                ? 0
                : Mathf.Clamp(state, 0, presetAngles.Length - 1);
        }

        void ApplyCurrentAngle()
        {
            if (!mirrorPivot || presetAngles == null || presetAngles.Length == 0) return;
            mirrorPivot.localRotation = Quaternion.Euler(0f, 0f, presetAngles[currentState]);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.7f);
            Gizmos.DrawWireSphere(transform.position, interactionRadius);

            var center = BeamPoint ? BeamPoint.position : transform.position;
            var surface = ReflectionSurfaceDirection;
            Gizmos.DrawLine(center - (Vector3)surface, center + (Vector3)surface);
            Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.8f);
            Gizmos.DrawLine(center, center + (Vector3)ReflectionNormal * 0.75f);
        }
    }
}
