using UnityEngine;

namespace YLFramework.Extensions
{
    /// <summary>
    /// Camera 扩展：相机画面捕获。
    /// </summary>
    public static class CameraExtension
    {
        /// <summary>
        /// 截取相机当前画面。rect 以屏幕像素为单位，必须落在 <see cref="Screen.width"/> × <see cref="Screen.height"/> 范围内。
        /// 渲染前会替换相机的 targetTexture，结束后无论成功失败都会恢复原 targetTexture 与 <see cref="RenderTexture.active"/>。
        /// 内部使用的临时 RenderTexture 由本方法负责销毁。
        /// </summary>
        /// <param name="camera">目标相机。</param>
        /// <param name="rect">截取区域（屏幕像素）。</param>
        /// <returns>返回的 Texture2D 归调用方所有，由调用方负责销毁。</returns>
        public static Texture2D CaptureCamera(this Camera camera, Rect rect)
        {
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;

            var renderTexture = new RenderTexture(Screen.width, Screen.height, 0);
            try
            {
                camera.targetTexture = renderTexture;
                camera.Render();

                RenderTexture.active = renderTexture;

                var screenShot = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGB24, false);
                screenShot.ReadPixels(rect, 0, 0);
                screenShot.Apply();
                return screenShot;
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;

                if (Application.isPlaying)
                {
                    Object.Destroy(renderTexture);
                }
                else
                {
                    Object.DestroyImmediate(renderTexture);
                }
            }
        }
    }
}
