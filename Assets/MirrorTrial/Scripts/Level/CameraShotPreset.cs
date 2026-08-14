using UnityEngine;

namespace MirrorTrial.Level
{
    [CreateAssetMenu(menuName = "Mirror Trial/Camera Shot Preset", fileName = "CameraShotPreset")]
    public class CameraShotPreset : ScriptableObject
    {
        [Min(0f)] public float blendIn = 0.5f;
        [Min(0f)] public float holdDuration = 1.2f;
        [Min(0f)] public float blendOut = 0.4f;
        public Vector3 targetOffset = new Vector3(0f, 0.8f, 0f);
        [Min(0.1f)] public float orthographicSize = 4f;
        public bool lockPlayerInput = true;
        public bool useUnscaledTime = true;
        public bool useImpulse;
        public Cinemachine.CinemachineImpulseDefinition impulseDefinition = new Cinemachine.CinemachineImpulseDefinition();
    }
}
