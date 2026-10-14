using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace YLFramework.Events
{
    /// <summary>
    /// <see cref="EventListener"/> 的便捷扩展：按 EventType 注册 / 注销鼠标、碰撞与 Trigger 回调，
    /// 注册类方法统一返回 <see cref="IUnRegister"/>；注销类方法在组件不存在时为空操作，不会自动创建组件。
    /// </summary>
    public static class EventListenerExtension
    {
        #region 工具方法

        /// <summary>
        /// 注册一个事件到身上的 EventListener 组件（不存在时自动添加），返回注销句柄。
        /// </summary>
        /// <typeparam name="T">事件参数类型。</typeparam>
        /// <param name="selfComponent">目标组件。</param>
        /// <param name="eventType">事件类型。</param>
        /// <param name="action">事件监听回调。</param>
        /// <returns>用于注销本次注册的句柄。</returns>
        public static IUnRegister RegisterEvent<T>(this Component selfComponent, EventType eventType, Action<T> action)
        {
            var owner = selfComponent.gameObject;
            var listener = owner.GetComponent<EventListener>();
            if (!listener)
            {
                listener = owner.AddComponent<EventListener>();
            }
            return listener.Register(eventType, action);
        }

        /// <summary>
        /// 从身上的 EventListener 组件上注销某一个事件。
        /// </summary>
        /// <typeparam name="T">事件参数类型。</typeparam>
        /// <param name="selfComponent">目标组件。</param>
        /// <param name="eventType">事件类型。</param>
        /// <param name="action">要注销的监听回调。</param>
        public static void UnRegisterEvent<T>(this Component selfComponent, EventType eventType, Action<T> action)
        {
            var listener = selfComponent.GetComponent<EventListener>();
            if (listener != null)
            {
                listener.UnRegister(eventType, action);
            }
        }

        /// <summary>
        /// 从身上的 EventListener 组件上注销某一类事件的全部监听。
        /// </summary>
        /// <param name="selfComponent">目标组件。</param>
        /// <param name="eventType">事件类型。</param>
        public static void UnRegisterEvent(this Component selfComponent, EventType eventType)
        {
            var listener = selfComponent.GetComponent<EventListener>();
            if (listener != null)
            {
                listener.UnRegister(eventType);
            }
        }

        /// <summary>
        /// 注销身上的 EventListener 组件上的所有代码事件。
        /// </summary>
        /// <param name="selfComponent">目标组件。</param>
        /// <param name="eventType">历史参数，已忽略。</param>
        public static void RemoveAllEvent(this Component selfComponent, EventType eventType)
        {
            RemoveAllEvent(selfComponent);
        }

        /// <summary>
        /// 注销身上的 EventListener 组件上的所有代码事件。
        /// </summary>
        /// <param name="selfComponent">目标组件。</param>
        public static void RemoveAllEvent(this Component selfComponent)
        {
            var listener = selfComponent.GetComponent<EventListener>();
            if (listener != null)
            {
                listener.UnRegisterAll();
            }
        }

        #endregion

        #region 鼠标相关事件

        /// <summary>注册指针进入回调。</summary>
        public static IUnRegister OnMouseEnter(this Component com, Action<PointerEventData> action)
        {
            return RegisterEvent(com, EventType.OnMouseEnter, action);
        }

        /// <summary>注册指针离开回调。</summary>
        public static IUnRegister OnMouseExit(this Component com, Action<PointerEventData> action)
        {
            return RegisterEvent(com, EventType.OnMouseExit, action);
        }

        /// <summary>注册指针点击回调。</summary>
        public static IUnRegister OnClick(this Component com, Action<PointerEventData> action)
        {
            return RegisterEvent(com, EventType.OnClick, action);
        }

        /// <summary>注册指针按下回调。</summary>
        public static IUnRegister OnClickDown(this Component com, Action<PointerEventData> action)
        {
            return RegisterEvent(com, EventType.OnClickDown, action);
        }

        /// <summary>注册指针抬起回调。</summary>
        public static IUnRegister OnClickUp(this Component com, Action<PointerEventData> action)
        {
            return RegisterEvent(com, EventType.OnClickUp, action);
        }

        /// <summary>注册拖拽中回调。</summary>
        public static IUnRegister OnDrag(this Component com, Action<PointerEventData> action)
        {
            return RegisterEvent(com, EventType.OnDrag, action);
        }

        /// <summary>注册开始拖拽回调。</summary>
        public static IUnRegister OnBeginDrag(this Component com, Action<PointerEventData> action)
        {
            return RegisterEvent(com, EventType.OnBeginDrag, action);
        }

        /// <summary>注册结束拖拽回调。</summary>
        public static IUnRegister OnEndDrag(this Component com, Action<PointerEventData> action)
        {
            return RegisterEvent(com, EventType.OnEndDrag, action);
        }

        /// <summary>注销全部点击类回调（OnClick / OnClickDown / OnClickUp 中的一种）。</summary>
        public static void RemoveClick(this Component com)
        {
            UnRegisterEvent(com, EventType.OnClick);
        }

        /// <summary>注销全部指针按下回调。</summary>
        public static void RemoveClickDown(this Component com)
        {
            UnRegisterEvent(com, EventType.OnClickDown);
        }

        /// <summary>注销全部指针抬起回调。</summary>
        public static void RemoveClickUp(this Component com)
        {
            UnRegisterEvent(com, EventType.OnClickUp);
        }

        /// <summary>注销全部拖拽中回调。</summary>
        public static void RemoveDrag(this Component com)
        {
            UnRegisterEvent(com, EventType.OnDrag);
        }

        /// <summary>注销全部开始拖拽回调。</summary>
        public static void RemoveBeginDrag(this Component com)
        {
            UnRegisterEvent(com, EventType.OnBeginDrag);
        }

        /// <summary>注销全部结束拖拽回调。</summary>
        public static void RemoveEndDrag(this Component com)
        {
            UnRegisterEvent(com, EventType.OnEndDrag);
        }

        /// <summary>注销指定的指针点击回调。</summary>
        public static void RemoveClick(this Component com, Action<PointerEventData> action)
        {
            UnRegisterEvent(com, EventType.OnClick, action);
        }

        /// <summary>注销指定的指针按下回调。</summary>
        public static void RemoveClickDown(this Component com, Action<PointerEventData> action)
        {
            UnRegisterEvent(com, EventType.OnClickDown, action);
        }

        /// <summary>注销指定的指针抬起回调。</summary>
        public static void RemoveClickUp(this Component com, Action<PointerEventData> action)
        {
            UnRegisterEvent(com, EventType.OnClickUp, action);
        }

        /// <summary>注销指定的拖拽中回调。</summary>
        public static void RemoveDrag(this Component com, Action<PointerEventData> action)
        {
            UnRegisterEvent(com, EventType.OnDrag, action);
        }

        /// <summary>注销指定的开始拖拽回调。</summary>
        public static void RemoveBeginDrag(this Component com, Action<PointerEventData> action)
        {
            UnRegisterEvent(com, EventType.OnBeginDrag, action);
        }

        /// <summary>注销指定的结束拖拽回调。</summary>
        public static void RemoveEndDrag(this Component com, Action<PointerEventData> action)
        {
            UnRegisterEvent(com, EventType.OnEndDrag, action);
        }

        #endregion

        #region 碰撞相关事件

        /// <summary>注册 3D 碰撞进入回调。</summary>
        public static IUnRegister OnCollisionEnter(this Component com, Action<Collision> action)
        {
            return RegisterEvent(com, EventType.OnCollisionEnter, action);
        }

        /// <summary>注册 3D 碰撞保持回调。</summary>
        public static IUnRegister OnCollisionStay(this Component com, Action<Collision> action)
        {
            return RegisterEvent(com, EventType.OnCollisionStay, action);
        }

        /// <summary>注册 3D 碰撞离开回调。</summary>
        public static IUnRegister OnCollisionExit(this Component com, Action<Collision> action)
        {
            return RegisterEvent(com, EventType.OnCollisionExit, action);
        }

        /// <summary>注册 2D 碰撞进入回调。</summary>
        public static IUnRegister OnCollisionEnter2D(this Component com, Action<Collision2D> action)
        {
            return RegisterEvent(com, EventType.OnCollisionEnter2D, action);
        }

        /// <summary>注册 2D 碰撞保持回调。</summary>
        public static IUnRegister OnCollisionStay2D(this Component com, Action<Collision2D> action)
        {
            return RegisterEvent(com, EventType.OnCollisionStay2D, action);
        }

        /// <summary>注册 2D 碰撞离开回调。</summary>
        public static IUnRegister OnCollisionExit2D(this Component com, Action<Collision2D> action)
        {
            return RegisterEvent(com, EventType.OnCollisionExit2D, action);
        }

        /// <summary>注销指定的 3D 碰撞进入回调。</summary>
        public static void RemoveCollisionEnter(this Component com, Action<Collision> action)
        {
            UnRegisterEvent(com, EventType.OnCollisionEnter, action);
        }

        /// <summary>注销指定的 3D 碰撞保持回调。</summary>
        public static void RemoveCollisionStay(this Component com, Action<Collision> action)
        {
            UnRegisterEvent(com, EventType.OnCollisionStay, action);
        }

        /// <summary>注销指定的 3D 碰撞离开回调。</summary>
        public static void RemoveCollisionExit(this Component com, Action<Collision> action)
        {
            UnRegisterEvent(com, EventType.OnCollisionExit, action);
        }

        /// <summary>注销指定的 2D 碰撞进入回调。</summary>
        public static void RemoveCollisionEnter2D(this Component com, Action<Collision2D> action)
        {
            UnRegisterEvent(com, EventType.OnCollisionEnter2D, action);
        }

        /// <summary>注销指定的 2D 碰撞保持回调。</summary>
        public static void RemoveCollisionStay2D(this Component com, Action<Collision2D> action)
        {
            UnRegisterEvent(com, EventType.OnCollisionStay2D, action);
        }

        /// <summary>注销指定的 2D 碰撞离开回调。</summary>
        public static void RemoveCollisionExit2D(this Component com, Action<Collision2D> action)
        {
            UnRegisterEvent(com, EventType.OnCollisionExit2D, action);
        }

        /// <summary>注销全部 3D 碰撞进入回调。</summary>
        public static void RemoveCollisionEnter(this Component com)
        {
            UnRegisterEvent(com, EventType.OnCollisionEnter);
        }

        /// <summary>注销全部 3D 碰撞保持回调。</summary>
        public static void RemoveCollisionStay(this Component com)
        {
            UnRegisterEvent(com, EventType.OnCollisionStay);
        }

        /// <summary>注销全部 3D 碰撞离开回调。</summary>
        public static void RemoveCollisionExit(this Component com)
        {
            UnRegisterEvent(com, EventType.OnCollisionExit);
        }

        /// <summary>注销全部 2D 碰撞进入回调。</summary>
        public static void RemoveCollisionEnter2D(this Component com)
        {
            UnRegisterEvent(com, EventType.OnCollisionEnter2D);
        }

        /// <summary>注销全部 2D 碰撞保持回调。</summary>
        public static void RemoveCollisionStay2D(this Component com)
        {
            UnRegisterEvent(com, EventType.OnCollisionStay2D);
        }

        /// <summary>注销全部 2D 碰撞离开回调。</summary>
        public static void RemoveCollisionExit2D(this Component com)
        {
            UnRegisterEvent(com, EventType.OnCollisionExit2D);
        }

        #endregion

        #region 触发相关事件

        /// <summary>注册 3D Trigger 进入回调。</summary>
        public static IUnRegister OnTriggerEnter(this Component com, Action<Collider> action)
        {
            return RegisterEvent(com, EventType.OnTriggerEnter, action);
        }

        /// <summary>注册 3D Trigger 保持回调。</summary>
        public static IUnRegister OnTriggerStay(this Component com, Action<Collider> action)
        {
            return RegisterEvent(com, EventType.OnTriggerStay, action);
        }

        /// <summary>注册 3D Trigger 离开回调。</summary>
        public static IUnRegister OnTriggerExit(this Component com, Action<Collider> action)
        {
            return RegisterEvent(com, EventType.OnTriggerExit, action);
        }

        /// <summary>注册 2D Trigger 进入回调。</summary>
        public static IUnRegister OnTriggerEnter2D(this Component com, Action<Collider2D> action)
        {
            return RegisterEvent(com, EventType.OnTriggerEnter2D, action);
        }

        /// <summary>注册 2D Trigger 保持回调。</summary>
        public static IUnRegister OnTriggerStay2D(this Component com, Action<Collider2D> action)
        {
            return RegisterEvent(com, EventType.OnTriggerStay2D, action);
        }

        /// <summary>注册 2D Trigger 离开回调。</summary>
        public static IUnRegister OnTriggerExit2D(this Component com, Action<Collider2D> action)
        {
            return RegisterEvent(com, EventType.OnTriggerExit2D, action);
        }

        /// <summary>注销指定的 3D Trigger 进入回调。</summary>
        public static void RemoveTriggerEnter(this Component com, Action<Collider> action)
        {
            UnRegisterEvent(com, EventType.OnTriggerEnter, action);
        }

        /// <summary>注销指定的 3D Trigger 保持回调。</summary>
        public static void RemoveTriggerStay(this Component com, Action<Collider> action)
        {
            UnRegisterEvent(com, EventType.OnTriggerStay, action);
        }

        /// <summary>注销指定的 3D Trigger 离开回调。</summary>
        public static void RemoveTriggerExit(this Component com, Action<Collider> action)
        {
            UnRegisterEvent(com, EventType.OnTriggerExit, action);
        }

        /// <summary>注销指定的 2D Trigger 进入回调。</summary>
        public static void RemoveTriggerEnter2D(this Component com, Action<Collider2D> action)
        {
            UnRegisterEvent(com, EventType.OnTriggerEnter2D, action);
        }

        /// <summary>注销指定的 2D Trigger 保持回调。</summary>
        public static void RemoveTriggerStay2D(this Component com, Action<Collider2D> action)
        {
            UnRegisterEvent(com, EventType.OnTriggerStay2D, action);
        }

        /// <summary>注销指定的 2D Trigger 离开回调。</summary>
        public static void RemoveTriggerExit2D(this Component com, Action<Collider2D> action)
        {
            UnRegisterEvent(com, EventType.OnTriggerExit2D, action);
        }

        /// <summary>注销全部 3D Trigger 进入回调。</summary>
        public static void RemoveTriggerEnter(this Component com)
        {
            UnRegisterEvent(com, EventType.OnTriggerEnter);
        }

        /// <summary>注销全部 3D Trigger 保持回调。</summary>
        public static void RemoveTriggerStay(this Component com)
        {
            UnRegisterEvent(com, EventType.OnTriggerStay);
        }

        /// <summary>注销全部 3D Trigger 离开回调。</summary>
        public static void RemoveTriggerExit(this Component com)
        {
            UnRegisterEvent(com, EventType.OnTriggerExit);
        }

        /// <summary>注销全部 2D Trigger 进入回调。</summary>
        public static void RemoveTriggerEnter2D(this Component com)
        {
            UnRegisterEvent(com, EventType.OnTriggerEnter2D);
        }

        /// <summary>注销全部 2D Trigger 保持回调。</summary>
        public static void RemoveTriggerStay2D(this Component com)
        {
            UnRegisterEvent(com, EventType.OnTriggerStay2D);
        }

        /// <summary>注销全部 2D Trigger 离开回调。</summary>
        public static void RemoveTriggerExit2D(this Component com)
        {
            UnRegisterEvent(com, EventType.OnTriggerExit2D);
        }

        #endregion
    }
}
