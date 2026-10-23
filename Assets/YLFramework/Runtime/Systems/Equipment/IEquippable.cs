using System.Collections.Generic;

namespace YLFramework.EquipmentSystem
{
    /// <summary>宿主稳定持有已初始化的列表，通过 Manager 修改；结束时显式 CloseHost。</summary>
    public interface IEquippable
    {
        List<IEquipment> Equipments
        {
            get;
        }
        bool CanEquip(IEquipment equipment);
    }
}
