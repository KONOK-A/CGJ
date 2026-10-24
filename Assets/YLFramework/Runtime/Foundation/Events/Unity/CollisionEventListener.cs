using System;
using UnityEngine;
using UnityEngine.Events;
using YLFramework.Foundation;


namespace YLFramework.Events
{

    [Serializable]
    public class CollisionEvent : UnityEvent<Collision>
    {
    }
    [Serializable]
    public class CollisionEvent2D : UnityEvent<Collision2D>
    {
    }

    [AddComponentMenu("YLFramework/Events/Collision Event Listener")]
    public class CollisionEventListener : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("过滤对方 GameObject 的层；3D/2D 共用，None 不接收任何碰撞。")]
        private LayerMask layerMask = Physics.AllLayers;
        public CollisionEvent onCollisionEnter = new CollisionEvent();
        public CollisionEvent onCollisionStay = new CollisionEvent();
        public CollisionEvent onCollisionExit = new CollisionEvent();
        public CollisionEvent2D onCollision2DEnter = new CollisionEvent2D();
        public CollisionEvent2D onCollision2DStay = new CollisionEvent2D();
        public CollisionEvent2D onCollision2DExit = new CollisionEvent2D();

        /// <summary>对方物理对象的层过滤；不影响 Unity 原生碰撞判定。</summary>
        public LayerMask PhysicsLayerMask
        {
            get => layerMask; set => layerMask = value;
        }



        private void Reset()
        {
            layerMask = Physics.AllLayers;
        }



        private void OnCollisionEnter(Collision collision)
        {
            if (!layerMask.Contains(collision.gameObject.layer))
            {
                return;
            }

            onCollisionEnter?.Invoke(collision);
        }


        private void OnCollisionStay(Collision collision)
        {
            if (!layerMask.Contains(collision.gameObject.layer))
            {
                return;
            }

            onCollisionStay?.Invoke(collision);
        }

        private void OnCollisionExit(Collision collision)
        {
            if (!layerMask.Contains(collision.gameObject.layer))
            {
                return;
            }

            onCollisionExit?.Invoke(collision);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!layerMask.Contains(collision.gameObject.layer))
            {
                return;
            }

            onCollision2DEnter?.Invoke(collision);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (!layerMask.Contains(collision.gameObject.layer))
            {
                return;
            }

            onCollision2DStay?.Invoke(collision);
        }

        private void OnCollisionExit2D(Collision2D collision)
        {
            if (!layerMask.Contains(collision.gameObject.layer))
            {
                return;
            }

            onCollision2DExit?.Invoke(collision);
        }

    }

}
