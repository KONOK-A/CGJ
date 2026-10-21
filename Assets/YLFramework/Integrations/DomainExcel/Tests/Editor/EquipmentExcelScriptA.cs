#if UNITY_EDITOR && YL_DOMAIN_EXCEL
using YLFramework.EquipmentSystem;
namespace YLFramework.Domain.Excel.Tests
{
    public class EquipmentExcelScriptA : IEquipment
    {
        public IEquippable Equiper { get; set; }
        public EquipmentConfigData EquipmentData { get; set; }
        public void OnLoaded() { } public void OnUnloaded() { } public void OnEquip() { } public void OnUnEquip() { }
    }
}
#endif
