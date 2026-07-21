using System.Linq;
using Cinemachine;
using MirrorTrial.Level;
using UnityEditor;
using UnityEngine;

namespace MirrorTrial.Editor
{
    public static class CameraSetupUtil
    {
        public const float DefaultOrthographicSize = 6f;
        public static readonly Color DefaultBackgroundColor = new Color(0.08f, 0.09f, 0.14f);

        /// <summary>
        /// Applies the default main-scene camera configuration and binds a follow target.
        /// If bounds are not provided, they are computed from all scene colliders.
        /// Override per-level only when special camera behavior is needed.
        /// </summary>
        public static void ApplyDefault(Camera camera, Transform followTarget, bool snap = true,
            float? boundsMinX = null, float? boundsMaxX = null)
        {
            if (!camera) return;

            camera.orthographic = true;
            camera.orthographicSize = DefaultOrthographicSize;
            camera.backgroundColor = DefaultBackgroundColor;
            camera.clearFlags = CameraClearFlags.SolidColor;

            if (!camera.GetComponent<CinemachineBrain>())
                camera.gameObject.AddComponent<CinemachineBrain>();
        }

        /// <summary>
        /// Computes scene horizontal bounds from Collider2D bounds.
        /// If root is provided, only colliders under that root are considered.
        /// Excludes trigger-only colliders.
        /// </summary>
        public static (float minX, float maxX) ComputeSceneBounds(Transform root = null)
        {
            var colliders = root
                ? root.GetComponentsInChildren<Collider2D>(true)
                : Object.FindObjectsOfType<Collider2D>();

            var boundsList = colliders
                .Where(c => c && !c.isTrigger)
                .Select(c => c.bounds)
                .ToList();

            if (boundsList.Count == 0) return (float.MinValue, float.MaxValue);

            var minX = boundsList.Min(b => b.min.x);
            var maxX = boundsList.Max(b => b.max.x);
            return (minX, maxX);
        }

        [MenuItem("Tools/镜像试炼/关卡/相机/应用默认相机设置")]
        static void ApplyDefaultToCurrentScene()
        {
            var camera = Camera.main;
            if (!camera)
            {
                Debug.LogWarning("[镜像试炼] 当前场景里找不到 Main Camera。");
                return;
            }

            var player = GameObject.Find("Player_MirrorTrial");
            if (!player)
            {
                Debug.LogWarning("[镜像试炼] 当前场景里找不到 Player_MirrorTrial。");
                return;
            }

            ApplyDefault(camera, player.transform, snap: true);
            EditorUtility.SetDirty(camera.gameObject);
            Debug.Log("[镜像试炼] 已根据场景边界应用默认相机设置。");
        }
    }
}
