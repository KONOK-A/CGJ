using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace YLFramework.Character2D
{
    internal class CheckTop
    {

        private ContactPoint2D[] _contacts = null;

        private CharacterController2D _character;

        private float _topAngle = 45;

        private float _lastCheckTime;

        private float _lastUpdateTopInfoTime;

        private float _timeInterval = 1.0f / 10.0f; // 每秒最多检测10次

        private int _contactsCount;

        public bool IsTopByContact
        {
            get; set;
        }

        internal void Check(ContactPoint2D[] contacts, CharacterController2D characher, int count, float topAngle)
        {
            this._contacts = contacts;
            this._character = characher;
            this._topAngle = topAngle;
            this._contactsCount = count;
            // 检测地面

            // 检测头顶

            IsTopByContact = IsTopByContacts(count);

            if (IsTopByContact != characher.IsTouchingCeiling)
            {
                CheckTopInfo();
            }

            if (IsTopByContact)
            {
                UpdateTopInfo();
            }
        }

        private void CheckTopInfo()
        {
            if (!_character.ChecksCeiling)
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


            Vector2 dir = _character.Top.transform.up;

            int count = 0;

            if (_character.Top is CircleCollider2D)
            {
                count = _character.Raycast(_character.Top as CircleCollider2D, dir);
            }
            else if (_character.Top is BoxCollider2D)
            {
                count = _character.Raycast(_character.Top as BoxCollider2D, dir);
            }
            else if (_character.Top is CapsuleCollider2D)
            {
                count = _character.Raycast(_character.Top as CapsuleCollider2D, dir);
            }

            CalculateIsTop(count);
        }

        private void CalculateIsTop(int count)
        {
            if (_character.SpeedVertical < 0)
            {
                _character.IsTouchingCeiling = false;
                return;
            }

            bool touching = false;

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

                float angle = Vector2.Angle(raycast.normal, Vector2.down);

                if (angle < _topAngle)
                {
                    _character.TopInfo = raycast;
                    touching = true;
                    break;
                }
            }
            _character.IsTouchingCeiling = touching;

        }

        private bool IsTopByContacts(int count)
        {
            bool isTop = false;

            float minAngle = 90;
            //int min_index = 0;

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

                if (_contacts[i].collider.usedByEffector)
                {
                    Vector3 center = _contacts[i].collider.bounds.center;
                    Vector2 point = _character.Rigidbody.ClosestPoint(center);
                    if (_contacts[i].collider.bounds.Contains(point))
                    {
                        continue;
                    }
                }

                float angle = Vector2.Angle(_contacts[i].normal, Vector2.down);

                if (angle < minAngle)
                {
                    minAngle = angle;
                }
            }

            if (minAngle < _topAngle)
            {
                isTop = true;
            }

            return isTop;
        }

        private void UpdateTopInfo()
        {
            if (!_character.IsTouchingCeiling)
            {
                return;
            }

            if (Time.time - _lastUpdateTopInfoTime < 0.06f)
            {
                return;
            }

            for (int i = 0; i < _contactsCount; i++)
            {
                // 只要有一个点 和 当前法线比较接近 就不需要更新
                bool d = Vector3.Distance(_contacts[i].point, _character.TopInfo.point) < 0.1f;
                bool a = Vector3.Angle(_contacts[i].normal, _character.TopInfo.normal) < 3;
                // 认为法线区别不大 不需要更新
                if (d && a)
                {
                    return;
                }
            }

            // 更新碰撞点 和 法线
            _lastUpdateTopInfoTime = Time.time;
            CheckTopInfo();
        }

    }

}
