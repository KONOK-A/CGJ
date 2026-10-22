using System.Collections.Generic;
using System.Threading.Tasks;

namespace YLFramework.Samples.HybridCLR.HotUpdate
{
    public static class VersionInfo
    {
        public static string Version => "v1";
        public static int Count<T>(List<T> items) => items.Count;
        public static async Task<string> DescribeAsync()
        {
            await Task.Yield();
            return Version + "-async";
        }
    }
}
