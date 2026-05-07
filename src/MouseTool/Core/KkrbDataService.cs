using System.Text.Json;
using MouseTool.Models;
using Npgsql;

namespace MouseTool.Core;

/// <summary>
/// 从 PostgreSQL 查询 kkrb_overview_snapshots 最新一条快照。
/// </summary>
public class KkrbDataService
{
    private static readonly JsonSerializerOptions JsonOpt = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _connectionString;

    public KkrbDataService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<KkrbSnapshot?> GetLatestAsync(CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            SELECT source_date, fetched_at,
                   password_blocks, hourly_products,
                   activity_items, material_items,
                   ammo_top10, ammo_profit_items
            FROM kkrb_overview_snapshots
            ORDER BY fetched_at DESC
            LIMIT 1";

        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var rd = await cmd.ExecuteReaderAsync(ct);

        if (!await rd.ReadAsync(ct)) return null;

        var snap = new KkrbSnapshot
        {
            SourceDate = rd["source_date"] is DateOnly d ? d.ToString("yyyy-MM-dd")
                       : rd["source_date"]?.ToString()?.Split(' ')[0] ?? "",
            FetchedAt  = rd["fetched_at"]?.ToString() ?? "",
        };

        snap.PasswordBlocks  = ParseJsonb<List<PasswordBlock>>(rd["password_blocks"])  ?? new();
        snap.HourlyProducts  = ParseJsonb<List<HourlyProduct>>(rd["hourly_products"])  ?? new();
        snap.ActivityItems   = ParseJsonb<List<ActivityItem>>(rd["activity_items"])    ?? new();
        snap.MaterialItems   = ParseJsonb<List<MaterialItem>>(rd["material_items"])    ?? new();
        snap.AmmoTop10       = ParseJsonb<List<string>>(rd["ammo_top10"])              ?? new();
        snap.AmmoProfitItems = ParseJsonb<List<AmmoProfitItem>>(rd["ammo_profit_items"]) ?? new();

        return snap;
    }

    public async Task<List<ArmorItem>> GetArmorItemsAsync(CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            SELECT armor_name, armor_level, max_durability, is_tradeable,
                   sale_threshold_broken,
                   repair_loss_percent,
                   repaired_max_durability_full_empty,
                   can_compute_sale_after_repair,
                   protected_parts_raw
            FROM public.armor_items
            ORDER BY armor_level NULLS LAST, armor_name";

        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var rd = await cmd.ExecuteReaderAsync(ct);

        var list = new List<ArmorItem>();
        while (await rd.ReadAsync(ct))
        {
            list.Add(new ArmorItem
            {
                ArmorName = rd["armor_name"]?.ToString() ?? "",
                ArmorLevel = rd["armor_level"] is DBNull || rd["armor_level"] == null
                    ? null : Convert.ToInt32(rd["armor_level"]),
                MaxDurability = ToDouble(rd["max_durability"]),
                IsTradeable = rd["is_tradeable"] is true,
                SaleConditionDamagedDurability = ToDoubleN(rd["sale_threshold_broken"]),
                RepairMaxLossPct = ToDoubleN(rd["repair_loss_percent"]),
                PostRepairMaxDurability = ToDoubleN(rd["repaired_max_durability_full_empty"]),
                CanComputeSaleAfterRepair = rd["can_compute_sale_after_repair"] is true,
                ProtectedPartsRaw = rd["protected_parts_raw"] is DBNull or null
                    ? null : rd["protected_parts_raw"].ToString(),
            });
        }
        return list;
    }

    /// <summary>
    /// 从 armor_config 表读取装备配置（仅 3-6 级，与 config.json 完全对齐）。
    /// </summary>
    public async Task<List<ArmorConfigItem>> GetArmorConfigAsync(CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            SELECT category, item_id, name, level, color,
                   init_dur, sell_thr, loss_factor,
                   eff_m1, eff_m2, eff_m3, eff_m4
            FROM public.armor_config
            WHERE level >= 3
            ORDER BY category DESC, level DESC, item_id";

        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var rd  = await cmd.ExecuteReaderAsync(ct);

        var list = new List<ArmorConfigItem>();
        while (await rd.ReadAsync(ct))
        {
            list.Add(new ArmorConfigItem
            {
                Category   = rd["category"]?.ToString() ?? "",
                ItemId     = Convert.ToInt32(rd["item_id"]),
                Name       = rd["name"]?.ToString() ?? "",
                Level      = Convert.ToInt32(rd["level"]),
                Color      = rd["color"]?.ToString() ?? "",
                InitDur    = ToDouble(rd["init_dur"]),
                SellThr    = ToDouble(rd["sell_thr"]),
                LossFactor = ToDouble(rd["loss_factor"]),
                EffM1      = ToDouble(rd["eff_m1"]),
                EffM2      = ToDouble(rd["eff_m2"]),
                EffM3      = ToDouble(rd["eff_m3"]),
                EffM4      = ToDouble(rd["eff_m4"]),
            });
        }
        return list;
    }

    // ──────────────────────────────────────────────────────────────
    // df_weapons + df_gun_codes — 改枪 Tab
    // ──────────────────────────────────────────────────────────────

    /// <summary>读取全部武器列表，按名称排序。</summary>
    public async Task<List<DfWeapon>> GetWeaponsAsync(CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = "SELECT id, key, name, href FROM df_weapons ORDER BY name";
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var rd  = await cmd.ExecuteReaderAsync(ct);

        var list = new List<DfWeapon>();
        while (await rd.ReadAsync(ct))
            list.Add(new DfWeapon
            {
                Id   = Convert.ToInt32(rd["id"]),
                Key  = rd["key"]?.ToString()  ?? "",
                Name = rd["name"]?.ToString() ?? "",
                Href = rd["href"]?.ToString() ?? "",
            });
        return list;
    }

    /// <summary>
    /// 一次性读取全部改枪码，按 weapon_key 分组、copy_count 降序排列。
    /// 62 把武器 / ~510 条，数据量极小，全量加载避免反复连库。
    /// </summary>
    public async Task<Dictionary<string, List<DfGunCode>>> GetAllGunCodesAsync(
        CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        const string sql = @"
            SELECT id, weapon_key, code, description, value, copy_count, image_url
            FROM   df_gun_codes
            ORDER  BY weapon_key, copy_count DESC NULLS LAST";

        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var rd  = await cmd.ExecuteReaderAsync(ct);

        var dict = new Dictionary<string, List<DfGunCode>>();
        while (await rd.ReadAsync(ct))
        {
            var gc = new DfGunCode
            {
                Id          = Convert.ToInt32(rd["id"]),
                WeaponKey   = rd["weapon_key"]?.ToString()   ?? "",
                Code        = rd["code"]?.ToString()         ?? "",
                Description = rd["description"]?.ToString()  ?? "",
                Value       = rd["value"]?.ToString()        ?? "",
                CopyCount   = rd["copy_count"] is DBNull or null
                                  ? 0 : Convert.ToInt32(rd["copy_count"]),
                ImageUrl    = rd["image_url"] is DBNull or null
                                  ? null : rd["image_url"].ToString(),
            };
            if (!dict.TryGetValue(gc.WeaponKey, out var sub))
                dict[gc.WeaponKey] = sub = new List<DfGunCode>();
            sub.Add(gc);
        }
        return dict;
    }

    private static double ToDouble(object? v) =>
        v == null || v is DBNull ? 0 : Convert.ToDouble(v);

    private static double? ToDoubleN(object? v) =>
        v == null || v is DBNull ? null : Convert.ToDouble(v);

    private static T? ParseJsonb<T>(object? raw)
    {
        if (raw == null || raw is DBNull) return default;
        try
        {
            return JsonSerializer.Deserialize<T>(raw.ToString()!, JsonOpt);
        }
        catch { return default; }
    }
}
