// Copyright (c) 2021 弦小风. Licensed under the MIT License; see LICENSE.md.
// Source: https://gitee.com/xianfengkeji/XFGameFramework.git
// Revision: 2182708aecc596f30739fa8634fe2b9b6946ddd7 (XFGameFramework 1.18.8)
// Original path: Runtime/Extensions/RayExtensions.cs
// YL adaptation: independent identities and Foundation bindings; original runtime semantics retained.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace YLFramework.Core.Extensions
{
    public static class RayExtensions  
    {


        /// <summary>
        /// 计算两个射线之间的最小距离
        /// </summary>
        /// <param name="ray"></param>
        /// <param name="other"></param>
        /// <returns></returns>

        public static float MiniDistance(this Ray ray,Ray other )
        {

            if (Mathf.Approximately( Vector3.Angle(ray.direction, other.direction),0 )) 
            {
                // 方向相同 说明射线平行
                Vector3 n = other.origin - ray.origin;
                float angle = Vector3.Angle(n,ray.direction);
                float distance = Vector3.Distance(other.origin, ray.origin);
                return distance * Mathf.Sin(angle * Mathf.Deg2Rad);
            }

            // 先求出垂直这两个射线的向量
            Vector3 normal = Vector3.Cross(ray.direction, other.direction);
              
            // 任意在两个向量上分别取一个点 计算出向量
            Vector3 m = ray.origin - other.origin;
            // 求出该向量在法线上的投影 就是两个向量之间的最小距离  
            return Vector3.Dot(m.normalized, normal.normalized ) * Vector3.Distance(ray.origin, other.origin);
        }

        /// <summary>
        /// 计算两条射线之间最近的点
        /// </summary>
        /// <param name="ray"></param>
        /// <param name="other"></param>
        /// <param name="ray_point"></param>
        /// <param name="other_point"></param>
        public static void MiniDistancePoint(this Ray ray, Ray other,out Vector3 ray_point,out Vector3 other_point)
        {
            float ray_point_distance;
            float other_point_distance;

            ray.MiniDistancePoint(other,out ray_point_distance,out other_point_distance);

            ray_point = ray.GetPoint(ray_point_distance);
            other_point = other.GetPoint(other_point_distance);
        }


        /// <summary>
        /// 计算两条射线之间最近的点到原点的距离(返回的距离如果是负的,代表该点在起点的反方向)
        /// </summary>
        /// <param name="ray"></param>
        /// <param name="other"></param>
        /// <param name="ray_point_distance"></param>
        /// <param name="other_point_ditance"></param>
        public static void MiniDistancePoint(this Ray ray, Ray other, out float ray_point_distance, out float other_point_ditance)
        {

            // 判断向量是否平行
            if (Mathf.Approximately(Vector3.Angle(ray.direction, other.direction), 0))
            {
                float angle = Vector3.Angle(other.origin - ray.origin, ray.direction);
                float distance = Vector3.Distance(other.origin, ray.origin) * Mathf.Cos(angle * Mathf.Deg2Rad);

                if (distance >= 0)
                {
                    // 方向相同 说明射线平行  
                    other_point_ditance = 0;
                    ray_point_distance = distance;
                }
                else
                {
                    // 方向相同 说明射线平行 
                    ray_point_distance = 0;
                    other_point_ditance = -distance;
                }
                return;
            }
             
            float a = Vector3.Dot(other.origin, ray.direction);
            float b = Vector3.Dot(other.direction, ray.direction);
            float c = Vector3.Dot(ray.origin, ray.direction);
            float d = Vector3.Dot(ray.direction, ray.direction);
             
            float e = Vector3.Dot(other.origin, other.direction);
            float f = Vector3.Dot(other.direction, other.direction);
            float g = Vector3.Dot(ray.origin, other.direction);
            float h = Vector3.Dot(ray.direction, other.direction);

            float y = (e * d - g * d - a * h + c * h) / (b * h - f * d);
            float x = (a + b * y - c) / d;

            ray_point_distance = x;
            other_point_ditance = y;
  

            // 推导过程

            // 假设最近的点距离射线远点的位置是:x 另外一条线的距离是:y 两个点分别为: a , b

            // a = ray.origin + ray.direction * x 

            // b = other.origin + other.direction * y

            // (a - b   ) * ray.direction = 0

            // 代入a和b的值
            // (ray.origin + ray.direction * x - other.origin - other.direction * y) * ray.direction = 0

            // ray.origin * ray.direction + ray.direction * x* ray.direction - other.origin* ray.direction - other.direction * y * ray.direction = 0

            // ray.origin * ray.direction + ray.direction * x* ray.direction = other.origin* ray.direction + other.direction * y * ray.direction

            // x = (other.origin* ray.direction + other.direction * y * ray.direction - ray.origin * ray.direction ) / ray.direction * ray.direction

            // 相同的步骤推导 (a - b   ) * other.direction = 0 

            // 然后把 x 代入 即可求的 x 和 y 的值!
             
        }


        /// <summary>
        /// 计算点到射线的最短距离
        /// </summary>
        /// <param name="ray"></param>
        /// <param name="point"></param>
        /// <returns></returns>
        public static float Distance(this Ray ray, Vector3 point) 
        { 
            float angle = Vector3.Angle( point - ray.origin, ray.direction );
            if (Mathf.Approximately(angle, 0) || Mathf.Approximately(angle,180)) return 0;
            return Vector3.Distance(ray.origin,point) * Mathf.Sin(angle * Mathf.Deg2Rad);
        }

    }
}

