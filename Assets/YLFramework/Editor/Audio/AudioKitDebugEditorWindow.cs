using YLFramework.Editor.Help;
using YLFramework.Services.Audio;
using UnityEditor;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace YLFramework.Editor.Audio
{
    [MovedFrom(true, "Project.AudioKit.Editor", "Project.AudioKit.Editor", "AudioKitDebugEditorWindow")]
    internal sealed class AudioKitDebugEditorWindow : EditorWindow
    {
        private Vector2 mScrollPosition;
        private double mNextRepaint;

        [MenuItem("YLFramework/Audio/Debug")]
        private static void Open()
        {
            var window = GetWindow<AudioKitDebugEditorWindow>();
            window.titleContent = new GUIContent("AudioKit Debug");
            window.minSize = new Vector2(360, 240);
            window.Show();
        }

        private void Update()
        {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < mNextRepaint) return;
            mNextRepaint = EditorApplication.timeSinceStartup + 0.2;
            Repaint();
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("AudioKit 帮助", EditorStyles.toolbarButton)) HelpWindow.Open(AudioKitHelpProvider.ModuleId);
                using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
                {
                    if (GUILayout.Button(AudioKitDebugWindow.IsVisible ? "隐藏运行时 GUI" : "显示运行时 GUI", EditorStyles.toolbarButton))
                        AudioKitDebugWindow.Toggle();
                }
            }
            AudioKitDiagnostics.Draw(ref mScrollPosition);
        }
    }
}
