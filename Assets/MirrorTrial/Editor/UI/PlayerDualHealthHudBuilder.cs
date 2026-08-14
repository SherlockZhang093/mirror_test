using MirrorTrial.Level;
using MirrorTrial.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MirrorTrial.Editor.UI
{
    public static class PlayerDualHealthHudBuilder
    {
        const string FallbackHudPrefabPath = "Assets/MirrorTrial/Resources/UI/PlayerHealthBarUI.prefab";
        const string Reality01HudPrefabPath = "Assets/MirrorTrial/Resources/UI/PlayerHealthBarUI_Reality01.prefab";
        const string Reality02HudPrefabPath = "Assets/MirrorTrial/Resources/UI/PlayerHealthBarUI_Reality02.prefab";
        const string Reality01SixSlotHudPrefabPath = "Assets/MirrorTrial/Resources/UI/PlayerHealthBarUI_Reality01_6Slot.prefab";
        const string Reality02SixSlotHudPrefabPath = "Assets/MirrorTrial/Resources/UI/PlayerHealthBarUI_Reality02_6Slot.prefab";
        const string Reality01SevenSlotHudPrefabPath = "Assets/MirrorTrial/Resources/UI/PlayerHealthBarUI_Reality01_7Slot.prefab";
        const string Reality02SevenSlotHudPrefabPath = "Assets/MirrorTrial/Resources/UI/PlayerHealthBarUI_Reality02_7Slot.prefab";
        const string Reality01ConfigPath = "Assets/MirrorTrial/LevelConfigs/Level_Reality_01.asset";
        const string Reality02ConfigPath = "Assets/MirrorTrial/LevelConfigs/Level_Reality_02.asset";
        const string Reality01ScenePath = "Assets/MirrorTrial/Scenes/Level_Reality_01.unity";
        const string Reality02ScenePath = "Assets/MirrorTrial/Scenes/Level_Reality_02.unity";
        const string PlayerPrefabPath = "Assets/MirrorTrial/Prefabs/Characters/Player_MirrorTrial.prefab";
        const string HealthOuterPath = "Assets/Art/xuetiao_ui/Sprites/HB_OuterFrame_5Slot.png";
        const string HealthEmptyPath = "Assets/Art/xuetiao_ui/Sprites/HB_Block_Empty.png";
        const string HealthFramePath = "Assets/Art/xuetiao_ui/Sprites/HB_Block_Frame.png";
        const string HitFlashPath = "Assets/Art/xuetiao_ui/Sprites/HB_Block_Fill_Hit.png";
        const string OrnamentPath = "Assets/Art/xuetiao_ui/Sprites/HB_Center_Ornament.png";
        const string EnergyEmptyPath = "Assets/MirrorTrial/Art/MinimalMirrorBossBar_AssetPack/Sprites/BossBar_Fill_Empty.png";
        const string EnergyFillPath = "Assets/MirrorTrial/Art/MinimalMirrorBossBar_AssetPack/Sprites/BossBar_Fill_Normal.png";
        const string EnergyFramePath = "Assets/MirrorTrial/Art/MinimalMirrorBossBar_AssetPack/Sprites/BossBar_OuterFrame.png";

        static readonly HudPalette Reality01Palette = new HudPalette(
            frame: new Color(0.34f, 0.38f, 0.42f, 1f),
            empty: new Color(0.025f, 0.055f, 0.075f, 0.98f),
            health: new Color(0.32f, 0.40f, 0.43f, 1f),
            reserve: new Color(0.055f, 0.29f, 0.36f, 1f),
            casting: new Color(0.24f, 0.51f, 0.55f, 1f),
            hitFlash: new Color(0.54f, 0.64f, 0.66f, 0.82f));

        static readonly HudPalette Reality02Palette = new HudPalette(
            frame: new Color(0.69f, 0.64f, 0.38f, 1f),
            empty: new Color(0.06f, 0.15f, 0.14f, 0.96f),
            health: new Color(0.10f, 0.94f, 0.48f, 1f),
            reserve: new Color(0.12f, 0.94f, 0.88f, 1f),
            casting: new Color(0.72f, 1f, 0.82f, 1f),
            hitFlash: new Color(0.68f, 1f, 0.74f, 0.95f));

        [MenuItem("Tools/Mirror Trial/UI/重建双关卡生命条")]
        public static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[PlayerDualHealthHudBuilder] 当前处于运行模式，已延后双关卡生命 UI 构建。");
                return;
            }

            EnsureHudPrefabExists(Reality01HudPrefabPath);
            EnsureHudPrefabExists(Reality02HudPrefabPath);

            BuildHudPrefab(Reality01HudPrefabPath, "UnifiedHealthHud_Reality01", Reality01Palette, 5);
            BuildHudPrefab(Reality02HudPrefabPath, "UnifiedHealthHud_Reality02", Reality02Palette, 5);
            BuildHudPrefab(FallbackHudPrefabPath, "UnifiedHealthHud_Reality02", Reality02Palette, 5);
            BuildUpgradeSlotPrefabs();

            AssignHudToConfigAndScene(Reality01ConfigPath, Reality01ScenePath, Reality01HudPrefabPath);
            AssignHudToConfigAndScene(Reality02ConfigPath, Reality02ScenePath, Reality02HudPrefabPath);
            RemoveTemporaryHud();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PlayerDualHealthHudBuilder] 已生成 Reality_01/Reality_02 独立生命 UI，并写入对应关卡配置。");
        }

        public static void RebuildFromBatchMode()
        {
            Rebuild();
        }

        [MenuItem("Tools/Mirror Trial/UI/生成 Reality02 六格生命条 Prefab")]
        public static void BuildReality02SixSlotPrefab()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[PlayerDualHealthHudBuilder] 请退出运行模式后再生成六格生命条 Prefab。");
                return;
            }

            BuildSlotPrefab(Reality02HudPrefabPath, Reality02SixSlotHudPrefabPath,
                "UnifiedHealthHud_Reality02_6Slot", Reality02Palette, 6);
        }

        [MenuItem("Tools/Mirror Trial/UI/生成 Reality01 六格生命条 Prefab")]
        public static void BuildReality01SixSlotPrefab()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[PlayerDualHealthHudBuilder] 请退出运行模式后再生成六格生命条 Prefab。");
                return;
            }

            BuildSlotPrefab(Reality01HudPrefabPath, Reality01SixSlotHudPrefabPath,
                "UnifiedHealthHud_Reality01_6Slot", Reality01Palette, 6);
        }

        [MenuItem("Tools/Mirror Trial/UI/生成六格和七格生命条 Prefab")]
        public static void BuildUpgradeSlotPrefabs()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[PlayerDualHealthHudBuilder] 请退出运行模式后再生成生命条 Prefab。");
                return;
            }

            BuildSlotPrefab(Reality01HudPrefabPath, Reality01SixSlotHudPrefabPath,
                "UnifiedHealthHud_Reality01_6Slot", Reality01Palette, 6);
            BuildSlotPrefab(Reality02HudPrefabPath, Reality02SixSlotHudPrefabPath,
                "UnifiedHealthHud_Reality02_6Slot", Reality02Palette, 6);
            BuildSlotPrefab(Reality01HudPrefabPath, Reality01SevenSlotHudPrefabPath,
                "UnifiedHealthHud_Reality01_7Slot", Reality01Palette, 7);
            BuildSlotPrefab(Reality02HudPrefabPath, Reality02SevenSlotHudPrefabPath,
                "UnifiedHealthHud_Reality02_7Slot", Reality02Palette, 7);
        }

        static void BuildSlotPrefab(string sourcePath, string targetPath, string hudName, HudPalette palette,
            int slotCount)
        {
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(targetPath))
                AssetDatabase.CopyAsset(sourcePath, targetPath);

            BuildHudPrefab(targetPath, hudName, palette, slotCount);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[PlayerDualHealthHudBuilder] 已生成独立 {slotCount} 格生命条：{targetPath}");
        }

        static void EnsureHudPrefabExists(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path))
                return;

            if (!AssetDatabase.CopyAsset(FallbackHudPrefabPath, path))
                Debug.LogError($"[PlayerDualHealthHudBuilder] 无法创建 UI Prefab：{path}");
        }

        static void BuildHudPrefab(string path, string hudName, HudPalette palette, int lifeBlockCount)
        {
            lifeBlockCount = Mathf.Max(1, lifeBlockCount);
            var healthBarWidth = 350f + (lifeBlockCount - 5) * 63f;

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                for (var i = root.transform.childCount - 1; i >= 0; i--)
                    Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

                root.layer = 5;
                var rootRect = GetOrAdd<RectTransform>(root);
                rootRect.localScale = Vector3.one;
                rootRect.anchorMin = Vector2.zero;
                rootRect.anchorMax = Vector2.zero;
                rootRect.pivot = Vector2.zero;
                rootRect.anchoredPosition = Vector2.zero;
                rootRect.sizeDelta = Vector2.zero;

                var canvas = GetOrAdd<Canvas>(root);
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                var scaler = GetOrAdd<CanvasScaler>(root);
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0f;
                GetOrAdd<GraphicRaycaster>(root);
                var view = GetOrAdd<PlayerHealthBarView>(root);

                var hud = CreateRect(hudName, root.transform,
                    new Vector2(18f, -18f), new Vector2(healthBarWidth, 67f), new Vector2(0f, 1f));

                var outer = CreateImage("SharedOuterFrame", hud, LoadSprite(HealthOuterPath), palette.frame);
                SetRect(outer.rectTransform, Vector2.zero, new Vector2(healthBarWidth, 52f), new Vector2(0f, 1f));
                outer.preserveAspect = false;

                var lives = CreateRect("HealthSegments", hud,
                    Vector2.zero, new Vector2(healthBarWidth, 52f), new Vector2(0f, 1f));
                var fills = new Image[lifeBlockCount];
                var flashes = new Image[lifeBlockCount];
                for (var i = 0; i < lifeBlockCount; i++)
                    CreateHealthBlock(lives, i, palette, out fills[i], out flashes[i]);

                var reserveRoot = CreateRect("HealthReserve", hud,
                    new Vector2(28f, -47f), new Vector2(healthBarWidth - 56f, 17f), new Vector2(0f, 1f));
                var reserveGroup = reserveRoot.gameObject.AddComponent<CanvasGroup>();

                var reserveEmpty = CreateImage("Empty", reserveRoot, LoadSprite(EnergyEmptyPath), palette.empty);
                Stretch(reserveEmpty.rectTransform, 1f, 2f, 1f, 2f);

                var reserveFill = CreateImage("Fill", reserveRoot, LoadSprite(EnergyFillPath), palette.reserve);
                Stretch(reserveFill.rectTransform, 4f, 4f, 3f, 3f);
                reserveFill.type = Image.Type.Filled;
                reserveFill.fillMethod = Image.FillMethod.Horizontal;
                reserveFill.fillOrigin = (int)Image.OriginHorizontal.Left;
                reserveFill.fillAmount = 0f;

                var reserveFrame = CreateImage("Frame", reserveRoot, LoadSprite(EnergyFramePath), palette.frame);
                Stretch(reserveFrame.rectTransform, 0f, 0f, 0f, 0f);

                var badge = CreateImage("RecoverKeyBadge", hud, LoadSprite(OrnamentPath), palette.frame);
                SetRect(badge.rectTransform, new Vector2(healthBarWidth - 26f, -45f), new Vector2(27f, 27f), new Vector2(0f, 1f));
                var keyText = CreateText("Key", badge.transform, "G", 12, FontStyle.Bold, palette.reserve);
                Stretch(keyText.rectTransform, 0f, 0f, 1f, 1f);

                var serialized = new SerializedObject(view);
                AssignArray(serialized.FindProperty("fills"), fills);
                AssignArray(serialized.FindProperty("hitFlashes"), flashes);
                serialized.FindProperty("normalFill").objectReferenceValue = LoadSprite(EnergyFillPath);
                serialized.FindProperty("lowFill").objectReferenceValue = LoadSprite(EnergyFillPath);
                serialized.FindProperty("reserveFill").objectReferenceValue = reserveFill;
                serialized.FindProperty("reserveGroup").objectReferenceValue = reserveGroup;
                serialized.FindProperty("recoverKeyText").objectReferenceValue = keyText;
                serialized.FindProperty("reserveReadyColor").colorValue = palette.reserve;
                serialized.FindProperty("reserveEmptyColor").colorValue =
                    new Color(palette.reserve.r, palette.reserve.g, palette.reserve.b, 0.25f);
                serialized.FindProperty("reserveCastingColor").colorValue = palette.casting;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void CreateHealthBlock(Transform parent, int index, HudPalette palette,
            out Image fill, out Image flash)
        {
            var block = CreateRect($"Life Block {index + 1}", parent,
                new Vector2(43f + index * 63f, -25f), new Vector2(58f, 32f), new Vector2(0.5f, 0.5f));

            var empty = CreateImage("Empty", block, LoadSprite(HealthEmptyPath), palette.empty);
            Stretch(empty.rectTransform, 5f, 5f, 6f, 6f);

            fill = CreateImage("Fill", block, LoadSprite(EnergyFillPath), palette.health);
            Stretch(fill.rectTransform, 5f, 5f, 6f, 6f);

            var frame = CreateImage("Frame", block, LoadSprite(HealthFramePath), palette.frame);
            Stretch(frame.rectTransform, 0f, 0f, 0f, 0f);

            flash = CreateImage("HitFlash", block, LoadSprite(HitFlashPath), palette.hitFlash);
            Stretch(flash.rectTransform, 5f, 5f, 6f, 6f);
            flash.gameObject.SetActive(false);
        }

        static void AssignHudToConfigAndScene(string configPath, string scenePath, string hudPath)
        {
            var config = AssetDatabase.LoadAssetAtPath<LevelConfig>(configPath);
            var hudObject = AssetDatabase.LoadAssetAtPath<GameObject>(hudPath);
            var hud = hudObject ? hudObject.GetComponent<PlayerHealthBarView>() : null;
            if (!config || !hud)
            {
                Debug.LogError($"[PlayerDualHealthHudBuilder] 关卡 UI 绑定失败：{configPath} -> {hudPath}");
                return;
            }

            config.playerHealthHudPrefab = hud;
            EditorUtility.SetDirty(config);
            BindConfigToScene(scenePath, config);
        }

        static void BindConfigToScene(string scenePath, LevelConfig config)
        {
            var activeScene = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(scenePath);
            var openedForBinding = !scene.IsValid() || !scene.isLoaded;
            if (openedForBinding)
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            LevelManager manager = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                manager = root.GetComponentInChildren<LevelManager>(true);
                if (manager) break;
            }

            if (manager)
            {
                var serialized = new SerializedObject(manager);
                serialized.FindProperty("levelConfig").objectReferenceValue = config;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            else
            {
                Debug.LogError($"[PlayerDualHealthHudBuilder] 场景中找不到 LevelManager：{scenePath}");
            }

            if (openedForBinding)
                EditorSceneManager.CloseScene(scene, true);
            if (activeScene.IsValid() && activeScene.isLoaded)
                SceneManager.SetActiveScene(activeScene);
        }

        static void RemoveTemporaryHud()
        {
            var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                var temporary = root.GetComponent<MirrorTrial.UI.HealthReserveUI>();
                if (temporary) Object.DestroyImmediate(temporary);
                if (!root.GetComponent<PlayerHealthReserve>()) root.AddComponent<PlayerHealthReserve>();
                if (!root.GetComponent<PlayerRecoveryAbility>()) root.AddComponent<PlayerRecoveryAbility>();
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static RectTransform CreateRect(string name, Transform parent, Vector2 position, Vector2 size, Vector2 pivot)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            SetRect(rect, position, size, pivot);
            return rect;
        }

        static Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var rect = CreateRect(name, parent, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static Text CreateText(string name, Transform parent, string value, int fontSize, FontStyle style, Color color)
        {
            var rect = CreateRect(name, parent, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f));
            var text = rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            return text;
        }

        static void SetRect(RectTransform rect, Vector2 position, Vector2 size, Vector2 pivot)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        static void Stretch(RectTransform rect, float left, float right, float top, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            rect.localScale = Vector3.one;
        }

        static Sprite LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (!sprite) Debug.LogError($"[PlayerDualHealthHudBuilder] 找不到 UI Sprite：{path}");
            return sprite;
        }

        static T GetOrAdd<T>(GameObject target) where T : Component
        {
            var component = target.GetComponent<T>();
            return component ? component : target.AddComponent<T>();
        }

        static void AssignArray(SerializedProperty property, Image[] values)
        {
            property.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        readonly struct HudPalette
        {
            public readonly Color frame;
            public readonly Color empty;
            public readonly Color health;
            public readonly Color reserve;
            public readonly Color casting;
            public readonly Color hitFlash;

            public HudPalette(Color frame, Color empty, Color health, Color reserve, Color casting, Color hitFlash)
            {
                this.frame = frame;
                this.empty = empty;
                this.health = health;
                this.reserve = reserve;
                this.casting = casting;
                this.hitFlash = hitFlash;
            }
        }
    }
}
