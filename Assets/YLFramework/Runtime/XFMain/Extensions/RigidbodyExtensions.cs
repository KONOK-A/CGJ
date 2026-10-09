using UnityEngine; 

namespace YLFramework.Core.Extensions
{
    public static class RigidbodyExtensions 
    {
        /// <summary>
        /// 计算当前刚体以抛物线的形式，移动到某一个位置所需要的速度
        /// </summary>
        /// <param name="rigidbody"></param>
        /// <param name="upSpeed"></param>
        /// <param name="targetPosition"></param>
        /// <returns></returns>
        public static Vector3 ParabolicMotionVelocity(this Rigidbody rigidbody, float upSpeed, Vector3 targetPosition)
        {
            float g = Mathf.Abs(Physics.gravity.y);

            float v0 = upSpeed; // 竖直向上的初速度
            float t0 = v0 / g;
            float y0 = 0.5f * g * t0 * t0;

            float y = y0 +  rigidbody.transform.position.y - targetPosition.y;

            float t = 0;
            if (y > 0)
                t = Mathf.Sqrt(2 * y / g) + t0;
            else
                t = t0 * 2;


            // 忽略y轴
            targetPosition.y = rigidbody.transform.position.y;

            float v_hor = Vector3.Distance(targetPosition,rigidbody.transform.position) / t;

            return (targetPosition - rigidbody.transform.position).normalized * v_hor + Vector3.up * upSpeed;
        }

        /// <summary>
        /// 让刚体以抛物线的形式移动到某一个位置
        /// </summary>
        /// <param name="rigidbody"></param>
        /// <param name="upSpeed"></param>
        /// <param name="targetPosition"></param>
        public static void ParabolicMotion(this Rigidbody rigidbody,float upSpeed,Vector3 targetPosition) 
        {

#if UNITY_6000_0_OR_NEWER
            rigidbody.linearVelocity = rigidbody.ParabolicMotionVelocity(upSpeed, targetPosition);
#else  
            rigidbody.velocity = rigidbody.ParabolicMotionVelocity(upSpeed, targetPosition);
#endif


        }

        /// <summary>
        /// 计算当前刚体以抛物线的形式，移动到某一个位置所需要的速度
        /// </summary>
        /// <param name="rigidbody"></param>
        /// <param name="upSpeed"></param>
        /// <param name="targetPosition"></param>
        /// <returns></returns>
        public static Vector2 ParabolicMotionVelocity(this Rigidbody2D rigidbody, float upSpeed, Vector3 targetPosition)
        {
            float g = Mathf.Abs(Physics2D.gravity.y) * rigidbody.gravityScale;

            float v0 = upSpeed; // 竖直向上的初速度
            float t0 = v0 / g;
            float y0 = 0.5f * g * t0 * t0;

            float y = y0 + rigidbody.transform.position.y - targetPosition.y;

            float t = 0;
            if (y > 0)
                t = Mathf.Sqrt(2 * y / g) + t0;
            else
                t = t0 * 2;


            float v_hor = (targetPosition.x - rigidbody.transform.position.x ) / t;
             
            return new Vector2(v_hor, v0);
        }

        /// <summary>
        /// 让刚体以抛物线的形式移动到某一个位置
        /// </summary>
        /// <param name="rigidbody"></param>
        /// <param name="upSpeed"></param>
        /// <param name="targetPosition"></param>
        public static void ParabolicMotion(this Rigidbody2D rigidbody, float upSpeed, Vector3 targetPosition)
        { 
#if UNITY_6000_0_OR_NEWER
            rigidbody.linearVelocity = rigidbody.ParabolicMotionVelocity(upSpeed, targetPosition);
#else  
            rigidbody.velocity = rigidbody.ParabolicMotionVelocity(upSpeed, targetPosition);
#endif 
        }

    }
}

