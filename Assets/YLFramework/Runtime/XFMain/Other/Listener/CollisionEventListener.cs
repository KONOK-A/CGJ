// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Other/Listener/CollisionEventListener.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
using YLFramework.Core.Extensions;
using System; 
using UnityEngine;
using UnityEngine.Events;


namespace YLFramework.Core
{
     
    [Serializable]
    public class CollisionEvent : UnityEvent<Collision> { }
    [Serializable]
    public class CollisionEvent2D : UnityEvent<Collision2D> { }

    public class CollisionEventListener : MonoBehaviour
    {
        [SerializeField]
        private LayerMask layerMask;
        // Fix编码  
        [Obsolete("已过时,使用CommonSystem中的CollisionEventListener代替!")]
        [Header("已过时,使用CommonSystem中的CollisionEventListener代替!")]
        public CollisionEvent onCollisionEnter;
        [Obsolete("已过时,使用CommonSystem中的CollisionEventListener代替!")]
        [Header("已过时,使用CommonSystem中的CollisionEventListener代替!")]
        public CollisionEvent onCollisionStay;
        [Obsolete("已过时,使用CommonSystem中的CollisionEventListener代替!")]
        [Header("已过时,使用CommonSystem中的CollisionEventListener代替!")]
        public CollisionEvent onCollisionExit;
        [Obsolete("已过时,使用CommonSystem中的CollisionEventListener代替!")]
        [Header("已过时,使用CommonSystem中的CollisionEventListener代替!")]
        public CollisionEvent2D onCollision2DEnter;
        [Obsolete("已过时,使用CommonSystem中的CollisionEventListener代替!")]
        [Header("已过时,使用CommonSystem中的CollisionEventListener代替!")]
        public CollisionEvent2D onCollision2DStay;
        [Obsolete("已过时,使用CommonSystem中的CollisionEventListener代替!")]
        [Header("已过时,使用CommonSystem中的CollisionEventListener代替!")]
        public CollisionEvent2D onCollision2DExit;



        private void Reset()
        {
            layerMask = Physics.AllLayers;
        }



        private void OnCollisionEnter(Collision collision)
        {
            if (!layerMask.Contains(collision.gameObject.layer))
                return;
#pragma warning disable CS0618 // 类型或成员已过时
            onCollisionEnter?.Invoke(collision);
#pragma warning restore CS0618 // 类型或成员已过时
        }


        private void OnCollisionStay(Collision collision)
        {
            if (!layerMask.Contains(collision.gameObject.layer))
                return;
#pragma warning disable CS0618 // 类型或成员已过时
            onCollisionStay?.Invoke(collision);
#pragma warning restore CS0618 // 类型或成员已过时
        }

        private void OnCollisionExit(Collision collision)
        {
            if (!layerMask.Contains(collision.gameObject.layer))
                return;
#pragma warning disable CS0618 // 类型或成员已过时
            onCollisionExit?.Invoke(collision);
#pragma warning restore CS0618 // 类型或成员已过时
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!layerMask.Contains(collision.gameObject.layer))
                return;
#pragma warning disable CS0618 // 类型或成员已过时
            onCollision2DEnter?.Invoke(collision);
#pragma warning restore CS0618 // 类型或成员已过时
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (!layerMask.Contains(collision.gameObject.layer))
                return;
#pragma warning disable CS0618 // 类型或成员已过时
            onCollision2DStay?.Invoke(collision);
#pragma warning restore CS0618 // 类型或成员已过时
        }

        private void OnCollisionExit2D(Collision2D collision)
        {
            if (!layerMask.Contains(collision.gameObject.layer))
                return;
#pragma warning disable CS0618 // 类型或成员已过时
            onCollision2DExit?.Invoke(collision);
#pragma warning restore CS0618 // 类型或成员已过时
        }
         
    }

}
