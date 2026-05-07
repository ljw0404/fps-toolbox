namespace MouseTool.Models;

/// <summary>武器基本信息（df_weapons 表）。</summary>
public class DfWeapon
{
    public int    Id   { get; set; }
    public string Key  { get; set; } = "";
    public string Name { get; set; } = "";
    public string Href { get; set; } = "";
}

/// <summary>改枪码（df_gun_codes 表）。</summary>
public class DfGunCode
{
    public int     Id          { get; set; }
    public string  WeaponKey   { get; set; } = "";
    public string  Code        { get; set; } = "";
    public string  Description { get; set; } = "";
    public string  Value       { get; set; } = "";
    public int     CopyCount   { get; set; }
    public string? ImageUrl    { get; set; }
}
