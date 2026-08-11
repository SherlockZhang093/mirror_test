using System.Collections;
using MirrorTrial.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MirrorTrial.Level
{
    /// <summary>
    /// Plays the short weapon-acquisition beat after a mirror level and leaves a
    /// non-blocking slot guide on screen long enough for the player to try it.
    /// </summary>
    public sealed class PlayerWeaponUnlockSequence : MonoBehaviour
    {
        const float AnimationDuration = 0.9f;
        const float GuideDuration = 5f;

        GameObject player;
        PlayerWeaponType weapon;
        CanvasGroup group;
        TMP_Text title;
        TMP_Text hint;
        static TMP_FontAsset runtimeChineseFont;

        public static void Play(GameObject targetPlayer, PlayerWeaponType unlockedWeapon)
        {
            if (!targetPlayer || unlockedWeapon == PlayerWeaponType.Unarmed) return;
            var go = new GameObject("PlayerWeaponUnlockSequence");
            DontDestroyOnLoad(go);
            var sequence = go.AddComponent<PlayerWeaponUnlockSequence>();
            sequence.player = targetPlayer;
            sequence.weapon = unlockedWeapon;
            sequence.BuildUi();
            sequence.StartCoroutine(sequence.Run());
        }

        IEnumerator Run()
        {
            yield return null;
            if (!player)
            {
                Destroy(gameObject);
                yield break;
            }

            var input = player.GetComponent<PlayerInputReader>();
            var motor = player.GetComponent<PlayerMotor>();
            var weapons = player.GetComponent<PlayerWeaponController>();
            var animation = player.GetComponent<PlayerAnimationDriver>();
            var previousInputEnabled = !input || input.InputEnabled;
            var previousMovementLocked = motor && motor.MovementLocked;

            if (input) input.InputEnabled = false;
            if (motor) motor.MovementLocked = true;
            if (weapons) weapons.ForceEquipWeapon(weapon);

            var acquisitionState = weapon == PlayerWeaponType.Sword
                ? PlayerActionState.SwordStandingSlash
                : PlayerActionState.BowDraw;
            if (animation)
            {
                animation.ForceState(acquisitionState);
                animation.PlayStateImmediately(acquisitionState);
            }

            title.text = weapon == PlayerWeaponType.Sword ? "获得剑之能力" : "获得弓箭能力";
            hint.text = weapon == PlayerWeaponType.Sword
                ? "已自动装备剑  ·  [1] 拳头   [2] 剑"
                : "已自动装备弓箭  ·  [1] 拳头   [2] 剑   [3] 弓箭";

            yield return Fade(0f, 1f, 0.18f);
            yield return new WaitForSecondsRealtime(AnimationDuration);

            if (animation) animation.ClearForcedState(acquisitionState);
            if (motor) motor.MovementLocked = previousMovementLocked;
            if (input) input.InputEnabled = previousInputEnabled;

            var end = Time.unscaledTime + GuideDuration;
            while (Time.unscaledTime < end)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1) ||
                    Input.GetKeyDown(weapon == PlayerWeaponType.Sword ? KeyCode.Alpha2 : KeyCode.Alpha3))
                    break;
                yield return null;
            }

            yield return Fade(1f, 0f, 0.25f);
            Destroy(gameObject);
        }

        IEnumerator Fade(float from, float to, float duration)
        {
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            group.alpha = to;
        }

        void BuildUi()
        {
            var canvasObject = new GameObject("WeaponUnlockCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2100;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            group = canvasObject.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;

            var panel = new GameObject("WeaponUnlockPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panel.transform.SetParent(canvasObject.transform, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.27f, 0.73f);
            panelRect.anchorMax = new Vector2(0.73f, 0.9f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.015f, 0.025f, 0.055f, 0.94f);

            title = CreateText(panel.transform, "Title", 38f, FontStyles.Bold, new Vector2(0.06f, 0.46f), new Vector2(0.94f, 0.88f));
            title.alignment = TextAlignmentOptions.Center;
            title.color = new Color(0.45f, 0.94f, 1f, 1f);
            hint = CreateText(panel.transform, "Hint", 25f, FontStyles.Normal, new Vector2(0.06f, 0.1f), new Vector2(0.94f, 0.48f));
            hint.alignment = TextAlignmentOptions.Center;
            hint.color = new Color(0.9f, 0.95f, 1f, 1f);
        }

        static TMP_Text CreateText(Transform parent, string objectName, float size, FontStyles style,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = ResolveChineseFont();
            text.fontSize = size;
            text.fontStyle = style;
            text.raycastTarget = false;
            return text;
        }

        static TMP_FontAsset ResolveChineseFont()
        {
            if (runtimeChineseFont) return runtimeChineseFont;
            var sourceFont = Resources.Load<Font>("Fonts/ZCOOLKuaiLe-Regular");
            if (!sourceFont) return TMP_Settings.defaultFontAsset;
            runtimeChineseFont = TMP_FontAsset.CreateFontAsset(sourceFont);
            runtimeChineseFont.name = "MirrorTrial_WeaponUnlockChinese";
            runtimeChineseFont.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            runtimeChineseFont.isMultiAtlasTexturesEnabled = true;
            runtimeChineseFont.hideFlags = HideFlags.HideAndDontSave;
            return runtimeChineseFont;
        }
    }
}
