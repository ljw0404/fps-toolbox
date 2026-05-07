using FPSToolbox.Shared;
using FPSToolbox.Shared.Config;
using FPSToolbox.Shared.Ipc;
using MouseTool.Models;

namespace MouseTool.Core;

/// <summary>
/// 鼠鼠工具配置的读写入口。配置文件位于 %AppData%\FPSToolbox\MouseTool\config.json。
/// </summary>
public class ConfigManager
{
    private static readonly string ConfigPath = PathService.GetToolConfigPath(ToolIds.MouseTool);

    /// <summary>
    /// 需要自动迁移到新连接串的旧连接串特征（Neon 旧域名 / 旧密码）。
    /// 检测到任意一条时，自动覆盖为当前默认值并落盘，方便用户静默升级。
    /// </summary>
    private static readonly string[] LegacyConnStrMarkers =
    [
        "ep-cold-art-anybipxb-pooler.c-6.us-east-1.aws.neon.tech", // 旧 Neon 云 DB
        "Password=admin123",                                          // 旧本地 DB 密码
    ];

    private MouseToolConfig _config;

    public ConfigManager()
    {
        _config = JsonConfigStore.Load<MouseToolConfig>(ConfigPath);
        MigrateLegacyConnectionString();
    }

    /// <summary>
    /// 升级路径：发现配置里包含任意旧连接串特征时，自动覆盖为当前默认连接串并落盘。
    /// </summary>
    private void MigrateLegacyConnectionString()
    {
        if (string.IsNullOrEmpty(_config.DbConnectionString)) return;
        var needsMigration = LegacyConnStrMarkers.Any(marker =>
            _config.DbConnectionString.Contains(marker, StringComparison.OrdinalIgnoreCase));
        if (!needsMigration) return;

        _config.DbConnectionString = new MouseToolConfig().DbConnectionString;
        Save();
    }

    public MouseToolConfig Current => _config;

    public void Update(Action<MouseToolConfig> mutate)
    {
        mutate(_config);
        Save();
    }

    public void Save() => JsonConfigStore.Save(ConfigPath, _config);
}
