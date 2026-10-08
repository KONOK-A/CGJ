using YLFramework.Actions;
// 按本地 ActionKit Prefab 的设置创建模块自有资源，不读取、复制或覆盖旧资源。
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace YLFramework.Editor.Actions
{
    internal static class ScreenTransitionResourceCreator
    {
        private const string AssetPath = "Assets/YLFramework/Runtime/Action/Resources/ActionKit/ScreenTransitionCanvas.prefab";

        [MenuItem("YLFramework/Action/创建屏幕过渡资源")]
        private static void CreateResource()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Exit Play Mode before creating the ActionKit resource.");
            }

            var absolutePath = Path.Combine(Application.dataPath, AssetPath.Substring("Assets/".Length));
            if (File.Exists(absolutePath) || File.Exists(absolutePath + ".meta"))
            {
                var existing = AssetDatabase.LoadMainAssetAtPath(AssetPath);
                if (existing) EditorGUIUtility.PingObject(existing);
                Debug.Log("Existing ActionKit ScreenTransition resource or metadata was kept unchanged: " + AssetPath);
                return;
            }

            EnsureFolder("Assets/YLFramework/Runtime/Action/Resources/ActionKit");
            var temporaryScene = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            GameObject imageObject = null;
            try
            {
                // 先移入临时预览场景再创建 UI，避免影响用户当前场景。
                root = EditorUtility.CreateGameObjectWithHideFlags("ScreenTransitionCanvas",
                    HideFlags.HideAndDontSave, typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(root, temporaryScene);
                root.hideFlags = HideFlags.None;
                root.AddComponent<Canvas>();
                root.AddComponent<CanvasScaler>();
                root.AddComponent<ScreenTransitionCanvas>();
                root.layer = 5;
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 10000;
                canvas.sortingLayerID = 0;
                canvas.planeDistance = 100;
                canvas.pixelPerfect = false;
                canvas.overrideSorting = false;
                canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.None;
                canvas.targetDisplay = 0;
                var scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = 1;
                scaler.referencePixelsPerUnit = 100;
                scaler.referenceResolution = new Vector2(800, 600);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0;
                scaler.physicalUnit = CanvasScaler.Unit.Points;
                scaler.fallbackScreenDPI = 96;
                scaler.defaultSpriteDPI = 96;
                scaler.dynamicPixelsPerUnit = 1;

                imageObject = EditorUtility.CreateGameObjectWithHideFlags("ColorImage",
                    HideFlags.HideAndDontSave, typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(imageObject, temporaryScene);
                imageObject.hideFlags = HideFlags.None;
                imageObject.AddComponent<CanvasRenderer>();
                imageObject.AddComponent<Image>();
                imageObject.layer = 5;
                var imageRect = imageObject.GetComponent<RectTransform>();
                imageRect.SetParent(root.transform, false);
                imageRect.anchorMin = Vector2.zero;
                imageRect.anchorMax = Vector2.one;
                imageRect.pivot = new Vector2(0.5f, 0.5f);
                imageRect.anchoredPosition = Vector2.zero;
                imageRect.sizeDelta = Vector2.zero;
                var image = imageObject.GetComponent<Image>();
                image.color = Color.black;
                image.raycastTarget = true;
                image.type = Image.Type.Simple;
                image.sprite = null;
                image.useSpriteMesh = false;
                imageObject.GetComponent<CanvasRenderer>().cullTransparentMesh = false;
                root.GetComponent<ScreenTransitionCanvas>().ColorImage = image;

                var rootRect = root.GetComponent<RectTransform>();
                rootRect.anchorMin = Vector2.zero;
                rootRect.anchorMax = Vector2.zero;
                rootRect.pivot = Vector2.zero;
                rootRect.anchoredPosition = Vector2.zero;
                rootRect.sizeDelta = Vector2.zero;
                // 保留既有序列化基线；视觉效果由 Unity 验证，不在此猜测调整。
                rootRect.localScale = Vector3.zero;

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, AssetPath, out var success);
                if (!success || !prefab)
                    throw new InvalidOperationException("Could not save the new ActionKit ScreenTransition resource: " + AssetPath);
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
                Debug.Log("Created ActionKit ScreenTransition resource: " + AssetPath);
            }
            finally
            {
                if (imageObject) Object.DestroyImmediate(imageObject);
                if (root) Object.DestroyImmediate(root);
                if (temporaryScene.IsValid()) EditorSceneManager.ClosePreviewScene(temporaryScene);
            }
        }

        private static void EnsureFolder(string path)
        {
            var segments = path.Split('/');
            var parent = segments[0];
            for (var index = 1; index < segments.Length; index++)
            {
                var next = parent + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next) && string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, segments[index])))
                    throw new IOException("Could not create the ActionKit resource folder: " + next);
                parent = next;
            }
        }
    }
}
