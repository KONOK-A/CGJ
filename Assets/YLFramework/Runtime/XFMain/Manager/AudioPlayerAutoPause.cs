// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Manager/AudioPlayerAutoPause.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
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

