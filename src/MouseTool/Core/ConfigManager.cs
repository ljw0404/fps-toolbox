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

    private MouseToolConfig _config;

    public ConfigManager()
    {
        _config = JsonConfigStore.Load<MouseToolConfig>(ConfigPath);
    }

    public MouseToolConfig Current => _config;

    public void Update(Action<MouseToolConfig> mutate)
    {
        mutate(_config);
        Save();
    }

    public void Save() => JsonConfigStore.Save(ConfigPath, _config);
}
