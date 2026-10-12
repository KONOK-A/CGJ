using System;
using UnityEngine;

namespace YLFramework.Utilities.Extensions
{
    public static class RayExtensions
    {
        /// <summary>
        /// 计算两个 Ray 所在无限直线之间的非负最小距离，不限制在射线半轴。
        /// 零方向按原点处理。
        /// </summary>
        public static float MiniDistance(this Ray ray, Ray other)
        {
            Vector3 u = ray.direction, v = other.direction;
            double nx = (double)u.y * v.z - (double)u.z * v.y;
            double ny = (double)u.z * v.x - (double)u.x * v.z;
            double nz = (double)u.x * v.y - (double)u.y * v.x;
            double lengthSquared = nx * nx + ny * ny + nz * nz;
            if (lengthSquared == 0)
                return Dot(u, u) == 0 ? other.Distance(ray.origin) : ray.Distance(other.origin);
            double dx = (double)other.origin.x - ray.origin.x;
            double dy = (double)other.origin.y - ray.origin.y;
            double dz = (double)other.origin.z - ray.origin.z;
            // 距离不经过 float 最近点重建，避免近乎平行时巨大的参数放大舍入误差。
            return FiniteParameter(Math.Abs(dx * nx + dy * ny + dz * nz) / Math.Sqrt(lengthSquared));
        }

        /// <summary>计算无限直线之间最近的点；点可能在原点后方。</summary>
        public static void MiniDistancePoint(this Ray ray, Ray other, out Vector3 ray_point, out Vector3 other_point)
        {
            ray.MiniDistancePoint(other, out float first, out float second);
            ray_point = ray.GetPoint(first);
            other_point = other.GetPoint(second);
        }

        /// <summary>
        /// 计算无限直线最近点的有符号参数；负值代表原点反方向，不保证落在射线半轴。
        /// 平行时返回一组最近点，零方向按原点处理。
        /// </summary>
        public static void MiniDistancePoint(this Ray ray, Ray other, out float ray_point_distance, out float other_point_ditance)
        {
            Vector3 u = ray.direction, v = other.direction;
            Vector3 delta = other.origin - ray.origin;
            double a = Dot(u, u), c = Dot(v, v);
            double first = 0, second = 0;
            if (a == 0)
            {
                if (c != 0) second = -Dot(delta, v) / c;
            }
            else if (c == 0) first = Dot(delta, u) / a;
            else
            {
                // 用 double 叉积求分母，避免 a*c-b*b 在近乎平行时消减为零。
                double nx = (double)u.y * v.z - (double)u.z * v.y;
                double ny = (double)u.z * v.x - (double)u.x * v.z;
                double nz = (double)u.x * v.y - (double)u.y * v.x;
                double denominator = nx * nx + ny * ny + nz * nz;
                if (denominator == 0)
                {
                    first = Dot(delta, u) / a;
                    if (first < 0)
                    {
                        first = 0;
                        second = -Dot(delta, v) / c;
                    }
                }
                else
                {
                    first = (((double)delta.y * v.z - (double)delta.z * v.y) * nx
                        + ((double)delta.z * v.x - (double)delta.x * v.z) * ny
                        + ((double)delta.x * v.y - (double)delta.y * v.x) * nz) / denominator;
                    second = (((double)delta.y * u.z - (double)delta.z * u.y) * nx
                        + ((double)delta.z * u.x - (double)delta.x * u.z) * ny
                        + ((double)delta.x * u.y - (double)delta.y * u.x) * nz) / denominator;
                }
            }
            ray_point_distance = FiniteParameter(first);
            other_point_ditance = FiniteParameter(second);
        }

        /// <summary>
        /// 计算点到 Ray 所在无限直线的最短距离；保留反向延长线语义。
        /// 射线半轴距离请用 DistanceToRay，零方向按原点处理。
        /// </summary>
        public static float Distance(this Ray ray, Vector3 point)
        {
            return PointDistance(ray, point, false);
        }

        /// <summary>计算点到射线半轴的距离，投影在原点后方时取到原点的距离；零方向按原点处理。</summary>
        public static float DistanceToRay(this Ray ray, Vector3 point)
        {
            return PointDistance(ray, point, true);
        }

        private static float PointDistance(Ray ray, Vector3 point, bool halfLine)
        {
            double dx = (double)point.x - ray.origin.x;
            double dy = (double)point.y - ray.origin.y;
            double dz = (double)point.z - ray.origin.z;
            double lengthSquared = Dot(ray.direction, ray.direction);
            double parameter = lengthSquared == 0 ? 0
                : (dx * ray.direction.x + dy * ray.direction.y + dz * ray.direction.z) / lengthSquared;
            if (halfLine && parameter < 0) parameter = 0;
            dx -= parameter * ray.direction.x;
            dy -= parameter * ray.direction.y;
            dz -= parameter * ray.direction.z;
            return FiniteParameter(Math.Sqrt(dx * dx + dy * dy + dz * dz));
        }

        private static double Dot(Vector3 a, Vector3 b) =>
            (double)a.x * b.x + (double)a.y * b.y + (double)a.z * b.z;

        private static float FiniteParameter(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || Math.Abs(value) > float.MaxValue)
                throw new ArgumentException("几何参数必须可表示为有限 float。");
            return (float)value;
        }
    }
}
