using Automaton.Helpers;

namespace Automaton.Tests.Stubs;

internal sealed class StubDiscoveryGameActions : IDiscoveryGameActions
{
    public int ToggleProjectDiscoveryWindowCallCount { get; private set; }

    public void ToggleProjectDiscoveryWindow(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ToggleProjectDiscoveryWindowCallCount++;
    }
}