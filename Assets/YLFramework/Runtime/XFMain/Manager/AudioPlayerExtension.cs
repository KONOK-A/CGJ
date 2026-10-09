using System;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
using AssetBundleManager = XFABManager.AssetBundleManager;
using ApplicationTool = YLFramework.Foundation.ApplicationTool;
using CoroutineStarter = YLFramework.Foundation.CoroutineStarter;


namespace YLFramework.Core
{
    internal class AudioPlayerExtension  
    {
        #region 常量

        private const int UNLOAD_TIME_OUT = 600; // 默认超过十分钟卸载 
         
        private static WaitForSeconds CheckTimeInterval = new WaitForSeconds(60); // 1分钟检测异常

        #endregion


        #region 单例

        private static AudioPlayerExtension _instance;

        internal static AudioPlayerExtension Instance
        {
            get
            {
#pragma warning disable IDE0074 // 使用复合分配
                if (_instance == null)
                {
                    _instance = new AudioPlayerExtension(); 
#if UNITY_EDITOR
                    EditorApplication.playModeStateChanged -= EditorApplication_playModeStateChanged;
                    EditorApplication.playModeStateChanged += EditorApplication_playModeStateChanged;
#endif
                }
#pragma warning restore IDE0074 // 使用复合分配

                return _instance;
            }
        }


#if UNITY_EDITOR

        private static void EditorApplication_playModeStateChanged(PlayModeStateChange obj)
        {
            // Edit > Project Settings > Editor >  Enter Play Mode Settings > Reload Domain 
            // 该选项如果禁用掉,重新运行时静态数据不会清空,可能会导致异常,所以这里置空,防止异常
            switch (obj)
            {
                case PlayModeStateChange.ExitingEditMode:
                    _instance = null;
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    _instance = null;
                    break;
            }
        }
#endif


        private AudioPlayerExtension()
        {
        }

        #endregion


        #region 静态字段

        /// <summary>
        /// 音频文件的引用计数
        /// </summary>
        private Dictionary<AudioClip, int> AuidoClipRefCount = new Dictionary<AudioClip, int>();

        /// <summary>
        /// 引用计数为0 准备卸载的音频
        /// </summary>
        private Dictionary<AudioClip, float> ReadyUnloadClip = new Dictionary<AudioClip, float>();

        private List<AudioClip> TempAudioClipList = new List<AudioClip>();


        private Coroutine coroutine_check_unload = null;

        private Dictionary<string, AudioController> AudioControllers = new Dictionary<string, AudioController>();

        #endregion
          
        #region 静态方法

        /// <summary>
        /// 注册AudioController
        /// </summary>
        /// <param name="name">名称,勿重复!</param>
        /// <param name="loop">是否循环播放</param> 
        internal static void RegisterAudioController(string name, bool loop, float defaultVolume = 1)
        {
            if (Instance.AudioControllers.ContainsKey(name)) return;
            if (!ApplicationTool.isPlaying) return;
            AudioController config = new AudioController(name, loop, defaultVolume);
            Instance.AudioControllers.Add(config.Name, config);
        }

        /// <summary>
        /// 注册AudioController(使用指定的AudioSource)
        /// </summary>
        /// <param name="name">名称,勿重复!</param>
        /// <param name="audioSource">AudioSource对象，该AudioController会使用这个AudioSourece来播放音频</param>
        /// <param name="worldPositionStays">AudioSource所在的游戏物体的位置是否保持不变,true:不变 false:会放到一个统一的节点下面</param>
        internal static void RegisterAudioController(string name, AudioSource audioSource, bool worldPositionStays = true, float defaultVolume = 1)
        {
            if (audioSource == null) return;
            if (!ApplicationTool.isPlaying) return;
            if (Instance.AudioControllers.ContainsKey(name)) return;
            AudioController config = new AudioController(name, audioSource, worldPositionStays, defaultVolume);
            Instance.AudioControllers.Add(config.Name, config);
        }

        /// <summary>
        /// 查询AudioController
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        internal static AudioController GetAuidoController(string name)
        {
            if (Instance.AudioControllers.ContainsKey(name))
                return Instance.AudioControllers[name];
            return null;
        }

        /// <summary>
        /// 移除AudioController
        /// </summary>
        /// <param name="name"></param>
        internal static void RemoveAudioController(string name)
        {
            if (!Instance.AudioControllers.ContainsKey(name)) return;
            Instance.AudioControllers[name].Remove();
            Instance.AudioControllers.Remove(name);
        }

        /// <summary>
        /// 播放音频
        /// </summary>
        /// <param name="controllerName">AudioController名称</param>
        /// <param name="clip">音频</param>
        /// <param name="playOneShot">是否为PlayOneShot</param>
        /// <exception cref="Exception"></exception>
        internal static void Play(string controllerName, AudioClip clip, bool playOneShot)
        {
            if (Instance.coroutine_check_unload == null)
                Instance.coroutine_check_unload = CoroutineStarter.Start(CheckReadyUnloadClip());

            if (clip == null) return;
            AudioController controller = GetAuidoController(controllerName);
            if (controller == null)
                throw new Exception(string.Format("查询AudioController:{0}失败!", controllerName));

            controller.Play(clip, playOneShot, OnAudioClipPlayEnd);
            AddRefCount(clip);
        }

        internal static void Play(AudioPlayer player, string controllerName, AudioClip clip, bool playOneShot)
        {
            if (Instance.coroutine_check_unload == null)
                Instance.coroutine_check_unload = CoroutineStarter.Start(CheckReadyUnloadClip());

            if (clip == null) return;
            AudioController controller = GetAuidoController(controllerName);
            if (controller == null)
                throw new Exception(string.Format("查询AudioController:{0}失败!", controllerName));

            controller.Play(player, clip, playOneShot, OnAudioClipPlayEnd);
            AddRefCount(clip);
        }

        /// <summary>
        /// 在某一个位置播放音频
        /// </summary>
        /// <param name="controllerName"></param>
        /// <param name="clip"></param>
        /// <param name="position"></param>
        /// <exception cref="Exception"></exception>
        internal static void PlayAtPoint(string controllerName, AudioClip clip, Vector3 position)
        {

            if (Instance.coroutine_check_unload == null)
                Instance.coroutine_check_unload = CoroutineStarter.Start(CheckReadyUnloadClip());

            if (clip == null) return;
            AudioController controller = GetAuidoController(controllerName);
            if (controller == null)
                throw new Exception(string.Format("查询AudioController:{0}失败!", controllerName));

            controller.PlayAtPoint(clip, position, OnAudioClipPlayEnd);
            AddRefCount(clip);
        }

        /// <summary>
        /// 暂停播放
        /// </summary>
        /// <param name="controllerName">AudioController名称</param>
        /// <param name="pause">true:暂停 false:取消暂停</param>
        /// <exception cref="Exception"></exception>
        internal static void Pause(string controllerName, bool pause)
        {
            AudioController controller = GetAuidoController(controllerName);
            if (controller == null)
                throw new Exception(string.Format("查询AudioConfig:{0}失败!", controllerName));

            controller.Pause(pause);
        }

        /// <summary>
        /// 停止播放
        /// </summary>
        /// <param name="controllerName">AudioController名称</param>
        internal static void Stop(string controllerName, StopType type = StopType.All)
        {
            AudioController controller = GetAuidoController(controllerName);
            if (controller == null) return;
            controller.Stop(type);
        }

        /// <summary>
        /// 添加引用数量
        /// </summary>
        /// <param name="clip"></param>
        internal static void AddRefCount(AudioClip clip)
        {
            if (clip == null) return;

            if (Instance.AuidoClipRefCount.ContainsKey(clip))
                Instance.AuidoClipRefCount[clip]++;
            else
                Instance.AuidoClipRefCount.Add(clip, 1);

            // 取消卸载
            if (Instance.ReadyUnloadClip.ContainsKey(clip))
                Instance.ReadyUnloadClip.Remove(clip);


        }

        private static void RemoveRefCount(AudioClip clip)
        {
            if (clip == null) return;

            if (!Instance.AuidoClipRefCount.ContainsKey(clip)) return;
            Instance.AuidoClipRefCount[clip]--;

            if (Instance.AuidoClipRefCount[clip] <= 0)
            {
                // 准备卸载
                if (!Instance.ReadyUnloadClip.ContainsKey(clip))
                    Instance.ReadyUnloadClip.Add(clip, Time.time);
            }

        }

        private static void OnAudioClipPlayEnd(AudioClip clip)
        {
            //Debug.LogFormat("音频播放结束!{0}",Time.time);
            RemoveRefCount(clip);
        }

        internal static IEnumerator CheckReadyUnloadClip()
        {
            while (true)
            {
                Instance.TempAudioClipList.Clear();
                foreach (var item in Instance.ReadyUnloadClip.Keys)
                {
                    Instance.TempAudioClipList.Add(item);
                }

                foreach (var item in Instance.TempAudioClipList)
                {
                    yield return null;
                    if (!Instance.ReadyUnloadClip.ContainsKey(item)) continue;
                    if (Time.time - Instance.ReadyUnloadClip[item] >= UNLOAD_TIME_OUT)
                    {
                        // 卸载资源
                        AssetBundleManager.UnloadAsset(item);
                        Instance.ReadyUnloadClip.Remove(item);
                    }
                }

                yield return CheckTimeInterval; // 1分钟检测一次
            }
        }

        #endregion
    }
}

