using Automaton.Helpers;
using Automaton.CommonAutomationStates;
using Serilog;

namespace Automaton.MiningStates;

internal sealed class LoginState(IAutomationInputController automationInputController)
    : IMiningAutomationState
{
    private const int PilotIndex = 2;
    private const string CaptureSuffix = ".mining-login";

    private readonly ILogger m_Logger = Log.ForContext<LoginState>();

    public MiningAutomationStateKind Kind => MiningAutomationStateKind.Login;

    public MiningAutomationStateTransition Execute(
        MiningAutomationContext context,
        CancellationToken cancellationToken)
    {
        m_Logger.Debug("Executing {State}", Kind);
        cancellationToken.ThrowIfCancellationRequested();
        using var capture = context.ScreenCaptureService.CaptureCurrentScreen($"{CaptureSuffix}-{PilotIndex}");
        cancellationToken.ThrowIfCancellationRequested();
        var commonLoginState = new CommonLoginState(automationInputController);

        if (!commonLoginState.TryLoginPilot(
            PilotIndex,
            capture.CapturePath,
            cancellationToken,
            out _))
        {
            return Recover(capture.CapturePath);
        }

        m_Logger.Information("Logging in mining pilot {PilotIndex}...", PilotIndex);

        return new MiningAutomationStateTransition(
            Kind,
            MiningAutomationStateKind.UnloadCargo,
            MiningAutomationActionKind.LoginPilot,
            capture.CapturePath);
    }

    private MiningAutomationStateTransition Recover(string capturePath)
    {
        return new MiningAutomationStateTransition(
            Kind,
            MiningAutomationStateKind.Recovery,
            MiningAutomationActionKind.Recover,
            capturePath);
    }
}
