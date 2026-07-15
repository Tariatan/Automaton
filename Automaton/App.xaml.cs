using System.IO;
using System.Windows;
using Automaton.Detectors;
using Automaton.Helpers;
using Automaton.Infrastructure;
using Automaton.Primitives;
using Microsoft.Extensions.DependencyInjection;
using OpenCvSharp;
using Serilog;

namespace Automaton;

public partial class App
{
    private ServiceProvider? m_ServiceProvider;
    private ApplicationLogFiles? m_LogFiles;

    protected override void OnStartup(StartupEventArgs e)
    {
        m_LogFiles = ApplicationLogging.Configure();
        Log.ForContext<App>().Information(
            "Automaton started. ActiveLogFilePath={ActiveLogFilePath}, TelemetryLogFilePath={TelemetryLogFilePath}, Arguments={Arguments}",
            m_LogFiles.ActiveLogFilePath,
            m_LogFiles.TelemetryLogFilePath,
            e.Args);
        Log.ForContext<App>().Information(
            "Storage roots. ConfiguredTelemetryRoot={ConfiguredTelemetryRoot}, ConfiguredHallmarkRoot={ConfiguredHallmarkRoot}, ConfiguredPilotAvatarDirectory={ConfiguredPilotAvatarDirectory}, EffectiveCapturesDirectory={EffectiveCapturesDirectory}, EffectiveLogsDirectory={EffectiveLogsDirectory}, EffectiveExpectedDirectory={EffectiveExpectedDirectory}, EffectivePilotAvatarDirectory={EffectivePilotAvatarDirectory}",
            TelemetryRootDirectory.GetConfiguredRootDirectory(),
            TelemetryRootDirectory.GetConfiguredHallmarkRootDirectory(),
            PilotAvatarDirectory.GetConfiguredDirectory(),
            TelemetryRootDirectory.GetCapturesDirectory(),
            TelemetryRootDirectory.GetLogsDirectory(),
            TelemetryRootDirectory.GetExpectedDirectory(),
            PilotAvatarDirectory.GetDirectory());

        try
        {
            var startupOptions = ApplicationStartupOptions.Parse(e.Args);
            if (startupOptions.ProcessSamples)
            {
                RunSampleProcessing();
                Shutdown();
                return;
            }

            var services = new ServiceCollection();
            services.AddSingleton(startupOptions);
            services.AddAutomatonServices();
            m_ServiceProvider = services.BuildServiceProvider();

            var window = m_ServiceProvider.GetRequiredService<MainWindow>();
            window.Show();
            base.OnStartup(e);
        }
        catch (Exception exception)
        {
            Log.ForContext<App>().Fatal(exception, "Automaton startup failed.");
            throw;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            Log.ForContext<App>().Information("Automaton exited. ExitCode={ExitCode}", e.ApplicationExitCode);
            Log.ForContext<App>().Information(
                "Publishing completed log file on exit. ActiveLogFilePath={ActiveLogFilePath}, TelemetryLogFilePath={TelemetryLogFilePath}",
                m_LogFiles?.ActiveLogFilePath,
                m_LogFiles?.TelemetryLogFilePath);
        }
        finally
        {
            try
            {
                m_ServiceProvider?.Dispose();
            }
            finally
            {
                Log.CloseAndFlush();
                ApplicationLogging.TryPublish(m_LogFiles);
                base.OnExit(e);
            }
        }
    }

    private static void RunSampleProcessing()
    {
        Log.ForContext<App>().Information("Command-line sample processing started.");

        var processor = new SampleImageProcessor(new PlayfieldDetector(), null);
        if (!Directory.Exists(Settings.ProjectDiscoverySamplesFolderName))
        {
            throw new DirectoryNotFoundException($"Samples folder was not found: {Settings.ProjectDiscoverySamplesFolderName}");
        }

        var sampleFiles = SampleImageProcessor.EnumerateSampleImageFiles(Settings.ProjectDiscoverySamplesFolderName);

        if (sampleFiles.Count == 0)
        {
            throw new InvalidOperationException($"No files were found in {Settings.ProjectDiscoverySamplesFolderName}.");
        }

        var results = new List<SampleProcessingResult>(sampleFiles.Count);
        foreach (var sampleFile in sampleFiles)
        {
            using var image = Cv2.ImRead(sampleFile);
            var analysis = processor.AnalyzeImage(image, sampleFile);
            var outputPath = ScreenCaptureService.WriteAnnotatedOutput(image, analysis, sampleFile);
            results.Add(analysis.Result with { OutputPath = outputPath });
        }

        Log.ForContext<App>().Information(
            "Command-line sample processing finished. SamplesDirectory={SamplesDirectory}, ResultCount={ResultCount}",
            Settings.ProjectDiscoverySamplesFolderName,
            results.Count);
    }
}
