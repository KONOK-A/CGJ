
using System.Collections.Generic;
using UnityEngine;

namespace YLFramework.Platform2D
{
    [RequireComponent(typeof(PlatformCatcher2D))]
    public class MoveablePlatform2D : Moveable
    {
        private PlatformCatcher2D _catcher;
        private readonly List<RaycastHit2D> _snapshot = new List<RaycastHit2D>();
        private readonly HashSet<Rigidbody2D> _movedBodies = new HashSet<Rigidbody2D>();


        protected override void Awake()
        {
            _catcher = GetComponent<PlatformCatcher2D>();
            base.Awake();
        }


        protected override void Move(Vector3 target)
        {
            Vector2 detal = target - transform.position;
            // 移动位置
            transform.position = target;

            // 带着游戏角色一起移动
            _snapshot.Clear();
            _snapshot.AddRange(_catcher.Contacts);
            _movedBodies.Clear();
            foreach (var item in _snapshot)
            {
                if (!item.rigidbody || item.rigidbody.transform == transform || item.rigidbody.transform.IsChildOf(transform) || !_movedBodies.Add(item.rigidbody))
                {
                    continue;
                }

                item.rigidbody.position += detal;
            }
            _snapshot.Clear();
            _movedBodies.Clear();
        }



    }

}
