namespace MouseTool.Models;

public class ArmorItem
{
    public string ArmorName { get; set; } = "";
    public int? ArmorLevel { get; set; }
    public double MaxDurability { get; set; }
    public bool IsTradeable { get; set; }
    public double? SaleConditionDamagedDurability { get; set; }
    public double? RepairMaxLossPct { get; set; }
    public double? PostRepairMaxDurability { get; set; }
    public bool CanComputeSaleAfterRepair { get; set; }
    public string? ProtectedPartsRaw { get; set; }

    // 派生属性
    public bool IsHelmet => ProtectedPartsRaw?.Contains("头") == true;
    public bool IsArmor  => ProtectedPartsRaw?.Contains("胸") == true;
}
