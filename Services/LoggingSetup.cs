using Serilog;
using Serilog.Core;
using System.IO;

namespace OmniPresence.Services;

public static class LoggingSetup
{
    private static GuiLogSink? _guiLogSink;

    public static Logger ConfigureLogging(GuiLogSink? guiLogSink = null)
    {
        _guiLogSink = guiLogSink;

        var config = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");

        // Add GUI sink if provided
        if (guiLogSink != null)
        {
            config.WriteTo.Sink(guiLogSink);
        }

        config.WriteTo.File(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "OmniPresence",
                "Logs",
                "log-.log"),
            rollingInterval: Serilog.RollingInterval.Day,
            retainedFileCountLimit: 10,
            outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] {Message:lj}{NewLine}{Exception}");

        return config.CreateLogger();
    }

    public static void CloseAndFlush()
    {
        Log.CloseAndFlush();
    }

    public static GuiLogSink? GetGuiLogSink() => _guiLogSink;
}
