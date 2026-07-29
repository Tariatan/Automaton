using System.Text.Json;

namespace Automaton.Core.Infrastructure;

internal sealed class UserSettings
{
    internal sealed class SettingsData
    {
        public string TelemetryRootBase { get; set; } = "";
        public string TemplatesDirectory { get; set; } = "";
        public string PilotAvatarDirectory { get; set; } = "";
        public string FormLocation { get; set; } = "";
    }

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
        var resolvedPath = string.IsNullOrWhiteSpace(filePath) ? "" : Path.GetFullPath(filePath);
        Default = new UserSettings(resolvedPath, TryLoad(resolvedPath));
    }

    public string TelemetryRootBase
    {
        get => m_Data.TelemetryRootBase;
        set => m_Data.TelemetryRootBase = value;
    }

    public string TemplatesDirectory
    {
        get => m_Data.TemplatesDirectory;
        set => m_Data.TemplatesDirectory = value;
    }

    public string PilotAvatarDirectory
    {
        get => m_Data.PilotAvatarDirectory;
        set => m_Data.PilotAvatarDirectory = value;
    }

    public string FormLocation
    {
        get => m_Data.FormLocation;
        set => m_Data.FormLocation = value;
    }

    public void Save()
    {
        if (string.IsNullOrEmpty(m_FilePath))
        {
            return;
        }

        File.WriteAllText(m_FilePath, JsonSerializer.Serialize(m_Data, JsonOptions));
    }

    private static SettingsData TryLoad(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            return new SettingsData();
        }

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