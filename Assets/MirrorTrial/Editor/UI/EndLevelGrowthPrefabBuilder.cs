#if UNITY_EDITOR
using System.IO;
using MirrorTrial.Growth;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace MirrorTrial.Editor.UI
{
    public static class EndLevelGrowthPrefabBuilder
    {
        const string Folder = "Assets/MirrorTrial/Resources/UI/Growth";
        const string DefinitionFolder = "Assets/MirrorTrial/Resources/Growth/Cards";
        const string PanelPath = Folder + "/EndLevelGrowthPanel.prefab";
        const string FontPath = "Assets/MirrorTrial/Resources/Fonts/ZCOOLKuaiLe-Regular.ttf";

        static readonly Color Backdrop = new Color(0.008f, 0.014f, 0.030f, 0.94f);
        static readonly Color Panel = new Color(0.018f, 0.035f, 0.065f, 0.98f);
        static readonly Color Card = new Color(0.025f, 0.090f, 0.140f, 0.98f);
        static readonly Color CardHover = new Color(0.050f, 0.190f, 0.240f, 1f);
        static readonly Color Cyan = new Color(0.25f, 0.92f, 0.94f, 1f);
        static readonly Color White = new Color(0.90f, 0.96f, 0.98f, 1f);
        static readonly Color Muted = new Color(0.50f, 0.68f, 0.72f, 1f);

        [InitializeOnLoadMethod]
        static void EnsurePrefabsExist()
        {
            EditorApplication.delayCall += () =>
            {
                var definitionsReady = EnsureDefaultDefinitions();
                if (!AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath) || !definitionsReady || CardsNeedMigration())
                    BuildAll();
            };
        }

        [MenuItem("Mirror Trial/成长/创建或修复关末卡牌 UI")]
        public static void BuildAll()
        {
            if (!Directory.Exists(Folder))
                Directory.CreateDirectory(Folder);
            if (!Directory.Exists(DefinitionFolder))
                Directory.CreateDirectory(DefinitionFolder);

            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (!font)
            {
                Debug.LogError("[GrowthUI] 中文字体不存在：" + FontPath);
                return;
            }

            var healthDefinition = GetOrCreateDefinition("MaxHealth", GrowthEffectType.MaxHealth, 5, 1f);
            var attackDefinition = GetOrCreateDefinition("Attack", GrowthEffectType.AttackDamage, 6, 2f);
            var speedDefinition = GetOrCreateDefinition("MoveSpeed", GrowthEffectType.MoveSpeed, 6, 0.5f);

            var healthCard = BuildCard(
                "GrowthCard_MaxHealth", healthDefinition, font,
                Folder + "/GrowthCard_MaxHealth.prefab");
            var attackCard = BuildCard(
                "GrowthCard_Attack", attackDefinition, font,
                Folder + "/GrowthCard_Attack.prefab");
            var speedCard = BuildCard(
                "GrowthCard_MoveSpeed", speedDefinition, font,
                Folder + "/GrowthCard_MoveSpeed.prefab");

            BuildPanel(font, new[] { healthCard, attackCard, speedCard });
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);
            Debug.Log("[GrowthUI] 关末成长面板和 3 张卡牌 Prefab 已生成。\n" + PanelPath);
        }

        static GameObject BuildCard(string objectName, GrowthCardDefinition definition, Font font, string path)
        {
            var effect = definition.EffectType;
            var cost = definition.EssenceCost;
            var amount = definition.IncreaseAmount;
            var root = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(Button), typeof(CanvasGroup), typeof(LayoutElement), typeof(GrowthCardView));
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(360f, 500f);

            var image = root.GetComponent<Image>();
            image.color = Card;
            var button = root.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.35f, 1.35f, 1.35f, 1f);
            colors.pressedColor = new Color(0.65f, 0.90f, 0.92f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.52f, 0.58f, 0.62f, 1f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.10f;
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            var layout = root.GetComponent<LayoutElement>();
            layout.preferredWidth = 360f;
            layout.preferredHeight = 500f;
            layout.minWidth = 320f;
            layout.minHeight = 450f;

            var topLine = CreateImage("TopAccent", root.transform, Cyan);
            SetRect(topLine.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(20f, -8f), new Vector2(-20f, -2f));

            var corner = CreateText("Corner", root.transform, "成长", font, 24, Muted, TextAnchor.MiddleLeft);
            SetRect(corner.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(34f, -74f), new Vector2(-34f, -24f));

            var effectText = CreateText("Effect", root.transform,
                PlayerGrowthEffectApplier.FormatEffect(effect, amount), font, 48, White, TextAnchor.MiddleCenter);
            effectText.fontStyle = FontStyle.Bold;
            effectText.resizeTextForBestFit = true;
            effectText.resizeTextMinSize = 30;
            effectText.resizeTextMaxSize = 48;
            SetRect(effectText.rectTransform, new Vector2(0f, 0.50f), new Vector2(1f, 0.82f),
                new Vector2(32f, 0f), new Vector2(-32f, 0f));

            var divider = CreateImage("Divider", root.transform, new Color(Cyan.r, Cyan.g, Cyan.b, 0.35f));
            SetRect(divider.rectTransform, new Vector2(0.18f, 0.44f), new Vector2(0.82f, 0.44f),
                new Vector2(0f, -1f), new Vector2(0f, 1f));

            var costLabel = CreateText("CostLabel", root.transform, "消耗", font, 23, Muted, TextAnchor.MiddleCenter);
            SetRect(costLabel.rectTransform, new Vector2(0f, 0.27f), new Vector2(1f, 0.39f), Vector2.zero, Vector2.zero);

            var costText = CreateText("Cost", root.transform, cost + " 生命精华", font, 36, Cyan, TextAnchor.MiddleCenter);
            costText.fontStyle = FontStyle.Bold;
            SetRect(costText.rectTransform, new Vector2(0f, 0.15f), new Vector2(1f, 0.29f), Vector2.zero, Vector2.zero);

            var stateText = CreateText("State", root.transform, "点击选择", font, 22, Muted, TextAnchor.MiddleCenter);
            SetRect(stateText.rectTransform, new Vector2(0f, 0.02f), new Vector2(1f, 0.13f), Vector2.zero, Vector2.zero);

            var serialized = new SerializedObject(root.GetComponent<GrowthCardView>());
            serialized.FindProperty("definition").objectReferenceValue = definition;
            serialized.FindProperty("effectText").objectReferenceValue = effectText;
            serialized.FindProperty("costText").objectReferenceValue = costText;
            serialized.FindProperty("stateText").objectReferenceValue = stateText;
            serialized.FindProperty("selectButton").objectReferenceValue = button;
            serialized.FindProperty("canvasGroup").objectReferenceValue = root.GetComponent<CanvasGroup>();
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static bool EnsureDefaultDefinitions()
        {
            if (!Directory.Exists(DefinitionFolder))
                Directory.CreateDirectory(DefinitionFolder);
            GetOrCreateDefinition("MaxHealth", GrowthEffectType.MaxHealth, 5, 1f);
            GetOrCreateDefinition("Attack", GrowthEffectType.AttackDamage, 6, 2f);
            GetOrCreateDefinition("MoveSpeed", GrowthEffectType.MoveSpeed, 6, 0.5f);
            AssetDatabase.SaveAssets();
            return true;
        }

        static GrowthCardDefinition GetOrCreateDefinition(string name, GrowthEffectType effect, int cost, float amount)
        {
            var path = $"{DefinitionFolder}/GrowthCard_{name}.asset";
            var definition = AssetDatabase.LoadAssetAtPath<GrowthCardDefinition>(path);
            if (!definition)
            {
                definition = ScriptableObject.CreateInstance<GrowthCardDefinition>();
                definition.Configure(cost, effect, amount);
                AssetDatabase.CreateAsset(definition, path);
            }
            return definition;
        }

        static bool CardsNeedMigration()
        {
            var paths = new[]
            {
                Folder + "/GrowthCard_MaxHealth.prefab",
                Folder + "/GrowthCard_Attack.prefab",
                Folder + "/GrowthCard_MoveSpeed.prefab"
            };
            for (var i = 0; i < paths.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
                var view = prefab ? prefab.GetComponent<GrowthCardView>() : null;
                if (!view)
                    return true;
                var serialized = new SerializedObject(view);
                if (!serialized.FindProperty("definition").objectReferenceValue)
                    return true;
            }
            return false;
        }

        static void BuildPanel(Font font, GameObject[] cardPrefabs)
        {
            var root = new GameObject("EndLevelGrowthPanel", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(EndLevelGrowthPanel));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 300;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var backdrop = CreateImage("Backdrop", root.transform, Backdrop);
            Stretch(backdrop.rectTransform);

            var frame = CreateImage("Frame", root.transform, Panel);
            SetRect(frame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-780f, -450f), new Vector2(780f, 450f));

            var topAccent = CreateImage("TopAccent", frame.transform, Cyan);
            SetRect(topAccent.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -6f), new Vector2(0f, 0f));

            var title = CreateText("Title", frame.transform, "选择一项成长", font, 58, White, TextAnchor.MiddleLeft);
            title.fontStyle = FontStyle.Bold;
            SetRect(title.rectTransform, new Vector2(0f, 1f), new Vector2(0.65f, 1f),
                new Vector2(74f, -126f), new Vector2(0f, -42f));

            var balanceText = CreateText("Balance", frame.transform, "生命精华  --", font, 36, Cyan, TextAnchor.MiddleRight);
            SetRect(balanceText.rectTransform, new Vector2(0.58f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -124f), new Vector2(-74f, -44f));

            var cardContainer = new GameObject("Cards", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            cardContainer.transform.SetParent(frame.transform, false);
            var cardsRect = cardContainer.GetComponent<RectTransform>();
            SetRect(cardsRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-600f, -250f), new Vector2(600f, 250f));
            var layout = cardContainer.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 40f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var statusText = CreateText("Status", frame.transform, "选择一项提升，或保留精华", font, 25, Muted, TextAnchor.MiddleLeft);
            SetRect(statusText.rectTransform, new Vector2(0f, 0f), new Vector2(0.72f, 0f),
                new Vector2(74f, 34f), new Vector2(0f, 100f));

            var skipButtonObject = new GameObject("SkipButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            skipButtonObject.transform.SetParent(frame.transform, false);
            var skipRect = skipButtonObject.GetComponent<RectTransform>();
            SetRect(skipRect, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-374f, 34f), new Vector2(-74f, 106f));
            var skipImage = skipButtonObject.GetComponent<Image>();
            skipImage.color = new Color(0.035f, 0.12f, 0.16f, 1f);
            var skipButton = skipButtonObject.GetComponent<Button>();
            skipButton.targetGraphic = skipImage;
            var skipColors = skipButton.colors;
            skipColors.highlightedColor = CardHover;
            skipColors.pressedColor = Cyan;
            skipButton.colors = skipColors;
            var skipText = CreateText("Text", skipButtonObject.transform, "保留精华并继续", font, 27, White, TextAnchor.MiddleCenter);
            Stretch(skipText.rectTransform);

            var serialized = new SerializedObject(root.GetComponent<EndLevelGrowthPanel>());
            serialized.FindProperty("cardContainer").objectReferenceValue = cardContainer.transform;
            var prefabs = serialized.FindProperty("cardPrefabs");
            prefabs.arraySize = cardPrefabs.Length;
            for (var i = 0; i < cardPrefabs.Length; i++)
                prefabs.GetArrayElementAtIndex(i).objectReferenceValue = cardPrefabs[i];
            serialized.FindProperty("balanceText").objectReferenceValue = balanceText;
            serialized.FindProperty("statusText").objectReferenceValue = statusText;
            serialized.FindProperty("skipButton").objectReferenceValue = skipButton;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
            Object.DestroyImmediate(root);
        }

        static Image CreateImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        static Text CreateText(string name, Transform parent, string value, Font font, int size, Color color, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.text = value;
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
#endif
