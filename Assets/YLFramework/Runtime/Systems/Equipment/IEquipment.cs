namespace YLFramework.EquipmentSystem
{
    public enum EquipState
    {
        Success = 0,
        CannotEquip = 1,
        AlreadyEquipped = 2
    }

    /// <summary>单件装备。定义只读共享；实例状态由实现类维护，归还后不得继续使用旧引用。</summary>
    public interface IEquipment
    {
        IEquippable Equipper
        {
            get; set;
        }
        EquipmentConfigData EquipmentData
        {
            get; set;
        }
        /// <summary>自有实例取得后调用，此时本次 EquipmentData 已设置。</summary>
        void OnLoaded();
        /// <summary>自有实例归还前清理本次状态；外部借用实例不触发。</summary>
        void OnUnloaded();
        void OnEquip();
        void OnUnequip();
    }
}
