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
        services.AddSingleton<ClickTraceRecorder>();
        services.AddSingleton<IAutomationInputController, AutomationInputController>();
        services.AddSingleton<IGameActionService, GameActionService>();
        services.AddSingleton<IAutomationClock, SystemAutomationClock>();

        services.AddSingleton<PlayfieldDetector>();
        services.AddSingleton<PlayNowButtonDetector>();
        services.AddSingleton<ClientIsRunningButtonDetector>();
        services.AddSingleton<KnownSampleMatcher>();
        services.AddSingleton<MaxSubmissionsPopupDetector>();
        services.AddSingleton<SlowDownPopupDetector>();
        services.AddSingleton<ConnectionLostPopupDetector>();
        services.AddSingleton<AccuracyDetector>();
        services.AddSingleton<AsteroidBeltOverviewDetector>();
        services.AddSingleton<LocationChangeTimerDetector>();
        services.AddSingleton<InventoryDetector>();
        services.AddSingleton<DowntimeDetector>();
        services.AddSingleton<MineOverviewDetector>();
        services.AddSingleton<FirstAsteroidWithinReachDetector>();
        services.AddSingleton<MiningAsteroidDetector>();
        services.AddSingleton<MiningLaserDetector>();
        services.AddSingleton<WarOverviewDetector>();
        services.AddSingleton<PilotAvatarDetector>();
        services.AddSingleton<LoggedInPilotDetector>();

        services.AddSingleton<SampleImageProcessor>();
        services.AddSingleton<ScreenCaptureService>();

        services.AddSingleton<CommonStartGameState>();
        services.AddSingleton<ConnectionLostPopupRecoveryBehavior>();
        services.AddSingleton<CommonRecoverClientIsRunningButtonVisibleState>();
        services.AddTransient<StartingGameState>();
        services.AddTransient<LoginState>();
        services.AddTransient<DiscoverState>();
        services.AddTransient<RecoveryState>();
        services.AddTransient<RecoverOverlapState>();
        services.AddTransient<RecoverSlowDownPopupState>();
        services.AddTransient<RecoverConnectionLostPopupState>();
        services.AddTransient<RecoverMaxSubmissionsPopupState>();
        services.AddTransient<RecoverClientIsRunningButtonVisibleState>();
        services.AddSingleton<IDiscoveryAutomationStateFactory, DiscoveryAutomationStateFactory>();

        services.AddSingleton<ProjectDiscoveryAutomationService>();
        services.AddSingleton<MiningAutomationService>();
        services.AddTransient<MainWindow>();

        return services;
    }
}
