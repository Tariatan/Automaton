using System.Configuration;
using System.Windows;

namespace Automaton.Properties;

internal sealed class Settings : ApplicationSettingsBase
{
    private static readonly Settings s_DefaultInstance = (Settings)Synchronized(new Settings());

    public static Settings Default => s_DefaultInstance;

    [UserScopedSetting]
    [DefaultSettingValue("0,0")]
    [SettingsSerializeAs(SettingsSerializeAs.String)]
    public Point FormLocation
    {
        get => this[nameof(FormLocation)] is Point point ? point : new Point(0, 0);
        set => this[nameof(FormLocation)] = value;
    }

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
}
