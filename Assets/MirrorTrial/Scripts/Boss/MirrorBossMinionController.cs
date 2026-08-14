using System.Collections.Generic;
using MirrorTrial.Enemies;
using UnityEngine;

namespace MirrorTrial.Boss
{
    public sealed class MirrorBossMinionController : MonoBehaviour
    {
        readonly List<GameObject> alive = new List<GameObject>();
        MirrorBossSimpleProfile profile;
        int totalSummoned;
        float arenaLeft = float.NegativeInfinity;
        float arenaRight = float.PositiveInfinity;

        public int AliveCount
        {
            get
            {
                alive.RemoveAll(item => !item);
                return alive.Count;
            }
        }

        public void Configure(MirrorBossSimpleProfile bossProfile, float left, float right)
        {
            profile = bossProfile;
            arenaLeft = Mathf.Min(left, right);
            arenaRight = Mathf.Max(left, right);
        }

        public int GetMaximumAlive(int phase)
        {
            if (!profile) return 0;
            return phase <= 1 ? profile.phaseOneMaxMinions
                : phase == 2 ? profile.phaseTwoMaxMinions : profile.phaseThreeMaxMinions;
        }

        public bool CanSummon(int phase)
        {
            return profile && profile.minionPrefab && totalSummoned < profile.totalSummonLimit &&
                   AliveCount < GetMaximumAlive(phase);
        }

        public int SummonToCurrentLimit(int phase)
        {
            if (!CanSummon(phase)) return 0;
            var remainingTotal = profile.totalSummonLimit - totalSummoned;
            var count = Mathf.Min(GetMaximumAlive(phase) - AliveCount, remainingTotal);
            var spawned = 0;
            for (var i = 0; i < count; i++)
            {
                var direction = (i & 1) == 0 ? -1f : 1f;
                var x = transform.position.x + direction * profile.minionSpawnHorizontalOffset;
                if (!float.IsInfinity(arenaLeft) && !float.IsInfinity(arenaRight))
                    x = Mathf.Clamp(x, arenaLeft + 0.5f, arenaRight - 0.5f);
                var instance = Instantiate(profile.minionPrefab,
                    new Vector3(x, transform.position.y, transform.position.z), Quaternion.identity);
                var link = instance.GetComponent<MirrorBossMinionLink>() ?? instance.AddComponent<MirrorBossMinionLink>();
                link.Bind(this);
                alive.Add(instance);
                totalSummoned++;
                spawned++;
            }
            return spawned;
        }

        public void NotifyDestroyed(GameObject minion)
        {
            if (minion) alive.Remove(minion);
            else alive.RemoveAll(item => !item);
        }

        public void ClearAll()
        {
            for (var i = alive.Count - 1; i >= 0; i--)
                if (alive[i]) Destroy(alive[i]);
            alive.Clear();
        }
    }

    public sealed class MirrorBossMinionLink : MonoBehaviour
    {
        MirrorBossMinionController owner;
        public void Bind(MirrorBossMinionController value) => owner = value;
        void OnDestroy() => owner?.NotifyDestroyed(gameObject);
    }
}
