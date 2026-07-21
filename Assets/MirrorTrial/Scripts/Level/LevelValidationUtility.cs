using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MirrorTrial.Level
{
    public static class LevelValidationUtility
    {
        public static List<LevelValidationIssue> Validate(LevelManager manager)
        {
            var issues = new List<LevelValidationIssue>();
            if (!manager)
            {
                issues.Add(new LevelValidationIssue(LevelValidationSeverity.Error, "LevelManager 为空。", null));
                return issues;
            }

            manager.CollectAll();

            if (!manager.PlayerSpawn)
                issues.Add(new LevelValidationIssue(LevelValidationSeverity.Error, "未配置 PlayerSpawn。", manager));

            if (!manager.GeometryRoot)
                issues.Add(new LevelValidationIssue(LevelValidationSeverity.Warning, "未配置 GeometryRoot。", manager));

            ValidateSegments(manager, issues);
            ValidateEncounters(manager, issues);
            ValidateTriggers(manager, issues);
            ValidateGates(manager, issues);
            ValidateMirrorGates(manager, issues);
            ValidateSpawnPoints(manager, issues);

            return issues;
        }

        static void ValidateSegments(LevelManager manager, List<LevelValidationIssue> issues)
        {
            foreach (var segment in manager.Segments)
            {
                if (!segment) continue;
                if (string.IsNullOrWhiteSpace(segment.SegmentId))
                    issues.Add(new LevelValidationIssue(LevelValidationSeverity.Error, $"Segment '{segment.name}' 未设置 ID。", segment));
                if (!segment.BoundsCollider)
                    issues.Add(new LevelValidationIssue(LevelValidationSeverity.Error, $"Segment '{segment.name}' 未设置 BoundsCollider。", segment));
            }
        }

        static void ValidateEncounters(LevelManager manager, List<LevelValidationIssue> issues)
        {
            foreach (var encounter in manager.Encounters)
            {
                if (!encounter) continue;
                if (string.IsNullOrWhiteSpace(encounter.EncounterId))
                    issues.Add(new LevelValidationIssue(LevelValidationSeverity.Error, $"Encounter '{encounter.name}' 未设置 ID。", encounter));

                bool hasPlacedEnemies = encounter.PlacedEnemies != null && encounter.PlacedEnemies.Count > 0;
                if (!hasPlacedEnemies && !encounter.HasWaves)
                {
                    issues.Add(new LevelValidationIssue(LevelValidationSeverity.Warning, $"Encounter '{encounter.name}' 没有登记场景敌人。", encounter));
                    continue;
                }

                var waves = encounter.Waves;

                for (var i = 0; i < waves.Count; i++)
                {
                    var wave = waves[i];
                    if (wave == null || wave.spawnEntries == null) continue;
                    for (var j = 0; j < wave.spawnEntries.Length; j++)
                    {
                        var entry = wave.spawnEntries[j];
                        if (entry == null) continue;
                        if (!entry.enemyPrefab && (!entry.spawnPoint || !entry.spawnPoint.DefaultEnemyPrefab))
                            issues.Add(new LevelValidationIssue(LevelValidationSeverity.Error, $"Encounter '{encounter.name}' Wave {i} Entry {j} 未配置敌人。", encounter));
                    }
                }
            }
        }

        static void ValidateTriggers(LevelManager manager, List<LevelValidationIssue> issues)
        {
            foreach (var trigger in manager.Triggers)
            {
                if (!trigger) continue;
                if (string.IsNullOrWhiteSpace(trigger.TriggerId))
                    issues.Add(new LevelValidationIssue(LevelValidationSeverity.Error, $"Trigger '{trigger.name}' 未设置 ID。", trigger));

                if (trigger.Actions == null || trigger.Actions.Count == 0)
                {
                    issues.Add(new LevelValidationIssue(LevelValidationSeverity.Warning, $"Trigger '{trigger.name}' 没有配置动作。", trigger));
                    continue;
                }

                for (var i = 0; i < trigger.Actions.Count; i++)
                {
                    var action = trigger.Actions[i];
                    if (action == null) continue;
                    if (!ActionHasTarget(action))
                        issues.Add(new LevelValidationIssue(LevelValidationSeverity.Error, $"Trigger '{trigger.name}' Action[{i}] 未绑定目标。", trigger));
                    else if (action.type == LevelActionType.StartWave &&
                             (action.waveIndex < 0 || action.waveIndex >= action.encounter.WaveCount))
                        issues.Add(new LevelValidationIssue(LevelValidationSeverity.Error, $"Trigger '{trigger.name}' Action[{i}] references an invalid wave index.", trigger));
                }
            }
        }

        static bool ActionHasTarget(LevelAction action)
        {
            switch (action.type)
            {
                case LevelActionType.StartEncounter: return action.encounter;
                case LevelActionType.StartWave: return action.encounter;
                case LevelActionType.LockGate:
                case LevelActionType.OpenGate: return action.gate;
                case LevelActionType.SmashMirrorGate: return action.mirrorGate;
                case LevelActionType.TeleportPlayer: return action.teleportTarget;
                case LevelActionType.UnlockAbility: return action.ability != MirrorRewardAbility.None;
                case LevelActionType.EnableSegment: return action.segment;
                case LevelActionType.PlayFeedback: return true;
                default: return false;
            }
        }

        static void ValidateGates(LevelManager manager, List<LevelValidationIssue> issues)
        {
            foreach (var gate in manager.Gates)
            {
                if (!gate) continue;
                var gateType = gate.GetType();
                var colField = gateType.GetField("gateCollider", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var col = colField?.GetValue(gate) as Collider2D;
                if (!col && !gate.GetComponent<Collider2D>())
                    issues.Add(new LevelValidationIssue(LevelValidationSeverity.Error, $"Gate '{gate.name}' 未绑定 Collider。", gate));
            }
        }

        static void ValidateMirrorGates(LevelManager manager, List<LevelValidationIssue> issues)
        {
            foreach (var mirror in manager.MirrorGates)
            {
                if (!mirror) continue;
                if (string.IsNullOrWhiteSpace(mirror.MirrorSceneName))
                    issues.Add(new LevelValidationIssue(LevelValidationSeverity.Error, $"MirrorGate '{mirror.name}' 未配置镜中场景名(MirrorSceneName)。", mirror));
                if (mirror.HitPoints <= 0)
                    issues.Add(new LevelValidationIssue(LevelValidationSeverity.Warning, $"MirrorGate '{mirror.name}' 血量 <= 0，将被一击碎裂。", mirror));
            }
        }

        static void ValidateSpawnPoints(LevelManager manager, List<LevelValidationIssue> issues)
        {
            foreach (var point in manager.SpawnPoints)
            {
                if (!point) continue;
                if (!point.DefaultEnemyPrefab)
                    issues.Add(new LevelValidationIssue(LevelValidationSeverity.Warning, $"SpawnPoint '{point.name}' 没有默认敌人（若被 Wave 覆盖可忽略）。", point));
            }
        }
    }
}
