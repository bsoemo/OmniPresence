using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniPresence;
using OmniPresence.Services;
using Serilog;

Console.WriteLine("[STARTUP] Application starting...");

try
{
    [STAThread]
    static void Main(string[] args)
    {
        Console.WriteLine("[MAIN] Entered Main method");
        Console.WriteLine("[MAIN] Configuring logging...");
        
        // Create GUI log sink first
        var guiLogSink = new OmniPresence.Services.GuiLogSink(maxLogs: 500);
        
        Log.Logger = OmniPresence.Services.LoggingSetup.ConfigureLogging(guiLogSink);
        Console.WriteLine("[MAIN] Logging configured");

        try
        {
            Console.WriteLine("[MAIN] Enabling visual styles...");
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);

            Console.WriteLine("[MAIN] Building DI container...");
            var serviceProvider = new ServiceCollection()
                .AddLogging(config =>
                {
                    config.ClearProviders();
                    config.AddSerilog();
                })
                .AddSingleton<IConfigurationService>(sp =>
                {
                    var logger = sp.GetRequiredService<ILogger<ConfigurationService>>();
                    return new ConfigurationService(logger);
                })
                .AddSingleton<IStatusMonitor>(sp =>
                {
                    var logger = sp.GetRequiredService<ILogger<TeamsLogMonitor>>();
                    var config = sp.GetRequiredService<IConfigurationService>();
                    return new TeamsLogMonitor(logger, config);
                })
                .AddSingleton<IHomeAssistantClient>(sp =>
                {
                    var logger = sp.GetRequiredService<ILogger<HomeAssistantClient>>();
                    var config = sp.GetRequiredService<IConfigurationService>();
                    return new HomeAssistantClient(logger, config);
                })
                .AddSingleton<IStatusService>(sp =>
                {
                    var logger = sp.GetRequiredService<ILogger<StatusService>>();
                    var monitor = sp.GetRequiredService<IStatusMonitor>();
                    var client = sp.GetRequiredService<IHomeAssistantClient>();
                    var config = sp.GetRequiredService<IConfigurationService>();
                    return new StatusService(logger, monitor, client, config);
                })
                .AddSingleton<IconService>(sp =>
                {
                    var logger = sp.GetRequiredService<ILogger<IconService>>();
                    return new IconService(logger);
                })
                .AddSingleton(guiLogSink) // Add GUI log sink to DI
                .BuildServiceProvider();

            Console.WriteLine("[MAIN] DI container built");

            var logger = serviceProvider.GetRequiredService<ILogger<ConfigurationService>>();
            var configService = serviceProvider.GetRequiredService<IConfigurationService>();

            logger.LogInformation("=== Application Starting ===");
            Console.WriteLine("[MAIN] Services retrieved");

            if (!configService.IsValid())
            {
                logger.LogError("Configuration is invalid");
                Console.WriteLine("[MAIN] Configuration invalid, showing configuration form");
                
                // Show configuration form to let user configure
                var statusService = serviceProvider.GetRequiredService<IStatusService>();
                var iconService = serviceProvider.GetRequiredService<IconService>();
                var haClient = serviceProvider.GetRequiredService<IHomeAssistantClient>();
                
                using (var configForm = new ConfigurationForm(statusService, configService, haClient, guiLogSink))
                {
                    var result = configForm.ShowDialog();
                    
                    // After user closes the form, check if configuration is now valid
                    if (!configService.IsValid())
                    {
                        logger.LogError("Configuration still invalid after user input");
                        System.Windows.Forms.MessageBox.Show(
                            "Configuration is still invalid. Please configure Home Assistant settings to continue.",
                            "Configuration Error",
                            System.Windows.Forms.MessageBoxButtons.OK,
                            System.Windows.Forms.MessageBoxIcon.Error);
                        return;
                    }
                }
                
                logger.LogInformation("Configuration now valid, continuing startup");
            }

            logger.LogInformation("Configuration valid");
            Console.WriteLine("[MAIN] Creating status service");
            var statusService_Main = serviceProvider.GetRequiredService<IStatusService>();
            var cts = new CancellationTokenSource();

            Console.WriteLine("[MAIN] Initializing status service");
            statusService_Main.InitializeAsync(cts.Token).Wait(TimeSpan.FromSeconds(5));
            logger.LogInformation("Status service initialized");

            Console.WriteLine("[MAIN] Creating tray icon");
            var iconService_Main = serviceProvider.GetRequiredService<IconService>();
            var haClient_Main = serviceProvider.GetRequiredService<IHomeAssistantClient>();
            using (var trayIcon = new TrayIcon(statusService_Main, configService, haClient_Main, iconService_Main, guiLogSink))
            {
                Console.WriteLine("[MAIN] Tray icon created");
                logger.LogInformation("Tray icon created, application ready");

            Console.WriteLine("[MAIN] Creating hidden form");
                using (var hiddenForm = new OmniPresence.HiddenForm())
                {
                    Console.WriteLine("[MAIN] Hidden form created");
                    hiddenForm.Show();
                    Console.WriteLine("[MAIN] Starting message loop");
                    System.Windows.Forms.Application.Run(hiddenForm);
                    Console.WriteLine("[MAIN] Message loop exited");
                }

                cts.Cancel();
            }

            logger.LogInformation("Application closed normally");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MAIN] Exception in try block: {ex}");
            Console.WriteLine($"[MAIN] Stack: {ex.StackTrace}");
            Log.Logger.Fatal(ex, "Application crashed");
            System.Windows.Forms.MessageBox.Show($"Error: {ex.Message}", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
        }
        finally
        {
            Console.WriteLine("[MAIN] In finally block");
            Log.Logger.Information("=== Application Shutdown ===");
            OmniPresence.Services.LoggingSetup.CloseAndFlush();
            System.Threading.Thread.Sleep(2000);
            Console.WriteLine("[MAIN] Shutdown complete");
        }
    }

    Main(args);
}
catch (Exception ex)
{
    Console.WriteLine($"[STARTUP] Fatal error: {ex}");
    Console.WriteLine($"[STARTUP] Stack: {ex.StackTrace}");
}
