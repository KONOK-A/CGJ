using System;
using System.Collections.Generic;
using UnityEngine;
using YLFramework.EquipmentSystem;

namespace YLFramework.Tests.Extensions.Equipment
{
    public sealed class EquipmentResourceProbe : MonoBehaviour, IEquipment, IEquippable
    {
        public static bool FailLoad;
        public static EquipmentResourceProbe LastLoaded;
        public IEquippable Equiper { get; set; }
        public EquipmentConfigData EquipmentData { get; set; }
        public List<IEquipment> Equipments { get; } = new();
        public int Loads, Unloads;
        public bool IsCanEquip(IEquipment equipment) => true;
        public void OnLoaded()
        {
            Loads++;
            LastLoaded = this;
            if (!EquipmentData) throw new InvalidOperationException("Definition must precede OnLoaded.");
            if (FailLoad) throw new InvalidOperationException("probe-load-fail");
        }
        public void OnUnloaded() { Unloads++; }
        public void OnEquip() { }
        public void OnUnEquip() { }
    }
}
