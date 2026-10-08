// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Module/SingletonModule.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YLFramework.Core.RoslynAnalyzers;


namespace YLFramework.Core
{
    public class SingletonModule<T> : Module where T : Module
    {

        public static T Instance;

        [MustCallBase]
        public override void OnStart()
        {
            Instance = this as T;
        }
    }

}

