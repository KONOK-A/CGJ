using System.IO;
using System.Text;

namespace YLFramework.StateMachine
{
    internal static class FSMStateFileWriter
    {
        public static bool TryWriteNew(string path, string content, out string error)
        {
            FileStream stream;
            try
            {
                // CreateNew同时保护尚未导入的文件及检查/写入之间的竞争，不覆盖现有源码。
                stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
            }
            catch (IOException) when (File.Exists(path))
            {
                error = $"Script file already exists: {path}";
                return false;
            }
            using (stream)
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false))) writer.Write(content);
            error = null;
            return true;
        }
    }
}
