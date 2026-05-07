using System.Text.Json.Serialization;

namespace MouseTool.Models;

public class PasswordBlock
{
    [JsonPropertyName("map_name")]    public string MapName { get; set; } = "";
    [JsonPropertyName("code")]        public string Code { get; set; } = "";
    [JsonPropertyName("updated_date")]public string UpdatedDate { get; set; } = "";
}

public class HourlyProduct
{
    [JsonPropertyName("workstation")]   public string Workstation { get; set; } = "";
    [JsonPropertyName("product_name")]  public string ProductName { get; set; } = "";
    [JsonPropertyName("current_profit")]public long CurrentProfit { get; set; }
    [JsonPropertyName("ideal_price")]   public long IdealPrice { get; set; }
    [JsonPropertyName("sell_time")]     public string SellTime { get; set; } = "";
}

public class ActivityItem
{
    [JsonPropertyName("item_name")]    public string ItemName { get; set; } = "";
    [JsonPropertyName("current_price")]public long CurrentPrice { get; set; }
    [JsonPropertyName("ideal_price")]  public long IdealPrice { get; set; }
}

public class MaterialItem
{
    [JsonPropertyName("item_name")]    public string ItemName { get; set; } = "";
    [JsonPropertyName("current_price")]public long CurrentPrice { get; set; }
    [JsonPropertyName("lowest_price")] public long LowestPrice { get; set; }
    [JsonPropertyName("highest_price")]public long HighestPrice { get; set; }
    [JsonPropertyName("buy_time")]     public string BuyTime { get; set; } = "";
    [JsonPropertyName("sell_time")]    public string SellTime { get; set; } = "";
}

public class AmmoProfitItem
{
    [JsonPropertyName("ammo_name")]public string AmmoName { get; set; } = "";
    [JsonPropertyName("profit")]   public long Profit { get; set; }
}

public class KkrbSnapshot
{
    public string SourceDate { get; set; } = "";
    public string FetchedAt { get; set; } = "";
    public List<PasswordBlock> PasswordBlocks { get; set; } = new();
    public List<HourlyProduct> HourlyProducts { get; set; } = new();
    public List<ActivityItem> ActivityItems { get; set; } = new();
    public List<MaterialItem> MaterialItems { get; set; } = new();
    public List<string> AmmoTop10 { get; set; } = new();
    public List<AmmoProfitItem> AmmoProfitItems { get; set; } = new();
}
