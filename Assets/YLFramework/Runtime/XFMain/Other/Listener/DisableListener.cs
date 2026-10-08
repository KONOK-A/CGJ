// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Other/Listener/DisableListener.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace YLFramework.Core
{
    internal class DisableListener : MonoBehaviour
    {
        [SerializeField]
        internal UnityEvent onDisable = new UnityEvent();
         
        private void OnDisable()
        {
            onDisable?.Invoke();
        } 
    }

}

