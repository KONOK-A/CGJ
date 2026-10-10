using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using YLFramework.Events;
using UpdateManager = YLFramework.Foundation.UpdateManager;

namespace YLFramework.Core.Tests
{
    // 仅清理本 fixture 首次创建、且已无公共责任的宿主，不能接管先前对象或其他订阅。
    internal sealed class CoreUpdateHostScope
    {
        private readonly HashSet<int> previous = new();
        private static readonly FieldInfo[] Channels =
        {
            typeof(UpdateManager).GetField("_update", BindingFlags.Instance | BindingFlags.NonPublic),
            typeof(UpdateManager).GetField("_fixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic),
            typeof(UpdateManager).GetField("_lateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)
        };

        internal CoreUpdateHostScope()
        {
            foreach (var host in FindHosts()) previous.Add(host.GetInstanceID());
        }

        internal void ReleaseOwnedIdleHosts()
        {
            foreach (var host in FindHosts())
            {
                if (previous.Contains(host.GetInstanceID())) continue;
                var idle = true;
                foreach (var field in Channels)
                    if (((EventChannel)field.GetValue(host)).HasListener) { idle = false; break; }
                if (idle) Object.Destroy(host.gameObject);
            }
        }

        private static UpdateManager[] FindHosts() => Object.FindObjectsByType<UpdateManager>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
    }
}
