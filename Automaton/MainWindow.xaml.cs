using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Automaton.Core.Helpers;
using Automaton.Core.Infrastructure;
using Automaton.Infrastructure;
using Automaton.ProjectDiscoveryStates;
using Microsoft.Win32;
using Serilog;

namespace Automaton;

internal partial class MainWindow
{
    private const int HotKeyId = 1;
    private const int WindowMessageHotKey = 0x0312;
    private const uint ModifierAlt = 0x0001;
    private const uint ModifierShift = 0x0004;
    private const uint VirtualKeyF11 = 0x7A;
    private const string HotKeyDisplayText = "Hotkey: Shift+Alt+F11";
    private static readonly Brush StartBrush = new SolidColorBrush(Color.FromRgb(0x2D, 0x88, 0x53));
    private static readonly Brush StartBorderBrush = new SolidColorBrush(Color.FromRgb(0x9B, 0xFF, 0xC0));
    private static readonly Brush StopBrush = new SolidColorBrush(Color.FromRgb(0x91, 0x2D, 0x3D));
    private static readonly Brush StopBorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x5E, 0x6B));
    private static readonly Brush StatusPausedBrush = new SolidColorBrush(Color.FromRgb(0x91, 0xA7, 0xB4));
    private static readonly Brush StatusRunningBrush = new SolidColorBrush(Color.FromRgb(0x8B, 0xD1, 0xA3));
    private static readonly Color StartGlowColor = Color.FromRgb(0x86, 0xF6, 0xA4);
    private static readonly Color StopGlowColor = Color.FromRgb(0xFF, 0x5E, 0x6B);
    private static readonly ILogger Logger = Log.ForContext<MainWindow>();

    private readonly ProjectDiscoveryAutomationService m_ProjectDiscoveryAutomationService;
    private readonly IGameActionService m_GameActionService;
    private readonly DiscoveryStartStateOption[] m_DiscoveryStartStateOptions =
    {
        new(DiscoveryAutomationStateKind.StartingGame, "Starting game"),
        new(DiscoveryAutomationStateKind.Login, "Login"),
        new(DiscoveryAutomationStateKind.Discover, "Discover"),
        new(DiscoveryAutomationStateKind.Recovery, "Recovery"),
        new(DiscoveryAutomationStateKind.RecoverSlowDownPopup, "Recover slow-down popup"),
        new(DiscoveryAutomationStateKind.RecoverConnectionLostPopup, "Recover connection-lost popup"),
        new(DiscoveryAutomationStateKind.RecoverMaxSubmissionsPopup, "Recover max-submissions popup")
    };
    private HwndSource? m_WindowSource;
    private CancellationTokenSource? m_AutomationCancellationSource;
    private bool m_IsAutomationRunning;
    private bool m_IsUpdatingControls;
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
        InitializeControls();
        SetPilotIndexControlsEnabled(isEnabled: true);
        RestoreWindowPosition();
        SourceInitialized += MainWindow_SourceInitialized;
        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
        Logger.Information(
            "Main window initialized. AutoStartAutomation={AutoStartAutomation}",
            m_AutoStartAutomation);
    }

    private void InitializeControls()
    {
        DiscoveryStartStateComboBox.ItemsSource = m_DiscoveryStartStateOptions;
        DiscoveryStartStateComboBox.DisplayMemberPath = nameof(DiscoveryStartStateOption.DisplayName);
        HotkeyTextBlock.Text = HotKeyDisplayText;
        SetDiscoveryStartState(m_SelectedDiscoveryStartState);
        SetPilotIndex(m_DefaultPilotIndex);
        SetStartButtonState(isRunning: false);
        LoadSettingsFields();
        UpdatePinButtonState();
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
        StartButton.Content = isRunning ? "STOP AUTOMATION" : "START AUTOMATION";
        StartButton.Background = isRunning ? StopBrush : StartBrush;
        StartButton.BorderBrush = isRunning ? StopBorderBrush : StartBorderBrush;
        StartButtonGlow.Background = isRunning ? StopBrush : StartBrush;
        StatusTextBlock.Text = isRunning
            ? "Running: Project Discovery automation"
            : "Paused";
        StatusTextBlock.Foreground = isRunning ? StatusRunningBrush : StatusPausedBrush;
        SetupStatusTextBlock.Text = isRunning
            ? "Automation is running. Stop it before changing setup."
            : "Choose the starting workflow and pilot before launching automation.";

        if (StartButtonGlow.Effect is DropShadowEffect glowEffect)
        {
            glowEffect.Color = isRunning ? StopGlowColor : StartGlowColor;
        }
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
        DiscoveryStartStateComboBox.IsEnabled = isEnabled;
        Pilot1RadioButton.IsEnabled = isEnabled;
        Pilot2RadioButton.IsEnabled = isEnabled;
        Pilot3RadioButton.IsEnabled = isEnabled;
        ProcessSamplesButton.IsEnabled = isEnabled;
    }

    private void Pilot1RadioButton_Click(object sender, RoutedEventArgs e)
    {
        SetPilotIndex(1);
    }

    private void Pilot2RadioButton_Click(object sender, RoutedEventArgs e)
    {
        SetPilotIndex(2);
    }

    private void Pilot3RadioButton_Click(object sender, RoutedEventArgs e)
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
        var wasUpdatingControls = m_IsUpdatingControls;
        m_IsUpdatingControls = true;
        try
        {
            Pilot1RadioButton.IsChecked = pilotIndex == 1;
            Pilot2RadioButton.IsChecked = pilotIndex == 2;
            Pilot3RadioButton.IsChecked = pilotIndex == 3;
        }
        finally
        {
            m_IsUpdatingControls = wasUpdatingControls;
        }

        SetupStatusTextBlock.Text = $"Pilot {pilotIndex} selected.";
        Logger.Information("Default pilot index changed. DefaultPilotIndex={DefaultPilotIndex}", m_DefaultPilotIndex);
    }

    private void Samples_Click(object sender, RoutedEventArgs e)
    {
        Logger.Information("Sample processing requested from main window.");
        m_ProjectDiscoveryAutomationService.ProcessSamples();
        SetupStatusTextBlock.Text = "Sample processing completed.";
    }

    private void DiscoveryStartStateComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (m_IsUpdatingControls || DiscoveryStartStateComboBox.SelectedItem is not DiscoveryStartStateOption option)
        {
            return;
        }

        SetDiscoveryStartState(option.StateKind);
    }

    private void SetDiscoveryStartState(DiscoveryAutomationStateKind stateKind)
    {
        m_SelectedDiscoveryStartState = stateKind;
        var wasUpdatingControls = m_IsUpdatingControls;
        m_IsUpdatingControls = true;
        try
        {
            DiscoveryStartStateComboBox.SelectedItem = m_DiscoveryStartStateOptions.FirstOrDefault(option => option.StateKind == stateKind);
        }
        finally
        {
            m_IsUpdatingControls = wasUpdatingControls;
        }

        SetupStatusTextBlock.Text = $"Start state set to {GetDiscoveryStartStateDisplayName(stateKind)}.";
        Logger.Information("Discovery start state changed. DiscoveryStartState={DiscoveryStartState}", m_SelectedDiscoveryStartState);
    }

    private string GetDiscoveryStartStateDisplayName(DiscoveryAutomationStateKind stateKind)
    {
        return m_DiscoveryStartStateOptions.First(option => option.StateKind == stateKind).DisplayName;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        UpdatePinButtonState();
        Logger.Information("Main window topmost changed. Topmost={Topmost}", Topmost);
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void UpdatePinButtonState()
    {
        PinButton.Opacity = Topmost ? 1 : 0.45;
    }

    private void LoadSettingsFields()
    {
        SettingsFilePathTextBox.Text = UserSettings.Default.FilePath;
        UserNameTextBox.Text = PrivateSettings.UserName;
        TelemetryRootTextBox.Text = UserSettings.Default.TelemetryRootBase;
        AvatarsDirectoryTextBox.Text = UserSettings.Default.PilotAvatarDirectory;
        TemplatesDirectoryTextBox.Text = UserSettings.Default.TemplatesDirectory;
        SettingsStatusTextBlock.Text = "Settings are loaded from the active settings file.";
    }

    private void BrowseSettingsFileButton_Click(object sender, RoutedEventArgs e)
    {
        var currentPath = SettingsFilePathTextBox.Text.Trim();
        var dialog = new SaveFileDialog
        {
            Title = "Select Automaton settings file",
            Filter = "JSON settings (*.json)|*.json|All files (*.*)|*.*",
            FileName = string.IsNullOrWhiteSpace(currentPath) ? "automaton.json" : Path.GetFileName(currentPath),
            OverwritePrompt = false
        };

        var initialDirectory = TryGetDirectoryName(currentPath);
        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

        if (dialog.ShowDialog(this) == true)
        {
            SettingsFilePathTextBox.Text = dialog.FileName;
        }
    }

    private void BrowseTelemetryRootButton_Click(object sender, RoutedEventArgs e)
    {
        BrowseForFolder(TelemetryRootTextBox, "Select telemetry root directory");
    }

    private void BrowseAvatarsButton_Click(object sender, RoutedEventArgs e)
    {
        BrowseForFolder(AvatarsDirectoryTextBox, "Select avatar directory");
    }

    private void BrowseTemplatesButton_Click(object sender, RoutedEventArgs e)
    {
        BrowseForFolder(TemplatesDirectoryTextBox, "Select templates directory");
    }

    private void BrowseForFolder(TextBox textBox, string title)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title
        };

        var path = textBox.Text.Trim();
        if (Directory.Exists(path))
        {
            dialog.InitialDirectory = path;
        }

        if (dialog.ShowDialog(this) == true)
        {
            textBox.Text = dialog.FolderName;
        }
    }

    private void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var previousFormLocation = UserSettings.Default.FormLocation;
            var requestedSettingsFilePath = SettingsFilePathTextBox.Text.Trim();
            var resolvedSettingsFilePath = UserSettings.ResolveFilePath(requestedSettingsFilePath);
            PrivateSettings.SetSettingsFilePath(string.IsNullOrWhiteSpace(requestedSettingsFilePath) ? "" : resolvedSettingsFilePath);
            PrivateSettings.SetUserName(UserNameTextBox.Text.Trim());

            if (!AreSamePath(UserSettings.Default.FilePath, resolvedSettingsFilePath))
            {
                UserSettings.Initialize(resolvedSettingsFilePath);
                UserSettings.Default.FormLocation = previousFormLocation;
            }

            UserSettings.Default.TelemetryRootBase = TelemetryRootTextBox.Text.Trim();
            UserSettings.Default.PilotAvatarDirectory = AvatarsDirectoryTextBox.Text.Trim();
            UserSettings.Default.TemplatesDirectory = TemplatesDirectoryTextBox.Text.Trim();
            UserSettings.Default.Save();
            SettingsFilePathTextBox.Text = UserSettings.Default.FilePath;
            SettingsStatusTextBlock.Text = "Settings saved.";
            Logger.Information(
                "Settings saved from main window. SettingsFilePath={SettingsFilePath}, UserName={UserName}, TelemetryRootBase={TelemetryRootBase}, PilotAvatarDirectory={PilotAvatarDirectory}, TemplatesDirectory={TemplatesDirectory}",
                UserSettings.Default.FilePath,
                PrivateSettings.UserName,
                UserSettings.Default.TelemetryRootBase,
                UserSettings.Default.PilotAvatarDirectory,
                UserSettings.Default.TemplatesDirectory);
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "Settings save failed.");
            SettingsStatusTextBlock.Text = $"Settings save failed: {exception.Message}";
        }
    }

    private static string? TryGetDirectoryName(string path)
    {
        try
        {
            return string.IsNullOrWhiteSpace(path) ? null : Path.GetDirectoryName(Path.GetFullPath(path));
        }
        catch
        {
            return null;
        }
    }

    private static bool AreSamePath(string first, string second)
    {
        try
        {
            return string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed record DiscoveryStartStateOption(DiscoveryAutomationStateKind StateKind, string DisplayName);
}
