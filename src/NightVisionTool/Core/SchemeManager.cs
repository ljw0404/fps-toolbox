using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using FPSToolbox.Shared;
using FPSToolbox.Shared.Config;
using FPSToolbox.Shared.Ipc;
using NightVisionTool.Models;

namespace NightVisionTool.Core;

/// <summary>
/// 夜视方案管理。每个方案一个 json 文件,存在 %AppData%\FPSToolbox\NightVisionTool\presets\。
/// </summary>
public class SchemeManager
{
    private static readonly string PresetsDir = PathService.GetToolPresetsDir(ToolIds.NightVisionTool);
    private static readonly string ConfigPath = PathService.GetToolConfigPath(ToolIds.NightVisionTool);

    private static readonly JsonSerializerOptions JsonOpt = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public NightVisionToolConfig LoadConfig() => JsonConfigStore.Load<NightVisionToolConfig>(ConfigPath);
    public void SaveConfig(NightVisionToolConfig cfg) => JsonConfigStore.Save(ConfigPath, cfg);

    public List<NightVisionScheme> LoadAllSchemes()
    {
        EnsureBuiltInSchemes();
        var list = new List<NightVisionScheme>();
        if (!Directory.Exists(PresetsDir)) return list;
        foreach (var file in Directory.GetFiles(PresetsDir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var scheme = JsonSerializer.Deserialize<NightVisionScheme>(json, JsonOpt);
                if (scheme != null) list.Add(scheme);
            }
            catch { }
        }
        return list;
    }

    private void EnsureBuiltInSchemes()
    {
        Directory.CreateDirectory(PresetsDir);
        if (Directory.GetFiles(PresetsDir, "*.json").Length > 0) return;
        foreach (var s in BuiltInSchemes.All()) Save(s);
    }

    public void Save(NightVisionScheme scheme)
    {
        Directory.CreateDirectory(PresetsDir);
        var path = Path.Combine(PresetsDir,
            $"{JsonConfigStore.SanitizeFileName(scheme.Name)}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(scheme, JsonOpt));
    }

    public void Delete(string name)
    {
        var path = Path.Combine(PresetsDir,
            $"{JsonConfigStore.SanitizeFileName(name)}.json");
        if (File.Exists(path)) File.Delete(path);
    }
}

public class NightVisionToolConfig
{
    public string? LastSchemeName { get; set; }
    /// <summary>启动时自动应用上次的方案(默认 false —— 避免用户开机屏幕直接变形)。</summary>
    public bool ApplyOnStart { get; set; } = false;
    /// <summary>开关夜视的全局热键。默认 F9,避开 Crosshair 的 F8。</summary>
    public string? ToggleHotkey { get; set; } = "F9";
}

public static class BuiltInSchemes
{
    public const string Generic = "通用夜战";
    public const string DeltaNight = "三角洲-夜战";

    public static IEnumerable<NightVisionScheme> All()
    {
        yield return new NightVisionScheme
        {
            Name = Generic,
            ApplyToAllMonitors = true,
            Config = new NightVisionConfig
            {
                Strength = 0.60,
                HighlightProtect = 0.50,
            },
        };
        yield return new NightVisionScheme
        {
            Name = DeltaNight,
            ApplyToAllMonitors = true,
            Config = new NightVisionConfig
            {
                Strength = 0.75,
                HighlightProtect = 0.65,
            },
        };
    }
}
