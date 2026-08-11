using System.Collections.Generic;
using MirrorTrial.Combat;
using MirrorTrial.Enemies;
using UnityEngine;

namespace MirrorTrial.Boss
{
    [DisallowMultipleComponent]
    public sealed class MirrorBossRuntimeColliderDebug : MonoBehaviour
    {
        [SerializeField] bool visible;
        [SerializeField] KeyCode shortcut = KeyCode.F8;
        [SerializeField] Vector2 buttonPosition = new Vector2(18f, 18f);

#if UNITY_EDITOR
        readonly List<Hitbox> hitboxes = new List<Hitbox>();
        readonly List<Hurtbox> hurtboxes = new List<Hurtbox>();
        readonly List<EnemyAI> enemies = new List<EnemyAI>();
        Camera worldCamera;
        GUIStyle labelStyle;
        float nextRefreshTime;

        static readonly Color ActiveAttack = new Color(1f, 0.12f, 0.08f, 0.28f);
        static readonly Color ActiveAttackBorder = new Color(1f, 0.2f, 0.12f, 1f);
        static readonly Color InactiveAttackBorder = new Color(0.55f, 0.12f, 0.1f, 0.8f);
        static readonly Color Hurt = new Color(0.05f, 0.9f, 1f, 0.18f);
        static readonly Color HurtBorder = new Color(0.08f, 0.9f, 1f, 1f);

        static MirrorBossRuntimeColliderDebug instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void EnsureGlobalDebugger()
        {
            if (FindObjectOfType<MirrorBossRuntimeColliderDebug>(true)) return;
            var host = new GameObject("全局战斗判定框调试器_F8");
            DontDestroyOnLoad(host);
            host.AddComponent<MirrorBossRuntimeColliderDebug>();
        }

        void Awake()
        {
            if (instance && instance != this)
            {
                Destroy(this);
                return;
            }
            instance = this;
            DontDestroyOnLoad(gameObject);
            RefreshTargets();
        }
        void Update()
        {
            if (Input.GetKeyDown(shortcut)) visible = !visible;
            if (Time.unscaledTime >= nextRefreshTime) RefreshTargets();
        }

        void RefreshTargets()
        {
            nextRefreshTime = Time.unscaledTime + 0.5f;
            worldCamera = Camera.main;
            hitboxes.Clear();
            hurtboxes.Clear();
            enemies.Clear();
            hitboxes.AddRange(FindObjectsOfType<Hitbox>(true));
            hurtboxes.AddRange(FindObjectsOfType<Hurtbox>(true));
            enemies.AddRange(FindObjectsOfType<EnemyAI>(true));
        }

        void OnGUI()
        {
            EnsureStyle();
            var buttonRect = new Rect(buttonPosition.x, buttonPosition.y, 230f, 36f);
            if (GUI.Button(buttonRect, visible ? "隐藏攻击框 / 受击框  [F8]" : "显示攻击框 / 受击框  [F8]"))
                visible = !visible;

            if (!visible) return;
            if (!worldCamera) worldCamera = Camera.main;
            if (!worldCamera) return;

            GUI.Label(new Rect(buttonRect.x, buttonRect.yMax + 5f, 420f, 24f),
                "红色：攻击框（亮红=开启，暗红=关闭）　青色：受击框", labelStyle);

            foreach (var hurtbox in hurtboxes)
            {
                if (!hurtbox || !hurtbox.gameObject.activeInHierarchy) continue;
                var collider = hurtbox.GetComponent<Collider2D>();
                if (!collider) continue;
                DrawCollider(collider, Hurt, HurtBorder, $"受击框  {hurtbox.transform.root.name}", true);
            }

            foreach (var hitbox in hitboxes)
            {
                if (!hitbox || !hitbox.gameObject.activeInHierarchy) continue;
                var collider = hitbox.GetComponent<Collider2D>();
                if (!collider) continue;
                var active = collider.enabled;
                DrawCollider(collider, active ? ActiveAttack : Color.clear,
                    active ? ActiveAttackBorder : InactiveAttackBorder,
                    $"攻击框 {(active ? "开启" : "关闭")}  {hitbox.transform.root.name}", active);
            }
            // 普通敌人的近战攻击使用瞬时 OverlapBox，没有实体 Hitbox 组件，需单独绘制。
            foreach (var enemy in enemies)
            {
                if (!enemy || !enemy.gameObject.activeInHierarchy) continue;
                if (enemy.Profile && enemy.Profile.IsRanged) continue;
                var size = enemy.AttackSize;
                var center = (Vector2)enemy.transform.position + enemy.FacingDirection * size.x * 0.5f;
                var active = enemy.CurrentState == EnemyAI.State.Attack;
                DrawWorldRect(center, size,
                    active ? ActiveAttack : Color.clear,
                    active ? ActiveAttackBorder : InactiveAttackBorder,
                    $"普通怪攻击框 {(active ? "攻击中" : "未攻击")}  {enemy.name}", active);
            }
        }

        void DrawWorldRect(Vector2 center, Vector2 size, Color fill, Color border, string text, bool strongLabel)
        {
            DrawBounds(new Bounds(center, size), fill, border, text, strongLabel);
        }
        void DrawCollider(Collider2D collider, Color fill, Color border, string text, bool strongLabel)
        {
            var bounds = GetVisualBounds(collider);
            DrawBounds(bounds, fill, border, text, strongLabel);
        }

        void DrawBounds(Bounds bounds, Color fill, Color border, string text, bool strongLabel)
        {
            var min = worldCamera.WorldToScreenPoint(bounds.min);
            var max = worldCamera.WorldToScreenPoint(bounds.max);
            if (min.z < 0f && max.z < 0f) return;

            var rect = Rect.MinMaxRect(
                Mathf.Min(min.x, max.x),
                Screen.height - Mathf.Max(min.y, max.y),
                Mathf.Max(min.x, max.x),
                Screen.height - Mathf.Min(min.y, max.y));
            if (rect.xMax < 0f || rect.xMin > Screen.width || rect.yMax < 0f || rect.yMin > Screen.height) return;

            if (fill.a > 0f) DrawRect(rect, fill);
            DrawOutline(rect, border, strongLabel ? 3f : 2f);
            var previous = labelStyle.normal.textColor;
            labelStyle.normal.textColor = border;
            GUI.Label(new Rect(rect.xMin, rect.yMin - 19f, Mathf.Max(260f, rect.width), 20f), text, labelStyle);
            labelStyle.normal.textColor = previous;
        }

        static Bounds GetVisualBounds(Collider2D collider)
        {
            if (collider.enabled) return collider.bounds;
            var box = collider as BoxCollider2D;
            if (!box) return collider.bounds;
            var scale = box.transform.lossyScale;
            var size = new Vector3(Mathf.Abs(box.size.x * scale.x), Mathf.Abs(box.size.y * scale.y), 0f);
            return new Bounds(box.transform.TransformPoint(box.offset), size);
        }
        static void DrawOutline(Rect rect, Color color, float thickness)
        {
            DrawRect(new Rect(rect.xMin, rect.yMin, rect.width, thickness), color);
            DrawRect(new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), color);
            DrawRect(new Rect(rect.xMin, rect.yMin, thickness, rect.height), color);
            DrawRect(new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height), color);
        }

        static void DrawRect(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        void EnsureStyle()
        {
            if (labelStyle != null) return;
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            labelStyle.normal.textColor = Color.white;
        }
#else
        // Keep the component type valid for prefabs, but strip all runtime debug
        // input and drawing from standalone builds.
        void Awake()
        {
            enabled = false;
        }
#endif
    }
}
