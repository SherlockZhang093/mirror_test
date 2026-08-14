using MirrorTrial.Level;
using UnityEngine;

using MirrorTrial.Feedback;

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
            if (!boss) return;
            boss.Defeated += OnBossDefeated;
            boss.PhaseChanged += OnBossPhaseChanged;
        }

        void OnDisable()
        {
            if (boss)
            {
                boss.Defeated -= OnBossDefeated;
                boss.PhaseChanged -= OnBossPhaseChanged;
            }
            ScreenFx.End(ScreenFxType.BossBattle, this);
        }

        public void BeginBossFight()
        {
            if (!boss) return;
            ScreenFx.Begin(ScreenFxType.BossBattle, this);
            CameraDirector.Ensure().PlayBossIntro(boss.transform, () =>
            {
                encounter?.StartEncounter();
                boss.Activate();
            });
        }

        void OnBossPhaseChanged(MirrorBossController changedBoss, int phase)
        {
            ScreenFx.Play(ScreenFxType.BossPhasePulse, Mathf.Clamp01(0.65f + phase * 0.12f), source: this);
        }

        void OnBossDefeated(MirrorBossController defeatedBoss)
        {
            ScreenFx.End(ScreenFxType.BossBattle, this);
            Invoke(nameof(Clear), clearDelay);
        }

        void Clear()
        {
            encounter?.ClearEncounter();
        }
    }
}


