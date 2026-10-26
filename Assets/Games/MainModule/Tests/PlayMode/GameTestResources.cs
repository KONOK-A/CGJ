using System.Collections;
using NUnit.Framework;
using XFABManager;

internal static class GameTestResources
{
    public static IEnumerator Ready()
    {
        var request = AssetBundleManager.ReadyRes("MainModule");
        while (!request.isDone) yield return null;
        Assert.IsTrue(string.IsNullOrEmpty(request.error), request.error);
    }
}
