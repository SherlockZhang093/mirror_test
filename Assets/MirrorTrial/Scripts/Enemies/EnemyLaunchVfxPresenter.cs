using UnityEngine;

namespace MirrorTrial.Enemies
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyLaunchController2D), typeof(Collider2D))]
    public sealed class EnemyLaunchVfxPresenter : MonoBehaviour
    {
        EnemyLaunchController2D controller;
        Collider2D bodyCollider;
        GameObject followedEffect;

        void Awake()
        {
            controller = GetComponent<EnemyLaunchController2D>();
            bodyCollider = GetComponent<Collider2D>();
        }

        void OnEnable() => controller.PhaseChanged += OnPhaseChanged;

        void OnDisable()
        {
            controller.PhaseChanged -= OnPhaseChanged;
            StopFollowedEffect();
        }

        void OnPhaseChanged(EnemyLaunchController2D.LaunchPhase phase, Vector2 velocity)
        {
            var profile = controller.Settings?.presentation;
            if (!profile) return;

            switch (phase)
            {
                case EnemyLaunchController2D.LaunchPhase.Rising:
                    Spawn(profile.takeoffPrefab, BodyCenter());
                    StartFollowedEffect(profile.airbornePrefab);
                    break;
                case EnemyLaunchController2D.LaunchPhase.Falling:
                    break;
                case EnemyLaunchController2D.LaunchPhase.Landing:
                    StopFollowedEffect();
                    Spawn(profile.landingPrefab, GroundPoint());
                    break;
                case EnemyLaunchController2D.LaunchPhase.Sliding:
                    StartFollowedEffect(profile.slidePrefab, GroundPoint());
                    break;
                case EnemyLaunchController2D.LaunchPhase.Knockdown:
                    StopFollowedEffect();
                    break;
                case EnemyLaunchController2D.LaunchPhase.None:
                    StopFollowedEffect();
                    break;
            }
        }

        void Spawn(GameObject prefab, Vector3 position)
        {
            if (prefab) Instantiate(prefab, position, Quaternion.identity);
        }

        void StartFollowedEffect(GameObject prefab, Vector3? position = null)
        {
            StopFollowedEffect();
            if (!prefab) return;
            followedEffect = Instantiate(prefab, position ?? BodyCenter(), Quaternion.identity, transform);
        }

        void StopFollowedEffect()
        {
            if (followedEffect) Destroy(followedEffect);
            followedEffect = null;
        }

        Vector3 BodyCenter() => bodyCollider ? bodyCollider.bounds.center : transform.position;

        Vector3 GroundPoint()
        {
            if (!bodyCollider) return transform.position;
            return new Vector3(bodyCollider.bounds.center.x, bodyCollider.bounds.min.y, transform.position.z);
        }
    }
}
