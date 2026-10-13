using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace YLFramework.Core.Editor
{
    public class ListenerInitOnLoad
    {
        public const string ResourceRoot = "Assets/YLFramework/Runtime/XFMain/Resources/YLFramework/Core";
        public const string CanvasPath = ResourceRoot + "/Canvas.prefab";
        public const string EventSystemPath = ResourceRoot + "/EventSystem.prefab";

        [InitializeOnLoadMethod]
        private static void InitOnLoad()
        {
            EditorApplication.delayCall += GenerateDefaultResources;
        }

        [MenuItem("YLFramework/Core/Create Missing Default Resources")]
        public static void GenerateDefaultResources()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
                return;
            GenerateCanvas();
            GenerateEventSystem();
        }

        private static bool CanCreate(string path)
        {
            // Do not overwrite unimported files, existing assets or orphaned metadata.
            return !File.Exists(path) && !File.Exists(path + ".meta") &&
                AssetDatabase.LoadMainAssetAtPath(path) == null;
        }

        private static void GenerateCanvas()
        {
            if (!CanCreate(CanvasPath))
                return;
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var canvas = CreateCanvas(scene);
                Directory.CreateDirectory(ResourceRoot);
                PrefabUtility.SaveAsPrefabAsset(canvas, CanvasPath);
                EnablePrefabAsset(CanvasPath);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void GenerateEventSystem()
        {
            if (!CanCreate(EventSystemPath))
                return;
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var eventSystem = CreateEventSystem(scene);
                Directory.CreateDirectory(ResourceRoot);
                PrefabUtility.SaveAsPrefabAsset(eventSystem, EventSystemPath);
                EnablePrefabAsset(EventSystemPath);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static GameObject CreateCanvas(Scene scene)
        {
            var canvas = new GameObject("Canvas", typeof(RectTransform));
            canvas.SetActive(false);
            SceneManager.MoveGameObjectToScene(canvas, scene);
            var component = canvas.AddComponent<Canvas>();
            component.renderMode = RenderMode.ScreenSpaceOverlay;
            component.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2 | AdditionalCanvasShaderChannels.TexCoord3;
            var scaler = canvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1;
            canvas.AddComponent<GraphicRaycaster>();
            canvas.AddComponent<AutomaticAdaptationCanvasScaler>();
            return canvas;
        }

        private static GameObject CreateEventSystem(Scene scene)
        {
            var eventSystem = new GameObject("EventSystem");
            eventSystem.SetActive(false);
            SceneManager.MoveGameObjectToScene(eventSystem, scene);
            eventSystem.AddComponent<EventSystem>();
            return eventSystem;
        }

        private static void EnablePrefabAsset(string path)
        {
            // Set the serialized asset active without activating scene input components.
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                throw new IOException("Could not create YL default resource: " + path);
            using (var serialized = new SerializedObject(prefab))
            {
                serialized.FindProperty("m_IsActive").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SavePrefabAsset(prefab);
        }
    }
}
