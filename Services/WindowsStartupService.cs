using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System.Diagnostics;

namespace OmniPresence.Services;

public class WindowsStartupService
{
    private readonly ILogger _logger;
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "OmniPresence";

    public WindowsStartupService(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Enables or disables OmniPresence to start at Windows startup
    /// </summary>
    public bool SetStartupEnabled(bool enabled)
    {
        try
        {
            if (enabled)
            {
                return EnableStartup();
            }
            else
            {
                return DisableStartup();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting startup state");
            return false;
        }
    }

    /// <summary>
    /// Checks if OmniPresence is set to start at Windows startup
    /// </summary>
    public bool IsStartupEnabled()
    {
        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey))
            {
                if (key == null)
                {
                    _logger.LogDebug("Run registry key not found");
                    return false;
                }

                var value = key.GetValue(AppName);
                bool isEnabled = value != null && !string.IsNullOrEmpty(value.ToString());
                _logger.LogInformation("Startup enabled check: {IsEnabled}", isEnabled);
                return isEnabled;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking startup enabled state");
            return false;
        }
    }

    private bool EnableStartup()
    {
        try
        {
            var appPath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(appPath))
            {
                _logger.LogError("Could not determine application path");
                return false;
            }

            using (var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true))
            {
                if (key == null)
                {
                    _logger.LogError("Could not open Run registry key for writing");
                    return false;
                }

                key.SetValue(AppName, appPath);
                _logger.LogInformation("Windows startup enabled. Registry entry set to: {AppPath}", appPath);
                return true;
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied when trying to enable startup. Admin rights may be required.");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enabling Windows startup");
            return false;
        }
    }

    private bool DisableStartup()
    {
        try
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true))
            {
                if (key == null)
                {
                    _logger.LogDebug("Run registry key not found, nothing to disable");
                    return true;
                }

                key.DeleteValue(AppName, throwOnMissingValue: false);
                _logger.LogInformation("Windows startup disabled. Registry entry removed.");
                return true;
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied when trying to disable startup. Admin rights may be required.");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disabling Windows startup");
            return false;
        }
    }
}
