using Microsoft.Extensions.DependencyInjection;

namespace Automaton.ProjectDiscoveryStates;

internal sealed class DiscoveryAutomationStateFactory(IServiceProvider serviceProvider) : IDiscoveryAutomationStateFactory
{
    public IProjectDiscoveryAutomationState Create(DiscoveryAutomationStateKind stateKind)
    {
        return stateKind switch
        {
            DiscoveryAutomationStateKind.StartingGame => serviceProvider.GetRequiredService<StartingGameState>(),
            DiscoveryAutomationStateKind.Login => serviceProvider.GetRequiredService<LoginState>(),
            DiscoveryAutomationStateKind.Discover => serviceProvider.GetRequiredService<DiscoverState>(),
            DiscoveryAutomationStateKind.Recovery => serviceProvider.GetRequiredService<RecoveryState>(),
            DiscoveryAutomationStateKind.RecoverSlowDownPopup => serviceProvider.GetRequiredService<RecoverSlowDownPopupState>(),
            DiscoveryAutomationStateKind.RecoverConnectionLostPopup => serviceProvider.GetRequiredService<RecoverConnectionLostPopupState>(),
            DiscoveryAutomationStateKind.RecoverMaxSubmissionsPopup => serviceProvider.GetRequiredService<RecoverMaxSubmissionsPopupState>(),
            _ => serviceProvider.GetRequiredService<DiscoverState>()
        };
    }
}
