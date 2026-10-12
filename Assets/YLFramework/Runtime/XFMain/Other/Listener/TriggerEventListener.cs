using YLFramework.Foundation;
using System; 
using UnityEngine;
using UnityEngine.Events;

namespace YLFramework.Core
{
     
    [Serializable]
    public class TriggerEvent : UnityEvent<Collider> { }
    [Serializable]
    public class TriggerEvent2D : UnityEvent<Collider2D> { }

    public class TriggerEventListener : MonoBehaviour
    {

        [SerializeField]
        private LayerMask layerMask;

        // Fix编码
        [Obsolete("已过时,使用CommonSystem中的TriggerEventListener代替!")]
        [Header("已过时,使用CommonSystem中的TriggerEventListener代替!")]
        public TriggerEvent onTriggerEnter;
        [Obsolete("已过时,使用CommonSystem中的TriggerEventListener代替!")]
        [Header("已过时,使用CommonSystem中的TriggerEventListener代替!")]
        public TriggerEvent onTriggerStay;
        [Obsolete("已过时,使用CommonSystem中的TriggerEventListener代替!")]
        [Header("已过时,使用CommonSystem中的TriggerEventListener代替!")]
        public TriggerEvent onTriggerExit;
        [Obsolete("已过时,使用CommonSystem中的TriggerEventListener代替!")]
        [Header("已过时,使用CommonSystem中的TriggerEventListener代替!")]
        public TriggerEvent2D onTriggerEnter2D;
        [Obsolete("已过时,使用CommonSystem中的TriggerEventListener代替!")]
        [Header("已过时,使用CommonSystem中的TriggerEventListener代替!")]
        public TriggerEvent2D onTriggerStay2D;
        [Obsolete("已过时,使用CommonSystem中的TriggerEventListener代替!")]
        [Header("已过时,使用CommonSystem中的TriggerEventListener代替!")]
        public TriggerEvent2D onTriggerExit2D;


        private void Reset() {
            layerMask = Physics.AllLayers;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!layerMask.Contains(other.gameObject.layer))
                return;

#pragma warning disable CS0618 // 类型或成员已过时
            onTriggerEnter?.Invoke(other);
#pragma warning restore CS0618 // 类型或成员已过时
        }

        private void OnTriggerStay(Collider other)
        {
            if (!layerMask.Contains(other.gameObject.layer))
                return;
#pragma warning disable CS0618 // 类型或成员已过时
            onTriggerStay?.Invoke(other);
#pragma warning restore CS0618 // 类型或成员已过时
        }

        private void OnTriggerExit(Collider other)
        {
            if (!layerMask.Contains(other.gameObject.layer))
                return;
#pragma warning disable CS0618 // 类型或成员已过时
            onTriggerExit?.Invoke(other);
#pragma warning restore CS0618 // 类型或成员已过时
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (!layerMask.Contains(collision.gameObject.layer))
                return;
#pragma warning disable CS0618 // 类型或成员已过时
            onTriggerEnter2D?.Invoke(collision);
#pragma warning restore CS0618 // 类型或成员已过时
        }

        private void OnTriggerStay2D(Collider2D collision)
        {
            if (!layerMask.Contains(collision.gameObject.layer))
                return;
#pragma warning disable CS0618 // 类型或成员已过时
            onTriggerStay2D?.Invoke(collision);
#pragma warning restore CS0618 // 类型或成员已过时
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            if (!layerMask.Contains(collision.gameObject.layer))
                return;
#pragma warning disable CS0618 // 类型或成员已过时
            onTriggerExit2D?.Invoke(collision);
#pragma warning restore CS0618 // 类型或成员已过时
        } 
    
    
    }
}
