using System;
using UnityEngine;

namespace YLFramework.Services.Save
{
    /// <summary>
    /// <see cref="Vector3"/> 的可序列化替身：以三个 float 字段存储，便于存档 JSON 或 Unity 序列化读写三维向量。
    /// 支持与 <see cref="Vector3"/> / <see cref="Vector3Int"/> 的隐式互转。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="Vector3Int"/> 互转时按 float 存储整数分量；转向 <see cref="Vector3Int"/> 使用 <c>(int)</c> 截断（向零取整），非整数分量会丢失小数。
    /// </remarks>
    [Serializable]
    public class SerializedVector3
    {
        /// <summary>X / Y / Z 分量。</summary>
        public float x, y, z;

        /// <summary>
        /// 按分量创建向量。
        /// </summary>
        /// <param name="x">X 分量。</param>
        /// <param name="y">Y 分量。</param>
        /// <param name="z">Z 分量。</param>
        public SerializedVector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        /// <summary>
        /// 返回 <c>(x, y, z)</c> 形式的字符串，便于调试输出。
        /// </summary>
        public override string ToString()
        {
            return $"({x}, {y}, {z})";
        }

        /// <summary>从 <see cref="Vector3"/> 隐式转换（逐分量复制）。</summary>
        public static implicit operator SerializedVector3(Vector3 vector3)
        {
            return new SerializedVector3(vector3.x, vector3.y, vector3.z);
        }

        /// <summary>隐式转换回 <see cref="Vector3"/>（逐分量复制）。</summary>
        public static implicit operator Vector3(SerializedVector3 vector3)
        {
            return new Vector3(vector3.x, vector3.y, vector3.z);
        }

        /// <summary>从 <see cref="Vector3Int"/> 隐式转换（整数分量按 float 存储，值不变）。</summary>
        public static implicit operator SerializedVector3(Vector3Int vector3)
        {
            return new SerializedVector3(vector3.x, vector3.y, vector3.z);
        }

        /// <summary>隐式转换为 <see cref="Vector3Int"/>（<c>(int)</c> 截断取整，非整数分量丢失小数）。</summary>
        public static implicit operator Vector3Int(SerializedVector3 vector3)
        {
            return new Vector3Int((int)vector3.x, (int)vector3.y, (int)vector3.z);
        }
    }
    
    /// <summary>
    /// <see cref="Vector2"/> 的可序列化替身：以两个 float 字段存储，便于存档 JSON 或 Unity 序列化读写二维向量。
    /// 支持与 <see cref="Vector2"/> / <see cref="Vector2Int"/> 的隐式互转。
    /// </summary>
    /// <remarks>
    /// 与 <see cref="Vector2Int"/> 互转时按 float 存储整数分量；转向 <see cref="Vector2Int"/> 使用 <c>(int)</c> 截断（向零取整），非整数分量会丢失小数。
    /// </remarks>
    [Serializable]
    public struct SerializedVector2
    {
        /// <summary>X / Y 分量。</summary>
        public float x, y;

        /// <summary>
        /// 按分量创建向量。
        /// </summary>
        /// <param name="x">X 分量。</param>
        /// <param name="y">Y 分量。</param>
        public SerializedVector2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }

        /// <summary>
        /// 返回 <c>(x,y)</c> 形式的字符串，便于调试输出。
        /// </summary>
        public override string ToString()
        {
            return $"({x},{y})";
        }

        /// <summary>从 <see cref="Vector2"/> 隐式转换（逐分量复制）。</summary>
        public static implicit operator SerializedVector2(Vector2 vector2)
        {
            return new SerializedVector2(vector2.x, vector2.y);
        }

        /// <summary>隐式转换回 <see cref="Vector2"/>（逐分量复制）。</summary>
        public static implicit operator Vector2(SerializedVector2 vector2)
        {
            return new Vector2(vector2.x, vector2.y);
        }

        /// <summary>从 <see cref="Vector2Int"/> 隐式转换（整数分量按 float 存储，值不变）。</summary>
        public static implicit operator SerializedVector2(Vector2Int vector2)
        {
            return new SerializedVector2(vector2.x, vector2.y);
        }

        /// <summary>隐式转换为 <see cref="Vector2Int"/>（<c>(int)</c> 截断取整，非整数分量丢失小数）。</summary>
        public static implicit operator Vector2Int(SerializedVector2 vector2)
        {
            return new Vector2Int((int)vector2.x, (int)vector2.y);
        }
    }

    /// <summary>
    /// 序列化向量类型与 Unity 向量类型的显式转换扩展方法；
    /// 与各类型上的隐式转换运算符等价，提供可读性更强的调用形式。
    /// </summary>
    public static class SerializedVectorExtensions
    {
        /// <summary>将 <see cref="SerializedVector3"/> 转换回 <see cref="Vector3"/>。</summary>
        /// <param name="sv3">源向量。</param>
        /// <returns>等值的 <see cref="Vector3"/>。</returns>
        public static Vector3 ConvertToVector3(this SerializedVector3 sv3)
        {
            return new Vector3(sv3.x, sv3.y, sv3.z);
        }

        /// <summary>将 <see cref="Vector3"/> 转换为 <see cref="SerializedVector3"/>。</summary>
        /// <param name="v3">源向量。</param>
        /// <returns>等值的 <see cref="SerializedVector3"/>。</returns>
        public static SerializedVector3 ConvertToSVector3(this Vector3 v3)
        {
            return new SerializedVector3(v3.x, v3.y, v3.z);
        }

        /// <summary>将 <see cref="SerializedVector3"/> 转换为 <see cref="Vector3Int"/>（<c>(int)</c> 截断取整）。</summary>
        /// <param name="sv3">源向量。</param>
        /// <returns>截断取整后的 <see cref="Vector3Int"/>。</returns>
        public static Vector3Int ConvertToVector3Int(this SerializedVector3 sv3)
        {
            return new Vector3Int((int)sv3.x, (int)sv3.y, (int)sv3.z);
        }

        /// <summary>
        /// 将 <see cref="Vector3Int"/> 转换为 <see cref="SerializedVector3"/>。
        /// 注意：方法名中的 S 表示“序列化替身”，此处替身类型为 <see cref="SerializedVector3"/>（而非不存在的 SerializedVector3Int）。
        /// </summary>
        /// <param name="v3">源向量。</param>
        /// <returns>等值的 <see cref="SerializedVector3"/>。</returns>
        public static SerializedVector3 ConvertToSVector3Int(this Vector3Int v3)
        {
            return new SerializedVector3(v3.x, v3.y, v3.z);
        }

        /// <summary>
        /// 将 <see cref="SerializedVector2"/> 转换回 <see cref="Vector2"/>。
        /// 注意：本重载方向为 SerializedVector2 → Vector2，与同名重载 <see cref="ConvertToSVector2(Vector2)"/> 的命名方向相反。
        /// </summary>
        /// <param name="sv2">源向量。</param>
        /// <returns>等值的 <see cref="Vector2"/>。</returns>
        public static Vector2 ConvertToSVector2(this SerializedVector2 sv2)
        {
            return new Vector2(sv2.x, sv2.y);
        }

        /// <summary>将 <see cref="SerializedVector2"/> 转换为 <see cref="Vector2Int"/>（<c>(int)</c> 截断取整）。</summary>
        /// <param name="sv2">源向量。</param>
        /// <returns>截断取整后的 <see cref="Vector2Int"/>。</returns>
        public static Vector2Int ConvertToVector2Int(this SerializedVector2 sv2)
        {
            return new Vector2Int((int)sv2.x, (int)sv2.y);
        }

        /// <summary>将 <see cref="Vector2"/> 转换为 <see cref="SerializedVector2"/>。</summary>
        /// <param name="v2">源向量。</param>
        /// <returns>等值的 <see cref="SerializedVector2"/>。</returns>
        public static SerializedVector2 ConvertToSVector2(this Vector2 v2)
        {
            return new SerializedVector2(v2.x, v2.y);
        }

        /// <summary>将 <see cref="Vector2Int"/> 转换为 <see cref="SerializedVector2"/>。</summary>
        /// <param name="v2">源向量。</param>
        /// <returns>等值的 <see cref="SerializedVector2"/>。</returns>
        public static SerializedVector2 ConvertToSVector2(this Vector2Int v2)
        {
            return new SerializedVector2(v2.x, v2.y);
        }
    }
}