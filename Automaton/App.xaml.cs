using System.Windows;
using Automaton.Helpers;
using Automaton.Infrastructure;
using Serilog;

namespace Automaton;

public partial class App
{
    protected override void OnStartup(StartupEventArgs e)
    {
        var logFilePath = ApplicationLogging.Configure();
        Log.ForContext<App>().Information(
            "Automaton started. LogFilePath={LogFilePath}, Arguments={Arguments}",
            logFilePath,
            e.Args);
        Log.ForContext<App>().Information(
            "Storage roots. ConfiguredTelemetryRoot={ConfiguredTelemetryRoot}, ConfiguredHallmarkRoot={ConfiguredHallmarkRoot}, EffectiveCapturesDirectory={EffectiveCapturesDirectory}, EffectiveLogsDirectory={EffectiveLogsDirectory}, EffectiveExpectedDirectory={EffectiveExpectedDirectory}",
            TelemetryRootDirectory.GetConfiguredRootDirectory(),
            TelemetryRootDirectory.GetConfiguredHallmarkRootDirectory(),
            TelemetryRootDirectory.GetCapturesDirectory(),
            TelemetryRootDirectory.GetLogsDirectory(),
            TelemetryRootDirectory.GetExpectedDirectory());

        try
        {
            var startupOptions = ApplicationStartupOptions.Parse(e.Args);
            if (startupOptions.ProcessSamples)
            {
                RunSampleProcessing();
                Shutdown();
                return;
            }

            var window = new MainWindow(startupOptions.AutomationMode, startupOptions.AutoStartAutomation);
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
        }
        finally
        {
            Log.CloseAndFlush();
            base.OnExit(e);
        }
    }

    private static void RunSampleProcessing()
    {
        Log.ForContext<App>().Information("Command-line sample processing started.");
        var processor = new SampleImageProcessor();
        var summary = processor.ProcessSamples();
        Log.ForContext<App>().Information(
            "Command-line sample processing finished. SamplesDirectory={SamplesDirectory}, ResultCount={ResultCount}",
            summary.SamplesDirectory,
            summary.Results.Count);
    }
}
