using Automaton.Detectors;
using Automaton.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Automaton.Infrastructure;

internal static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAutomatonServices(this IServiceCollection services)
    {
        services.AddSingleton<IScreenCaptureProvider, ScreenCaptureProvider>();
        services.AddSingleton<IAutomationInputController, AutomationInputController>();
        services.AddSingleton<IAutomationClock, SystemAutomationClock>();

        services.AddSingleton<PlayfieldDetector>();
        services.AddSingleton<PlayNowButtonLocator>();
        services.AddSingleton<KnownSampleMatcher>();
        services.AddSingleton<ErrorPopupDetector>();
        services.AddSingleton<AsteroidBeltOverviewDetector>();
        services.AddSingleton<HomeStationDetector>();
        services.AddSingleton<LocationChangeTimerDetector>();
        services.AddSingleton<InventoryDetector>();
        services.AddSingleton<DowntimeDetector>();
        services.AddSingleton<MineOverviewDetector>();
        services.AddSingleton<FirstAsteroidWithinReachDetector>();
        services.AddSingleton<MiningAsteroidDetector>();
        services.AddSingleton<MiningLaserDetector>();
        services.AddSingleton<WarOverviewDetector>();

        services.AddSingleton<SampleImageProcessor>();
        services.AddSingleton<ScreenCaptureService>();

        services.AddSingleton<ProjectDiscoveryAutomationService>();
        services.AddSingleton<MiningAutomationService>();
        services.AddTransient<MainWindow>();

        return services;
    }
}
