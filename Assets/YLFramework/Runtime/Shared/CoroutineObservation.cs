using System;
using System.Collections;
using UnityEngine;

namespace YLFramework.Foundation
{
    /// <summary>
    /// 借用 Coroutine 的窄观察句柄。仅主线程使用；Dispose 仅停止自己的观察 wrapper，不停止 producer。
    /// </summary>
    /// <remarks>
    /// callback 只表示 Unity 对 producer 的原生等待结束，不证明业务成功、未取消或资源已释放。
    /// producer 被停止或其 owner 销毁时是否结束等待遵循 Unity 原生行为，不补造终态通知。
    /// Unity 只允许同一 Coroutine 有一个等待者；已有等待者时观察可能未启动，须检查 IsStarted。
    /// Starter 宿主失效时不恢复观察，也不保证 callback 执行；不提供统一异步结果协议。
    /// </remarks>
    public sealed class CoroutineObservation : IDisposable
    {
        private Coroutine _wrapper;
        private Action _callback;
        private bool _ended;

        private CoroutineObservation(Action callback) => _callback = callback;

        /// <summary>
        /// Starter 是否返回了有效观察句柄；false 明确表示未取得可运行观察，不代表 producer 已结束。
        /// 此值记录启动结果，不承诺后续宿主存活。
        /// </summary>
        public bool IsStarted { get; private set; }

        /// <summary>通过 CoroutineStarter 的原 owner 路由启动观察；参数无效时不创建宿主。</summary>
        public static CoroutineObservation Observe(Coroutine producer, Action callback)
        {
            if (producer == null) throw new ArgumentNullException(nameof(producer));
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            var observation = new CoroutineObservation(callback);
            var wrapper = CoroutineStarter.Start(observation.Wait(producer));
            observation.IsStarted = wrapper != null;
            if (wrapper == null)
            {
                observation._ended = true;
                observation._callback = null;
            }
            else if (observation._ended) CoroutineStarter.Stop(wrapper);
            else observation._wrapper = wrapper;
            return observation;
        }

        private IEnumerator Wait(Coroutine producer)
        {
            yield return producer;
            if (_ended) yield break;
            _ended = true;
            _wrapper = null;
            var callback = _callback;
            _callback = null;
            callback();
        }

        /// <summary>幂等释放观察及 callback；不取消 producer，不创建或查找替代 Starter 宿主。</summary>
        public void Dispose()
        {
            if (_ended) return;
            _ended = true;
            _callback = null;
            var wrapper = _wrapper;
            _wrapper = null;
            CoroutineStarter.Stop(wrapper);
        }
    }
}
