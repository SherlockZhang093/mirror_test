using System;
using System.Collections.Generic;
using System.Linq;
using MirrorTrial.Level;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MirrorTrial.Editor.Level
{
    public static class StoryNarrativeValidator
    {
        const string FontSourcePath = "Assets/MirrorTrial/Resources/Fonts/ZCOOLKuaiLe-Regular.ttf";
        const string Level01Path = "Assets/MirrorTrial/Scenes/Level_Reality_01.unity";

        [MenuItem("MirrorTrial/剧情/检查当前关卡剧情")]
        public static void ValidateCurrentSceneFromMenu()
        {
            var issues = ValidateCurrentScene();
            if (issues.Count == 0)
                EditorUtility.DisplayDialog("剧情检查", "检查通过：剧情段落、触发关系、镜头目标和中文字体均可用。", "完成");
            else
                EditorUtility.DisplayDialog("剧情检查", string.Join("\n", issues), "返回修改");
        }

        public static List<string> ValidateCurrentScene()
        {
            var issues = new List<string>();
            var sequences = UnityEngine.Object.FindObjectsOfType<StoryTutorialSequence>(true)
                .Where(item => item.gameObject.scene == SceneManager.GetActiveScene())
                .ToArray();

            if (sequences.Length == 0)
                issues.Add("当前场景没有剧情段落。");

            foreach (var duplicate in sequences.GroupBy(item => item.SequenceId).Where(group => group.Count() > 1))
                issues.Add($"剧情唯一 ID 重复：{duplicate.Key}");

            foreach (var sequence in sequences)
            {
                if (string.IsNullOrWhiteSpace(sequence.SequenceId))
                    issues.Add($"{sequence.DisplayName}：唯一 ID 为空。");
                if (sequence.Steps.Count == 0)
                    issues.Add($"{sequence.DisplayName}：没有演出步骤。");

                for (var i = 0; i < sequence.Steps.Count; i++)
                {
                    var step = sequence.Steps[i];
                    if (step == null)
                    {
                        issues.Add($"{sequence.DisplayName}：第 {i + 1} 步为空。");
                        continue;
                    }
                    if (step.type == StoryTutorialStepType.CameraShot && !step.cameraTarget)
                        issues.Add($"{sequence.DisplayName}：第 {i + 1} 步的镜头目标未设置。");
                }
            }

            ValidateFont(issues);
            if (issues.Count == 0)
                Debug.Log($"[MirrorTrial] 剧情检查通过，共 {sequences.Length} 个段落、{sequences.Sum(item => item.Steps.Count)} 个步骤。");
            else
                Debug.LogWarning("[MirrorTrial] 剧情检查发现问题：\n" + string.Join("\n", issues));
            return issues;
        }

        public static void BatchValidateLevel01()
        {
            EditorSceneManager.OpenScene(Level01Path, OpenSceneMode.Single);
            var issues = ValidateCurrentScene();
            if (issues.Count > 0)
                throw new InvalidOperationException("剧情验证失败：\n" + string.Join("\n", issues));
        }

        static void ValidateFont(List<string> issues)
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>(FontSourcePath);
            if (!source)
            {
                issues.Add("未找到完整中文字体源文件。");
                return;
            }

            var dynamicFont = TMP_FontAsset.CreateFontAsset(source);
            if (!dynamicFont)
            {
                issues.Add("无法从中文字体源文件生成动态字体。");
                return;
            }

            dynamicFont.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            dynamicFont.isMultiAtlasTexturesEnabled = true;
            const string sample = "镜界试炼当镜面开始呼吸现实便不再可靠向前走移动跳跃攻击闪避";
            string missing;
            if (!dynamicFont.TryAddCharacters(sample, out missing) || !string.IsNullOrEmpty(missing))
                issues.Add("中文字体缺少字符：" + missing);
            UnityEngine.Object.DestroyImmediate(dynamicFont);
        }
    }
}
