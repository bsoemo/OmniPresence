using System.Windows.Forms;
using OmniPresence;
using OmniPresence.Services;

namespace OmniPresence;

public class TrayIcon : IDisposable
{
    private readonly IStatusService _statusService;
    private readonly IConfigurationService _configService;
    private readonly IHomeAssistantClient _haClient;
    private readonly IconService _iconService;
    private readonly GuiLogSink _guiLogSink;
    private NotifyIcon _notifyIcon;
    private ConfigurationForm? _configForm;

    public TrayIcon(IStatusService statusService, IConfigurationService configService, IHomeAssistantClient haClient, IconService iconService, GuiLogSink guiLogSink)
    {
        _statusService = statusService;
        _configService = configService;
        _haClient = haClient;
        _iconService = iconService;
        _guiLogSink = guiLogSink;
        _notifyIcon = new NotifyIcon();
        InitializeNotifyIcon();
        SubscribeToStatusChanges();
    }

    private void InitializeNotifyIcon()
    {
        _notifyIcon.Icon = _iconService.GetIconForStatus(_statusService.CurrentStatus) ?? SystemIcons.Application;
        _notifyIcon.Visible = true;
        _notifyIcon.Text = $"OmniPresence - {_statusService.CurrentStatus}";

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("Configure", null, (s, e) => ShowConfigurationForm());
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("Exit", null, (s, e) => ExitApplication());

        _notifyIcon.ContextMenuStrip = contextMenu;
    }

    private void ShowConfigurationForm()
    {
        if (_configForm == null || _configForm.IsDisposed)
        {
            _configForm = new ConfigurationForm(_statusService, _configService, _haClient, _guiLogSink);
        }

        _configForm.Show();
        _configForm.Activate();
        _configForm.BringToFront();
    }

    private void ExitApplication()
    {
        Application.Exit();
    }

    private void SubscribeToStatusChanges()
    {
        _statusService.StatusChanged += (s, e) =>
        {
            _notifyIcon.Icon = _iconService.GetIconForStatus(e.NewStatus) ?? SystemIcons.Application;
            _notifyIcon.Text = $"OmniPresence - {e.NewStatus}";
        };
    }

    public void Dispose()
    {
        _notifyIcon?.Dispose();
        _configForm?.Dispose();
        GC.SuppressFinalize(this);
    }
}
