using System.IO;
using System.Windows;
using Automaton.Core.Infrastructure;
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

        var startupOptions = ApplicationStartupOptions.Parse(e.Args);
        UserSettings.Initialize(startupOptions.SettingsFilePath ?? PrivateSettings.SettingsFilePath);

        m_LogFiles = ApplicationLogging.Configure();

        Log.ForContext<App>().Information(
            "Automaton started. ActiveLogFilePath={ActiveLogFilePath}, TelemetryLogFilePath={TelemetryLogFilePath}, Arguments={Arguments}",
            m_LogFiles.ActiveLogFilePath,
            m_LogFiles.TelemetryLogFilePath,
            e.Args);
        Log.ForContext<App>().Information(
            "Storage roots. CapturesDirectory={CapturesDirectory}, LogsDirectory={LogsDirectory}, TemplatesDirectory={TemplatesDirectory}, PilotAvatarDirectory={PilotAvatarDirectory}",
            TelemetryRootDirectory.GetCapturesDirectory(),
            TelemetryRootDirectory.GetLogsDirectory(),
            TelemetryRootDirectory.GetTemplatesDirectory(DiscoverySettings.TemplatesFolderName),
            AvatarsDirectory.GetDirectory());

        try
        {
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
        if (!Directory.Exists(DiscoverySettings.SamplesFolderName))
        {
            throw new DirectoryNotFoundException($"Samples folder was not found: {DiscoverySettings.SamplesFolderName}");
        }

        var sampleFiles = SampleImageProcessor.EnumerateSampleImageFiles(DiscoverySettings.SamplesFolderName);

        if (sampleFiles.Count == 0)
        {
            throw new InvalidOperationException($"No files were found in {DiscoverySettings.SamplesFolderName}.");
        }

        var results = new List<SampleProcessingResult>(sampleFiles.Count);
        foreach (var sampleFile in sampleFiles)
        {
            using var image = Cv2.ImRead(sampleFile);
            var analysis = processor.AnalyzeImage(image, sampleFile);
            var outputPath = ImageAnnotator.WriteAnnotatedOutput(image, analysis, sampleFile);
            results.Add(analysis.Result with { OutputPath = outputPath });
        }

        Log.ForContext<App>().Information(
            "Command-line sample processing finished. SamplesDirectory={SamplesDirectory}, ResultCount={ResultCount}",
            DiscoverySettings.SamplesFolderName,
            results.Count);
    }
}