using System;
using MirrorTrial.Player;
using UnityEngine;

namespace MirrorTrial.Puzzles
{
    [DisallowMultipleComponent]
    public sealed class RotatablePuzzleMirror : MonoBehaviour
    {
        [SerializeField] Transform rotatingVisual;
        [SerializeField] Transform beamPoint;
        [SerializeField] MirrorSurfaceLine2D reflectionSurface;
        [SerializeField] float[] angleSteps = { -28f, 0f, 28f };
        [SerializeField, Min(0)] int correctStep = 6;
        [SerializeField, Min(0)] int currentStep;
        [SerializeField] bool lockWhenCorrect;
        [SerializeField, Min(0.1f)] float interactionRadius = 1.6f;

        PlayerInputReader nearbyInput;
        bool locked;

        public event Action Changed;
        public bool IsCorrect => currentStep == correctStep;
        public Transform BeamPoint => beamPoint ? beamPoint : transform;
        public MirrorSurfaceLine2D ReflectionSurface
        {
            get
            {
                if (!reflectionSurface)
                    reflectionSurface = GetComponentInChildren<MirrorSurfaceLine2D>(true);
                return reflectionSurface;
            }
        }

        void Awake()
        {
            if (!rotatingVisual) rotatingVisual = transform;
            if (!reflectionSurface) reflectionSurface = GetComponentInChildren<MirrorSurfaceLine2D>(true);
            ApplyAngle();
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

            if (!locked && nearbyInput && nearbyInput.InteractPressed)
                RotateNext();
        }

        public void RotateNext()
        {
            if (angleSteps == null || angleSteps.Length == 0 || locked)
                return;
            currentStep = (currentStep + 1) % angleSteps.Length;
            ApplyAngle();
            if (lockWhenCorrect && IsCorrect) locked = true;
            Changed?.Invoke();
        }

        void ApplyAngle()
        {
            if (!rotatingVisual || angleSteps == null || angleSteps.Length == 0) return;
            currentStep = Mathf.Clamp(currentStep, 0, angleSteps.Length - 1);
            rotatingVisual.localRotation = Quaternion.Euler(0f, 0f, angleSteps[currentStep]);
        }

    }
}
