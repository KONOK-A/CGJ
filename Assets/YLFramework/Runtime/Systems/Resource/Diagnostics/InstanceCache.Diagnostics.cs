using System.Collections.Generic;
using UnityEngine;

namespace YLFramework.Resource
{
    public sealed partial class InstanceCache
    {
        internal ResourceInstanceCacheDiagnostics CaptureDiagnostics()
        {
            // 复用 Foundation 的公开只读队列快照；不 Prune Resource 条目、不释放任何 Prefab pin。
            var idleObjects = _data == null ? new HashSet<GameObject>() : new HashSet<GameObject>(_data.PoolQueue);
            var active = 0;
            var idle = 0;
            var destroying = 0;
            var dead = 0;
            var transitions = 0;
            foreach (var entry in _instances)
            {
                if (!entry.Object)
                {
                    dead++;
                }
                else if (entry.IsDestroying)
                {
                    destroying++;
                }
                else if (entry.Checkout != null)
                {
                    active++;
                }
                else if (idleObjects.Contains(entry.Object))
                {
                    idle++;
                }
                else if (_operations != 0)
                {
                    transitions++;
                }
                else
                {
                    destroying++;
                }
            }
            return new ResourceInstanceCacheDiagnostics(AssetRecord, Policy, ScopeScene.handle, IsRetired,
                active, idle, destroying, dead, transitions, _operations);
        }
    }
}
