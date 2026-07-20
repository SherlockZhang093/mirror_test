using System;
using System.Collections;
using Cinemachine;
using MirrorTrial.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MirrorTrial.CameraSystem
{
    [DefaultExecutionOrder(-50)]
    public sealed class CameraDirector : MonoBehaviour
    {
        public static CameraDirector Instance { get; private set; }

        [Header("Cinemachine Cameras")]
        [SerializeField] CinemachineVirtualCamera playerCamera;
        [SerializeField] CinemachineVirtualCamera shotCamera;
        [SerializeField] CinemachineBrain brain;
        [SerializeField] int playerPriority = 10;
        [SerializeField] int shotPriority = 100;

        Coroutine activeShot;
        PlayerInputReader lockedInput;
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
            SceneManager.sceneLoaded += OnSceneLoaded;
            ResolveBrain();
        }

        void Start()
        {
            ResolvePlayerTarget();
            ConfigurePlayerCamera();
        }

        public void Configure(CinemachineVirtualCamera player, CinemachineVirtualCamera shot)
        {
            playerCamera = player;
            shotCamera = shot;
            ResolveBrain();
            ConfigurePlayerCamera();
        }

        public void SetPlayerTarget(Transform target)
        {
            playerTarget = target;
            if (playerCamera)
                playerCamera.Follow = target;
        }

        public void PlayShot(Transform target, CameraShotPreset preset, Action onComplete = null)
        {
            if (!target || !shotCamera || !playerCamera)
            {
                onComplete?.Invoke();
                return;
            }

            StopActiveShot();
            activeShot = StartCoroutine(PlayShotRoutine(target, preset, onComplete));
        }

        public void StopShot()
        {
            StopActiveShot();
            RestorePlayerCamera();
        }

        IEnumerator PlayShotRoutine(Transform target, CameraShotPreset preset, Action onComplete)
        {
            preset = preset ? preset : ScriptableObject.CreateInstance<CameraShotPreset>();
            if (preset.lockPlayerInput)
                LockPlayerInput();

            shotCamera.Follow = target;
            shotCamera.LookAt = target;
            shotCamera.GetCinemachineComponent<CinemachineFramingTransposer>().m_TrackedObjectOffset = preset.targetOffset;
            shotCamera.m_Lens.OrthographicSize = preset.orthographicSize;
            shotCamera.Priority = shotPriority;

            yield return Wait(preset.blendIn);
            if (!target)
            {
                RestorePlayerCamera();
                yield break;
            }

            yield return Wait(preset.holdDuration);
            shotCamera.Priority = playerPriority;
            yield return Wait(preset.blendOut);

            RestorePlayerCamera();
            onComplete?.Invoke();
            activeShot = null;
        }

        IEnumerator Wait(float duration)
        {
            if (duration <= 0f) yield break;
            yield return new WaitForSecondsRealtime(duration);
        }

        void ConfigurePlayerCamera()
        {
            if (!playerCamera) return;
            playerCamera.Priority = playerPriority;
            playerCamera.Follow = playerTarget;
            playerCamera.LookAt = null;
        }

        void RestorePlayerCamera()
        {
            if (shotCamera) shotCamera.Priority = playerPriority;
            if (playerCamera) playerCamera.Priority = playerPriority;
            UnlockPlayerInput();
        }

        void LockPlayerInput()
        {
            lockedInput = FindObjectOfType<PlayerInputReader>();
            if (lockedInput) lockedInput.InputEnabled = false;
        }

        void UnlockPlayerInput()
        {
            if (lockedInput) lockedInput.InputEnabled = true;
            lockedInput = null;
        }

        void ResolvePlayerTarget()
        {
            var input = FindObjectOfType<PlayerInputReader>();
            SetPlayerTarget(input ? input.transform : null);
        }

        void ResolveBrain()
        {
            if (!brain && Camera.main)
                brain = Camera.main.GetComponent<CinemachineBrain>();
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ResolveBrain();
            ResolvePlayerTarget();
            ConfigurePlayerCamera();
        }

        void StopActiveShot()
        {
            if (activeShot != null)
                StopCoroutine(activeShot);
            activeShot = null;
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this) Instance = null;
        }
    }
}
