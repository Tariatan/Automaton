using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Automaton.Core.Helpers;
using Automaton.Core.Infrastructure;
using Automaton.Infrastructure;
using Automaton.ProjectDiscoveryStates;
using Serilog;

namespace Automaton;

internal partial class MainWindow
{
    private const int HotKeyId = 1;
    private const int WindowMessageHotKey = 0x0312;
    private const uint ModifierAlt = 0x0001;
    private const uint ModifierShift = 0x0004;
    private const uint VirtualKeyF11 = 0x7A;
    private static readonly Brush StartBrush = new SolidColorBrush(Color.FromRgb(0x2C, 0xB4, 0x3A));
    private static readonly Brush StopBrush = new SolidColorBrush(Color.FromRgb(0xD1, 0x34, 0x34));
    private static readonly ILogger Logger = Log.ForContext<MainWindow>();

    private readonly ProjectDiscoveryAutomationService m_ProjectDiscoveryAutomationService;
    private readonly IGameActionService m_GameActionService;
    private HwndSource? m_WindowSource;
    private CancellationTokenSource? m_AutomationCancellationSource;
    private bool m_IsAutomationRunning;
    private long m_CurrentAutomationSessionId;
    private readonly bool m_AutoStartAutomation;
    private int m_DefaultPilotIndex = 1;
    private DiscoveryAutomationStateKind m_SelectedDiscoveryStartState = DiscoveryAutomationStateKind.StartingGame;

    public MainWindow(
        ApplicationStartupOptions startupOptions,
        ProjectDiscoveryAutomationService projectDiscoveryAutomationService,
        IGameActionService gameActionService)
    {
        m_ProjectDiscoveryAutomationService = projectDiscoveryAutomationService;
        m_GameActionService = gameActionService;
        m_AutoStartAutomation = startupOptions.AutoStartAutomation;
        InitializeComponent();
        SetDiscoveryStartState(m_SelectedDiscoveryStartState);
        SetPilotIndexControlsEnabled(isEnabled: true);
        RestoreWindowPosition();
        SourceInitialized += MainWindow_SourceInitialized;
        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
        Logger.Information(
            "Main window initialized. AutoStartAutomation={AutoStartAutomation}",
            m_AutoStartAutomation);
    }

    private async void Automate_Click(object sender, RoutedEventArgs e)
    {
        if (!StartButton.IsEnabled)
        {
            return;
        }

        if (m_IsAutomationRunning)
        {
            Logger.Information("Stop requested from automation button.");
            StopAutomation();
            return;
        }

        var initialPilotIndex = GetPilotIndex();
        Logger.Information("Start requested from automation button. InitialPilotIndex={InitialPilotIndex}, SelectedDiscoveryStartState={SelectedDiscoveryStartState}", initialPilotIndex, m_SelectedDiscoveryStartState);
        await StartProjectDiscoveryAutomationAsync(initialPilotIndex, new CancellationTokenSource());
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= MainWindow_Loaded;
        if (!m_AutoStartAutomation)
        {
            return;
        }

        if (m_IsAutomationRunning)
        {
            return;
        }

        Logger.Information("Starting project discovery automation from startup argument.");
        var initialPilotIndex = GetPilotIndex();
        await StartProjectDiscoveryAutomationAsync(initialPilotIndex, new CancellationTokenSource());
    }

    private async Task StartProjectDiscoveryAutomationAsync(int initialPilotIndex, CancellationTokenSource cancellationSource, long? sessionId = null)
    {
        var effectiveSessionId = sessionId ?? BeginAutomationSession(cancellationSource);

        try
        {
            var automationTask = Task.Run(
                () => m_ProjectDiscoveryAutomationService.Automate(
                    cancellationSource.Token,
                    m_SelectedDiscoveryStartState,
                    initialPilotIndex),
                cancellationSource.Token);
            var (automationStateKind, nextState, automationActionKind, capturePath) = await automationTask;
            Logger.Information(
                "Discovery automation completed. State={State}, NextState={NextState}, Action={Action}, CapturePath={CapturePath}",
                automationStateKind,
                nextState,
                automationActionKind,
                capturePath);

            switch (automationActionKind)
            {
                case DiscoveryAutomationActionKind.Reboot:
                    Logger.Error("Discovery automation requested operating system reboot. Closing application.");
                    Application.Current.Shutdown();
                    break;
                case DiscoveryAutomationActionKind.Shutdown:
                    Logger.Error("Discovery automation requested safe operating system shutdown.");
                    m_GameActionService.ShutdownOperatingSystem(CancellationToken.None);
                    Application.Current.Shutdown();
                    break;
                case DiscoveryAutomationActionKind.NoFurtherPilotsAvailable:
                    Logger.Error("No further pilots are available. Scheduling operating system shutdown.");
                    m_GameActionService.ShutdownOperatingSystem(CancellationToken.None);
                    Application.Current.Shutdown();
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            Logger.Information("Automation was canceled");
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "Automation failed");
            throw;
        }
        finally
        {
            EndAutomationSession(cancellationSource, effectiveSessionId, disposeCancellationSource: true);
        }
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var windowInteropHelper = new WindowInteropHelper(this);
        m_WindowSource = HwndSource.FromHwnd(windowInteropHelper.Handle);
        m_WindowSource?.AddHook(WindowMessageHook);

        var registered = RegisterHotKey(
            windowInteropHelper.Handle,
            HotKeyId,
            ModifierShift | ModifierAlt,
            VirtualKeyF11);
        if (!registered)
        {
            Logger.Error("Could not register global hotkey Shift+Alt+F11.");
            throw new InvalidOperationException("Could not register global hotkey Shift+Alt+F11.");
        }

        Logger.Information("Registered global hotkey Shift+Alt+F11.");
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        Logger.Information("Main window closing.");
        StopAutomation();
        UserSettings.Default.FormLocation = $"{Left},{Top}";
        UserSettings.Default.Save();
        var windowInteropHelper = new WindowInteropHelper(this);
        _ = UnregisterHotKey(windowInteropHelper.Handle, HotKeyId);
        m_WindowSource?.RemoveHook(WindowMessageHook);
        m_WindowSource = null;
    }

    private void RestoreWindowPosition()
    {
        if (!TryParseFormLocation(UserSettings.Default.FormLocation, out var savedPosition))
        {
            return;
        }

        if (!IsWindowPositionVisible(savedPosition))
        {
            return;
        }

        Left = savedPosition.X;
        Top = savedPosition.Y;
    }

    private static bool TryParseFormLocation(string? raw, out Point point)
    {
        point = default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }
        var parts = raw.Split(',');
        if (parts.Length != 2)
        {
            return false;
        }
        if (!double.TryParse(parts[0], out var x) || !double.TryParse(parts[1], out var y))
        {
            return false;
        }

        point = new Point(x, y);
        return true;
    }

    private IntPtr WindowMessageHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != WindowMessageHotKey || wParam.ToInt32() != HotKeyId)
        {
            return IntPtr.Zero;
        }

        handled = true;
        Logger.Information("Global hotkey activated.");
        Automate_Click(this, new RoutedEventArgs());

        return IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private static bool IsWindowPositionVisible(Point position)
    {
        var left = SystemParameters.VirtualScreenLeft;
        var top = SystemParameters.VirtualScreenTop;
        var right = left + SystemParameters.VirtualScreenWidth;
        var bottom = top + SystemParameters.VirtualScreenHeight;

        return position.X >= left &&
               position.Y >= top &&
               position.X < right &&
               position.Y < bottom;
    }

    private void StopAutomation()
    {
        if (m_AutomationCancellationSource is not null)
        {
            Logger.Information("Automation cancellation requested.");
        }

        m_AutomationCancellationSource?.Cancel();
        m_IsAutomationRunning = false;
        SetStartButtonState(isRunning: false);
    }

    private void SetStartButtonState(bool isRunning)
    {
        StartButton.Content = isRunning ? "Stop" : "Start";
        StartButton.Background = isRunning ? StopBrush : StartBrush;
    }

    private long BeginAutomationSession(CancellationTokenSource cancellationSource)
    {
        m_CurrentAutomationSessionId++;
        m_IsAutomationRunning = true;
        m_AutomationCancellationSource = cancellationSource;
        SetStartButtonState(isRunning: true);
        SetPilotIndexControlsEnabled(isEnabled: false);
        return m_CurrentAutomationSessionId;
    }

    private void EndAutomationSession(CancellationTokenSource cancellationSource, long sessionId, bool disposeCancellationSource)
    {
        if (disposeCancellationSource)
        {
            cancellationSource.Dispose();
        }

        if (sessionId != m_CurrentAutomationSessionId)
        {
            return;
        }

        if (ReferenceEquals(m_AutomationCancellationSource, cancellationSource))
        {
            m_AutomationCancellationSource = null;
        }

        m_IsAutomationRunning = false;
        SetStartButtonState(isRunning: false);
        SetPilotIndexControlsEnabled(isEnabled: true);
    }

    private void SetPilotIndexControlsEnabled(bool isEnabled)
    {
        DiscoveryMenuItem.IsEnabled = isEnabled;
        SamplesMenuItem.IsEnabled = isEnabled;
    }


    private void Pilot1MenuItem_Click(object sender, RoutedEventArgs e)
    {
        SetPilotIndex(1);
    }

    private void Pilot2MenuItem_Click(object sender, RoutedEventArgs e)
    {
        SetPilotIndex(2);
    }

    private void Pilot3MenuItem_Click(object sender, RoutedEventArgs e)
    {
        SetPilotIndex(3);
    }

    private int GetPilotIndex()
    {
        return m_DefaultPilotIndex;
    }

    private void SetPilotIndex(int pilotIndex)
    {
        m_DefaultPilotIndex = pilotIndex;
        Pilot1MenuItem.IsChecked = pilotIndex == 1;
        Pilot2MenuItem.IsChecked = pilotIndex == 2;
        Pilot3MenuItem.IsChecked = pilotIndex == 3;
        Logger.Information("Default pilot index changed. DefaultPilotIndex={DefaultPilotIndex}", m_DefaultPilotIndex);
    }

    private void Samples_Click(object sender, RoutedEventArgs e)
    {
        Logger.Information("Sample processing requested from main window.");
        m_ProjectDiscoveryAutomationService.ProcessSamples();
    }

    private void DiscoveryStartingGameMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SetDiscoveryStartState(DiscoveryAutomationStateKind.StartingGame);
    }

    private void DiscoveryLoginMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SetDiscoveryStartState(DiscoveryAutomationStateKind.Login);
    }

    private void DiscoveryDiscoverMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SetDiscoveryStartState(DiscoveryAutomationStateKind.Discover);
    }

    private void DiscoveryRecoveryMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SetDiscoveryStartState(DiscoveryAutomationStateKind.Recovery);
    }

    private void DiscoveryRecoverSlowDownPopupMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SetDiscoveryStartState(DiscoveryAutomationStateKind.RecoverSlowDownPopup);
    }

    private void DiscoveryRecoverConnectionLostPopupMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SetDiscoveryStartState(DiscoveryAutomationStateKind.RecoverConnectionLostPopup);
    }

    private void DiscoveryRecoverMaxSubmissionsPopupMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SetDiscoveryStartState(DiscoveryAutomationStateKind.RecoverMaxSubmissionsPopup);
    }

    private void SetDiscoveryStartState(DiscoveryAutomationStateKind stateKind)
    {
        m_SelectedDiscoveryStartState = stateKind;
        DiscoveryStartingGameMenuItem.IsChecked = stateKind == DiscoveryAutomationStateKind.StartingGame;
        DiscoveryLoginMenuItem.IsChecked = stateKind == DiscoveryAutomationStateKind.Login;
        DiscoveryDiscoverMenuItem.IsChecked = stateKind == DiscoveryAutomationStateKind.Discover;
        DiscoveryRecoveryMenuItem.IsChecked = stateKind == DiscoveryAutomationStateKind.Recovery;
        DiscoveryRecoverSlowDownPopupMenuItem.IsChecked = stateKind == DiscoveryAutomationStateKind.RecoverSlowDownPopup;
        DiscoveryRecoverConnectionLostPopupMenuItem.IsChecked = stateKind == DiscoveryAutomationStateKind.RecoverConnectionLostPopup;
        DiscoveryRecoverMaxSubmissionsPopupMenuItem.IsChecked = stateKind == DiscoveryAutomationStateKind.RecoverMaxSubmissionsPopup;
        Logger.Information("Discovery start state changed. DiscoveryStartState={DiscoveryStartState}", m_SelectedDiscoveryStartState);
    }
}