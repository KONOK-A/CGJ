using YLFramework.Actions;
using Object = UnityEngine.Object;

namespace YLFramework.Tests.Actions
{
    internal static class ActionFixtureSession
    {
        internal static bool CanOwnUninitializedSession()
        {
            if (ActionKit.Initialized || ActionKit.IsShuttingDown ||
                Object.FindObjectOfType<MonoUpdateActionExecutor>(true) ||
                Object.FindObjectOfType<ActionGlobalHost>(true) ||
                Object.FindObjectOfType<ActionKitCurrentScene>(true) ||
                Object.FindObjectOfType<ActionRecycleDriver>(true) ||
                Object.FindObjectOfType<ScreenTransitionCanvas>(true) ||
                ActionQueue.PendingCount != 0)
                return false;
            return true;
        }
    }
}
