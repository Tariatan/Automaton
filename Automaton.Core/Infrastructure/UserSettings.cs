using System.Configuration;

namespace Automaton.Core.Infrastructure;

internal sealed class UserSettings : ApplicationSettingsBase
{
    public static UserSettings Default { get; } = (UserSettings)Synchronized(new UserSettings());

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