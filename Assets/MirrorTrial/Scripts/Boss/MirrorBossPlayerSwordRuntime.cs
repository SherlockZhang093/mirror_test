using MirrorTrial.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MirrorTrial.Boss
{
    public sealed class MirrorBossPlayerSwordRuntime : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (!SceneManager.GetActiveScene().name.Equals("Level_Mirror_01", System.StringComparison.OrdinalIgnoreCase)) return;
            new GameObject("MirrorBossPlayerSwordRuntime").AddComponent<MirrorBossPlayerSwordRuntime>();
        }

        void Update()
        {
            var reader = FindObjectOfType<PlayerInputReader>();
            var weapons = reader ? reader.GetComponent<PlayerWeaponController>() : null;
            if (!weapons) return;
            weapons.TryEquipSlot(1);
            Destroy(gameObject);
        }
    }
}
