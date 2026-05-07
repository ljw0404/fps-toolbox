namespace MouseTool.Models;

/// <summary>
/// 来自 armor_config 表的装备配置，字段与 config.json / calc.js 完全对齐。
/// </summary>
public class ArmorConfigItem
{
    public string Category    { get; set; } = "";   // "head" | "armor"
    public int    ItemId      { get; set; }
    public string Name        { get; set; } = "";
    public int    Level       { get; set; }
    public string Color       { get; set; } = "";   // red / gold / purple / blue
    public double InitDur     { get; set; }          // 满耐初始值
    public double SellThr     { get; set; }          // 出售门槛
    public double LossFactor  { get; set; }          // 维修损耗系数 (loss)
    public double EffM1       { get; set; }          // 自制效率
    public double EffM2       { get; set; }          // 标准效率
    public double EffM3       { get; set; }          // 精密效率
    public double EffM4       { get; set; }          // 高级效率

    public bool IsHelmet => Category == "head";
    public bool IsArmor  => Category == "armor";
}
