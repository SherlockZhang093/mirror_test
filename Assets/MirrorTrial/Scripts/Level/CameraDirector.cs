using System;
using System.Collections;
using MirrorTrial.Player;
using UnityEngine;
using Cinemachine;

namespace MirrorTrial.Level
{
    public class CameraDirector : MonoBehaviour
    {
        public static CameraDirector Instance { get; private set; }

        [Header("Cinemachine Cameras")]
        [SerializeField] CinemachineVirtualCamera playerCamera;
        [SerializeField] CinemachineVirtualCamera shotCamera;
        [SerializeField] CinemachineBrain brain;
        [SerializeField] int playerPriority = 10;
        [SerializeField] int shotPriority = 20;
        [SerializeField] CinemachineImpulseSource impulseSource;
        [SerializeField] CameraTuning tuning;

        Coroutine activeShot;
        PlayerInputReader lockedPlayer;
        Transform playerTarget;

        public bool IsPlaying => activeShot != null;
        public CinemachineVirtualCamera PlayerCamera => playerCamera;
        public CinemachineVirtualCamera ShotCamera => shotCamera;

        void Awake()
        {
            if (Instance && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            if (!tuning) tuning = Resources.Load<CameraTuning>("CameraTuning");
            if (!tuning) tuning = ScriptableObject.CreateInstance<CameraTuning>();
            EnsureCameraComponents();
        }

        void OnDestroy()
        {
            StopShot();
            if (Instance == this) Instance = null;
        }

        public static CameraDirector Ensure()
        {
            if (Instance) return Instance;
            var go = new GameObject("CameraDirector");
            return go.AddComponent<CameraDirector>();
        }

        public void SetPlayerTarget(Transform target)
        {
            playerTarget = target;
            if (!playerCamera) return;
            playerCamera.Follow = target;
            playerCamera.LookAt = target;
        }

        public void SetBrain(CinemachineBrain value)
        {
            brain = value;
        }

        public void SyncLens(Camera source)
        {
            if (!source) return;
            SyncLens(playerCamera, source);
            SyncLens(shotCamera, source);
        }

        public void SetHorizontalBounds(float minX, float maxX)
        {
            if (!playerCamera) return;
            var confiner = playerCamera.GetComponent<CinemachineHorizontalConfiner>();
            if (!confiner) confiner = playerCamera.gameObject.AddComponent<CinemachineHorizontalConfiner>();
            confiner.SetBounds(minX, maxX);
        }

        public void PlayHitFeedback(Vector2 direction, float power)
        {
            if (!impulseSource) return;
            var impulseDirection = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.right;
            var strength = Mathf.Lerp(tuning.lightHitStrength, tuning.heavyHitStrength, Mathf.Clamp01(power));
            impulseSource.GenerateImpulseWithVelocity((Vector3)impulseDirection * strength);
        }

        public void PlayBossIntro(Transform target, Action onComplete = null)
        {
            if (!target) { onComplete?.Invoke(); return; }
            var preset = ScriptableObject.CreateInstance<CameraShotPreset>();
            preset.blendIn = tuning.bossBlendIn;
            preset.holdDuration = tuning.bossHold;
            preset.blendOut = tuning.bossBlendOut;
            preset.orthographicSize = tuning.bossOrthographicSize;
            preset.targetOffset = tuning.bossTargetOffset;
            preset.lockPlayerInput = true;
            preset.useUnscaledTime = true;
            PlayShot(target, preset, () =>
            {
                Destroy(preset);
                onComplete?.Invoke();
            });
        }
        static void SyncLens(CinemachineVirtualCamera camera, Camera source)
        {
            if (!camera) return;
            camera.m_Lens.Orthographic = source.orthographic;
            if (source.orthographic) camera.m_Lens.OrthographicSize = source.orthographicSize;
        }

        public void PlayShot(Transform target, CameraShotPreset preset, Action onComplete = null)
        {
            if (!target || !preset) { onComplete?.Invoke(); return; }
            StopShot(false);
            activeShot = StartCoroutine(PlayShotRoutine(target, preset, onComplete));
        }

        public void StopShot(bool invokeRecovery = true)
        {
            if (activeShot != null)
            {
                StopCoroutine(activeShot);
                activeShot = null;
            }
            UnlockPlayer();
            if (shotCamera) shotCamera.Priority = playerPriority - 1;
            if (invokeRecovery && playerCamera) playerCamera.Priority = playerPriority;
        }

        IEnumerator PlayShotRoutine(Transform target, CameraShotPreset preset, Action onComplete)
        {
            if (preset.lockPlayerInput)
            {
                lockedPlayer = FindObjectOfType<PlayerInputReader>();
                if (lockedPlayer) lockedPlayer.InputEnabled = false;
            }

            shotCamera.Follow = target;
            shotCamera.LookAt = target;
            var framing = shotCamera.GetCinemachineComponent<CinemachineFramingTransposer>();
            if (framing) framing.m_TrackedObjectOffset = preset.targetOffset;
            shotCamera.m_Lens.OrthographicSize = preset.orthographicSize;
            SetBlend(preset.blendIn);
            shotCamera.Priority = shotPriority;
            yield return Wait(preset.blendIn + preset.holdDuration, preset.useUnscaledTime);

            if (!target)
            {
                StopShot();
                yield break;
            }

            SetBlend(preset.blendOut);
            shotCamera.Priority = playerPriority - 1;
            yield return Wait(preset.blendOut, preset.useUnscaledTime);
            UnlockPlayer();
            activeShot = null;
            onComplete?.Invoke();
        }

        static IEnumerator Wait(float duration, bool unscaled)
        {
            if (duration <= 0f) yield break;
            if (unscaled) yield return new WaitForSecondsRealtime(duration);
            else yield return new WaitForSeconds(duration);
        }

        void SetBlend(float duration)
        {
            if (!brain) return;
            brain.m_DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Style.EaseInOut, Mathf.Max(0f, duration));
        }

        void UnlockPlayer()
        {
            if (lockedPlayer) lockedPlayer.InputEnabled = true;
            lockedPlayer = null;
        }

        void EnsureCameraComponents()
        {
            if (!playerCamera) playerCamera = CreateVirtualCamera("PlayerCamera", playerPriority);
            if (!shotCamera) shotCamera = CreateVirtualCamera("ShotCamera", playerPriority - 1);
            EnsureFraming(playerCamera);
            EnsureFraming(shotCamera);
            ConfigurePlayerFraming(playerCamera.GetCinemachineComponent<CinemachineFramingTransposer>());
            ConfigureShotFraming(shotCamera.GetCinemachineComponent<CinemachineFramingTransposer>());
            if (!playerCamera.GetComponent<CinemachineHorizontalConfiner>())
                playerCamera.gameObject.AddComponent<CinemachineHorizontalConfiner>();
            ConfigureImpulseListener(GetOrAddImpulseListener(playerCamera));
            ConfigureImpulseListener(GetOrAddImpulseListener(shotCamera));
            if (!impulseSource) impulseSource = gameObject.AddComponent<CinemachineImpulseSource>();
            ConfigureImpulseSource(impulseSource);
        }

        CinemachineVirtualCamera CreateVirtualCamera(string name, int priority)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform);
            var camera = go.AddComponent<CinemachineVirtualCamera>();
            camera.Priority = priority;
            return camera;
        }

        static void EnsureFraming(CinemachineVirtualCamera camera)
        {
            if (camera && !camera.GetCinemachineComponent<CinemachineFramingTransposer>())
                camera.AddCinemachineComponent<CinemachineFramingTransposer>();
        }

        static void ConfigurePlayerFraming(CinemachineFramingTransposer framing)
        {
            if (!framing) return;
            framing.m_TrackedObjectOffset = new Vector3(0f, 1.25f, 0f);
            framing.m_LookaheadTime = 0.12f;
            framing.m_LookaheadSmoothing = 0.15f;
            framing.m_XDamping = 0.22f;
            framing.m_YDamping = 0.32f;
            framing.m_ZDamping = 0f;
            framing.m_ScreenX = 0.5f;
            framing.m_ScreenY = 0.45f;
            framing.m_DeadZoneWidth = 0.08f;
            framing.m_DeadZoneHeight = 0.12f;
            framing.m_SoftZoneWidth = 0.8f;
            framing.m_SoftZoneHeight = 0.72f;
            framing.m_CameraDistance = 10f;
        }

        static void ConfigureShotFraming(CinemachineFramingTransposer framing)
        {
            if (!framing) return;
            framing.m_LookaheadTime = 0f;
            framing.m_LookaheadSmoothing = 0f;
            framing.m_XDamping = 0.3f;
            framing.m_YDamping = 0.3f;
            framing.m_ZDamping = 0f;
            framing.m_ScreenX = 0.5f;
            framing.m_ScreenY = 0.5f;
            framing.m_DeadZoneWidth = 0f;
            framing.m_DeadZoneHeight = 0f;
        }

        static CinemachineImpulseListener GetOrAddImpulseListener(CinemachineVirtualCamera camera)
        {
            if (!camera) return null;
            var listener = camera.GetComponent<CinemachineImpulseListener>();
            return listener ? listener : camera.gameObject.AddComponent<CinemachineImpulseListener>();
        }

        void ConfigureImpulseListener(CinemachineImpulseListener listener)
        {
            if (!listener) return;
            listener.m_ApplyAfter = CinemachineCore.Stage.Noise;
            listener.m_ChannelMask = 1;
            listener.m_Gain = tuning.impulseGain;
            listener.m_Use2DDistance = true;
            listener.m_UseCameraSpace = true;
        }

        void ConfigureImpulseSource(CinemachineImpulseSource source)
        {
            if (!source) return;
            var definition = source.m_ImpulseDefinition ?? new CinemachineImpulseDefinition();
            definition.m_ImpulseChannel = 1;
            definition.m_ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump;
            definition.m_ImpulseDuration = tuning.impulseDuration;
            definition.m_ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
            definition.m_DissipationDistance = 100f;
            source.m_ImpulseDefinition = definition;
        }    }
}






