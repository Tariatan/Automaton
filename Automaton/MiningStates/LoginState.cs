using Automaton.Helpers;
using Automaton.CommonAutomationStates;
using Serilog;

namespace Automaton.MiningStates;

internal sealed class LoginState(IAutomationInputController automationInputController)
    : IMiningAutomationState
{
    private const int PilotIndex = 2;
    private const string CaptureSuffix = ".mining-login";
    private readonly CommonLoginState m_CommonLoginState = new(automationInputController);

    private readonly ILogger m_Logger = Log.ForContext<LoginState>();

    public MiningAutomationStateKind Kind => MiningAutomationStateKind.Login;

    public MiningAutomationStateTransition Execute(
        MiningAutomationContext context,
        CancellationToken cancellationToken)
    {
        m_Logger.Information("Executing {State}", Kind);
        if (!m_CommonLoginState.TryLoginPilot(
            context.ScreenCaptureService,
            PilotIndex,
            CaptureSuffix,
            cancellationToken,
            out var capturePath))
        {
            return Recover(capturePath, MiningAutomationFailureReason.DetectionMiss);
        }

        if( context.LastAction == MiningAutomationActionKind.StartGame)
        {
            automationInputController.TryHideUi(capturePath, cancellationToken);
        }

        return new MiningAutomationStateTransition(
            Kind,
            MiningAutomationStateKind.UnloadCargo,
            MiningAutomationActionKind.LoginPilot,
            capturePath);
    }

    private MiningAutomationStateTransition Recover(string capturePath, MiningAutomationFailureReason failureReason = MiningAutomationFailureReason.None)
    {
        return new MiningAutomationStateTransition(
            Kind,
            MiningAutomationStateKind.Recovery,
            MiningAutomationActionKind.Recover,
            capturePath)
        {
            FailureReason = failureReason
        };
    }
}
