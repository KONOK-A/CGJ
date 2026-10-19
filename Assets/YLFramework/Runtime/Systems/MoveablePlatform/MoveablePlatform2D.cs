
using UnityEngine;
using System.Collections.Generic;

namespace YLFramework.Platform2D
{
    [RequireComponent(typeof(PlatformCatcher2D))]
    public class MoveablePlatform2D : Moveable
    {
        private PlatformCatcher2D catcher;
        private readonly List<RaycastHit2D> snapshot = new List<RaycastHit2D>();
        private readonly HashSet<Rigidbody2D> movedBodies = new HashSet<Rigidbody2D>();


        protected override void Awake()
        {
            catcher = GetComponent<PlatformCatcher2D>();
            base.Awake();
        }


        protected override void Move(Vector3 target)
        {
            Vector2 detal = target - transform.position;
            // 移动位置
            transform.position = target;

            // 带着游戏角色一起移动
            snapshot.Clear();
            snapshot.AddRange(catcher.Contacts);
            movedBodies.Clear();
            foreach (var item in snapshot)
            {
                if (!item.rigidbody || item.rigidbody.transform == transform || item.rigidbody.transform.IsChildOf(transform) || !movedBodies.Add(item.rigidbody)) continue;
                item.rigidbody.position += detal;
            }
            snapshot.Clear();
            movedBodies.Clear();
        }



    }

}
