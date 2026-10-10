using System; 
using System.Collections.Generic; 
using UnityEngine;
using ApplicationTool = YLFramework.Foundation.ApplicationTool;
using UpdateManager = YLFramework.Foundation.UpdateManager;
using YLFramework.Events;

namespace YLFramework.Core
{
    public struct AudioData
    {
        public AudioClip clip;
        public Action<AudioClip> onPlayEnd;
        public float playTime;
        public bool playOneShot;
        public bool ignorePause;

        public AudioData(AudioClip clip, Action<AudioClip> onPlayEnd,bool playOneShot,bool ignorePause = false)
        {
            this.clip = clip;
            this.onPlayEnd = onPlayEnd;
            this.playTime = 0;
            this.playOneShot = playOneShot;
            this.ignorePause = ignorePause;
        }

        public bool IsPlayEnd() {
            return playTime - clip.length > 1;
        }

        public void OnFinsh() {
            onPlayEnd?.Invoke(clip);
        }

    }

    public class AudioController
    {

        #region 静态字段

        private static Transform _audioSourceParent;

        #endregion


        #region 静态属性 

        /// <summary>
        /// 音频组件的父节点
        /// </summary>
        public static Transform AudioSourceParent
        {
            get {

                if (!_audioSourceParent&& ApplicationTool.isPlaying)
                {
                    GameObject parent = new GameObject("YLFramework.Core.AudioManager");
                    GameObject.DontDestroyOnLoad(parent);
                    _audioSourceParent = parent.transform;
                }

                return _audioSourceParent;
            }
        }
         
        #endregion

        #region 属性
        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get;private set; }
        /// <summary>
        /// 播放音频的组件
        /// </summary>
        public AudioSource AudioSource { get;private set; }
        /// <summary>
        /// 音量,可以通过修改此属性来调整音量大小(持久化存储)
        /// </summary>
        public float Volume
        {
            get
            {
                return PlayerPrefs.GetFloat(string.Format("YLFramework.Core.Audio.Volume:{0}", Name), DefaultVolume);
            }
            set
            {
                if (AudioSource.volume == value) return;
                AudioSource.volume = value;
                onVolumeValueChange?.Invoke(value);
                PlayerPrefs.SetFloat(string.Format("YLFramework.Core.Audio.Volume:{0}", Name), value);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// 是否静音,可以通过修改此属性来修改是否静音(持久化存储)
        /// </summary>
        public bool Mute
        {
            get
            {
                return PlayerPrefs.GetInt(string.Format("YLFramework.Core.Audio.Mute:{0}", Name), 0) == 1;
            }
            set
            {
                if (AudioSource.mute == value) return;
                PlayerPrefs.SetInt(string.Format("YLFramework.Core.Audio.Mute:{0}", Name), value?1:0);
                PlayerPrefs.Save();
                AudioSource.mute = value;
                onMuteValueChange?.Invoke(value);
            }
        }

        /// <summary>
        /// 当前音频播放的进度(PlayOneShot播放模式无法获取进度)
        /// </summary>
        public float Progress
        {
            get {
                if (AudioSource.clip == null) return 0;
                return AudioSource.time / AudioSource.clip.length;
            }
        }


        /// <summary>
        /// 默认音量大小
        /// </summary>
        public float DefaultVolume { get; set; }


        #endregion

        #region 字段

        private List<AudioData> AudioDatas = new List<AudioData>();

        /// <summary>
        /// 是不是正在暂停
        /// </summary>
        private bool isPause = false;

        /// <summary>
        /// 用于AudioPlayer的暂停
        /// </summary>
        private AudioPlayer audioPlayer = null;


        private bool createByMyself = false; // 是否是自己创建的AudioSource
        private IUnRegister updateSubscription;
        private bool removed;

        #endregion

        #region 事件
        /// <summary>
        /// 音量改变的事件
        /// </summary>
        public Action<float> onVolumeValueChange;
        /// <summary>
        /// 是否静音改变的事件
        /// </summary>
        public Action<bool> onMuteValueChange;
        #endregion

        #region 构造函数

        internal AudioController() { }

        internal AudioController(string name, bool loop = false,float defaultVolume = 1) 
        {
            if (!ApplicationTool.isPlaying) return;

            Name = name;
            this.DefaultVolume = defaultVolume;
            GameObject audioSource = new GameObject(name);
            audioSource.transform.SetParent(AudioSourceParent);
            AudioSource = audioSource.AddComponent<AudioSource>();
            AudioSource.loop = loop;
            AudioSource.volume = Volume;
            AudioSource.mute = Mute;
            AudioSource.spatialBlend = 0;
            // 注册Update
            updateSubscription = UpdateManager.AddUpdateEvent(Update);

            createByMyself = true;

        }


        internal AudioController(string name, AudioSource audioSource,bool worldPositionStays = true, float defaultVolume = 1)
        {
            if (!ApplicationTool.isPlaying) return;
            if (audioSource == null) throw new Exception("audioSource is null!");
            Name = name;

            this.DefaultVolume= defaultVolume;

            if (!worldPositionStays) 
            {
                audioSource.transform.SetParent(AudioSourceParent,true);
            }
            
            AudioSource = audioSource;
            Volume = AudioSource.volume;
            Mute = AudioSource.mute;
            // 注册Update 
            updateSubscription = UpdateManager.AddUpdateEvent(Update);
        }

        #endregion

        #region 方法

        internal void Play(AudioClip clip,bool playOneShot,Action<AudioClip> onPlayEnd = null)
        {
            if (AudioSource == null)
                throw new Exception("AudioSource为空，可能已被销毁!");
             
            if (playOneShot)
            { 
                AudioSource.PlayOneShot(clip);
            }
            else
            { 
                AudioSource.clip = clip;
                AudioSource.Play(); 
            }

            AddAudioData(clip, onPlayEnd, playOneShot);
            isPause = false;
        }

        internal void Play(AudioPlayer player, AudioClip clip, bool playOneShot, Action<AudioClip> onPlayEnd = null) {

            if (!playOneShot) 
                audioPlayer = player; 

            Play(clip,playOneShot,onPlayEnd);
        }

        internal void PlayAtPoint(AudioClip clip ,Vector3 position, Action<AudioClip> onPlayEnd = null)
        {
            AudioSource.PlayClipAtPoint(clip,position,Mute?0:Volume);
            AddAudioData(clip, onPlayEnd,true,true);
            isPause = false;
        }

        internal void Pause(bool pause) {
            if (pause)
            {
                AudioSource.Pause();
            }
            else {
                AudioSource.UnPause();
            }
            isPause = pause;
        }

        internal void Stop(StopType type = StopType.All) 
        {
            if (type == StopType.All)
            {
                if (AudioSource != null)
                {
                    AudioSource.Stop();
                    AudioSource.clip = null;
                }

                foreach (var item in AudioDatas)
                {
                    item.OnFinsh();
                }
                AudioDatas.Clear();
                isPause = false;
            }
            else
            {
                if (AudioSource != null) 
                    AudioSource.clip = null; 
            }

            
        }

        internal void Stop(AudioPlayer player)
        {
            if (AudioSource == null)
                return;

            if (player == null)
                return;

            if (audioPlayer != player)
                return;

            audioPlayer = null;
            AudioSource.clip = null; 
        }         

        private void AddAudioData(AudioClip clip , Action<AudioClip> onPlayEnd ,bool playOneShot ,bool ignorePause = false )
        {
            AudioData data = new AudioData(clip, onPlayEnd,playOneShot, ignorePause);
            AudioDatas.Add(data);
        }

        private void Update()
        {
            if (removed) return;
            if (AudioSource == null)
            {
                ReleaseUpdate();
                Stop();
                return;
            }

            if (AudioDatas.Count == 0) return; 
 
            for (int i = 0; i < AudioDatas.Count; i++)
            { 
                AudioData data = AudioDatas[i];

                if (data.ignorePause)
                {
                    // 忽略暂停 不需要处理循环播放的逻辑
                    data.playTime += Time.unscaledDeltaTime;
                }
                else if(!isPause) // 如果不忽略暂停 就必须在播放中计时
                { 
                    data.playTime += Time.unscaledDeltaTime;

                    // 处理循环播放的逻辑
                    if (!data.playOneShot)
                    {
                        // 不是playOneShot的情况
                        if (data.clip != AudioSource.clip)
                            data.playTime = data.clip.length + 10; // 会被判定为播放结束
                        else
                        {
                            if (AudioSource.loop)
                            {
                                if (data.playTime >= data.clip.length)
                                    data.playTime = 0; // 循环播放 从头开始
                            }
                        }
                    }
                }

                AudioDatas[i] = data;

                if (AudioDatas[i].IsPlayEnd()) 
                {
                    AudioDatas[i].OnFinsh();
                    if (removed) return;
                    AudioDatas.RemoveAt(i);
                    i--;
                }
            } 
        }

        private void ReleaseUpdate()
        {
            removed = true;
            var subscription = updateSubscription;
            updateSubscription = null;
            subscription?.UnRegister();
        }

        internal void Remove() {
            if (removed) return;
            ReleaseUpdate();
            try { Stop(); }
            finally
            {
                if (createByMyself && AudioSource) GameObject.Destroy(AudioSource.gameObject);
            }
        } 
        #endregion

    }

}

