using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using UnityEngine;

namespace YLFramework.Character2D
{
    internal class CheckWall
    {
        private ContactPoint2D[] _contacts = null;

        private CharacterController2D _character;

        private float _wallAngle = 20;

        private float _lastCheckTime;

        private float _lastUpdateWallInfoTime;

        private float _timeInterval = 1.0f / 10.0f; // 每秒最多检测30次

        private int _contactsCount;

        internal void Check(ContactPoint2D[] contacts, CharacterController2D characher, int count, float wallAngle)
        {
            this._contacts = contacts;
            this._character = characher;
            this._wallAngle = wallAngle;

            this._contactsCount = count;
            int version = characher.PhysicsVersion;

            int wallIndex = 0;

            bool isWallByContacts = IsWallByContacts(count, out wallIndex);

            // 检测墙面
            if (isWallByContacts != characher.IsTouchingWall)
            {
                CheckWallTouch();
            }

            if (!characher.IsPhysicsCurrent(version))
            {
                return;
            }

            if (isWallByContacts)
            {
                UpdateWallInfo(wallIndex);
            }
        }

        private bool IsWallByContacts(int count, out int index)
        {
            bool isWall = false;
            float minAngle = 90;
            int minIndex = 0;

            for (int i = 0; i < count; i++)
            {
                ContactPoint2D raycast = _contacts[i];
                if (raycast.collider == null)
                {
                    continue;
                }

                if (raycast.collider.isTrigger)
                {
                    continue;
                }

                if (raycast.collider is BoxCollider2D || raycast.collider is CapsuleCollider2D)
                {
                    if (Mathf.Abs(Vector2.Dot(raycast.normal, raycast.collider.transform.up)) > 0.01f)
                    {
                        continue;
                    }
                }

                float angle = Vector2.Angle(raycast.normal, raycast.normal.x < 0 ? -Vector2.right : Vector2.right);

                if (angle < minAngle)
                {
                    minAngle = angle;
                    minIndex = i;
                }
            }


            if (minAngle < _wallAngle)
            {
                isWall = true;
            }

            index = minIndex;
            return isWall;
        }

        private void UpdateWallInfo(int index)
        {
            if (!_character.IsTouchingWall)
            {
                return;
            }

            if (Time.time - _lastUpdateWallInfoTime < 0.1f)
            {
                return;
            }

            for (int i = 0; i < _contactsCount; i++)
            {
                // 只要有一个点 和 当前法线比较接近 就不需要更新
                bool d = Vector3.Distance(_contacts[i].point, _character.WallInfo.point) < 0.1f;
                bool a = Vector3.Angle(_contacts[i].normal, _character.WallInfo.normal) < 3;
                // 认为法线区别不大 不需要更新
                if (d && a)
                {
                    return;
                }
            }

            _lastUpdateWallInfoTime = Time.time;
            CheckWallTouch();
        }


        private void CheckWallTouch()
        {
            if (!_character.ChecksWalls)
            {
                return;
            }

            int version = _character.PhysicsVersion;

            if (Time.time - _lastCheckTime < _timeInterval)
            {
                return;
            }

            _lastCheckTime = Time.time;

#if UNITY_EDITOR
            _character.CheckCount++;
#endif


            Vector2 dir = _character.Right.transform.right;

            // 如果缩放值小于0 翻转方向
            if (_character.Right.transform.lossyScale.x < 0)
            {
                dir = -dir;
            }

            int count = 0;

            if (_character.Right is CircleCollider2D)
            {
                count = _character.Raycast(_character.Right as CircleCollider2D, dir);
            }
            else if (_character.Right is BoxCollider2D)
            {
                count = _character.Raycast(_character.Right as BoxCollider2D, dir);

            }
            else if (_character.Right is CapsuleCollider2D)
            {
                count = _character.Raycast(_character.Right as CapsuleCollider2D, dir);
            }

            CaculateIsWallTouch(count);

            if (!_character.IsPhysicsCurrent(version))
            {
                return;
            }

            if (_character.IsTouchingWall)
            {
                return;
            }

            dir = -_character.Left.transform.right;

            // 如果缩放值小于0 翻转方向
            if (_character.Left.transform.lossyScale.x < 0)
            {
                dir = -dir;
            }

            if (_character.Left is CircleCollider2D)
            {
                count = _character.Raycast(_character.Left as CircleCollider2D, dir);
            }
            else if (_character.Left is BoxCollider2D)
            {
                count = _character.Raycast(_character.Left as BoxCollider2D, dir);
            }
            else if (_character.Left is CapsuleCollider2D)
            {
                count = _character.Raycast(_character.Left as CapsuleCollider2D, dir);
            }

            CaculateIsWallTouch(count);
        }

        private void CaculateIsWallTouch(int count)
        {
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

                if (raycast.collider is BoxCollider2D || raycast.collider is CapsuleCollider2D)
                {
                    if (Mathf.Abs(Vector2.Dot(raycast.normal, raycast.transform.up)) > 0.01f)
                    {
                        continue;
                    }
                }

                float angle = Vector2.Angle(raycast.normal, raycast.normal.x < 0 ? -Vector2.right : Vector2.right);

                if (angle < _wallAngle)
                {
                    _character.WallInfo = raycast;
                    touching = true;
                    break;
                }
            }
            _character.IsTouchingWall = touching;


        }

    }

}
