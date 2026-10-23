using System.Collections.Generic;
using UnityEngine;

namespace YLFramework.Logging
{
    /// <summary>Enable时复制配置。文件输出必须显式开启；空过滤表表示全部关闭，null表示全部开启。</summary>
    public sealed class LoggerOptions
    {
        public bool WriteToFile
        {
            get; set;
        }
        public string Directory
        {
            get; set;
        }
        public Dictionary<LogType, bool> ConsoleFilter
        {
            get; set;
        }
        public Dictionary<LogType, bool> FileFilter
        {
            get; set;
        }
    }
}
