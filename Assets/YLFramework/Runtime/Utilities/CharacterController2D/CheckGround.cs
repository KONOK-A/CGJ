using UnityEngine;

namespace YLFramework.Character2D
{
    internal class CheckGround
    {
        private ContactPoint2D[] _contacts = null;

        private int _contactsCount;

        private CharacterController2D _character;

        private float _groundAngle = 35;

        private float _lastCheckTime;

        private float _lastUpdateGroundInfoTime;

        public bool IsGroundedByContacts
        {
            get; protected set;
        }

        //private RaycastHit2D raycastHit;
        //private bool check = false;

        private float _timeInterval = 1.0f / 15; // 每秒最多检测15次

        internal void Check(ContactPoint2D[] contacts, CharacterController2D characher, int count, float groundAngle)
        {
            this._contacts = contacts;
            this._character = characher;
            _contactsCount = count;
            this._groundAngle = groundAngle;

            // 检测地面

            IsGroundedByContacts = HasGroundContacts(count);

            if (IsGroundedByContacts != characher.IsGrounded)
            {
                CheckGrounded();
            }

            if (IsGroundedByContacts)
            {
                UpdateGroundInfo();
            }
        }

        private bool HasGroundContacts(int count)
        {
            bool isGrounded = false;

            float minAngle = 90;

            for (int i = 0; i < count; i++)
            {
                if (_contacts[i].collider == null)
                {
                    continue;
                }

                if (_contacts[i].collider.isTrigger)
                {
                    continue;
                }
                // 如果该游戏物体的最低点比该碰撞体的最低点小，说明该碰撞体不在该游戏物体下面
                if (_character.Bottom.bounds.min.y < _contacts[i].collider.bounds.min.y)
                {
                    continue;
                }

                float angle = Vector2.Angle(_contacts[i].normal, Vector2.up);

                if (angle < minAngle)
                {
                    minAngle = angle;
                }
            }
            if (minAngle < _groundAngle)
            {
                isGrounded = true;
            }

            return isGrounded;
        }

        private void CheckGrounded()
        {
            if (!_character.ChecksGround)
            {
                return;
            }

            if (Time.time - _lastCheckTime < _timeInterval)
            {
                return;
            }

            _lastCheckTime = Time.time;

#if UNITY_EDITOR
            _character.CheckCount++;
#endif


            Vector2 dir = -_character.Bottom.transform.up;

            int count = 0;

            if (_character.Bottom is CircleCollider2D)
            {
                count = _character.Raycast(_character.Bottom as CircleCollider2D, dir);
            }
            else if (_character.Bottom is BoxCollider2D)
            {
                count = _character.Raycast(_character.Bottom as BoxCollider2D, dir);
            }
            else if (_character.Bottom is CapsuleCollider2D)
            {
                count = _character.Raycast(_character.Bottom as CapsuleCollider2D, dir);
            }

            CaculateIsGrounded(count);
        }

        private void CaculateIsGrounded(int count)
        {
            if (_character.SpeedVertical > 0)
            {
                _character.IsGrounded = false;
                return;
            }

            bool grounded = false;

            for (int i = 0; i < count; i++)
            {
                RaycastHit2D raycast = _character.Hits[i];
                if (raycast.collider == null)
                {
                    continue;
                }

                if (raycast.collider.isTrigger)
                {
                    continue;
                }

                if (raycast.transform == _character.transform || raycast.transform.IsChildOf(_character.transform))
                {
                    continue;
                }

                float angle = Vector2.Angle(raycast.normal, Vector2.up);

                if (angle < _groundAngle)
                {
                    _character.GroundInfo = raycast;
                    grounded = true;
                    break;
                }
            }
            _character.IsGrounded = grounded;
        }

        private void UpdateGroundInfo()
        {
            if (!_character.IsGrounded)
            {
                return;
            }

            if (Time.time - _lastUpdateGroundInfoTime < 0.1f)
            {
                return;
            }

            for (int i = 0; i < _contactsCount; i++)
            {
                // 只要有一个点 和 当前法线比较接近 就不需要更新
                bool d = Vector3.Distance(_contacts[i].point, _character.GroundInfo.point) < 0.06f;
                bool a = Vector3.Angle(_contacts[i].normal, _character.GroundInfo.normal) < 3;
                // 认为法线区别不大 不需要更新
                if (d && a)
                {
                    return;
                }
            }
            // 更新碰撞点 和 法线
            _lastUpdateGroundInfoTime = Time.time;
            CheckGrounded();
        }

        public bool HasColliderBelow(ContactPoint2D[] contacts, int count)
        {

            for (int i = 0; i < count; i++)
            {
                if (contacts[i].collider == null)
                {
                    continue;
                }

                if (contacts[i].collider.isTrigger)
                {
                    continue;
                }
                // 如果该游戏物体的最低点比该碰撞体的最低点小，说明该碰撞体不在该游戏物体下面
                if (_character.Bottom.bounds.min.y < contacts[i].collider.bounds.min.y)
                {
                    continue;
                }

                float angle = Vector2.Angle(contacts[i].normal, Vector2.up);

                // 下方有碰撞体
                if (angle < _groundAngle)
                {
                    return true;
                }
            }

            return false;
        }

    }
}
