using System;
using UnityEngine;
using YLFramework.EquipmentSystem;

namespace YLFramework.Equipment.Acceptance
{
    public sealed class PrefabEquipmentProbe : MonoBehaviour, IEquipment, IAcceptanceState
    {
        public IEquippable Equiper { get; set; }
        public EquipmentConfigData EquipmentData { get; set; }
        public int Loads { get; private set; }
        public int Unloads { get; private set; }
        public int State { get; set; }
        public void OnLoaded()
        {
            if (!EquipmentData || Equiper != null) throw new InvalidOperationException("Incorrect Prefab initialization order.");
            Loads++; State = 0;
            if (EquipmentProbeControl.FailLoad) { EquipmentProbeControl.FailLoad = false; throw new InvalidOperationException("EXPECTED_EQUIPMENT_LOAD_FAILURE"); }
        }
        public void OnUnloaded() { Unloads++; State = 0; }
        public void OnEquip() { if (Equiper == null) throw new InvalidOperationException("Missing host."); }
        public void OnUnEquip() { if (Equiper == null) throw new InvalidOperationException("Host cleared before callback."); }
    }
}
