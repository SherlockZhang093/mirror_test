using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MirrorTrial.Editor.Level
{
    public class LevelEditorWindow : EditorWindow
    {
        static readonly string[] TabNames = { "关卡", "地形", "玩法", "敌人", "检查导出" };

        [SerializeField] MirrorTrial.Level.LevelManager manager;
        [SerializeField] int selectedTab;
        Vector2 scrollPosition;
        LevelManagerEditor managerEditor;

        [MenuItem("Tools/镜像试炼/关卡编辑器")]
        public static void OpenFromMenu()
        {
            Open(FindObjectOfType<MirrorTrial.Level.LevelManager>());
        }

        public static void Open(MirrorTrial.Level.LevelManager target)
        {
            var window = GetWindow<LevelEditorWindow>();
            window.titleContent = new GUIContent("关卡编辑器");
            window.minSize = new Vector2(380f, 420f);
            window.SetManager(target);
            window.Show();
        }

        void OnEnable()
        {
            titleContent = new GUIContent("关卡编辑器");
            minSize = new Vector2(380f, 420f);
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
            EnsureManager();
        }

        void OnDisable()
        {
            EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
            DestroyManagerEditor();
        }

        void OnSelectionChange()
        {
            Repaint();
        }

        void OnActiveSceneChanged(Scene oldScene, Scene newScene)
        {
            SetManager(FindObjectOfType<MirrorTrial.Level.LevelManager>());
            Repaint();
        }

        void OnGUI()
        {
            DrawHeader();

            if (!manager)
            {
                EditorGUILayout.HelpBox("当前场景中没有 LevelManager。新建或打开关卡后，此窗口会自动连接。", MessageType.Warning);
                if (GUILayout.Button("重新查找 LevelManager", GUILayout.Height(30)))
                    SetManager(FindObjectOfType<MirrorTrial.Level.LevelManager>());
                return;
            }

            var newTab = GUILayout.Toolbar(selectedTab, TabNames, GUILayout.Height(26));
            if (newTab != selectedTab)
            {
                selectedTab = newTab;
                SceneView.RepaintAll();
            }
            EditorGUILayout.Space(8);

            EnsureManagerEditor();
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            managerEditor.DrawTab((LevelManagerEditor.EditorTab)selectedTab);
            EditorGUILayout.EndScrollView();
        }

        void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField(manager ? manager.LevelDisplayName : "未连接关卡", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("定位", EditorStyles.toolbarButton, GUILayout.Width(44f)) && manager)
            {
                Selection.activeObject = manager;
                EditorGUIUtility.PingObject(manager);
            }
            EditorGUILayout.EndHorizontal();
        }

        void EnsureManager()
        {
            if (!manager || manager.gameObject.scene != SceneManager.GetActiveScene())
                SetManager(FindObjectOfType<MirrorTrial.Level.LevelManager>());
            else
                EnsureManagerEditor();
        }

        void SetManager(MirrorTrial.Level.LevelManager target)
        {
            if (manager == target && managerEditor) return;
            DestroyManagerEditor();
            manager = target;
            EnsureManagerEditor();
        }

        void EnsureManagerEditor()
        {
            if (!manager || managerEditor) return;
            managerEditor = UnityEditor.Editor.CreateEditor(manager, typeof(LevelManagerEditor)) as LevelManagerEditor;
        }

        void DestroyManagerEditor()
        {
            if (managerEditor)
                DestroyImmediate(managerEditor);
            managerEditor = null;
        }
    }
}
