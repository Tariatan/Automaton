using System.Reflection;
using System.Windows;
using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.Primitives;
using OpenCvSharp;
using Point = OpenCvSharp.Point;
using Rect = OpenCvSharp.Rect;
using Size = OpenCvSharp.Size;

namespace Automaton.Tests;

// ReSharper disable AccessToDisposedClosure
public sealed class ProjectDiscoveryAutomationServiceTests
{

    [Fact]
    public void AutomateCurrentScreen_PlayfieldAndControlButtonExist_ClicksPolygonPointsAndFocusesControlButton()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var capturePath = SyntheticDiscoveryImageFactory.GetTwoClusterImagePath();
        using var captureImage = Cv2.ImRead(capturePath);
        var captureInvocationCount = 0;
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(() =>
            {
                captureInvocationCount++;
                return captureImage.Clone();
            }),
            new SampleImageProcessor());
        var automationClock = new StubAutomationClock();
        using var cancellationTokenSource = new CancellationTokenSource();
        var sawStartupDelay = false;
        var shortDelaysAfterStartup = 0;
        var automationInputController = new StubAutomationInputController
        {
            OnDelay = milliseconds =>
            {
                automationClock.AdvanceBy(milliseconds);

                if (milliseconds == Delays.AutomationStartupDelayMs)
                {
                    sawStartupDelay = true;
                    return;
                }

                if (sawStartupDelay && milliseconds == Delays.MinimumClickMs)
                {
                    shortDelaysAfterStartup++;
                    if (captureInvocationCount >= 3 && shortDelaysAfterStartup >= 5)
                    {
                        cancellationTokenSource.Cancel();
                    }
                }
            }
        };
        var automationService = new ProjectDiscoveryAutomationService(screenCaptureService, automationInputController, automationClock, new ErrorPopupDetector(), new PlayNowButtonLocator());
        var dpi = new DpiScale(1.0, 1.0);
        AutomationSummary summary;

        // Act
        var currentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(workspace.Path);

        try
        {
            summary = automationService.AutomateCurrentScreen(dpi, cancellationTokenSource.Token);
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
        }

        // Assert
        Assert.True(summary.CaptureSummary.Analysis.Result.PlayfieldFound);
        Assert.True(summary.ClickedPointCount > 0);
        Assert.NotNull(summary.ControlButtonBounds);
        Assert.False(summary.MaximumSubmissionsReached);
        Assert.Equal("captures", summary.CaptureSummary.CapturesDirectory);
        Assert.InRange(automationInputController.MoveTargets.Count, summary.ClickedPointCount + 1, summary.ClickedPointCount + 2); // + Control button focus (+1 possible boundary move)
        Assert.InRange(automationInputController.ClickCount, summary.ClickedPointCount + 3, summary.ClickedPointCount + 4);
        Assert.Contains(
            automationInputController.MoveTargets,
            point => point.X >= 930 && point.X <= 1200 && point.Y >= 645 && point.Y <= 655);
        Assert.True(File.Exists(Path.Combine(workspace.Path, summary.CaptureSummary.CapturePath)));
        Assert.True(File.Exists(Path.Combine(workspace.Path, summary.CaptureSummary.Analysis.Result.OutputPath)));
        var focusedCaptureAbsolutePath = Path.Combine(workspace.Path, summary.FocusedCapturePath);
        Assert.True(File.Exists(focusedCaptureAbsolutePath));
        Assert.False(File.Exists(Path.ChangeExtension(focusedCaptureAbsolutePath, ".annotated.png")));
        Assert.Equal(
            Path.Combine(
                summary.CaptureSummary.CapturesDirectory,
                $"{Path.GetFileNameWithoutExtension(summary.CaptureSummary.CapturePath)}.focused.png"),
            summary.FocusedCapturePath);
    }

    [Fact]
    public void AutomateCurrentScreen_DebugImagesDisabled_DeletesCycleTraceImages()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var capturePath = SyntheticDiscoveryImageFactory.GetTwoClusterImagePath();
        var captureInvocationCount = 0;
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(() =>
            {
                captureInvocationCount++;
                return Cv2.ImRead(capturePath);
            }),
            new SampleImageProcessor());
        using var cancellationTokenSource = new CancellationTokenSource();
        var sawStartupDelay = false;
        var shortDelaysAfterStartup = 0;
        var automationInputController = new StubAutomationInputController
        {
            OnDelay = milliseconds =>
            {
                if (milliseconds == Delays.AutomationStartupDelayMs)
                {
                    sawStartupDelay = true;
                    return;
                }

                if (sawStartupDelay && milliseconds == Delays.MinimumClickMs)
                {
                    shortDelaysAfterStartup++;
                    if (captureInvocationCount >= 3 && shortDelaysAfterStartup >= 5)
                    {
                        cancellationTokenSource.Cancel();
                    }
                }
            }
        };
        var automationService = new ProjectDiscoveryAutomationService(screenCaptureService, automationInputController, new StubAutomationClock(), new ErrorPopupDetector(), new PlayNowButtonLocator())
        {
            KeepDebugImages = false
        };
        var dpi = new DpiScale(1.0, 1.0);
        AutomationSummary summary;

        // Act
        var currentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(workspace.Path);

        try
        {
            summary = automationService.AutomateCurrentScreen(dpi, cancellationTokenSource.Token);
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
        }

        // Assert
        Assert.False(File.Exists(Path.Combine(workspace.Path, summary.CaptureSummary.CapturePath)));
        Assert.False(File.Exists(Path.Combine(workspace.Path, summary.CaptureSummary.Analysis.Result.OutputPath)));
        Assert.False(File.Exists(Path.Combine(workspace.Path, summary.FocusedCapturePath)));
    }

    [Fact]
    public void AutomateCurrentScreen_StopRequestedAfterFirstCycle_StartsNextCycleOnlyWhenNotCanceled()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var capturePath = SyntheticDiscoveryImageFactory.GetTwoClusterImagePath();
        using var captureImage = Cv2.ImRead(capturePath);
        var captureInvocationCount = 0;
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(() =>
            {
                captureInvocationCount++;
                return captureImage.Clone();
            }),
            new SampleImageProcessor());
        var automationClock = new StubAutomationClock();
        using var cancellationTokenSource = new CancellationTokenSource();
        var sawStartupDelay = false;
        var shortDelaysAfterStartup = 0;
        var automationInputController = new StubAutomationInputController
        {
            OnDelay = milliseconds =>
            {
                automationClock.AdvanceBy(milliseconds);

                if (milliseconds == Delays.AutomationStartupDelayMs)
                {
                    sawStartupDelay = true;
                    return;
                }

                if (sawStartupDelay && milliseconds == Delays.MinimumClickMs)
                {
                    shortDelaysAfterStartup++;
                    if (captureInvocationCount >= 3 && shortDelaysAfterStartup >= 5)
                    {
                        cancellationTokenSource.Cancel();
                    }
                }
            }
        };
        var automationService = new ProjectDiscoveryAutomationService(screenCaptureService, automationInputController, automationClock, new ErrorPopupDetector(), new PlayNowButtonLocator());
        var dpi = new DpiScale(1.0, 1.0);

        // Act
        var currentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(workspace.Path);

        try
        {
            automationService.AutomateCurrentScreen(dpi, cancellationTokenSource.Token);
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
        }

        // Assert
        Assert.Equal(3, captureInvocationCount);
    }

    [Fact]
    public void AutomateCurrentScreen_MaximumSubmissionsPopupAppearsAfterSubmit_SelectsNextPilotAndContinues()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var capturePath = SyntheticDiscoveryImageFactory.GetTwoClusterImagePath();
        var popupPath = SyntheticDiscoveryImageFactory.GetMaximumSubmissionsPopupImagePath();
        var pilotSelectionScreenPath = Path.Combine(workspace.Path, "pilot-selection.png");
        var pilotDirectory = Path.Combine(workspace.Path, "pilot");
        SyntheticCommonImageFactory.CopyPilotAvatarTemplatesTo(pilotDirectory, 3);
        SyntheticCommonImageFactory.CopyLoginPilotSelectionScreenTo(pilotSelectionScreenPath);

        var captureInvocationCount = 0;
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(() =>
            {
                captureInvocationCount++;
                var sourcePath = captureInvocationCount switch
                {
                    1 => capturePath,
                    2 => popupPath,
                    _ => pilotSelectionScreenPath
                };
                return Cv2.ImRead(sourcePath);
            }),
            new SampleImageProcessor());
        var automationClock = new StubAutomationClock();
        using var cancellationTokenSource = new CancellationTokenSource();
        var pilotUnlockPressed = false;
        var automationInputController = new StubAutomationInputController
        {
            OnDelay = milliseconds =>
            {
                automationClock.AdvanceBy(milliseconds);
            },
            OnPressKeyChord = (modifierVirtualKey, virtualKey) =>
            {
                if (modifierVirtualKey == VirtualKeys.Alt && virtualKey == VirtualKeys.L)
                {
                    pilotUnlockPressed = true;
                    cancellationTokenSource.Cancel();
                }
            }
        };
        var automationService = new ProjectDiscoveryAutomationService(screenCaptureService, automationInputController, automationClock, new ErrorPopupDetector(), new PlayNowButtonLocator());
        var dpi = new DpiScale(1.0, 1.0);
        AutomationSummary summary;

        // Act
        var currentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(workspace.Path);

        try
        {
            summary = automationService.AutomateCurrentScreen(dpi, 2, cancellationTokenSource.Token);
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
        }

        // Assert
        Assert.True(pilotUnlockPressed);
        Assert.Equal(3, summary.CurrentPilotIndex);
        Assert.True(captureInvocationCount >= 3);
        Assert.True(automationInputController.ClickCount >= summary.ClickedPointCount + 2);
        Assert.Contains(
            automationInputController.MoveTargets,
            point => point.Y is >= 740 and <= 840 && point.X is >= 800 and <= 1800);
        Assert.Equal(3, automationInputController.KeyInputs.Count);
        AssertKeyChord(automationInputController.KeyInputs[0], VirtualKeys.Control, VirtualKeys.W);
        AssertKeyChord(automationInputController.KeyInputs[1], VirtualKeys.Control, VirtualKeys.W);
        AssertKeyChord(automationInputController.KeyInputs[2], VirtualKeys.Alt, VirtualKeys.L);
        Assert.Contains(Delays.PilotLogoutMs, automationInputController.Delays);
        Assert.Contains(Delays.ProjectDiscoveryPilotLoginMs, automationInputController.Delays);
        Assert.Equal(Delays.MinimumClickMs, automationInputController.Delays[^1]);
    }

    [Fact]
    public void AutomateCurrentScreen_MaximumSubmissionsPopupAppearsOnLastPilot_LogsOutAndStopsWithoutSwitchingPilot()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var capturePath = SyntheticDiscoveryImageFactory.GetTwoClusterImagePath();
        var popupPath = SyntheticDiscoveryImageFactory.GetMaximumSubmissionsPopupImagePath();
        var pilotDirectory = Path.Combine(workspace.Path, "pilot");
        SyntheticCommonImageFactory.CopyPilotAvatarTemplatesTo(pilotDirectory, 1);
        SyntheticCommonImageFactory.CopyPilotAvatarTemplatesTo(pilotDirectory, 2);
        SyntheticCommonImageFactory.CopyPilotAvatarTemplatesTo(pilotDirectory, 3);

        var captureInvocationCount = 0;
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(() =>
            {
                captureInvocationCount++;
                var sourcePath = captureInvocationCount switch
                {
                    1 => capturePath,
                    _ => popupPath
                };
                return Cv2.ImRead(sourcePath);
            }),
            new SampleImageProcessor());
        var automationClock = new StubAutomationClock();
        using var cancellationTokenSource = new CancellationTokenSource();
        var automationInputController = new StubAutomationInputController
        {
            OnDelay = milliseconds => automationClock.AdvanceBy(milliseconds)
        };
        var automationService = new ProjectDiscoveryAutomationService(screenCaptureService, automationInputController, automationClock, new ErrorPopupDetector(), new PlayNowButtonLocator());
        var dpi = new DpiScale(1.0, 1.0);
        AutomationSummary summary;

        // Act
        var currentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(workspace.Path);

        try
        {
            summary = automationService.AutomateCurrentScreen(dpi, 3, cancellationTokenSource.Token);
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
        }

        // Assert
        Assert.True(
            summary.MaximumSubmissionsReached,
            $"Expected maximum submissions stop. Actual: MaximumSubmissionsReached={summary.MaximumSubmissionsReached}, PilotSwitchSucceeded={summary.PilotSwitchSucceeded}, NoFurtherPilotsAvailable={summary.NoFurtherPilotsAvailable}, CurrentPilotIndex={summary.CurrentPilotIndex}, TargetPilotIndex={summary.TargetPilotIndex}, KeyInputs={automationInputController.KeyInputs.Count}, QuitGameCalled={automationInputController.QuitGameCalled}");
        Assert.False(summary.PilotSwitchSucceeded);
        Assert.True(summary.NoFurtherPilotsAvailable);
        Assert.Equal(3, summary.CurrentPilotIndex);
        Assert.Equal(3, summary.TargetPilotIndex);
        Assert.Null(summary.PilotSwitchCapturePath);
        Assert.Equal(2, captureInvocationCount);
        Assert.Equal(summary.ClickedPointCount + 1, automationInputController.ClickCount);
        Assert.Empty(automationInputController.KeyInputs);
        Assert.True(automationInputController.QuitGameCalled);
        Assert.Contains(Delays.AutomationStartupDelayMs, automationInputController.Delays);
    }

    [Fact]
    public void AutomateCurrentScreen_SlowDownPopupAppearsAfterSubmit_WaitsAndResumesAutomation()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var capturePath = SyntheticDiscoveryImageFactory.GetTwoClusterImagePath();
        var slowDownPopupPath = SyntheticDiscoveryImageFactory.GetSlowDownPopupImagePath();
        using var captureImage = Cv2.ImRead(capturePath);
        using var slowDownPopupImage = Cv2.ImRead(slowDownPopupPath);

        var captureInvocationCount = 0;
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(() =>
            {
                captureInvocationCount++;
                return captureInvocationCount == 2 ? slowDownPopupImage.Clone() : captureImage.Clone();
            }),
            new SampleImageProcessor());
        var automationClock = new StubAutomationClock();
        using var cancellationTokenSource = new CancellationTokenSource();
        var slowDownRecoveryObserved = false;
        var shortDelaysAfterRecovery = 0;
        var automationInputController = new StubAutomationInputController
        {
            OnDelay = milliseconds =>
            {
                automationClock.AdvanceBy(milliseconds);

                if (milliseconds == Delays.SubmissionWindowMs)
                {
                    slowDownRecoveryObserved = true;
                    return;
                }

                if (!slowDownRecoveryObserved)
                {
                    return;
                }

                if (milliseconds == Delays.MinimumClickMs)
                {
                    shortDelaysAfterRecovery++;
                    if (captureInvocationCount >= 4 && shortDelaysAfterRecovery >= 2)
                    {
                        cancellationTokenSource.Cancel();
                    }
                }
            }
        };
        var automationService = new ProjectDiscoveryAutomationService(screenCaptureService, automationInputController, automationClock, new ErrorPopupDetector(), new PlayNowButtonLocator());
        var dpi = new DpiScale(1.0, 1.0);
        AutomationSummary summary;

        // Act
        var currentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(workspace.Path);

        try
        {
            summary = automationService.AutomateCurrentScreen(dpi, cancellationTokenSource.Token);
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
        }

        // Assert
        Assert.False(summary.MaximumSubmissionsReached);
        Assert.True(summary.SlowDownPopupDetected);
        Assert.True(slowDownRecoveryObserved);
        Assert.Equal(4, captureInvocationCount);
        Assert.Contains(
            automationInputController.KeyInputs,
            keyInput => keyInput.ModifierVirtualKey == VirtualKeys.Control &&
                        keyInput.SecondModifierVirtualKey is null &&
                        keyInput.VirtualKey == VirtualKeys.W);
        Assert.Contains(
            automationInputController.KeyInputs,
            keyInput => keyInput.ModifierVirtualKey == VirtualKeys.Alt &&
                        keyInput.SecondModifierVirtualKey is null &&
                        keyInput.VirtualKey == VirtualKeys.L);
        Assert.Contains(Delays.SubmissionWindowMs, automationInputController.Delays);
    }

    [Fact]
    public void AutomateCurrentScreen_CurrentScreenContainsSlowDownPopup_WaitsAndResumesWithoutFallbackClicks()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var slowDownPopupPath = SyntheticDiscoveryImageFactory.GetSlowDownPopupImagePath();
        var twoClustersImagePath = SyntheticDiscoveryImageFactory.GetTwoClusterImagePath();

        var captureInvocationCount = 0;
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(() =>
            {
                captureInvocationCount++;
                return Cv2.ImRead(captureInvocationCount == 1 ? slowDownPopupPath : twoClustersImagePath);
            }),
            new SampleImageProcessor());
        var automationClock = new StubAutomationClock();
        using var cancellationTokenSource = new CancellationTokenSource();
        var slowDownRecoveryObserved = false;
        var automationInputController = new StubAutomationInputController
        {
            OnDelay = milliseconds =>
            {
                automationClock.AdvanceBy(milliseconds);

                switch (milliseconds)
                {
                    case Delays.SubmissionWindowMs:
                        slowDownRecoveryObserved = true;
                        return;
                    case Delays.MinimumClickMs when slowDownRecoveryObserved && captureInvocationCount >= 2:
                        cancellationTokenSource.Cancel();
                        break;
                }
            }
        };
        var automationService = new ProjectDiscoveryAutomationService(screenCaptureService, automationInputController, automationClock, new ErrorPopupDetector(), new PlayNowButtonLocator());
        var dpi = new DpiScale(1.0, 1.0);
        AutomationSummary summary;

        // Act
        var currentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(workspace.Path);

        try
        {
            summary = automationService.AutomateCurrentScreen(dpi, cancellationTokenSource.Token);
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
        }

        // Assert
        Assert.True(summary.SlowDownPopupDetected);
        Assert.False(summary.MaximumSubmissionsReached);
        Assert.True(slowDownRecoveryObserved);
        Assert.Equal(0, summary.ClickedPointCount);
        Assert.True(automationInputController.ClickCount <= 1);
        Assert.True(captureInvocationCount >= 2);
        Assert.Contains(
            automationInputController.KeyInputs,
            keyInput => keyInput is { ModifierVirtualKey: VirtualKeys.Control, SecondModifierVirtualKey: null, VirtualKey: VirtualKeys.W });
        Assert.Contains(
            automationInputController.KeyInputs,
            keyInput => keyInput is { ModifierVirtualKey: VirtualKeys.Alt, SecondModifierVirtualKey: null, VirtualKey: VirtualKeys.L });
        Assert.DoesNotContain(
            automationInputController.KeyInputs,
            keyInput => keyInput is { ModifierVirtualKey: VirtualKeys.Alt, VirtualKey: VirtualKeys.Q });
        Assert.Contains(Delays.SubmissionWindowMs, automationInputController.Delays);
    }

    [Fact]
    public void AutomateCurrentScreen_ConnectionLostPopupAppearsAfterSubmit_StopsAutomationImmediately()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var capturePath = SyntheticDiscoveryImageFactory.GetSingleClusterImagePath();
        var connectionLostPopupPath = SyntheticDiscoveryImageFactory.GetConnectionLostPopupImagePath();

        var captureInvocationCount = 0;
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(() =>
            {
                captureInvocationCount++;
                return Cv2.ImRead(captureInvocationCount % 2 == 0 ? connectionLostPopupPath : capturePath);
            }),
            new SampleImageProcessor());
        var automationClock = new StubAutomationClock();
        using var cancellationTokenSource = new CancellationTokenSource();
        var delayCount = 0;
        var automationInputController = new StubAutomationInputController
        {
            OnDelay = milliseconds =>
            {
                automationClock.AdvanceBy(milliseconds);
                delayCount++;
                if (delayCount > 200)
                {
                    cancellationTokenSource.Cancel();
                }
            }
        };
        var automationService = new ProjectDiscoveryAutomationService(screenCaptureService, automationInputController, automationClock, new ErrorPopupDetector(), new PlayNowButtonLocator());
        var dpi = new DpiScale(1.0, 1.0);
        AutomationSummary summary;

        // Act
        var currentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(workspace.Path);

        try
        {
            summary = automationService.AutomateCurrentScreen(dpi, cancellationTokenSource.Token);
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
        }

        // Assert
        Assert.True(summary.ConnectionLostDetected);
        Assert.False(summary.MaximumSubmissionsReached);
        Assert.False(summary.SlowDownPopupDetected);
        Assert.True(captureInvocationCount >= 2);
        Assert.True(automationInputController.ClickCount >= summary.ClickedPointCount + 1);
        Assert.Single(automationInputController.KeyInputs);
        AssertKey(automationInputController.KeyInputs[0], VirtualKeys.Enter);
        Assert.Contains(Delays.ConnectionLostExitMs, automationInputController.Delays);
    }

    [Fact]
    public void AutomateCurrentScreen_MaximumSubmissionsPopupAppearsWithNoNextPilotConfigured_LogsOutWithoutPilotSelection()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var capturePath = SyntheticDiscoveryImageFactory.GetTwoClusterImagePath();
        var popupPath = SyntheticDiscoveryImageFactory.GetMaximumSubmissionsPopupImagePath();

        var captureInvocationCount = 0;
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(() =>
            {
                captureInvocationCount++;
                return Cv2.ImRead(captureInvocationCount == 1 ? capturePath : popupPath);
            }),
            new SampleImageProcessor());
        using var cancellationTokenSource = new CancellationTokenSource();
        var automationInputController = new StubAutomationInputController();
        var automationService = new ProjectDiscoveryAutomationService(screenCaptureService, automationInputController, new StubAutomationClock(), new ErrorPopupDetector(), new PlayNowButtonLocator());
        var dpi = new DpiScale(1.0, 1.0);
        AutomationSummary summary;

        // Act
        var currentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(workspace.Path);

        try
        {
            summary = automationService.AutomateCurrentScreen(dpi, cancellationTokenSource.Token);
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
        }

        // Assert
        Assert.True(summary.MaximumSubmissionsReached);
        Assert.False(summary.PilotSwitchSucceeded);
        Assert.True(summary.NoFurtherPilotsAvailable);
        Assert.Equal(1, summary.CurrentPilotIndex);
        Assert.Equal(1, summary.TargetPilotIndex);
        Assert.Equal(2, captureInvocationCount);
        Assert.Equal(summary.ClickedPointCount + 1, automationInputController.ClickCount);
        Assert.True(File.Exists(Path.Combine(workspace.Path, summary.FocusedCapturePath)));
        Assert.True(CountMaximumSubmissionsDebugOverlayPixels(Path.Combine(workspace.Path, summary.FocusedCapturePath)) > 0);
        Assert.Null(summary.PilotSwitchCapturePath);
        Assert.Empty(automationInputController.KeyInputs);
        Assert.True(automationInputController.QuitGameCalled);
        Assert.Contains(Delays.AutomationStartupDelayMs, automationInputController.Delays);
    }

    [Fact]
    public void ScalePointForDpi_PointUsesScaledDisplayCoordinates_ReturnsDevicePixelPoint()
    {
        // Arrange
        var point = new Point(1065, 650);
        var dpi = new DpiScale(1.25, 1.25);

        // Act
        var scaledPoint = ProjectDiscoveryAutomationService.ScalePointForDpi(point, dpi);

        // Assert
        Assert.Equal(1331, scaledPoint.X);
        Assert.Equal(813, scaledPoint.Y);
    }

    [Fact]
    public void PrepareAutomationFromLauncherStartup_PlayButtonIsMissing_DrawsDebugOverlayWithoutInput()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var startupCapturePath = Path.Combine(workspace.Path, "startup.png");
        WriteBlankStartupScreen(startupCapturePath);
        var captureInvocationCount = 0;
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(() =>
            {
                captureInvocationCount++;
                return Cv2.ImRead(startupCapturePath);
            }),
            new SampleImageProcessor());
        var automationInputController = new StubAutomationInputController();
        var automationService = new ProjectDiscoveryAutomationService(screenCaptureService, automationInputController, new StubAutomationClock(), new ErrorPopupDetector(), new PlayNowButtonLocator());
        StartupAutomationSummary summary;

        // Act
        var currentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(workspace.Path);

        try
        {
            summary = automationService.PrepareAutomationFromLauncherStartup(1, CancellationToken.None);
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
        }

        // Assert
        Assert.False(summary.PlayButtonFound);
        Assert.False(summary.ShouldStartAutomation);
        Assert.Equal(1, captureInvocationCount);
        Assert.Empty(automationInputController.MoveTargets);
        Assert.Equal(0, automationInputController.ClickCount);
        Assert.Empty(automationInputController.KeyInputs);
        Assert.True(File.Exists(Path.Combine(workspace.Path, summary.PlayButtonCapturePath)));
        Assert.True(CountDebugOverlayPixels(Path.Combine(workspace.Path, summary.PlayButtonCapturePath)) > 0);
    }

    [Fact]
    public void PrepareAutomationFromLauncherStartup_DebugImagesDisabled_DeletesStartupTraceImages()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var startupCapturePath = Path.Combine(workspace.Path, "startup.png");
        WriteBlankStartupScreen(startupCapturePath);
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(() => Cv2.ImRead(startupCapturePath)),
            new SampleImageProcessor());
        var automationInputController = new StubAutomationInputController();
        var automationService = new ProjectDiscoveryAutomationService(screenCaptureService, automationInputController, new StubAutomationClock(), new ErrorPopupDetector(), new PlayNowButtonLocator())
        {
            KeepDebugImages = false
        };
        StartupAutomationSummary summary;

        // Act
        var currentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(workspace.Path);

        try
        {
            summary = automationService.PrepareAutomationFromLauncherStartup(1, CancellationToken.None);
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
        }

        // Assert
        Assert.False(summary.PlayButtonFound);
        Assert.False(File.Exists(Path.Combine(workspace.Path, summary.PlayButtonCapturePath)));
        Assert.True(File.Exists(startupCapturePath));
    }

    [Fact]
    public void PrepareAutomationFromLauncherStartup_PlayButtonAndPilotExist_ClicksLauncherAndUnlocksPilot()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var playButtonLocation = new Point(320, 140);
        var startupCapturePath = Path.Combine(workspace.Path, "startup.png");
        var pilotSelectionScreenPath = Path.Combine(workspace.Path, "pilot-selection.png");
        var pilotDirectory = Path.Combine(workspace.Path, "pilot");
        WritePlayButtonScreen(startupCapturePath, playButtonLocation);
        SyntheticCommonImageFactory.CopyPilotAvatarTemplatesTo(pilotDirectory, 1);
        SyntheticCommonImageFactory.CopyLoginPilotSelectionScreenTo(pilotSelectionScreenPath);

        var captureInvocationCount = 0;
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(() =>
            {
                captureInvocationCount++;
                return Cv2.ImRead(captureInvocationCount == 1 ? startupCapturePath : pilotSelectionScreenPath);
            }),
            new SampleImageProcessor());
        var automationInputController = new StubAutomationInputController();
        var automationService = new ProjectDiscoveryAutomationService(screenCaptureService, automationInputController, new StubAutomationClock(), new ErrorPopupDetector(), new PlayNowButtonLocator());
        StartupAutomationSummary summary;

        // Act
        var currentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(workspace.Path);

        try
        {
            summary = automationService.PrepareAutomationFromLauncherStartup(1, CancellationToken.None);
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
        }

        // Assert
        Assert.True(summary.PlayButtonFound);
        Assert.True(summary.PilotLocated);
        Assert.True(summary.ShouldStartAutomation);
        Assert.Equal(2, captureInvocationCount);
        Assert.Equal(2, automationInputController.ClickCount);
        Assert.Equal([Delays.ProjectDiscoveryLauncherStartupMs, Delays.ProjectDiscoveryLauncherStartupMs], automationInputController.Delays);
        Assert.Equal(3, automationInputController.KeyInputs.Count);
        AssertKeyChord(automationInputController.KeyInputs[0], VirtualKeys.Control, VirtualKeys.W);
        AssertKeyChord(automationInputController.KeyInputs[1], VirtualKeys.Control, VirtualKeys.Shift, VirtualKeys.F9);
        AssertKeyChord(automationInputController.KeyInputs[2], VirtualKeys.Alt, VirtualKeys.L);
        Assert.InRange(automationInputController.MoveTargets[^1].X, 800, 1800);
        Assert.InRange(automationInputController.MoveTargets[^1].Y, 740, 840);
        Assert.InRange(automationInputController.MoveTargets[0].X, 1200, 1900);
        Assert.InRange(automationInputController.MoveTargets[0].Y, 250, 700);
    }

    [Fact]
    public void PrepareAutomationFromLauncherStartup_PilotIsMissing_DrawsDebugOverlayWithoutUnlock()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var startupCapturePath = Path.Combine(workspace.Path, "startup.png");
        var pilotSelectionScreenPath = Path.Combine(workspace.Path, "pilot-selection.png");
        WritePlayButtonScreen(startupCapturePath, new Point(320, 140));
        WriteBlankStartupScreen(pilotSelectionScreenPath);

        var captureInvocationCount = 0;
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(() =>
            {
                captureInvocationCount++;
                return Cv2.ImRead(captureInvocationCount == 1 ? startupCapturePath : pilotSelectionScreenPath);
            }),
            new SampleImageProcessor());
        var automationInputController = new StubAutomationInputController();
        var automationService = new ProjectDiscoveryAutomationService(screenCaptureService, automationInputController, new StubAutomationClock(), new ErrorPopupDetector(), new PlayNowButtonLocator());
        StartupAutomationSummary summary;

        // Act
        var currentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(workspace.Path);

        try
        {
            summary = automationService.PrepareAutomationFromLauncherStartup(1, CancellationToken.None);
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
        }

        // Assert
        Assert.True(summary.PlayButtonFound);
        Assert.False(summary.PilotLocated);
        Assert.False(summary.ShouldStartAutomation);
        Assert.Equal(2, captureInvocationCount);
        Assert.Equal(1, automationInputController.ClickCount);
        Assert.Equal([Delays.ProjectDiscoveryLauncherStartupMs], automationInputController.Delays);
        Assert.Single(automationInputController.KeyInputs);
        AssertKeyChord(automationInputController.KeyInputs[0], VirtualKeys.Control, VirtualKeys.W);
        Assert.NotNull(summary.PilotCapturePath);
        Assert.True(CountPilotNotFoundDebugOverlayPixels(Path.Combine(workspace.Path, summary.PilotCapturePath)) > 0);
    }

    [Fact]
    public void AutomateCurrentScreen_CancellationRequested_StopsBeforeAnyClicks()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(() => throw new InvalidOperationException("Capture should not run when automation is already canceled.")),
            new SampleImageProcessor());
        var automationClock = new StubAutomationClock();
        var automationInputController = new StubAutomationInputController
        {
            OnDelay = milliseconds => automationClock.AdvanceBy(milliseconds)
        };
        var automationService = new ProjectDiscoveryAutomationService(screenCaptureService, automationInputController, automationClock, new ErrorPopupDetector(), new PlayNowButtonLocator());
        var dpi = new DpiScale(1.0, 1.0);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        // Act
        var currentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(workspace.Path);

        try
        {
            Assert.Throws<OperationCanceledException>(() => automationService.AutomateCurrentScreen(dpi, cancellationTokenSource.Token));
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
        }

        // Assert
        Assert.Empty(automationInputController.MoveTargets);
        Assert.Equal(0, automationInputController.ClickCount);
    }

    [Fact]
    public void AutomateCurrentScreen_SixthSubmitWouldOccurInsideWindow_WaitsUntilWindowExpires()
    {
        // Arrange
        var limiterType = typeof(ProjectDiscoveryAutomationService).GetNestedType("AutomationSubmitRateLimiter", BindingFlags.NonPublic);
        Assert.NotNull(limiterType);
        var limiter = Activator.CreateInstance(limiterType!);
        Assert.NotNull(limiter);
        var recordSubmit = limiterType!.GetMethod("RecordSubmit", BindingFlags.Instance | BindingFlags.Public);
        var getDelayBeforeNextSubmit = limiterType.GetMethod("GetDelayBeforeNextSubmit", BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(recordSubmit);
        Assert.NotNull(getDelayBeforeNextSubmit);

        var startedAt = DateTime.UtcNow;
        var submitTimes = new List<DateTime>();
        for (var submitIndex = 0; submitIndex < 5; submitIndex++)
        {
            var submitAt = startedAt.AddSeconds(submitIndex);
            submitTimes.Add(submitAt);
            recordSubmit!.Invoke(limiter, [submitAt]);
        }
        var beforeSixthSubmitAt = startedAt.AddSeconds(5);

        // Act
        var delayBeforeSixthSubmit = (TimeSpan)getDelayBeforeNextSubmit!.Invoke(limiter, [beforeSixthSubmitAt])!;
        var sixthSubmitAt = beforeSixthSubmitAt.Add(delayBeforeSixthSubmit);
        submitTimes.Add(sixthSubmitAt);
        var delayAfterWindowExpires = (TimeSpan)getDelayBeforeNextSubmit.Invoke(
            limiter,
            [startedAt.AddMilliseconds(Delays.SubmissionWindowMs + 1)])!;

        // Assert
        Assert.True(submitTimes.Count >= 6);
        Assert.True(delayBeforeSixthSubmit > TimeSpan.Zero);
        Assert.True((submitTimes[5] - submitTimes[0]).TotalMilliseconds >= Delays.SubmissionWindowMs);
        Assert.Equal(TimeSpan.Zero, delayAfterWindowExpires);
        AssertNoMoreThanFiveSubmissionsPerMinute(submitTimes);
    }

    [Fact]
    public void AutomateCurrentScreen_PilotSwitchHappensAfterFifthSubmit_PreservesRateLimitForNextPilot()
    {
        // Arrange
        var limiterType = typeof(ProjectDiscoveryAutomationService).GetNestedType("AutomationSubmitRateLimiter", BindingFlags.NonPublic);
        Assert.NotNull(limiterType);
        var limiter = Activator.CreateInstance(limiterType!);
        Assert.NotNull(limiter);
        var recordSubmit = limiterType!.GetMethod("RecordSubmit", BindingFlags.Instance | BindingFlags.Public);
        var getDelayBeforeNextSubmit = limiterType.GetMethod("GetDelayBeforeNextSubmit", BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(recordSubmit);
        Assert.NotNull(getDelayBeforeNextSubmit);
        var startedAt = DateTime.UtcNow;

        for (var submitIndex = 0; submitIndex < 5; submitIndex++)
        {
            recordSubmit!.Invoke(limiter, [startedAt.AddSeconds(submitIndex)]);
        }

        // Simulate pilot switch; limiter must still preserve submission window budget.
        var pilotSwitchedAt = startedAt.AddSeconds(5);

        // Act
        var delayAfterSwitch = (TimeSpan)getDelayBeforeNextSubmit!.Invoke(limiter, [pilotSwitchedAt])!;
        var delayAfterWindowExpires = (TimeSpan)getDelayBeforeNextSubmit.Invoke(
            limiter,
            [startedAt.AddMilliseconds(Delays.SubmissionWindowMs + 1)])!;

        // Assert
        Assert.True(delayAfterSwitch > TimeSpan.Zero);
        Assert.True(delayAfterSwitch.TotalMilliseconds >= Delays.SubmissionWindowMs - 5_000);
        Assert.Equal(TimeSpan.Zero, delayAfterWindowExpires);
    }

    [Fact]
    public void AutomateCurrentScreen_PlayfieldMissingFiveTimesInARow_StopsAutomation()
    {
        // Arrange
        using var workspace = new TemporaryDirectory();
        var capturePath = Path.Combine(workspace.Path, "blank-capture.png");
        WriteBlankStartupScreen(capturePath);
        var captureInvocationCount = 0;
        var screenCaptureService = new ScreenCaptureService(
            new StubScreenCaptureProvider(() =>
            {
                captureInvocationCount++;
                return Cv2.ImRead(capturePath);
            }),
            new SampleImageProcessor());
        var automationClock = new StubAutomationClock();
        var automationInputController = new StubAutomationInputController
        {
            OnDelay = milliseconds => automationClock.AdvanceBy(milliseconds)
        };
        var automationService = new ProjectDiscoveryAutomationService(screenCaptureService, automationInputController, automationClock, new ErrorPopupDetector(), new PlayNowButtonLocator());
        var dpi = new DpiScale(1.0, 1.0);
        AutomationSummary summary;

        // Act
        var currentDirectory = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(workspace.Path);

        try
        {
            summary = automationService.AutomateCurrentScreen(dpi, CancellationToken.None);
        }
        finally
        {
            Directory.SetCurrentDirectory(currentDirectory);
        }

        // Assert
        Assert.True(summary.PlayfieldMissingLimitReached);
        Assert.False(summary.CaptureSummary.Analysis.Result.PlayfieldFound);
        Assert.False(summary.MaximumSubmissionsReached);
        Assert.Equal(0, summary.ClickedPointCount);
        Assert.Null(summary.ControlButtonBounds);
        Assert.Equal(string.Empty, summary.FocusedCapturePath);
        Assert.True(summary.RestartFromLauncherRequested);
        Assert.Equal(1, summary.CurrentPilotIndex);
        Assert.Equal(9, captureInvocationCount);
        Assert.Contains(Delays.AutomationStartupDelayMs, automationInputController.Delays);
        Assert.Empty(automationInputController.KeyInputs);
        Assert.True(automationInputController.QuitGameCalled);
    }

    private static int CountMaximumSubmissionsDebugOverlayPixels(string imagePath)
    {
        using var image = Cv2.ImRead(imagePath);
        if (image.Empty())
        {
            return 0;
        }

        using var region = new Mat(image, new Rect(0, 0, Math.Min(700, image.Width), Math.Min(80, image.Height)));
        using var mask = new Mat();
        Cv2.InRange(region, new Scalar(70, 110, 240), new Scalar(90, 130, 255), mask);
        return Cv2.CountNonZero(mask);
    }

    private static int CountPilotNotFoundDebugOverlayPixels(string imagePath)
    {
        return CountDebugOverlayPixels(imagePath);
    }

    private static int CountDebugOverlayPixels(string imagePath)
    {
        using var image = Cv2.ImRead(imagePath);
        if (image.Empty())
        {
            return 0;
        }

        using var region = new Mat(image, new Rect(0, 0, Math.Min(700, image.Width), Math.Min(80, image.Height)));
        using var mask = new Mat();
        Cv2.InRange(region, new Scalar(70, 110, 240), new Scalar(90, 130, 255), mask);
        return Cv2.CountNonZero(mask);
    }

    private static void WriteBlankStartupScreen(string outputPath)
    {
        using var image = new Mat(new Size(900, 640), MatType.CV_8UC3, new Scalar(18, 18, 18));
        Cv2.ImWrite(outputPath, image);
    }

    private static void WritePlayButtonScreen(string outputPath, Point playButtonLocation)
    {
        _ = playButtonLocation;
        SyntheticCommonImageFactory.CopyPlayButtonScreenTo(outputPath);
    }

    private static void AssertKey(
        KeyboardInput keyInput,
        ushort virtualKey)
    {
        Assert.Null(keyInput.ModifierVirtualKey);
        Assert.Null(keyInput.SecondModifierVirtualKey);
        Assert.Equal(virtualKey, keyInput.VirtualKey);
    }

    // ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local
    private static void AssertKeyChord(
        KeyboardInput keyInput,
        ushort modifierVirtualKey,
        ushort virtualKey)
    {
        Assert.Equal(modifierVirtualKey, keyInput.ModifierVirtualKey);
        Assert.Null(keyInput.SecondModifierVirtualKey);
        Assert.Equal(virtualKey, keyInput.VirtualKey);
    }

    private static void AssertKeyChord(
        KeyboardInput keyInput,
        ushort firstModifierVirtualKey,
        ushort secondModifierVirtualKey,
        ushort virtualKey)
    {
        Assert.Equal(firstModifierVirtualKey, keyInput.ModifierVirtualKey);
        Assert.Equal(secondModifierVirtualKey, keyInput.SecondModifierVirtualKey);
        Assert.Equal(virtualKey, keyInput.VirtualKey);
    }
    // ReSharper restore ParameterOnlyUsedForPreconditionCheck.Local

    private static void AssertNoMoreThanFiveSubmissionsPerMinute(IReadOnlyList<DateTime> submitTimes)
    {
        foreach (var windowStartedAt in submitTimes)
        {
            var windowEndedAt = windowStartedAt.AddMinutes(1);
            var at = windowStartedAt;
            var submissionsInWindow = submitTimes.Count(submitTime => submitTime >= at && submitTime < windowEndedAt);
            Assert.True(submissionsInWindow <= 5);
        }
    }
}
// ReSharper restore AccessToDisposedClosure
