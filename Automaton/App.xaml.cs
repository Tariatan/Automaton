using System.Windows;
using Automaton.Helpers;
using Automaton.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Automaton;

public partial class App
{
    private ServiceProvider? m_ServiceProvider;

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
        }
        finally
        {
            m_ServiceProvider?.Dispose();
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
