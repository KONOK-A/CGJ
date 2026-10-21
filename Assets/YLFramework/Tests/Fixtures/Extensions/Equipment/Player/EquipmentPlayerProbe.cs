using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using YLFramework.EquipmentSystem;
using YLFramework.Services.Resource;
using ResourceLocation = YLFramework.Services.Resource.ResourceLocation;

namespace YLFramework.Equipment.Acceptance
{
    public sealed class EquipmentPlayerProbe : MonoBehaviour
    {
        public string Prefix;
        [Serializable] private sealed class Result
        {
            public string run, backend, unity, error, window;
            public bool passed, quitObserved, development, graphical;
            public int width, height;
            public string[] checks;
        }
        private sealed class Host : IEquippable
        {
            public List<IEquipment> Equipments { get; } = new();
            public bool Allow = true;
            public bool IsCanEquip(IEquipment equipment) => Allow;
        }
        private sealed class Borrowed : IEquipment
        {
            public IEquippable Equiper { get; set; }
            public EquipmentConfigData EquipmentData { get; set; }
            public int Unloads;
            public void OnLoaded() => throw new InvalidOperationException("Borrowed object must not be acquired.");
            public void OnUnloaded() => Unloads++;
            public void OnEquip() { } public void OnUnEquip() { }
        }
        [DllImport("user32.dll")] private static extern IntPtr GetActiveWindow();
        private readonly List<string> checks = new();
        private Result result;
        private string output;
        private static string Argument(string key)
        {
            var args = Environment.GetCommandLineArgs(); var index = Array.IndexOf(args, key);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
        private void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException("Equipment Player contract: " + name);
            checks.Add(name);
        }
        private IEnumerator Start()
        {
            output = Argument("--eq-result");
            result = new Result { run = Argument("--eq-run"), unity = Application.unityVersion, development = Debug.isDebugBuild,
#if ENABLE_IL2CPP
                backend = "IL2CPP"
#else
                backend = "Mono"
#endif
            };
            Application.quitting += Quitting;
            var run = Run();
            while (true)
            {
                bool next;
                try { next = run.MoveNext(); }
                catch (Exception error)
                {
                    result.error = error.ToString(); result.checks = checks.ToArray(); Write();
                    Debug.LogError("YL_EQUIPMENT_PLAYER_FAIL " + result.run + " " + error);
                    Application.Quit(1); yield break;
                }
                if (!next) break;
                yield return run.Current;
            }
            result.passed = true; result.checks = checks.ToArray(); Write();
            Debug.Log("YL_EQUIPMENT_PLAYER_PASS run=" + result.run + "; backend=" + result.backend + "; checks=" + checks.Count);
            Application.Quit(0);
        }
        private IEnumerator Run()
        {
            Check(!Application.isEditor && !Debug.isDebugBuild && !string.IsNullOrEmpty(output) && !string.IsNullOrEmpty(result.run), "real-non-development-player");
            for (var i = 0; i < 12; i++) yield return null;
            result.window = GetActiveWindow().ToInt64().ToString("X");
            result.width = Screen.width; result.height = Screen.height;
            result.graphical = SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null && !Application.isBatchMode;
            Check(result.graphical && result.window != "0" && Screen.width > 0 && Screen.height > 0, "real-window-and-graphics");
            Check(ResourceSystem.Current == null, "no-foreign-resource-authority");
            var configAddress = new ResourceAddress("equipment-acceptance", "config");
            var prefabAddress = new ResourceAddress("equipment-acceptance", "prefab");
            var entries = new[]
            {
                new ResourceCatalogEntry("config", configAddress, new ResourceLocation(ResourceSourceId.Resources, Prefix + "Root", typeof(EquipmentsConfig))),
                new ResourceCatalogEntry("prefab", prefabAddress, new ResourceLocation(ResourceSourceId.Resources, Prefix + "Prefab", typeof(GameObject)))
            };
            using var init = ResourceSystem.InitializeAsync(new ResourceConfiguration(new[] { new ResourceCatalog("equipment-acceptance", entries) }));
            while (!init.IsDone) yield return null;
            var resources = init.TakeResult();
            var independent = resources.Acquire<EquipmentsConfig>(configAddress);
            EquipmentManager.LoadConfig(resources, configAddress); EquipmentManager.LoadConfig(resources, configAddress);
            var host = new Host(); var other = new Host();
            var first = EquipmentManager.CreateEquipment(1001);
            Check(first.GetType().FullName == "YLFramework.Equipment.Acceptance.StringOnlyEquipment" && ((ProbeEquipmentData)first.EquipmentData).bonus == 42, "string-only-type-and-derived-definition");
            ((IAcceptanceState)first).State = 99; EquipmentManager.DestroyEquipment(first);
            var reused = EquipmentManager.CreateEquipment(1001);
            Check(ReferenceEquals(first, reused) && ((IAcceptanceState)reused).State == 0 && ((IAcceptanceState)reused).Loads == 2, "clr-cache-reset");
            Check(EquipmentManager.Equip(host, reused) == EquipState.Success && EquipmentManager.Equip(other, reused) == EquipState.AlreadyEquipped, "exclusive-instance-owner");
            Check(EquipmentManager.CreateAndEquip(host, 1001) != null && host.Equipments.Count == 2, "same-id-distinct-instances");
            other.Allow = false; Check(EquipmentManager.CreateAndEquip(other, 1001) == null && other.Equipments.Count == 0, "rejected-create-is-returned");
            var prefab = EquipmentManager.CreateAndEquip(host, 1002);
            ((IAcceptanceState)prefab).State = 77; EquipmentManager.DestroyEquipment(prefab);
            var prefabReused = EquipmentManager.CreateEquipment(1002);
            Check(ReferenceEquals(prefab, prefabReused) && ((IAcceptanceState)prefabReused).State == 0, "prefab-cache-reset");
            EquipmentManager.DestroyEquipment(prefabReused); EquipmentProbeControl.FailLoad = true;
            var failed = false;
            try { EquipmentManager.CreateEquipment(1002); }
            catch (InvalidOperationException error) { failed = error.Message == "EXPECTED_EQUIPMENT_LOAD_FAILURE"; }
            yield return null;
            Check(failed && resources.GetInstanceCache(prefabAddress).CachedCount == 0, "failed-prefab-discarded");
            var borrowed = new Borrowed { EquipmentData = independent.Asset.Equipments[0] };
            Check(EquipmentManager.Equip(host, borrowed) == EquipState.Success, "borrowed-attachment");
            var detached = EquipmentManager.CreateEquipment(1001);
            EquipmentManager.CreateAndEquip(host, 1002);
            EquipmentManager.CloseHost(host); EquipmentManager.Shutdown();
            Check(host.Equipments.Count == 0 && other.Equipments.Count == 0 && borrowed.Equiper == null && borrowed.Unloads == 0, "host-close-does-not-own-borrower");
            Check(detached.EquipmentData == null && ((IAcceptanceState)detached).Unloads > 0 && EquipmentManager.GetConfig(independent.Asset.name) == null, "shutdown-returns-detached-and-config");
            Check(independent.Asset && ReferenceEquals(ResourceSystem.Current, resources), "independent-root-lease-preserved");
            independent.Dispose(); resources.ClearInstanceCaches();
            using var shutdown = resources.ShutdownAsync();
            while (!shutdown.IsDone) yield return null;
            Check(shutdown.Status == ResourceRequestStatus.Succeeded && resources.GetDiagnosticsSnapshot().RecordCount == 0, "owner-release-zero-resource-records");
        }
        private void Write()
        {
            if (string.IsNullOrEmpty(output)) return;
            File.WriteAllText(output, JsonUtility.ToJson(result, true));
        }
        private void Quitting() { result.quitObserved = true; Write(); }
    }
}
