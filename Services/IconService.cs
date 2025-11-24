using Microsoft.Extensions.Logging;
using System.IO;
using System.Drawing;

namespace OmniPresence.Services;

public class IconService
{
    private readonly ILogger<IconService> _logger;
    private readonly Dictionary<string, Icon> _icons = new();

    public IconService(ILogger<IconService> logger)
    {
        _logger = logger;
        LoadIcons();
    }

    private void LoadIcons()
    {
        try
        {
            var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Icons");

            if (!Directory.Exists(iconPath))
            {
                _logger.LogWarning("Icons directory not found at {IconPath}", iconPath);
                return;
            }

            var statuses = new[] { "Available", "Away", "Busy", "DND", "Offline", "Unknown" };
            foreach (var status in statuses)
            {
                var iconFile = Path.Combine(iconPath, $"{status}.png");
                if (File.Exists(iconFile))
                {
                    try
                    {
                        using (var bitmap = new Bitmap(iconFile))
                        {
                            var icon = Icon.FromHandle(bitmap.GetHicon());
                            _icons[status] = icon;
                            _logger.LogInformation("Loaded icon for status: {Status}", status);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to load icon for status {Status} from {IconFile}", status, iconFile);
                    }
                }
                else
                {
                    _logger.LogWarning("Icon file not found for status {Status}: {IconFile}", status, iconFile);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading icons");
        }
    }

    public Icon? GetIconForStatus(string status)
    {
        if (_icons.TryGetValue(status, out var icon))
        {
            return icon;
        }

        _logger.LogWarning("No icon found for status: {Status}", status);
        return _icons.TryGetValue("Unknown", out var unknownIcon) ? unknownIcon : null;
    }
}
