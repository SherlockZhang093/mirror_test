using MirrorTrial.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MirrorTrial.Boss
{
    public sealed class MirrorBossPlayerWeaponScope : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            new GameObject("MirrorBossPlayerWeaponScope").AddComponent<MirrorBossPlayerWeaponScope>();
        }

        void Update()
        {
            var reader = FindObjectOfType<PlayerInputReader>();
            var weapons = reader ? reader.GetComponent<PlayerWeaponController>() : null;
            if (!weapons) return;
            var mirror = SceneManager.GetActiveScene().name.Equals("Level_Mirror_01", System.StringComparison.OrdinalIgnoreCase);
            weapons.TryEquipSlot(mirror ? 1 : 0);
            Destroy(gameObject);
        }
    }
}
