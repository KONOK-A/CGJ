using System;
using System.Collections.Generic;
using UnityEngine;
using YLFramework.Core.Pool;

namespace YLFramework.Core
{
    /// <summary>根的短提示资源 owner。普通池只复用实例，每次业务显示由显式 Module 纳管。</summary>
    public sealed class UITipsPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject uiTipItemPrefab;
        [SerializeField] private RectTransform leftTopTipRoot;
        [SerializeField] private RectTransform leftDownTipRoot;
        [SerializeField] private RectTransform rightTopTipRoot;
        [SerializeField] private RectTransform rightDownTipRoot;
        [SerializeField] private RectTransform middleTipRoot;
        [SerializeField] private Transform uiTipItemPool;
        private ObjectPool<UITipsItem> pool;
        private readonly Dictionary<UITipsItem, UITipHandle> active = new Dictionary<UITipsItem, UITipHandle>();
        private bool ending;
        private bool cleanupRunning;
        public int ActiveCount => active.Count;

        /// <summary>默认使用 unscaled 时间；零时长立即完成。取消与宿主退出同样可靠释放会话和调用方引用。</summary>
        public UITipHandle Show(Module owner, string content, UITipPos position = UITipPos.Middle, float duration = 2,
            bool fade = true, Color? textColor = null, Sprite sprite = null, Action<UITipHandle> completed = null)
        {
            ViewManager.EnsureOwner(owner);
            if (ending || !isActiveAndEnabled) throw new InvalidOperationException("Tips host is not active.");
            if (float.IsNaN(duration) || float.IsInfinity(duration) || duration < 0) throw new ArgumentOutOfRangeException(nameof(duration));
            var parent = position switch
            {
                UITipPos.Middle => middleTipRoot,
                UITipPos.LeftTop => leftTopTipRoot,
                UITipPos.LeftDown => leftDownTipRoot,
                UITipPos.RightTop => rightTopTipRoot,
                UITipPos.RightDown => rightDownTipRoot,
                _ => throw new ArgumentOutOfRangeException(nameof(position))
            };
            if (!parent || !uiTipItemPool || !uiTipItemPrefab) throw new InvalidOperationException("Tips prefab, position and pool root must be configured.");
            if (pool == null)
                pool = new ObjectPool<UITipsItem>(() =>
                {
                    var instance = Instantiate(uiTipItemPrefab, uiTipItemPool);
                    instance.SetActive(false);
                    var item = instance.GetComponent<UITipsItem>();
                    if (item) return item;
                    Destroy(instance);
                    throw new InvalidOperationException("Tips prefab has no formal UITipsItem.");
                }, reset: item => { if (item) item.transform.SetParent(uiTipItemPool, false); },
                    onDiscard: item => { if (item) Destroy(item.gameObject); });
            var tip = pool.Allocate();
            while (!ReferenceEquals(tip, null) && !tip) { pool.Discard(tip); tip = pool.Allocate(); }
            var handle = new UITipHandle(tip, completed);
            active.Add(tip, handle);
            try
            {
                tip.transform.SetParent(parent, false);
                var args = LoadViewArgs.New();
                var canvas = PanelManager.ResolveParentRoot(parent);
                var context = canvas ? canvas.GetComponent<UIRootContext>() : null;
                // 只借用呈现配置，不为领域提示注册窗口 key。
                args.Window = new WindowDefinition(null, uiTipItemPrefab)
                {
                    Unique = false,
                    PanelType = PanelType.Overlay,
                    Layer = context ? context.OverlayLayer : 1
                };
                args.SetObject("presenter", this);
                args.SetObject("handle", handle);
                args.SetString("content", content);
                args.SetFloat("duration", duration);
                args.SetBool("fade", fade);
                args.SetObject("textColor", textColor);
                args.SetObject("sprite", sprite);
                owner.AttachView(tip, args);
                tip.gameObject.SetActive(true);
                if (duration == 0) handle.Finish(UITipResult.Completed);
                return handle;
            }
            catch
            {
                // 初始化回滚不派发 OnClosed；业务通知抛错后已归还的项不能被旧 catch 再次丢弃。
                if (active.TryGetValue(tip, out var current) && ReferenceEquals(current, handle))
                {
                    active.Remove(tip);
                    handle.End();
                    handle.Callback = null;
                    pool.Discard(tip);
                }
                throw;
            }
        }

        internal void Return(UITipsItem tip, UITipHandle handle)
        {
            if (!active.TryGetValue(tip, out var current) || !ReferenceEquals(current, handle)) return;
            active.Remove(tip);
            if (tip && !ending) pool.Recycle(tip);
            else pool.Discard(tip);
            handle.Notify();
        }

        private void OnDisable() => End();
        private void OnDestroy() => End();
        private void OnEnable() { if (!cleanupRunning) ending = false; }
        private void End()
        {
            if (ending) return;
            ending = true;
            cleanupRunning = true;
            var errors = new List<Exception>();
            foreach (var handle in new List<UITipHandle>(active.Values))
            {
                try { handle.Finish(UITipResult.HostEnded); }
                catch (Exception error) { errors.Add(error); }
                finally
                {
                    // 宿主 OnDestroy 时 Unity fake-null 可使项跳过 Return；仍释放本宿主的原借用。
                    if (active.TryGetValue(handle.Item, out var current) && ReferenceEquals(current, handle))
                    {
                        active.Remove(handle.Item);
                        handle.End();
                        handle.Callback = null;
                        try { pool.Discard(handle.Item); }
                        catch (Exception error) { errors.Add(error); }
                    }
                }
            }
            pool?.Dispose();
            pool = null;
            cleanupRunning = false;
            if (this && isActiveAndEnabled) ending = false;
            if (errors.Count != 0) Debug.LogException(new AggregateException("Tips host cleanup failed.", errors));
        }
    }
}
