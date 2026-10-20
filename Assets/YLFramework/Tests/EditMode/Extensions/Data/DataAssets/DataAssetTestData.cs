using YLFramework.DataAssets;

namespace YLFramework.Tests.DataAssets
{
    public sealed class DataAssetTestData : ScriptableObjectData
    {
        public int amount;
    }

    internal static class DataAcceptanceOwnership
    {
        public static void Own(string path)
        {
            string output = System.Environment.GetEnvironmentVariable("YL_DATA_UNITY_OUTPUT");
            if (string.IsNullOrEmpty(output) || !System.IO.Path.GetFullPath(output).Replace('\\', '/').StartsWith("G:/Unity/MB26/", System.StringComparison.Ordinal)) return;
            var type = System.Type.GetType("YLFramework.Tests.DataBatch.DataMainAcceptance, Assembly-CSharp-Editor", true);
            type.GetMethod("OwnTestPath").Invoke(null, new object[] { path });
        }
    }
}
