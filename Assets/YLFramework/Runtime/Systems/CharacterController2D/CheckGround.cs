using UnityEngine;

namespace YLFramework.Character.Controller2D
{
    public class CheckGround
    {
        private ContactPoint2D[] contacts = null;

        private int contactsCount;

        private CharacterController2D characher;

        private float groundAngle = 35;

        private float lastCheckTime;

        private float lastUpdateGroundInfoTime;

        public bool IsGroundByContacts { get; protected set; }

        //private RaycastHit2D raycastHit;
        //private bool check = false;

        private float timeInterval = 1.0f / 15; // 每秒最多检测15次

        internal void Check(ContactPoint2D[] contacts, CharacterController2D characher,int count,float groundAngle)
        {
            this.contacts = contacts;
            this.characher = characher;
            contactsCount = count;
            this.groundAngle = groundAngle;

            // 检测地面

            IsGroundByContacts = IsGroundedByContacts(count);

            if (IsGroundByContacts != characher.IsGrounded)
                CheckGrounded();

            if(IsGroundByContacts)
                UpdateGroundInfo();

        }

        private bool IsGroundedByContacts(int count )
        {
            bool isGrounded = false;

            float min_angle = 90;

            for (int i = 0; i < count; i++)
            {
                if (contacts[i].collider == null)
                    continue;

                if (contacts[i].collider.isTrigger)
                    continue;
                // 如果该游戏物体的最低点比该碰撞体的最低点小，说明该碰撞体不在该游戏物体下面
                if (characher.bottom.bounds.min.y < contacts[i].collider.bounds.min.y)
                    continue;

                float angle = Vector2.Angle(contacts[i].normal, Vector2.up);

                if (angle < min_angle)
                {
                    min_angle = angle;
                }
            }
            if (min_angle < groundAngle)
            {
                isGrounded = true;
            }

            return isGrounded;
        }

        private void CheckGrounded()
        {
            if (!characher.IsCheckGround)
                return;

            if (Time.time - lastCheckTime < timeInterval)
                return;

            lastCheckTime = Time.time;

#if UNITY_EDITOR
            characher.checkCount++;
#endif


            Vector2 dir = -characher.bottom.transform.up;

            int count = 0;

            if (characher.bottom is CircleCollider2D)
            {
                count = characher.Raycast(characher.bottom as CircleCollider2D, dir);
            }
            else if (characher.bottom is BoxCollider2D)
            {
                count = characher.Raycast(characher.bottom as BoxCollider2D, dir);
            }
            else if (characher.bottom is CapsuleCollider2D)
            {
                count = characher.Raycast(characher.bottom as CapsuleCollider2D, dir);
            }

            CaculateIsGrounded(count);
        }

        private void CaculateIsGrounded(int count)
        {
            if (characher.speedVertical > 0)
            {
                characher.IsGrounded = false;
                return;
            }

            bool grounded = false;

            for (int i = 0; i < count; i++)
            {
                RaycastHit2D raycast = characher.hits[i];
                if (raycast.collider == null)
                    continue;
                if (raycast.collider.isTrigger)
                    continue;

                if (raycast.transform == characher.transform || raycast.transform.IsChildOf(characher.transform))
                    continue;

                float angle = Vector2.Angle(raycast.normal, Vector2.up);

                if (angle < groundAngle)
                {
                    characher.GroundInfo = raycast;
                    grounded = true;
                    break;
                }
            }
            characher.IsGrounded = grounded;
        }

        private void UpdateGroundInfo()
        {
            if (!characher.IsGrounded)
                return;

            if (Time.time - lastUpdateGroundInfoTime < 0.1f)
                return;

            for (int i = 0; i < contactsCount; i++)
            {
                // 只要有一个点 和 当前法线比较接近 就不需要更新
                bool d = Vector3.Distance(contacts[i].point, characher.GroundInfo.point) < 0.06f;
                bool a = Vector3.Angle(contacts[i].normal, characher.GroundInfo.normal) < 3;
                // 认为法线区别不大 不需要更新
                if (d && a)
                    return;
            }
            // 更新碰撞点 和 法线
            lastUpdateGroundInfoTime = Time.time;
            CheckGrounded();
        }

        public bool IsHaveColliderOnBelow(ContactPoint2D[] contacts , int count ) {

            for (int i = 0; i < count; i++)
            {
                if (contacts[i].collider == null)
                    continue;

                if (contacts[i].collider.isTrigger)
                    continue;
                // 如果该游戏物体的最低点比该碰撞体的最低点小，说明该碰撞体不在该游戏物体下面
                if (characher.bottom.bounds.min.y < contacts[i].collider.bounds.min.y)
                    continue;

                float angle = Vector2.Angle(contacts[i].normal, Vector2.up);

                // 下方有碰撞体
                if (angle < groundAngle)
                    return true;
            }

            return false;
        }

    }
}
