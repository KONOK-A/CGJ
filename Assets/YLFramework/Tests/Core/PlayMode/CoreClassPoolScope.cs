using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace YLFramework.Core.Tests
{
    internal sealed class CoreClassPoolScope
    {
        private readonly HashSet<LoadViewArgs> arguments = new HashSet<LoadViewArgs>();

        public void Track(LoadViewArgs args) => arguments.Add(args);

        public void Restore(ICollection<Module> ownedModules)
        {
            foreach (var module in ownedModules)
            {
                if (module == null) continue;
                var manager = typeof(ModuleManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                if (manager == null) continue;
                var starting = (IDictionary)typeof(ModuleManager).GetField("startingModules", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
                if (starting[module.GetType()] is StartUpModuleRequest request && ReferenceEquals(request.Module, module))
                    throw new InvalidOperationException("Cannot restore a fixture with a pending startup record.");
                // 唯一关闭管线负责注销；Fixture只能报告残留，不能摘除索引掩盖清理失败。
                if (ModuleManager.AllModules.TryGetValue(module.ModuleName, out var registered) && ReferenceEquals(registered, module) ||
                    ModuleManager.AllBaseModules.TryGetValue(module.ModuleName, out var baseModule) && ReferenceEquals(baseModule, module))
                    throw new InvalidOperationException("Fixture Module is still registered: " + module.ModuleName);
                var order = (IList)typeof(ModuleManager).GetField("registrationOrder", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
                foreach (var item in order)
                    if (ReferenceEquals(item, module))
                        throw new InvalidOperationException("Fixture Module remains in registration order: " + module.ModuleName);
            }
            foreach (var args in arguments)
            {
                foreach (var name in new[] { "leased", "owned", "reading" })
                {
                    if ((bool)typeof(LoadViewArgs).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(args))
                        throw new InvalidOperationException("Fixture LoadViewArgs remains " + name);
                }
                foreach (var name in new[] { "strings", "ints", "bools", "floats", "longs", "doubles", "objects" })
                    if (((IDictionary)typeof(LoadViewArgs).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(args)).Count != 0)
                        throw new InvalidOperationException("Fixture LoadViewArgs retains " + name);
            }
            // 只验证生产终态，不通过补归还或移除共享缓存掩盖残留。
            arguments.Clear();
        }
    }
}
