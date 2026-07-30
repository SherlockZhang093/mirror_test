using UnityEngine;

namespace MirrorTrial.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyLaunchController2D))]
    public sealed class EnemyLaunchAnimationPresenter : MonoBehaviour
    {
        EnemyLaunchController2D controller;
        Animator animator;

        void Awake()
        {
            controller = GetComponent<EnemyLaunchController2D>();
            animator = GetComponentInChildren<Animator>(true);
        }

        void OnEnable() => controller.PhaseChanged += OnPhaseChanged;
        void OnDisable() => controller.PhaseChanged -= OnPhaseChanged;

        void OnPhaseChanged(EnemyLaunchController2D.LaunchPhase phase, Vector2 velocity)
        {
            var profile = controller.Settings?.presentation;
            if (!profile || !animator) return;
            if (phase == EnemyLaunchController2D.LaunchPhase.HitCompress)
                Play(profile.launchAnimation);
            else if (phase == EnemyLaunchController2D.LaunchPhase.Airborne)
                SetLaunchFrame(profile.launchAnimation, 3, true);
            else if (phase == EnemyLaunchController2D.LaunchPhase.Falling)
                SetLaunchFrame(profile.launchAnimation, 5, true);
            else if (phase == EnemyLaunchController2D.LaunchPhase.Landing)
                SetLaunchFrame(profile.launchAnimation, 6, false);
            else if (phase == EnemyLaunchController2D.LaunchPhase.Recovering)
                Play(profile.getUpAnimation);
            else if (phase == EnemyLaunchController2D.LaunchPhase.None)
                animator.speed = 1f;
        }

        void SetLaunchFrame(string stateName, int frame, bool hold)
        {
            if (string.IsNullOrWhiteSpace(stateName)) return;
            var hash = Animator.StringToHash(stateName);
            if (!animator.HasState(0, hash)) return;
            animator.Play(hash, 0, Mathf.Clamp01(frame / 8f));
            animator.Update(0f);
            animator.speed = hold ? 0f : 1f;
        }

        void Play(string stateName)
        {
            if (string.IsNullOrWhiteSpace(stateName)) return;
            var hash = Animator.StringToHash(stateName);
            if (!animator.HasState(0, hash)) return;
            animator.speed = 1f;
            animator.CrossFade(hash, 0.04f, 0, 0f);
        }
    }
}
