using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using YLFramework.Core.Singleton;

namespace YLFramework.Events
{
    /// <summary>
    /// EventListener 支持的事件类型。
    /// </summary>
    public enum EventType
    {
        /// <summary>指针进入（对应 IPointerEnterHandler）。</summary>
        OnMouseEnter,
        /// <summary>指针离开（对应 IPointerExitHandler）。</summary>
        OnMouseExit,
        /// <summary>指针点击（对应 IPointerClickHandler）。</summary>
        OnClick,
        /// <summary>指针按下（对应 IPointerDownHandler）。</summary>
        OnClickDown,
        /// <summary>指针抬起（对应 IPointerUpHandler）。</summary>
        OnClickUp,
        /// <summary>拖拽中（对应 IDragHandler）。</summary>
        OnDrag,
        /// <summary>开始拖拽（对应 IBeginDragHandler）。</summary>
        OnBeginDrag,
        /// <summary>结束拖拽（对应 IEndDragHandler）。</summary>
        OnEndDrag,
        /// <summary>3D 碰撞进入。</summary>
        OnCollisionEnter,
        /// <summary>3D 碰撞保持。</summary>
        OnCollisionStay,
        /// <summary>3D 碰撞离开。</summary>
        OnCollisionExit,
        /// <summary>2D 碰撞进入。</summary>
        OnCollisionEnter2D,
        /// <summary>2D 碰撞保持。</summary>
        OnCollisionStay2D,
        /// <summary>2D 碰撞离开。</summary>
        OnCollisionExit2D,
        /// <summary>3D 触发器进入。</summary>
        OnTriggerEnter,
        /// <summary>3D 触发器保持。</summary>
        OnTriggerStay,
        /// <summary>3D 触发器离开。</summary>
        OnTriggerExit,
        /// <summary>2D 触发器进入。</summary>
        OnTriggerEnter2D,
        /// <summary>2D 触发器保持。</summary>
        OnTriggerStay2D,
        /// <summary>2D 触发器离开。</summary>
        OnTriggerExit2D,
    }

    /// <summary>
    /// 鼠标 / 拖拽事件的聚合接口，便于统一接收 EventSystem 的指针回调。
    /// </summary>
    public interface IMouseEvent : IPointerEnterHandler, IPointerExitHandler,
        IPointerClickHandler, IPointerDownHandler, IPointerUpHandler,
        IBeginDragHandler, IEndDragHandler, IDragHandler
    {
    }

    /// <summary>
    /// Unity GameObject 事件监听组件：把鼠标 / 拖拽 / 碰撞 / Trigger 回调转成 EasyEvent 分发给代码注册，
    /// 同时支持 Inspector 中配置 UnityEvent。
    /// 每个 <see cref="EventType"/> 只持有一个参数类型确定的事件实例，
    /// 同一 EventType 混用不同参数类型时后续注册会被拒绝并输出警告。
    /// 组件销毁时会回收代码注册的池化事件（不修改 Inspector 中的 UnityEvent）。
    /// </summary>
    [HideMonoScript]
    public class EventListener : MonoBehaviour, IMouseEvent
    {
        // 为了避免枚举类作为字典 Key 的性能问题（Enum 未实现 IEquatable），使用自定义比较器避免装箱
        private class EventTypeEnumComparer : Singleton<EventTypeEnumComparer>, IEqualityComparer<EventType>
        {
            public bool Equals(EventType x, EventType y)
            {
                return x == y;
            }

            public int GetHashCode(EventType obj)
            {
                return (int)obj;
            }
        }

        #region 数据结构

        // 使用单例比较器
        private readonly Dictionary<EventType, IEasyEvent> _eventInfoDict = new(EventTypeEnumComparer.Instance);

        #endregion

        #region 外部访问

        /// <summary>
        /// 注册一个指定 EventType 的事件监听，返回用于注销本次注册的句柄。
        /// 同一 EventType 只能使用一种参数类型，类型不一致时返回 null 且不注册。
        /// </summary>
        /// <typeparam name="T">事件参数类型。</typeparam>
        /// <param name="eventType">事件类型。</param>
        /// <param name="action">事件监听回调。</param>
        /// <returns>用于注销本次注册的句柄；参数类型与已有事件不一致时返回 null。</returns>
        public IUnRegister Register<T>(EventType eventType, Action<T> action)
        {
            if (_eventInfoDict.TryGetValue(eventType, out var easyEvent))
            {
                if (easyEvent is EasyEvent<T> e)
                {
                    return e.Register(action);
                }

                Debug.LogWarning(
                    $"[Events] EventListener 上的 {eventType} 已注册为 {easyEvent.GetType().Name}，" +
                    $"无法再用 {typeof(T).Name} 注册；请保持同一 EventType 使用同一参数类型。");
                return null;
            }

            var newEvent = EasyEvent<T>.Allocate();
            _eventInfoDict.TryAdd(eventType, newEvent);
            return newEvent.Register(action);
        }

        /// <summary>
        /// 注销指定 EventType 上的一个监听。
        /// </summary>
        /// <typeparam name="T">事件参数类型。</typeparam>
        /// <param name="eventType">事件类型。</param>
        /// <param name="action">要注销的监听回调。</param>
        public void UnRegister<T>(EventType eventType, Action<T> action)
        {
            if (!_eventInfoDict.TryGetValue(eventType, out var easyEvent)) return;
            // 有保存的事件则移除
            if (easyEvent is EasyEvent<T> e)
            {
                e.UnRegister(action);
            }
        }

        /// <summary>
        /// 注销指定 EventType 上的全部代码监听，并回收对应事件。
        /// </summary>
        /// <param name="eventType">事件类型。</param>
        public void UnRegister(EventType eventType)
        {
            if (!_eventInfoDict.TryGetValue(eventType, out var easyEvent)) return;
            // 有保存的事件则移除
            easyEvent.UnRegisterAll();
            easyEvent.RecycleToCache();
            _eventInfoDict.Remove(eventType);
        }

        /// <summary>
        /// 注销当前监听的所有代码事件。
        /// </summary>
        /// <param name="isUnRegisterUnityEvent">是否同时清空在编辑器面板中设置的 UnityEvent。</param>
        public void UnRegisterAll(bool isUnRegisterUnityEvent = false)
        {
            foreach (var easyEvent in _eventInfoDict.Values)
            {
                easyEvent.UnRegisterAll();
                easyEvent.RecycleToCache();
            }
            _eventInfoDict.Clear();

            if (isUnRegisterUnityEvent)
            {
                // UnityEvent.RemoveAllListeners 只能移除运行时动态添加的监听，清不掉 Inspector 配置的持久监听；
                // 需要“全部清空”时只能把字段整体置空（仅影响运行时内存，不会改写资源文件）
                pointerEnter = null;
                pointerExit = null;
                pointerClick = null;
                pointerDown = null;
                pointerUp = null;
                beginDrag = null;
                drag = null;
                endDrag = null;

                collisionEnter = null;
                collisionStay = null;
                collisionExit = null;
                collisionEnter2D = null;
                collisionStay2D = null;
                collisionExit2D = null;

                triggerEnter = null;
                triggerStay = null;
                triggerExit = null;
                triggerEnter2D = null;
                triggerStay2D = null;
                triggerExit2D = null;
            }
        }

        #endregion

        #region UnityEvent

        #region Unity鼠标事件
        [FoldoutGroup("Unity事件中心")]
        [TabGroup("Unity事件中心/Events", "鼠标事件")]
        [SerializeField, LabelText("指针进入")]
        private UnityEvent<PointerEventData> pointerEnter;
        [TabGroup("Unity事件中心/Events", "鼠标事件")]
        [SerializeField, LabelText("指针离开")]
        private UnityEvent<PointerEventData> pointerExit;
        [TabGroup("Unity事件中心/Events", "鼠标事件")]
        [SerializeField, LabelText("指针点击")]
        private UnityEvent<PointerEventData> pointerClick;
        [TabGroup("Unity事件中心/Events", "鼠标事件")]
        [SerializeField, LabelText("指针按下")]
        private UnityEvent<PointerEventData> pointerDown;
        [TabGroup("Unity事件中心/Events", "鼠标事件")]
        [SerializeField, LabelText("指针抬起")]
        private UnityEvent<PointerEventData> pointerUp;
        [TabGroup("Unity事件中心/Events", "鼠标事件")]
        [SerializeField, LabelText("开始拖拽")]
        private UnityEvent<PointerEventData> beginDrag;
        [TabGroup("Unity事件中心/Events", "鼠标事件")]
        [SerializeField, LabelText("拖拽中")]
        private UnityEvent<PointerEventData> drag;
        [TabGroup("Unity事件中心/Events", "鼠标事件")]
        [SerializeField, LabelText("结束拖拽")]
        private UnityEvent<PointerEventData> endDrag;

        #endregion

        #region Unity碰撞事件
        [TabGroup("Unity事件中心/Events", "碰撞事件")]
        [SerializeField, LabelText("碰撞进入")]
        private UnityEvent<Collision> collisionEnter;
        [TabGroup("Unity事件中心/Events", "碰撞事件")]
        [SerializeField, LabelText("碰撞保持")]
        private UnityEvent<Collision> collisionStay;
        [TabGroup("Unity事件中心/Events", "碰撞事件")]
        [SerializeField, LabelText("碰撞离开")]
        private UnityEvent<Collision> collisionExit;
        [TabGroup("Unity事件中心/Events", "碰撞事件")]
        [SerializeField, LabelText("碰撞进入2D")]
        private UnityEvent<Collision2D> collisionEnter2D;
        [TabGroup("Unity事件中心/Events", "碰撞事件")]
        [SerializeField, LabelText("碰撞保持2D")]
        private UnityEvent<Collision2D> collisionStay2D;
        [TabGroup("Unity事件中心/Events", "碰撞事件")]
        [SerializeField, LabelText("碰撞离开2D")]
        private UnityEvent<Collision2D> collisionExit2D;

        #endregion

        #region Trigger事件

        [TabGroup("Unity事件中心/Events", "Trigger事件")]
        [SerializeField, LabelText("Trigger进入")]
        private UnityEvent<Collider> triggerEnter;
        [TabGroup("Unity事件中心/Events", "Trigger事件")]
        [SerializeField, LabelText("Trigger保持")]
        private UnityEvent<Collider> triggerStay;
        [TabGroup("Unity事件中心/Events", "Trigger事件")]
        [SerializeField, LabelText("Trigger离开")]
        private UnityEvent<Collider> triggerExit;
        [TabGroup("Unity事件中心/Events", "Trigger事件")]
        [SerializeField, LabelText("Trigger进入2D")]
        private UnityEvent<Collider2D> triggerEnter2D;
        [TabGroup("Unity事件中心/Events", "Trigger事件")]
        [SerializeField, LabelText("Trigger保持2D")]
        private UnityEvent<Collider2D> triggerStay2D;
        [TabGroup("Unity事件中心/Events", "Trigger事件")]
        [SerializeField, LabelText("Trigger离开2D")]
        private UnityEvent<Collider2D> triggerExit2D;

        #endregion

        #endregion

        private void OnDestroy()
        {
            // 回收代码注册的池化事件；Inspector 中的 UnityEvent 不受影响
            UnRegisterAll();
        }

        private void TriggerEvent<T>(EventType eventType, T eventData)
        {
            if (!_eventInfoDict.TryGetValue(eventType, out var easyEvent)) return;
            if (easyEvent is EasyEvent<T> e)
            {
                e.Trigger(eventData);
            }
        }

        #region 鼠标事件

        /// <summary>指针进入时触发 OnMouseEnter 与 Inspector 事件。</summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            TriggerEvent(EventType.OnMouseEnter, eventData);
            pointerEnter?.Invoke(eventData);
        }

        /// <summary>指针离开时触发 OnMouseExit 与 Inspector 事件。</summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            TriggerEvent(EventType.OnMouseExit, eventData);
            pointerExit?.Invoke(eventData);
        }

        /// <summary>开始拖拽时触发 OnBeginDrag 与 Inspector 事件。</summary>
        public void OnBeginDrag(PointerEventData eventData)
        {
            TriggerEvent(EventType.OnBeginDrag, eventData);
            beginDrag?.Invoke(eventData);
        }

        /// <summary>拖拽中触发 OnDrag 与 Inspector 事件。</summary>
        public void OnDrag(PointerEventData eventData)
        {
            TriggerEvent(EventType.OnDrag, eventData);
            drag?.Invoke(eventData);
        }

        /// <summary>结束拖拽时触发 OnEndDrag 与 Inspector 事件。</summary>
        public void OnEndDrag(PointerEventData eventData)
        {
            TriggerEvent(EventType.OnEndDrag, eventData);
            endDrag?.Invoke(eventData);
        }

        /// <summary>指针点击时触发 OnClick 与 Inspector 事件。</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            TriggerEvent(EventType.OnClick, eventData);
            pointerClick?.Invoke(eventData);
        }

        /// <summary>指针按下时触发 OnClickDown 与 Inspector 事件。</summary>
        public void OnPointerDown(PointerEventData eventData)
        {
            TriggerEvent(EventType.OnClickDown, eventData);
            pointerDown?.Invoke(eventData);
        }

        /// <summary>指针抬起时触发 OnClickUp 与 Inspector 事件。</summary>
        public void OnPointerUp(PointerEventData eventData)
        {
            TriggerEvent(EventType.OnClickUp, eventData);
            pointerUp?.Invoke(eventData);
        }

        #endregion

        #region 碰撞事件

        private void OnCollisionEnter(Collision collision)
        {
            TriggerEvent(EventType.OnCollisionEnter, collision);
            collisionEnter?.Invoke(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            TriggerEvent(EventType.OnCollisionStay, collision);
            collisionStay?.Invoke(collision);
        }

        private void OnCollisionExit(Collision collision)
        {
            TriggerEvent(EventType.OnCollisionExit, collision);
            collisionExit?.Invoke(collision);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            TriggerEvent(EventType.OnCollisionEnter2D, collision);
            collisionEnter2D?.Invoke(collision);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            TriggerEvent(EventType.OnCollisionStay2D, collision);
            collisionStay2D?.Invoke(collision);
        }

        private void OnCollisionExit2D(Collision2D collision)
        {
            TriggerEvent(EventType.OnCollisionExit2D, collision);
            collisionExit2D?.Invoke(collision);
        }

        #endregion

        #region 触发事件

        private void OnTriggerEnter(Collider other)
        {
            TriggerEvent(EventType.OnTriggerEnter, other);
            triggerEnter?.Invoke(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TriggerEvent(EventType.OnTriggerStay, other);
            triggerStay?.Invoke(other);
        }

        private void OnTriggerExit(Collider other)
        {
            TriggerEvent(EventType.OnTriggerExit, other);
            triggerExit?.Invoke(other);
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            TriggerEvent(EventType.OnTriggerEnter2D, collision);
            triggerEnter2D?.Invoke(collision);
        }

        private void OnTriggerStay2D(Collider2D collision)
        {
            TriggerEvent(EventType.OnTriggerStay2D, collision);
            triggerStay2D?.Invoke(collision);
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            TriggerEvent(EventType.OnTriggerExit2D, collision);
            triggerExit2D?.Invoke(collision);
        }

        #endregion
    }
}
