// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Module/SingletonModule.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.


namespace YLFramework.Core
{
    public class SingletonModule<T> : Module where T : Module
    {

        /// <summary>
        /// 查询实际注册实例（包括Starting）；未注册或关闭后返回null，不创建对象。
        /// </summary>
        public static T Instance => ModuleManager.GetModule<T>();

        public override void OnStart()
        {
        }
    }

}
