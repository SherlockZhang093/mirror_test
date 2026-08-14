using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MirrorTrial.HealthResources;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MirrorTrial.Editor.HealthResources
{
    public static class LifeEssenceNodeIdValidator
    {
        const string ScenesFolder = "Assets/MirrorTrial/Scenes";
        const string RequestRelativePath = "Temp/LifeEssenceNodeIdAutofill.request";

        static bool handlingRequestedAutofill;

        [InitializeOnLoadMethod]
        static void InstallRequestWatcher()
        {
            EditorApplication.update -= HandleRequestedAutofillOnUpdate;
            EditorApplication.update += HandleRequestedAutofillOnUpdate;
        }

        static void HandleRequestedAutofillOnUpdate()
        {
            if (handlingRequestedAutofill)
                return;

            var requestPath = GetRequestPath();
            if (!File.Exists(requestPath))
                return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            handlingRequestedAutofill = true;
            try
            {
                Debug.Log("[LifeEssenceNodeIdValidator] 检测到批量补齐请求，开始处理。");
                BatchAssignForProjectScenes();
                File.Delete(requestPath);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                handlingRequestedAutofill = false;
            }
        }

        static string GetRequestPath()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", RequestRelativePath));
        }

        [MenuItem("Tools/Mirror Trial/关卡/校验生命精华节点 ID")]
        public static void ValidateOpenScenes()
        {
            var nodes = ResourcesInLoadedScenes();
            ValidateNodes(nodes, "当前打开场景中的生命精华节点 ID");
        }

        [MenuItem("Tools/Mirror Trial/关卡/批量补齐生命精华节点 ID")]
        public static void BatchAssignForProjectScenes()
        {
            var openScenePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var activeScene = SceneManager.GetActiveScene();
            var activeScenePath = activeScene.path;
            for (var i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var loadedScene = EditorSceneManager.GetSceneAt(i);
                if (loadedScene.IsValid() && loadedScene.isLoaded && !string.IsNullOrEmpty(loadedScene.path))
                    openScenePaths.Add(loadedScene.path);
            }

            var scenePaths = AssetDatabase.FindAssets("t:Scene", new[] { ScenesFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var totalAssigned = 0;
            var touchedScenes = 0;

            foreach (var scenePath in scenePaths)
            {
                var wasLoaded = openScenePaths.Contains(scenePath);
                var scene = wasLoaded
                    ? SceneManager.GetSceneByPath(scenePath)
                    : EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

                try
                {
                    var assigned = AssignMissingIdsInScene(scene);
                    if (assigned <= 0)
                        continue;

                    totalAssigned += assigned;
                    touchedScenes++;
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log($"[LifeEssenceNodeIdValidator] {scene.name} 已补齐 {assigned} 个生命精华节点 ID。", FindAnyNodeInScene(scene));
                }
                finally
                {
                    if (!wasLoaded && scene.IsValid() && scene.isLoaded)
                        EditorSceneManager.CloseScene(scene, true);
                }
            }

            if (!string.IsNullOrEmpty(activeScenePath))
            {
                var restored = SceneManager.GetSceneByPath(activeScenePath);
                if (restored.IsValid() && restored.isLoaded)
                    SceneManager.SetActiveScene(restored);
            }

            Debug.Log($"[LifeEssenceNodeIdValidator] 批量补齐完成：修改场景 {touchedScenes} 个，新增节点 ID {totalAssigned} 个。");
            ValidateOpenScenes();
        }

        public static string AssignStableId(HealthResourceNode node)
        {
            if (!node || !node.gameObject.scene.IsValid())
                return string.Empty;
            if (!string.IsNullOrWhiteSpace(node.ResourceNodeId))
                return node.ResourceNodeId;

            var scene = node.gameObject.scene;
            var typeKey = DetectTypeKey(node);
            var used = new HashSet<string>(
                ResourcesInScene(scene)
                    .Select(candidate => candidate.ResourceNodeId)
                    .Where(id => !string.IsNullOrWhiteSpace(id)),
                StringComparer.Ordinal);

            var counter = 0;
            string generatedId;
            do
            {
                counter++;
                generatedId = $"{scene.name}.{typeKey}.{counter:000}";
            } while (used.Contains(generatedId));

            var data = new SerializedObject(node);
            data.FindProperty("resourceNodeId").stringValue = generatedId;
            data.ApplyModifiedProperties();
            EditorUtility.SetDirty(node);
            return generatedId;
        }

        public static string GetTypeKey(HealthResourceNode node)
        {
            return node ? DetectTypeKey(node) : "LifeNode";
        }

        static int AssignMissingIdsInScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return 0;

            var nodes = ResourcesInScene(scene)
                .OrderBy(node => BuildHierarchyPath(node.transform), StringComparer.Ordinal)
                .ToList();

            var used = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < nodes.Count; i++)
            {
                var existingId = nodes[i].ResourceNodeId;
                if (!string.IsNullOrWhiteSpace(existingId))
                    used.Add(existingId);
            }

            var typeCounters = new Dictionary<string, int>(StringComparer.Ordinal);
            var assigned = 0;
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (!string.IsNullOrWhiteSpace(node.ResourceNodeId))
                    continue;

                var typeKey = DetectTypeKey(node);
                if (!typeCounters.TryGetValue(typeKey, out var counter))
                    counter = 0;

                string generatedId;
                do
                {
                    counter++;
                    generatedId = $"{scene.name}.{typeKey}.{counter:000}";
                } while (used.Contains(generatedId));

                typeCounters[typeKey] = counter;
                used.Add(generatedId);

                var data = new SerializedObject(node);
                data.FindProperty("resourceNodeId").stringValue = generatedId;
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(node);
                assigned++;
            }

            return assigned;
        }

        static void ValidateNodes(IEnumerable<HealthResourceNode> nodes, string scopeLabel)
        {
            var duplicateMap = new Dictionary<string, List<HealthResourceNode>>(StringComparer.Ordinal);
            var missingCount = 0;

            foreach (var node in nodes)
            {
                if (!node || !node.gameObject.scene.IsValid())
                    continue;

                var nodeId = node.ResourceNodeId;
                if (string.IsNullOrWhiteSpace(nodeId))
                {
                    missingCount++;
                    Debug.LogError("[LifeEssenceNodeIdValidator] 节点缺少 resourceNodeId。", node);
                    continue;
                }

                if (!duplicateMap.TryGetValue(nodeId, out var list))
                {
                    list = new List<HealthResourceNode>();
                    duplicateMap.Add(nodeId, list);
                }
                list.Add(node);
            }

            var duplicateCount = 0;
            foreach (var pair in duplicateMap)
            {
                if (pair.Value.Count <= 1)
                    continue;

                duplicateCount++;
                for (var i = 0; i < pair.Value.Count; i++)
                    Debug.LogError($"[LifeEssenceNodeIdValidator] 重复的 resourceNodeId: {pair.Key}", pair.Value[i]);
            }

            if (missingCount == 0 && duplicateCount == 0)
                Debug.Log($"[LifeEssenceNodeIdValidator] {scopeLabel}校验通过。");
            else
                Debug.LogWarning($"[LifeEssenceNodeIdValidator] {scopeLabel}校验完成：缺失 {missingCount} 个，重复 {duplicateCount} 组。");
        }

        static IEnumerable<HealthResourceNode> ResourcesInLoadedScenes()
        {
            for (var i = 0; i < EditorSceneManager.sceneCount; i++)
            {
                var scene = EditorSceneManager.GetSceneAt(i);
                foreach (var node in ResourcesInScene(scene))
                    yield return node;
            }
        }

        static IEnumerable<HealthResourceNode> ResourcesInScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                yield break;

            var roots = scene.GetRootGameObjects();
            for (var i = 0; i < roots.Length; i++)
            {
                var nodes = roots[i].GetComponentsInChildren<HealthResourceNode>(true);
                for (var j = 0; j < nodes.Length; j++)
                    yield return nodes[j];
            }
        }

        static HealthResourceNode FindAnyNodeInScene(Scene scene)
        {
            foreach (var node in ResourcesInScene(scene))
                return node;
            return null;
        }

        static string DetectTypeKey(HealthResourceNode node)
        {
            var name = node.name;
            if (name.IndexOf("Reliquary", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Shrine", StringComparison.OrdinalIgnoreCase) >= 0)
                return "LifeShrine";
            if (name.IndexOf("Crystal", StringComparison.OrdinalIgnoreCase) >= 0)
                return "LifeCrystal";
            if (name.IndexOf("Fern", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Orchid", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Plant", StringComparison.OrdinalIgnoreCase) >= 0)
                return "LifePlant";
            return "LifeNode";
        }

        static string BuildHierarchyPath(Transform target)
        {
            var current = target;
            var path = current.name + "[" + current.GetSiblingIndex() + "]";
            while (current.parent != null)
            {
                current = current.parent;
                path = current.name + "[" + current.GetSiblingIndex() + "]" + "/" + path;
            }
            return path;
        }
    }
}
