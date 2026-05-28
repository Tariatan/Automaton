using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.CommonAutomationStates;
using Automaton.ProjectDiscoveryStates;
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
        services.AddSingleton<PlayNowButtonDetector>();
        services.AddSingleton<KnownSampleMatcher>();
        services.AddSingleton<MaxSubmissionsPopupDetector>();
        services.AddSingleton<SlowDownPopupDetector>();
        services.AddSingleton<ConnectionLostPopupDetector>();
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

        services.AddSingleton<CommonStartGameState>();
        services.AddSingleton<CommonExitState>();
        services.AddSingleton<ConnectionLostPopupRecoveryBehavior>();
        services.AddTransient<StartingGameState>();
        services.AddTransient<LoginState>();
        services.AddTransient<DiscoverState>();
        services.AddTransient<RecoveryState>();
        services.AddTransient<RecoverSlowDownPopupState>();
        services.AddTransient<RecoverConnectionLostPopupState>();
        services.AddTransient<RecoverMaxSubmissionsPopupState>();
        services.AddSingleton<IDiscoveryAutomationStateFactory, DiscoveryAutomationStateFactory>();

        services.AddSingleton<ProjectDiscoveryAutomationService>();
        services.AddSingleton<MiningAutomationService>();
        services.AddTransient<MainWindow>();

        return services;
    }
}
