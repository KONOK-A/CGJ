using System;
using System.Collections;
using System.Collections.Generic; 
using UnityEngine; 
using AssetBundleManager = XFABManager.AssetBundleManager;

namespace YLFramework.Core
{

    public enum AudioAssetType {
        AssetBundle,
        AudioClip
    }

    public enum AudioPlayType { 
        Play,
        PlayOneShot
    }

    public enum StopType 
    {
        [Tooltip("停止播放类型为Play的音频组!")]
        Play,
        [Tooltip("停止播放类型为Play 和 PlayOneShot 的音频组,停止播放所有音频")]
        All
    }

    //public enum AudioPlayPosition {
    //    IgnorePosition,
    //    CurrentTransfomrPosition,
    //    CustomPosition
    //}

    /// <summary>
    /// 音频资源名称信息
    /// </summary>
    [Serializable]
    public class ClipAssetNameInfo 
    {
        [Tooltip("音频资源组名称,不可重复!")]
        public string name = "Default"; 
        [Tooltip("音频资源名称,如果有多个,会随机一个播放!")]
        public List<string> assetNames;
        [Tooltip("Play Or PlayOneShot?")]
        public AudioPlayType playType = AudioPlayType.PlayOneShot;
        [Tooltip("是否在游戏物体激活时自动播放")]
        public bool playOnAwake;
    }

    /// <summary>
    /// 音频资源信息
    /// </summary>
    [Serializable]
    public class ClipAssetInfo 
    {
        [Tooltip("音频资源组名称,不可重复!")]
        public string name = "Default";
        [Tooltip("音频资源,如果有多个,会随机一个播放!")]
        public List<AudioClip> clips;
        [Tooltip("Play Or PlayOneShot?")]
        public AudioPlayType playType = AudioPlayType.PlayOneShot;
        [Tooltip("是否在游戏物体激活时自动播放")]
        public bool playOnAwake; 
    }

    public class AudioPlayer : MonoBehaviour
    {
         
        #region 拓展方法
         
        /// <summary>
        /// 注册AudioController
        /// </summary>
        /// <param name="name">名称,勿重复!</param>
        /// <param name="loop">是否循环播放</param> 
        public static void RegisterAudioController(string name, bool loop,float defaultVolume = 1)
        { 
            AudioPlayerExtension.RegisterAudioController(name, loop, defaultVolume);
        }
         
        /// <summary>
        /// 注册AudioController(使用指定的AudioSource)
        /// </summary>
        /// <param name="name">名称,勿重复!</param>
        /// <param name="audioSource">AudioSource对象，该AudioController会使用这个AudioSourece来播放音频</param>
        /// <param name="worldPositionStays">AudioSource所在的游戏物体的位置是否保持不变,true:不变 false:会放到一个统一的节点下面</param>
        public static void RegisterAudioController(string name, AudioSource audioSource, bool worldPositionStays = true, float defaultVolume = 1) {
            AudioPlayerExtension.RegisterAudioController(name,audioSource,worldPositionStays,defaultVolume);
        }

        /// <summary>
        /// 查询AudioController
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static AudioController GetAuidoController(string name)
        { 
            return AudioPlayerExtension.GetAuidoController(name);
        }

        /// <summary>
        /// 移除AudioController
        /// </summary>
        /// <param name="name"></param>
        public static void RemoveAudioController(string name) {
            AudioPlayerExtension.RemoveAudioController(name);
        }

        /// <summary>
        /// 播放音频
        /// </summary>
        /// <param name="controllerName">AudioController名称</param>
        /// <param name="clip">音频</param>
        /// <param name="playOneShot">是否为PlayOneShot</param>
        /// <exception cref="Exception"></exception>
        public static void Play(string controllerName,AudioClip clip, bool playOneShot)
        {
             AudioPlayerExtension.Play(controllerName,clip,playOneShot);
        }

        internal static void Play(AudioPlayer player, string controllerName, AudioClip clip, bool playOneShot)
        {
            AudioPlayerExtension.Play(player,controllerName,clip,playOneShot);
        }

        /// <summary>
        /// 在某一个位置播放音频
        /// </summary>
        /// <param name="controllerName"></param>
        /// <param name="clip"></param>
        /// <param name="position"></param>
        /// <exception cref="Exception"></exception>
        public static void PlayAtPoint(string controllerName, AudioClip clip, Vector3 position) {
            AudioPlayerExtension.PlayAtPoint(controllerName,clip,position);
        }

        /// <summary>
        /// 暂停播放
        /// </summary>
        /// <param name="controllerName">AudioController名称</param>
        /// <param name="pause">true:暂停 false:取消暂停</param>
        /// <exception cref="Exception"></exception>
        public static void Pause(string controllerName,bool pause) {
            AudioPlayerExtension.Pause(controllerName,pause);
        }

        /// <summary>
        /// 停止播放
        /// </summary>
        /// <param name="controllerName">AudioController名称</param>
        public static void Stop(string controllerName,StopType type = StopType.All) {
            AudioPlayerExtension.Stop(controllerName,type);
        }

#endregion

        #region 字段

        [SerializeField]
        [Tooltip("使用当前的AudioSource来播放音频")]
        private AudioSource audioSource;

        [SerializeField]
        [Tooltip("使用哪一个Controller来播放音频")]
        private string controllerName;

        [SerializeField]
        [Tooltip("音频资源类型")]
        private AudioAssetType audioAssetType;

        [SerializeField]
        [Tooltip("音频所在的资源模块名称")]
        private string projectName; 
        [Tooltip("音频资源名称")]
        public List<ClipAssetNameInfo> AssetNames;
         
        /// <summary>
        /// 存储音频资源名称映射关系
        /// </summary>
        private Dictionary<string, ClipAssetNameInfo> assetnames_dic = new Dictionary<string, ClipAssetNameInfo>();

        [SerializeField]
        [Tooltip("需要播放的音频文件")]
        public List<ClipAssetInfo> Clips;

        /// <summary>
        /// 存储音频资源映射关系
        /// </summary>
        private Dictionary<string, ClipAssetInfo> clips_dic = new Dictionary<string, ClipAssetInfo>();

        private string generateAudioControllerName;

        #endregion

        #region 属性

        private AudioController AudioController => AudioPlayer.GetAuidoController(controllerName);
         
        /// <summary>
        /// 当前AudioPlayer使用哪个AudioController来播放音频
        /// </summary>
        public string ControllerName
        {
            get { 
                return controllerName;
            }
            set {
                if (controllerName.Equals(value)) return;
                if (AudioController != null) 
                {
                    AudioController.onVolumeValueChange -= OnControllerVolumeChange;
                    AudioController.onMuteValueChange -= OnControllerMuteChange;
                }
                controllerName = value;
                if (AudioController != null)
                {
                    AudioController.onVolumeValueChange += OnControllerVolumeChange;
                    AudioController.onMuteValueChange += OnControllerMuteChange;
                }
            }
        }

        /// <summary>
        /// 音频资源类型,(AssetBundle Or AudioClip)
        /// </summary>
        public AudioAssetType AudioAssetType
        {
            get 
            {
                return audioAssetType;
            }
            set {
                audioAssetType = value;
            }
        }

        /// <summary>
        /// 音频所在的资源模块名称(AudioAssetType为AssetBundle时有效)
        /// </summary>
        public string ProjectName
        {
            get {
                return projectName;
            }
            set {
                projectName = value;
            }
        }

        /// <summary>
        /// AudioSource
        /// </summary>
        public AudioSource AudioSource => audioSource;

#endregion

        #region 生命周期

        private void Awake()
        {
            audioSource = transform.GetComponent<AudioSource>();
            if (audioSource != null)
            { 
                audioSource.clip = null;
                generateAudioControllerName = string.Format("{0}:{1}",transform.name,audioSource.GetHashCode());
                RegisterAudioController(generateAudioControllerName, audioSource);

                if (AudioController != null)
                {
                    AudioController.onVolumeValueChange += OnControllerVolumeChange;
                    AudioController.onMuteValueChange += OnControllerMuteChange;
                }
            }

            InitAudioAsset();

        }
         
        private void OnDestroy() 
        {
            Stop(StopType.Play);
            if (audioSource != null)
                RemoveAudioController(generateAudioControllerName);
        }


        private void OnEnable() 
        { 
            switch (audioAssetType)
            {
                case AudioAssetType.AssetBundle:

                    foreach (var item in AssetNames)
                    {
                        if(!item.playOnAwake) 
                            continue;
                        Play(item.name);
                    }

                    break;
                case AudioAssetType.AudioClip:
                    foreach (var item in Clips)
                    {
                        if(!item.playOnAwake) 
                            continue;
                        Play(item.name);
                    }

                    break; 
            }

        }

        #endregion

        #region 方法

        /// <summary>
        /// 初始化资源
        /// </summary>
        private void InitAudioAsset()
        {
            assetnames_dic.Clear();
            foreach (var item in AssetNames)
            {
                if (assetnames_dic.ContainsKey(item.name))
                    throw new Exception(string.Format("GameObject:{0} 音频资源组名称:{1}重复!",gameObject.name,item.name));
                assetnames_dic.Add(item.name, item);
            }

            clips_dic.Clear();

            foreach (var item in Clips)
            {
                if (clips_dic.ContainsKey(item.name))
                    throw new Exception(string.Format("GameObject:{0} 音频资源组名称:{1}重复!", gameObject.name, item.name));
                clips_dic.Add(item.name, item);
            }

        }


        /// <summary>
        /// 播放(默认播放第一组)
        /// </summary>
        /// <exception cref="Exception"></exception> 
        public void Play() 
        {
            Play(null);
        }

        /// <summary>
        /// 播放指定组音频
        /// </summary>
        /// <param name="name">音频组名称</param>
        public void Play(string name) 
        {
            if (name == null) 
                name = string.Empty;

            if (AudioController == null)
                throw new Exception(string.Format("未查询到AudioController:{0}", controllerName));

            if (audioSource != null)
            {
                audioSource.volume = AudioController.Volume;
                audioSource.mute = AudioController.Mute;
            }

            AudioPlayType type;
            AudioClip clip = GetCurrentClip(name, out type);

            Play(this,audioSource != null ? generateAudioControllerName : controllerName,clip, type == AudioPlayType.PlayOneShot);
        }

        private AudioClip GetCurrentClip(string name,out AudioPlayType playType) 
        { 
            int index = 0;

            switch (AudioAssetType)
            {
                case AudioAssetType.AssetBundle:

                    if (AssetNames.Count == 0)
                    {
                        playType = AudioPlayType.PlayOneShot;
                        return null;    
                    }

                    if(string.IsNullOrEmpty(name) && !assetnames_dic.ContainsKey(name))
                        name = AssetNames[0].name;

                    string audio_asset_name = null;

                    if (assetnames_dic.ContainsKey(name))
                    {
                        ClipAssetNameInfo assetNameInfo = assetnames_dic[name];

                        if (assetNameInfo.assetNames == null || assetNameInfo.assetNames.Count == 0)
                        {
                            playType = AudioPlayType.PlayOneShot;
                            return null;
                        }

                        index = UnityEngine.Random.Range(0, assetNameInfo.assetNames.Count);
                        audio_asset_name = assetNameInfo.assetNames[index];
                        playType = assetNameInfo.playType;
                    }
                    else 
                        playType = AudioPlayType.PlayOneShot;
                    

                    if (string.IsNullOrEmpty(audio_asset_name)) {
                        playType = AudioPlayType.PlayOneShot;
                        return null;
                    }
                     
                    return AssetBundleManager.LoadAsset<AudioClip>(projectName, audio_asset_name);
                     
                case AudioAssetType.AudioClip:

                    if (Clips.Count == 0) {
                        playType = AudioPlayType.PlayOneShot;
                        return null;
                    }

                    if(string.IsNullOrEmpty(name) && !clips_dic.ContainsKey(name))
                        name = Clips[0].name;


                    if (clips_dic.ContainsKey(name))
                    {
                        ClipAssetInfo clipAsset = clips_dic[name];

                        if (clipAsset.clips == null || clipAsset.clips.Count == 0) {
                            playType = AudioPlayType.PlayOneShot;
                            return null;
                        }
                        index = UnityEngine.Random.Range(0, clipAsset.clips.Count);
                        playType = clipAsset.playType;
                        return clipAsset.clips[index]; 
                    }
                    break;
            }

            playType = AudioPlayType.PlayOneShot; 
            return null;
        }

         
        /// <summary>
        /// 暂停
        /// </summary>
        /// <param name="pause"></param>
        public void Pause(bool pause)
        {
            Pause(audioSource != null ? generateAudioControllerName : controllerName, pause);
        }

        /// <summary>
        /// 停止当前controllerName所有音频播放
        /// </summary>
        public void Stop()
        {
            Stop(StopType.All);
        }

        /// <summary>
        /// 停止播放
        /// </summary>
        /// <param name="type">类型</param>
        public void Stop(StopType type) 
        {  
            string c = audioSource != null ? generateAudioControllerName : controllerName;

            switch (type)
            { 
                case StopType.All:
                    Stop(c);
                    break;

                case StopType.Play:

                    AudioController controller = GetAuidoController(c);
                    if (controller == null) return;
                    controller.Stop(this); 
                    break;
            }
        }


        private void OnControllerVolumeChange(float value)
        {
            if (audioSource == null) return;
            audioSource.volume = value;
        }

        private void OnControllerMuteChange(bool mute)
        {
            if (audioSource == null) return;
            audioSource.mute = mute;
        }

#endregion
    }
}


