using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace YLFramework.Core.Tests
{
    internal sealed class CoreClassPoolScope
    {
        private static readonly FieldInfo Instance = typeof(ClassObjectPoolManager)
            .GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
        private readonly HashSet<object> previousObjects = new HashSet<object>();
        private readonly HashSet<LoadViewArgs> arguments = new HashSet<LoadViewArgs>();

        public CoreClassPoolScope()
        {
            var pool = Instance.GetValue(null);
            if (pool == null) return;
            var objects = (IDictionary)pool.GetType().GetField("objects", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pool);
            foreach (DictionaryEntry entry in objects)
                foreach (var value in (IList)entry.Value) previousObjects.Add(value);
        }

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
                    throw new InvalidOperationException("Cannot detach a fixture with an active startup producer.");
                // 仅故障Fixture清理反射移除自有记录；生产代码不能修改只读注册视图。
                foreach (var name in new[] { "modules", "baseModules" })
                {
                    var registry = (IDictionary)typeof(ModuleManager).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(manager);
                    if (ReferenceEquals(registry[module.ModuleName], module)) registry.Remove(module.ModuleName);
                }
                var current = typeof(ModuleManager).GetField("current_module", BindingFlags.Instance | BindingFlags.NonPublic);
                if (ReferenceEquals(current.GetValue(manager), module)) current.SetValue(manager, null);
            }
            var pool = Instance.GetValue(null);
            if (pool == null) return;
            foreach (var args in arguments) args.OnLoaded();
            var objects = (IDictionary)pool.GetType().GetField("objects", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pool);
            var times = (IDictionary)pool.GetType().GetField("unloaded_times", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pool);
            var types = new List<object>();
            foreach (DictionaryEntry entry in objects) types.Add(entry.Key);
            foreach (var type in types)
            {
                var entries = (IList)objects[type];
                for (var i = entries.Count - 1; i >= 0; i--)
                {
                    var newArgs = entries[i] is LoadViewArgs args && arguments.Contains(args) && !previousObjects.Contains(args);
                    if (!newArgs && !IsOwned(entries[i], ownedModules)) continue;
                    times.Remove(entries[i]);
                    entries.RemoveAt(i);
                }
                if (entries.Count == 0) objects.Remove(type);
            }
            // Keep the framework pool and its maintenance coroutine alive; neither belongs to an individual test.
        }

        private static bool IsOwned(object value, ICollection<Module> modules)
        {
            if (value is Module module) return modules.Contains(module);
            if (value is Model model) return model.Module != null && modules.Contains(model.Module);
            if (value is View view) return view.Module != null && modules.Contains(view.Module);
            if (value is IList list)
                foreach (var item in list) if (IsOwned(item, modules)) return true;
            return false;
        }
    }
}
