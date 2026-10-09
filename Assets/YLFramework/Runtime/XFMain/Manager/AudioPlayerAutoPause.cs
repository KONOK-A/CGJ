using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace YLFramework.Core
{

    /// <summary>
    /// 自动暂停音频播放的组件,如果 Time.timeScale 为0时，暂停音频播放
    /// </summary>
    [RequireComponent(typeof(AudioPlayer))]
    [RequireComponent(typeof(AudioSource))]
    public class AudioPlayerAutoPause : MonoBehaviour
    {


        private AudioPlayer audioPlayer;

        private AudioSource audioSource;

        private void Awake()
        {
            audioPlayer = GetComponent<AudioPlayer>();
            audioSource = GetComponent<AudioSource>();
        }

        private void Update()
        {
            if (Mathf.Abs(Time.timeScale) > 0.01f)
            {
                if (!audioSource.isPlaying)
                { 
                    audioPlayer.Pause(false);
                }

            }
            else 
            { 
                if (audioSource.isPlaying)
                {
                    audioPlayer.Pause(true);
                } 
            } 
        }

    }
}

