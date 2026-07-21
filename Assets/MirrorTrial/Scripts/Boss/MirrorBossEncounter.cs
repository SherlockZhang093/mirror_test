using MirrorTrial.Level;
using UnityEngine;

namespace MirrorTrial.Boss
{
    public sealed class MirrorBossEncounter : MonoBehaviour
    {
        [SerializeField] CombatEncounter encounter;
        [SerializeField] MirrorBossController boss;
        [SerializeField] float clearDelay = 1.5f;

        void Awake()
        {
            if (!encounter) encounter = GetComponent<CombatEncounter>();
            if (!boss) boss = GetComponentInChildren<MirrorBossController>(true);
        }

        void OnEnable()
        {
            if (boss) boss.Defeated += OnBossDefeated;
        }

        void OnDisable()
        {
            if (boss) boss.Defeated -= OnBossDefeated;
        }

        public void BeginBossFight()
        {
            if (!boss) return;
            CameraDirector.Ensure().PlayBossIntro(boss.transform, () =>
            {
                encounter?.StartEncounter();
                boss.Activate();
            });
        }

        void OnBossDefeated(MirrorBossController defeatedBoss)
        {
            Invoke(nameof(Clear), clearDelay);
        }

        void Clear()
        {
            encounter?.ClearEncounter();
        }
    }
}


