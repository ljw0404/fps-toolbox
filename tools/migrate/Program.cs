using Npgsql;

const string connStr = "Host=103.65.39.210;Port=55432;Database=sanjiaozhou;Username=postgres;Password=yrx7ItgPEIwdyM5MPlo0vBe0JXjYTVL0;SSL Mode=Disable";

await using var conn = new NpgsqlConnection(connStr);
await conn.OpenAsync();
Console.WriteLine("Connected.");

// 建表
var createSql = """
    CREATE TABLE IF NOT EXISTS public.armor_config (
        id          SERIAL PRIMARY KEY,
        category    VARCHAR(10)  NOT NULL,
        item_id     INT          NOT NULL,
        name        VARCHAR(100) NOT NULL,
        level       INT          NOT NULL,
        color       VARCHAR(20)  NOT NULL,
        init_dur    NUMERIC      NOT NULL,
        sell_thr    NUMERIC      NOT NULL,
        loss_factor NUMERIC      NOT NULL,
        eff_m1      NUMERIC      NOT NULL,
        eff_m2      NUMERIC      NOT NULL,
        eff_m3      NUMERIC      NOT NULL,
        eff_m4      NUMERIC      NOT NULL
    )
    """;
await new NpgsqlCommand(createSql, conn).ExecuteNonQueryAsync();
Console.WriteLine("Table created.");

// 清空旧数据（重跑幂等）
await new NpgsqlCommand("TRUNCATE public.armor_config RESTART IDENTITY", conn).ExecuteNonQueryAsync();

// 插入数据（来自 config.json，仅 level 3-6）
var rows = new (string cat, int id, string name, int lv, string color, double init, double sell, double loss, double m1, double m2, double m3, double m4)[]
{
    // ── head ──
    ("head",  0, "GT5 指挥官头盔",  6, "red",    60, 279, 0.07, 0.03, 0.05, 0.08, 0.56),
    ("head",  1, "DICH-9重型头盔",  6, "red",    50, 279, 0.10, 0.03, 0.05, 0.08, 0.65),
    ("head",  2, "H70精英头盔",     6, "red",    55,  38, 0.16, 0.04, 0.05, 0.09, 0.96),
    ("head",  3, "GN重型夜视头盔",  5, "gold",   50,  35, 0.07, 0.05, 0.08, 0.64, 1.19),
    ("head",  4, "GN 重型头盔",     5, "gold",   50,  35, 0.07, 0.05, 0.08, 0.64, 1.19),
    ("head",  5, "DICH-1战术头盔",  5, "gold",   40,  28, 0.16, 0.05, 0.07, 0.45, 0.86),
    ("head",  6, "H09 防暴头盔",    5, "gold",   45,  31, 0.16, 0.06, 0.09, 0.79, 1.51),
    ("head",  7, "Mask-1铁壁头盔",  5, "gold",   75,  52, 0.07, 0.11, 0.17, 1.42, 2.68),
    ("head",  8, "GT1 战术头盔",    4, "purple", 48,  33, 0.10, 0.12, 0.88, 1.49, 2.88),
    ("head",  9, "DICH 训练头盔",   4, "purple", 35,  24, 0.15, 0.11, 0.61, 1.03, 1.98),
    ("head", 10, "MHS 战术头盔",    4, "purple", 30,  21, 0.12, 0.12, 0.80, 1.32, 2.64),
    ("head", 11, "D6战术头盔",      4, "purple", 55,  38, 0.08, 0.21, 1.49, 2.53, 4.60),
    ("head", 12, "MC201防弹头盔",   3, "blue",   34,  23, 0.13, 1.29, 1.97, 3.29, 7.40),
    ("head", 13, "DAS防弹头盔",     3, "blue",   40,  28, 0.09, 1.46, 2.28, 3.64, 7.28),
    ("head", 14, "H07战术头盔",     3, "blue",   20,  14, 0.10, 1.20, 1.80, 3.00, 6.00),
    ("head", 15, "防暴头盔",        3, "blue",   28,  19, 0.06, 1.65, 2.39, 4.39, 8.77),
    // ── armor ──
    ("armor",  0, "泰坦防弹装甲",    6, "red",    150, 279, 0.09, 0.10, 0.17, 0.26, 0.90),
    ("armor",  1, "特里克MAS2.0装甲",6, "red",    125, 279, 0.06, 0.10, 0.17, 0.27, 0.59),
    ("armor",  2, "HA-2重型防弹衣",  6, "red",    115,  80, 0.11, 0.12, 0.19, 0.30, 0.59),
    ("armor",  3, "金刚防弹衣",      6, "red",    140, 279, 0.15, 0.10, 0.17, 0.27, 0.82),
    ("armor",  4, "重型突击背心",    5, "gold",   125,  87, 0.10, 0.15, 0.25, 1.25, 1.91),
    ("armor",  5, "FS复合防弹衣",    5, "gold",   105,  73, 0.09, 0.18, 0.29, 0.82, 1.23),
    ("armor",  6, "Hvk-2防弹衣",     5, "gold",   115,  80, 0.14, 0.16, 0.27, 1.45, 2.20),
    ("armor",  7, "精英防弹背心",    5, "gold",    95,  66, 0.12, 0.19, 0.32, 1.19, 1.82),
    ("armor",  8, "HMP特勤防弹衣",   4, "purple",  80,  56, 0.10, 0.20, 1.36, 2.12, 3.27),
    ("armor",  9, "MK-2战术背心",    4, "purple", 110,  77, 0.09, 0.41, 1.43, 2.22, 3.34),
    ("armor", 10, "DT-AVS防弹衣",    4, "purple", 100,  70, 0.10, 0.50, 1.27, 2.00, 3.00),
    ("armor", 11, "突击手防弹背心",  4, "purple",  90,  62, 0.18, 0.24, 1.19, 1.89, 2.84),
    ("armor", 12, "武士防弹背心",    4, "purple",  80,  56, 0.06, 0.50, 1.88, 3.01, 4.42),
    ("armor", 13, "射手战术背心",    3, "blue",    85,  59, 0.07, 1.58, 2.14, 3.29, 4.94),
    ("armor", 14, "TG-H防弹衣",      3, "blue",    75,  53, 0.09, 1.95, 2.63, 4.27, 6.21),
    ("armor", 15, "Hvk快拆防弹衣",   3, "blue",    60,  42, 0.04, 1.60, 2.13, 3.39, 5.24),
    ("armor", 16, "制式防弹背心",    3, "blue",    50,  35, 0.05, 1.58, 2.50, 3.96, 5.94),
};

const string insertSql = """
    INSERT INTO public.armor_config
        (category, item_id, name, level, color, init_dur, sell_thr, loss_factor, eff_m1, eff_m2, eff_m3, eff_m4)
    VALUES
        (@cat, @id, @name, @lv, @color, @init, @sell, @loss, @m1, @m2, @m3, @m4)
    """;

int count = 0;
foreach (var r in rows)
{
    var cmd = new NpgsqlCommand(insertSql, conn);
    cmd.Parameters.AddWithValue("cat",   r.cat);
    cmd.Parameters.AddWithValue("id",    r.id);
    cmd.Parameters.AddWithValue("name",  r.name);
    cmd.Parameters.AddWithValue("lv",    r.lv);
    cmd.Parameters.AddWithValue("color", r.color);
    cmd.Parameters.AddWithValue("init",  r.init);
    cmd.Parameters.AddWithValue("sell",  r.sell);
    cmd.Parameters.AddWithValue("loss",  r.loss);
    cmd.Parameters.AddWithValue("m1",    r.m1);
    cmd.Parameters.AddWithValue("m2",    r.m2);
    cmd.Parameters.AddWithValue("m3",    r.m3);
    cmd.Parameters.AddWithValue("m4",    r.m4);
    await cmd.ExecuteNonQueryAsync();
    count++;
}

Console.WriteLine($"Inserted {count} rows.");

// 验证
await using var rd = await new NpgsqlCommand("SELECT COUNT(*) FROM public.armor_config", conn).ExecuteReaderAsync();
await rd.ReadAsync();
Console.WriteLine($"Total rows in armor_config: {rd[0]}");
