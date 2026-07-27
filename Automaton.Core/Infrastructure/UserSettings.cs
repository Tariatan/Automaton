using System.Configuration;

namespace Automaton.Infrastructure;

internal sealed class UserSettings : ApplicationSettingsBase
{
    private static readonly UserSettings SDefaultInstance = (UserSettings)Synchronized(new UserSettings());

    public static UserSettings Default => SDefaultInstance;

    [UserScopedSetting]
    [DefaultSettingValue("")]
    public string TelemetryRootDirectory
    {
        get => this[nameof(TelemetryRootDirectory)] as string ?? string.Empty;
        set => this[nameof(TelemetryRootDirectory)] = value;
    }

    [UserScopedSetting]
    [DefaultSettingValue("")]
    public string HallmarkRootDirectory
    {
        get => this[nameof(HallmarkRootDirectory)] as string ?? string.Empty;
        set => this[nameof(HallmarkRootDirectory)] = value;
    }

    [UserScopedSetting]
    [DefaultSettingValue("")]
    public string PilotAvatarDirectory
    {
        get => this[nameof(PilotAvatarDirectory)] as string ?? string.Empty;
        set => this[nameof(PilotAvatarDirectory)] = value;
    }
}