using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine;
using YLFramework.Core.Singleton;

namespace YLFramework.Services.Resource
{
    /// <summary>
    /// 二进制序列化工具契约（基于 BinaryFormatter）。
    /// <para>
    /// 参数为空等可预期错误通过返回值表达（Serialize 返回 false、Deserialize 返回 null）并输出警告日志；
    /// 数据格式错误等异常由 BinaryFormatter 抛出，不做包装。
    /// </para>
    /// </summary>
    public interface IBinarySerializer
    {
        /// <summary>把对象序列化为二进制文件；path 为空或 obj 为 null 时返回 false（不写文件）。</summary>
        bool SerializeBinary(string path,object obj);
        /// <summary>
        /// 从流反序列化对象并返回；stream 为 null 时返回 null（警告）。
        /// 注意：本方法会接管传入的 stream 并在读取结束后关闭它。
        /// </summary>
        object DeserializeBinary(Stream stream);
        /// <summary>从文件路径反序列化；路径为空或文件不存在时返回 null（警告）。</summary>
        object DeserializeBinary(string path);
        /// <summary>从字节数组反序列化。</summary>
        object DeserializeBinary(byte[] bytes);
    }
    
    /// <summary>
    /// <see cref="IBinarySerializer"/> 的默认实现（Singleton）。
    /// </summary>
    public class BinarySerializer : Singleton<BinarySerializer>, IBinarySerializer
    {
        /// <inheritdoc />
        public bool SerializeBinary(string path, object obj)
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning("#YLFramework: SerializeBinary path is empty.");
                return false;
            }

            if (obj == null)
            {
                Debug.LogWarning("#YLFramework: SerializeBinary object is null.");
                return false;
            }

            using var fs = new FileStream(path,FileMode.OpenOrCreate);
            var bf = new BinaryFormatter();//Formatter 格式器，格式工具
            bf.Serialize(fs, obj);
            return true;
        }

        /// <inheritdoc />
        public object DeserializeBinary(Stream stream)
        {
            if (stream == null)
            {
                Debug.LogWarning("#YLFramework: DeserializeBinary stream is null.");
                return null;
            }

            using (stream)
            {
                var bf = new BinaryFormatter();
                var data = bf.Deserialize(stream);
                return data;
            }
        }

        /// <inheritdoc />
        public object DeserializeBinary(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning("#YLFramework: DeserializeBinary path is empty.");
                return null;
            }
            var fileInfo = new FileInfo(path);
            if (!fileInfo.Exists)
            {
                Debug.LogWarning("#YLFramework: DeserializeBinary path does not exist.");
                return null;
            }

            using var fs = fileInfo.OpenRead();
            var bf = new BinaryFormatter();
            var data = bf.Deserialize(fs);
            return data;
        }

        /// <inheritdoc />
        public object DeserializeBinary(byte[] bytes)
        {
            using var ms = new MemoryStream(bytes);
            IFormatter iFormatter = new BinaryFormatter();
            var obj = iFormatter.Deserialize(ms);
            return obj;
        }
    }
}