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
                    SpawnGroundAligned(profile.landingPrefab, GroundPoint());
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

        void SpawnGroundAligned(GameObject prefab, Vector3 groundPoint)
        {
            if (!prefab) return;

            var instance = Instantiate(prefab, groundPoint, Quaternion.identity);
            var renderers = instance.GetComponentsInChildren<SpriteRenderer>();
            if (renderers.Length == 0) return;

            var lowestY = float.MaxValue;
            foreach (var spriteRenderer in renderers)
            {
                if (spriteRenderer.enabled)
                    lowestY = Mathf.Min(lowestY, spriteRenderer.bounds.min.y);
            }

            if (lowestY < float.MaxValue)
                instance.transform.position += Vector3.up * (groundPoint.y - lowestY);
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
