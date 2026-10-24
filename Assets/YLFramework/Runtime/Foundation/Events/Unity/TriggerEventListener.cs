using System;
using UnityEngine;
using UnityEngine.Events;
using YLFramework.Foundation;

namespace YLFramework.Events
{

    [Serializable]
    public class TriggerEvent : UnityEvent<Collider>
    {
    }
    [Serializable]
    public class TriggerEvent2D : UnityEvent<Collider2D>
    {
    }

    [AddComponentMenu("YLFramework/Events/Trigger Event Listener")]
    public class TriggerEventListener : MonoBehaviour
    {

        [SerializeField]
        [Tooltip("过滤对方 GameObject 的层；3D/2D 共用，None 不接收任何 Trigger。")]
        private LayerMask layerMask = Physics.AllLayers;

        public TriggerEvent onTriggerEnter = new TriggerEvent();
        public TriggerEvent onTriggerStay = new TriggerEvent();
        public TriggerEvent onTriggerExit = new TriggerEvent();
        public TriggerEvent2D onTriggerEnter2D = new TriggerEvent2D();
        public TriggerEvent2D onTriggerStay2D = new TriggerEvent2D();
        public TriggerEvent2D onTriggerExit2D = new TriggerEvent2D();

        /// <summary>对方物理对象的层过滤；不影响 Unity 原生 Trigger 判定。</summary>
        public LayerMask PhysicsLayerMask
        {
            get => layerMask; set => layerMask = value;
        }


        private void Reset()
        {
            layerMask = Physics.AllLayers;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!layerMask.Contains(other.gameObject.layer))
            {
                return;
            }

            onTriggerEnter?.Invoke(other);
        }

        private void OnTriggerStay(Collider other)
        {
            if (!layerMask.Contains(other.gameObject.layer))
            {
                return;
            }

            onTriggerStay?.Invoke(other);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!layerMask.Contains(other.gameObject.layer))
            {
                return;
            }

            onTriggerExit?.Invoke(other);
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (!layerMask.Contains(collision.gameObject.layer))
            {
                return;
            }

            onTriggerEnter2D?.Invoke(collision);
        }

        private void OnTriggerStay2D(Collider2D collision)
        {
            if (!layerMask.Contains(collision.gameObject.layer))
            {
                return;
            }

            onTriggerStay2D?.Invoke(collision);
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            if (!layerMask.Contains(collision.gameObject.layer))
            {
                return;
            }

            onTriggerExit2D?.Invoke(collision);
        }


    }
}
