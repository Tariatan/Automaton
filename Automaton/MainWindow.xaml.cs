using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Automaton.Properties;
using Automaton.MiningStates;
using Serilog;

namespace Automaton;

public partial class MainWindow
{
    private const int HotKeyId = 1;
    private const int WindowMessageHotKey = 0x0312;
    private const uint ModifierAlt = 0x0001;
    private const uint ModifierShift = 0x0004;
    private const uint VirtualKeyF11 = 0x7A;
    private static readonly Brush StartBrush = new SolidColorBrush(Color.FromRgb(0x2C, 0xB4, 0x3A));
    private static readonly Brush StopBrush = new SolidColorBrush(Color.FromRgb(0xD1, 0x34, 0x34));
    private static readonly ILogger Logger = Log.ForContext<MainWindow>();

    private readonly ProjectDiscoveryAutomationService m_ProjectDiscoveryAutomationService = new();
    private readonly MiningAutomationService m_MiningAutomationService = new();
    private ApplicationAutomationMode m_AutomationMode;
    private HwndSource? m_WindowSource;
    private CancellationTokenSource? m_AutomationCancellationSource;
    private bool m_IsAutomationRunning;
    private int m_DefaultPilotIndex = 1;
    private MiningAutomationStateKind m_SelectedMiningStartState = MiningAutomationStateKind.StartingGame;

    public MainWindow(ApplicationAutomationMode automationMode = ApplicationAutomationMode.ProjectDiscovery)
    {
        m_AutomationMode = automationMode;
        InitializeComponent();
        UpdateTelemetryMenuItemHeader();
        UpdateHallmarkMenuItemHeader();
        ApplyAutomationMode();
        RestoreWindowPosition();
        SourceInitialized += MainWindow_SourceInitialized;
        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
        Logger.Information("Main window initialized. AutomationMode={AutomationMode}", m_AutomationMode);
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

        if (m_AutomationMode == ApplicationAutomationMode.Mining)
        {
            Logger.Information("Start requested from automation button. AutomationMode={AutomationMode}, SelectedMiningStartState={SelectedMiningStartState}", m_AutomationMode, m_SelectedMiningStartState);
            await StartMiningAutomationAsync(new CancellationTokenSource());
            return;
        }

        var initialPilotIndex = GetPilotIndex();
        Logger.Information("Start requested from automation button. InitialPilotIndex={InitialPilotIndex}", initialPilotIndex);
        await StartProjectDiscoveryAutomationAsync(initialPilotIndex, new CancellationTokenSource());
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= MainWindow_Loaded;
        await TryRunStartupAutomationAsync();
    }

    private async Task TryRunStartupAutomationAsync()
    {
        if (m_IsAutomationRunning)
        {
            return;
        }

        if (m_AutomationMode == ApplicationAutomationMode.Mining)
        {
            Logger.Information("Starting mining automation from startup argument.");
            await StartMiningAutomationAsync(new CancellationTokenSource());
            return;
        }

        var cancellationSource = new CancellationTokenSource();
        BeginAutomationSession(cancellationSource);
        var initialPilotIndex = GetPilotIndex();
        var handedOffToAutomationRun = false;

        try
        {
            Logger.Information("Checking launcher startup automation. InitialPilotIndex={InitialPilotIndex}", initialPilotIndex);
            var startupSummary = await Task.Run(
                () => m_ProjectDiscoveryAutomationService.PrepareAutomationFromLauncherStartup(initialPilotIndex, cancellationSource.Token),
                cancellationSource.Token);
            Logger.Information(
                "Launcher startup automation prepared. ShouldStartAutomation={ShouldStartAutomation}, PlayButtonFound={PlayButtonFound}, PilotLocated={PilotLocated}, PlayCapturePath={PlayCapturePath}, PilotCapturePath={PilotCapturePath}",
                startupSummary.ShouldStartAutomation,
                startupSummary.PlayButtonFound,
                startupSummary.PilotLocated,
                startupSummary.PlayButtonCapturePath,
                startupSummary.PilotCapturePath);
            if (!startupSummary.ShouldStartAutomation)
            {
                return;
            }

            await StartProjectDiscoveryAutomationAsync(initialPilotIndex, cancellationSource);
            handedOffToAutomationRun = true;
        }
        catch (OperationCanceledException)
        {
            Logger.Information("Launcher startup automation was canceled.");
        }
        finally
        {
            if (!handedOffToAutomationRun)
            {
                EndAutomationSession(cancellationSource, disposeCancellationSource: true);
            }
        }
    }

    private async Task StartProjectDiscoveryAutomationAsync(int initialPilotIndex, CancellationTokenSource cancellationSource)
    {
        ApplyDebugImageRetention();
        BeginAutomationSession(cancellationSource);
        var dpi = VisualTreeHelper.GetDpi(this);
        Logger.Information(
            "Automation started. InitialPilotIndex={InitialPilotIndex}, DpiScaleX={DpiScaleX}, DpiScaleY={DpiScaleY}",
            initialPilotIndex,
            dpi.DpiScaleX,
            dpi.DpiScaleY);

        try
        {
            var automationTask = Task.Run(
                () => m_ProjectDiscoveryAutomationService.AutomateCurrentScreen(dpi, initialPilotIndex, cancellationSource.Token),
                cancellationSource.Token);
            var summary = await automationTask;
            Logger.Information(
                "Automation completed. CapturePath={CapturePath}, FocusedCapturePath={FocusedCapturePath}, ClickedPointCount={ClickedPointCount}, MaximumSubmissionsReached={MaximumSubmissionsReached}, SlowDownPopupDetected={SlowDownPopupDetected}, ConnectionLostDetected={ConnectionLostDetected}, PlayfieldMissingLimitReached={PlayfieldMissingLimitReached}, NoFurtherPilotsAvailable={NoFurtherPilotsAvailable}, CurrentPilotIndex={CurrentPilotIndex}, TargetPilotIndex={TargetPilotIndex}, PilotSwitchSucceeded={PilotSwitchSucceeded}",
                summary.CaptureSummary.CapturePath,
                summary.FocusedCapturePath,
                summary.ClickedPointCount,
                summary.MaximumSubmissionsReached,
                summary.SlowDownPopupDetected,
                summary.ConnectionLostDetected,
                summary.PlayfieldMissingLimitReached,
                summary.NoFurtherPilotsAvailable,
                summary.CurrentPilotIndex,
                summary.TargetPilotIndex,
                summary.PilotSwitchSucceeded);
            if (summary.ConnectionLostDetected)
            {
                Logger.Error("Connection Lost popup detected. Exiting process.");
                Application.Current.Shutdown();
                Environment.Exit(0);
            }

            if (summary.NoFurtherPilotsAvailable)
            {
                Logger.Information("No further pilots are available. Exiting process safely.");
                Application.Current.Shutdown();
                Environment.Exit(0);
            }
        }
        catch (OperationCanceledException)
        {
            Logger.Information("Automation was canceled.");
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "Automation failed.");
            throw;
        }
        finally
        {
            EndAutomationSession(cancellationSource, disposeCancellationSource: true);
        }
    }

    private async Task StartMiningAutomationAsync(CancellationTokenSource cancellationSource)
    {
        BeginAutomationSession(cancellationSource);
        var dpi = VisualTreeHelper.GetDpi(this);
        Logger.Information(
            "Mining automation started. DpiScaleX={DpiScaleX}, DpiScaleY={DpiScaleY}",
            dpi.DpiScaleX,
            dpi.DpiScaleY);

        try
        {
            var automationTask = Task.Run(
                () => m_MiningAutomationService.AutomateCurrentScreen(m_SelectedMiningStartState, cancellationSource.Token),
                cancellationSource.Token);
            var summary = await automationTask;
            Logger.Information(
                "Mining automation completed. State={State}, NextState={NextState}, Action={Action}, CapturePath={CapturePath}",
                summary.State,
                summary.NextState,
                summary.Action,
                summary.CapturePath);
        }
        catch (OperationCanceledException)
        {
            Logger.Information("Mining automation was canceled.");
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "Mining automation failed.");
            throw;
        }
        finally
        {
            EndAutomationSession(cancellationSource, disposeCancellationSource: true);
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
        Settings.Default.FormLocation = new Point(Left, Top);
        Settings.Default.Save();
        var windowInteropHelper = new WindowInteropHelper(this);
        UnregisterHotKey(windowInteropHelper.Handle, HotKeyId);
        m_WindowSource?.RemoveHook(WindowMessageHook);
        m_WindowSource = null;
    }

    private void RestoreWindowPosition()
    {
        var savedPosition = Settings.Default.FormLocation;
        if (!IsWindowPositionVisible(savedPosition))
        {
            return;
        }

        Left = savedPosition.X;
        Top = savedPosition.Y;
    }

    private IntPtr WindowMessageHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WindowMessageHotKey && wParam.ToInt32() == HotKeyId)
        {
            handled = true;
            Logger.Information("Global hotkey activated.");
            Automate_Click(this, new RoutedEventArgs());
        }

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

    private void BeginAutomationSession(CancellationTokenSource cancellationSource)
    {
        ApplyDebugImageRetention();
        m_IsAutomationRunning = true;
        m_AutomationCancellationSource = cancellationSource;
        SetStartButtonState(isRunning: true);
        SetPilotIndexControlsEnabled(isEnabled: false);
    }

    private void EndAutomationSession(CancellationTokenSource cancellationSource, bool disposeCancellationSource)
    {
        if (disposeCancellationSource)
        {
            cancellationSource.Dispose();
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
        var projectDiscoveryControlsEnabled = isEnabled &&
                                              m_AutomationMode == ApplicationAutomationMode.ProjectDiscovery;
        var miningControlsEnabled = isEnabled &&
                                    m_AutomationMode == ApplicationAutomationMode.Mining;
        DiscoveryMenuItem.IsEnabled = projectDiscoveryControlsEnabled;
        SamplesMenuItem.IsEnabled = projectDiscoveryControlsEnabled;
        MiningMenuItem.IsEnabled = miningControlsEnabled;
    }

    private void ApplyDebugImageRetention()
    {
        var keepDebugImages = DebugMenuItem.IsChecked;
        m_ProjectDiscoveryAutomationService.KeepDebugImages = keepDebugImages;
        Logger.Information("Debug image retention set. KeepDebugImages={KeepDebugImages}", keepDebugImages);
    }

    private void DebugMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ApplyDebugImageRetention();
    }

    private void TelemetryMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var folderDialog = new OpenFolderDialog
        {
            Title = "Select telemetry root folder"
        };

        var configuredRootDirectory = TelemetryRootDirectory.GetConfiguredRootDirectory();
        if (!string.IsNullOrWhiteSpace(configuredRootDirectory) && Directory.Exists(configuredRootDirectory))
        {
            folderDialog.InitialDirectory = configuredRootDirectory;
        }

        if (folderDialog.ShowDialog() != true || string.IsNullOrWhiteSpace(folderDialog.FolderName))
        {
            return;
        }

        TelemetryRootDirectory.SetConfiguredRootDirectory(folderDialog.FolderName);
        UpdateTelemetryMenuItemHeader();
        Logger.Information("Telemetry root directory selected. TelemetryRootDirectory={TelemetryRootDirectory}", folderDialog.FolderName);
    }

    private void UpdateTelemetryMenuItemHeader()
    {
        var configuredRootDirectory = TelemetryRootDirectory.GetConfiguredRootDirectory();
        if (string.IsNullOrWhiteSpace(configuredRootDirectory))
        {
            TelemetryMenuItem.Header = "Telemetry";
            return;
        }

        var folderName = Path.GetFileName(configuredRootDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        TelemetryMenuItem.Header = string.IsNullOrWhiteSpace(folderName)
            ? configuredRootDirectory
            : folderName;
    }

    private void HallmarkMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var folderDialog = new OpenFolderDialog
        {
            Title = "Select hallmark root folder"
        };

        var configuredRootDirectory = TelemetryRootDirectory.GetConfiguredHallmarkRootDirectory();
        if (!string.IsNullOrWhiteSpace(configuredRootDirectory) && Directory.Exists(configuredRootDirectory))
        {
            folderDialog.InitialDirectory = configuredRootDirectory;
        }

        if (folderDialog.ShowDialog() != true || string.IsNullOrWhiteSpace(folderDialog.FolderName))
        {
            return;
        }

        TelemetryRootDirectory.SetConfiguredHallmarkRootDirectory(folderDialog.FolderName);
        UpdateHallmarkMenuItemHeader();
        Logger.Information("Hallmark root directory selected. HallmarkRootDirectory={HallmarkRootDirectory}", folderDialog.FolderName);
    }

    private void UpdateHallmarkMenuItemHeader()
    {
        var configuredRootDirectory = TelemetryRootDirectory.GetConfiguredHallmarkRootDirectory();
        if (string.IsNullOrWhiteSpace(configuredRootDirectory))
        {
            HallmarkMenuItem.Header = "Hallmark";
            return;
        }

        var folderName = Path.GetFileName(configuredRootDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        HallmarkMenuItem.Header = string.IsNullOrWhiteSpace(folderName)
            ? configuredRootDirectory
            : folderName;
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

    private void MiningStartingGameMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SetMiningStartState(MiningAutomationStateKind.StartingGame);
    }

    private void MiningLoginMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SetMiningStartState(MiningAutomationStateKind.Login);
    }

    private void MiningDockedMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SetMiningStartState(MiningAutomationStateKind.UnloadCargo);
    }

    private void MiningDockMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SetMiningStartState(MiningAutomationStateKind.Dock);
    }

    private void MiningUndockingMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SetMiningStartState(MiningAutomationStateKind.Undocking);
    }

    private void MiningEmptyOnUndockMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SetMiningStartState(MiningAutomationStateKind.SelectBeltAndWarp);
    }

    private void MiningWarpingToAsteroidFieldMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SetMiningStartState(MiningAutomationStateKind.WarpingToAsteroidField);
    }

    private void MiningApproachingAsteroidMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SetMiningStartState(MiningAutomationStateKind.ApproachingAsteroid);
    }

    private void MiningMiningMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SetMiningStartState(MiningAutomationStateKind.Mining);
    }

    private void MiningUnloadCargoMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SetMiningStartState(MiningAutomationStateKind.UnloadCargo);
    }

    private void MiningRecoveryMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SetMiningStartState(MiningAutomationStateKind.Recovery);
    }

    private void SetMiningStartState(MiningAutomationStateKind stateKind)
    {
        m_SelectedMiningStartState = stateKind;
        MiningStartingGameMenuItem.IsChecked = stateKind == MiningAutomationStateKind.StartingGame;
        MiningLoginMenuItem.IsChecked = stateKind == MiningAutomationStateKind.Login;
        MiningUnloadCargoMenuItem.IsChecked = stateKind == MiningAutomationStateKind.UnloadCargo;
        MiningUndockingMenuItem.IsChecked = stateKind == MiningAutomationStateKind.Undocking;
        MiningEmptyOnUndockMenuItem.IsChecked = stateKind == MiningAutomationStateKind.SelectBeltAndWarp;
        MiningWarpingToAsteroidFieldMenuItem.IsChecked = stateKind == MiningAutomationStateKind.WarpingToAsteroidField;
        MiningApproachingAsteroidMenuItem.IsChecked = stateKind == MiningAutomationStateKind.ApproachingAsteroid;
        MiningMiningMenuItem.IsChecked = stateKind == MiningAutomationStateKind.Mining;
        MiningDockMenuItem.IsChecked = stateKind == MiningAutomationStateKind.Dock;
        MiningRecoveryMenuItem.IsChecked = stateKind == MiningAutomationStateKind.Recovery;
        Logger.Information("Mining start state changed. MiningStartState={MiningStartState}", m_SelectedMiningStartState);
    }

    private void ApplyAutomationMode()
    {
        Title = m_AutomationMode == ApplicationAutomationMode.Mining
            ? "Automaton - Miner"
            : "Automaton";
        SettingsDiscoveryModeMenuItem.IsChecked = m_AutomationMode == ApplicationAutomationMode.ProjectDiscovery;
        SettingsMiningModeMenuItem.IsChecked = m_AutomationMode == ApplicationAutomationMode.Mining;

        SetMiningStartState(m_SelectedMiningStartState);
        SetPilotIndexControlsEnabled(isEnabled: true);
    }

    private void SettingsDiscoveryModeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SetAutomationMode(ApplicationAutomationMode.ProjectDiscovery);
    }

    private void SettingsMiningModeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SetAutomationMode(ApplicationAutomationMode.Mining);
    }

    private void SetAutomationMode(ApplicationAutomationMode automationMode)
    {
        if (m_IsAutomationRunning)
        {
            SettingsDiscoveryModeMenuItem.IsChecked = m_AutomationMode == ApplicationAutomationMode.ProjectDiscovery;
            SettingsMiningModeMenuItem.IsChecked = m_AutomationMode == ApplicationAutomationMode.Mining;
            Logger.Information(
                "Automation mode change ignored because automation is running. CurrentMode={CurrentMode}, RequestedMode={RequestedMode}",
                m_AutomationMode,
                automationMode);
            return;
        }

        if (m_AutomationMode == automationMode)
        {
            SettingsDiscoveryModeMenuItem.IsChecked = automationMode == ApplicationAutomationMode.ProjectDiscovery;
            SettingsMiningModeMenuItem.IsChecked = automationMode == ApplicationAutomationMode.Mining;
            return;
        }

        m_AutomationMode = automationMode;
        ApplyAutomationMode();
        Logger.Information("Automation mode changed from settings. AutomationMode={AutomationMode}", m_AutomationMode);
    }
}
