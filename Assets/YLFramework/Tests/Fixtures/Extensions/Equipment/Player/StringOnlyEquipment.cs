using System;
using YLFramework.EquipmentSystem;

namespace YLFramework.Equipment.Acceptance
{
    public interface IAcceptanceState { int Loads { get; } int Unloads { get; } int State { get; set; } }
    public static class EquipmentProbeControl { public static bool FailLoad; }

    // Player 无 new/typeof 入口，只由配置字符串及应用的定向 link.xml 保留。
    public sealed class StringOnlyEquipment : IEquipment, IAcceptanceState
    {
        public IEquippable Equiper { get; set; }
        public EquipmentConfigData EquipmentData { get; set; }
        public int Loads { get; private set; }
        public int Unloads { get; private set; }
        public int State { get; set; }
        public void OnLoaded()
        {
            if (!EquipmentData || Equiper != null) throw new InvalidOperationException("Incorrect CLR initialization order.");
            Loads++; State = 0;
        }
        public void OnUnloaded() { Unloads++; State = 0; }
        public void OnEquip() { if (Equiper == null) throw new InvalidOperationException("Missing host."); }
        public void OnUnEquip() { if (Equiper == null) throw new InvalidOperationException("Host cleared before callback."); }
    }
}
