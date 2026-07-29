using System.Text.Json;
using System.Text.Json.Serialization;

namespace Automaton.Core.Infrastructure;

internal sealed class UserSettings
{
    internal sealed class SettingsData
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? TelemetryRootBase { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? TemplatesDirectory { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? AvatarsDirectory { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? FormLocation { get; set; }
    }

    private const string SettingsFileName = "automaton.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static UserSettings Default { get; private set; } = new UserSettings("", new SettingsData());

    private readonly string m_FilePath;
    private readonly SettingsData m_Data;

    private UserSettings(string filePath, SettingsData data)
    {
        m_FilePath = filePath;
        m_Data = data;
    }

    public static void Initialize(string? filePath)
    {
        var resolvedPath = string.IsNullOrWhiteSpace(filePath)
            ? Path.Combine(Directory.GetCurrentDirectory(), SettingsFileName)
            : Path.GetFullPath(filePath);
        Default = new UserSettings(resolvedPath, TryLoad(resolvedPath));
    }

    public string TelemetryRootBase
    {
        get => m_Data.TelemetryRootBase ?? "";
        set => m_Data.TelemetryRootBase = string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public string TemplatesDirectory
    {
        get => m_Data.TemplatesDirectory ?? "";
        set => m_Data.TemplatesDirectory = string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public string PilotAvatarDirectory
    {
        get => m_Data.AvatarsDirectory ?? "";
        set => m_Data.AvatarsDirectory = string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public string FormLocation
    {
        get => m_Data.FormLocation ?? "";
        set => m_Data.FormLocation = string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public void Save()
    {
        File.WriteAllText(m_FilePath, JsonSerializer.Serialize(m_Data, JsonOptions));
    }

    private static SettingsData TryLoad(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return new SettingsData();
        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<SettingsData>(json) ?? new SettingsData();
        }
        catch
        {
            return new SettingsData();
        }
    }
}