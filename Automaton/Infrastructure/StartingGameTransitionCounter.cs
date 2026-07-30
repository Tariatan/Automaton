namespace Automaton.Infrastructure;

internal sealed class StartingGameTransitionCounter
{
    private int m_Count;

    public int Increment() => ++m_Count;
}