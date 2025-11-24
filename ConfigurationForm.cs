using System;
using System.IO;
using System.Windows.Forms;
using OmniPresence.Services;
using Microsoft.Extensions.Logging;

namespace OmniPresence;

public class ConfigurationForm : Form
{
    private readonly IStatusService _statusService;
    private readonly IConfigurationService _configService;
    private readonly IHomeAssistantClient _haClient;
    private readonly GuiLogSink _guiLogSink;
    
    private TabControl _tabControl = new();
    private Label _currentStatusLabel = new();
    private Label _currentActivityLabel = new();
    private Label _lastUpdateLabel = new();
    private DateTime _lastUpdate = DateTime.Now;

    // Settings Tab Controls
    private CheckBox _startOnWindowsStartupCheckBox = new();

    // Home Assistant Tab Controls
    private CheckBox _homeAssistantEnabledCheckBox = new();
    private TextBox _haUrlTextBox = new();
    private TextBox _haTokenTextBox = new();
    private TextBox _statusSensorNameTextBox = new();
    private TextBox _activitySensorNameTextBox = new();
    private Label _haStatusLabel = new();
    private Button _testHaConnectionButton = new();

    // Logs Tab Controls
    private RichTextBox _logsTextBox = new();
    private Button _clearLogsButton = new();

    // Teams Tab Controls
    private NumericUpDown _pollingIntervalNumeric = new();
    private CheckBox _autoDetectPathCheckBox = new();
    private TextBox _teamsLogPathTextBox = new();
    private DataGridView _statusMappingsGrid = new();
    private CheckBox _teamsDebugModeCheckBox = new();

    // Buttons
    private Button _saveButton = new();
    private Button _cancelButton = new();
    private Label _messageLabel = new();

    public ConfigurationForm(IStatusService statusService, IConfigurationService configService, IHomeAssistantClient haClient, GuiLogSink guiLogSink)
    {
        _statusService = statusService;
        _configService = configService;
        _haClient = haClient;
        _guiLogSink = guiLogSink;
        InitializeComponent();
        SubscribeToStatusChanges();
        SubscribeToLogs();
        LoadCurrentConfiguration();
    }

    private void InitializeComponent()
    {
        this.Text = "OmniPresence Configuration";
        this.Size = new System.Drawing.Size(500, 750);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.FormBorderStyle = FormBorderStyle.FixedSingle;
        this.MaximizeBox = false;
        
        // Load custom favicon
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "favicon.ico");
            if (File.Exists(iconPath))
            {
                this.Icon = new System.Drawing.Icon(iconPath);
            }
        }
        catch
        {
            // If icon fails to load, just skip it
        }

        // Header Panel with Status
        var headerPanel = new Panel { Dock = DockStyle.Top, Height = 100, BackColor = System.Drawing.Color.LightGray };
        
        var headerLabel = new Label 
        { 
            Text = "Current Status", 
            Font = new System.Drawing.Font("Arial", 12, System.Drawing.FontStyle.Bold),
            Location = new System.Drawing.Point(10, 10),
            AutoSize = true
        };

        _currentStatusLabel.Text = $"Status: {_statusService.CurrentStatus}";
        _currentStatusLabel.Location = new System.Drawing.Point(10, 35);
        _currentStatusLabel.AutoSize = true;

        _currentActivityLabel.Text = $"Activity: {(_statusService.IsInCall ? "In a call" : "Not in a call")}";
        _currentActivityLabel.Location = new System.Drawing.Point(10, 55);
        _currentActivityLabel.AutoSize = true;

        _lastUpdateLabel.Text = $"Last update: {_lastUpdate:HH:mm:ss}";
        _lastUpdateLabel.Location = new System.Drawing.Point(10, 75);
        _lastUpdateLabel.AutoSize = true;

        headerPanel.Controls.Add(headerLabel);
        headerPanel.Controls.Add(_currentStatusLabel);
        headerPanel.Controls.Add(_currentActivityLabel);
        headerPanel.Controls.Add(_lastUpdateLabel);

        // Tab Control
        _tabControl.Dock = DockStyle.Fill;
        _tabControl.Margin = new Padding(5);

        // Settings Tab (first)
        var settingsTab = new TabPage("Settings");
        InitializeSettingsTab(settingsTab);
        _tabControl.TabPages.Add(settingsTab);

        // Home Assistant Tab
        var haTab = new TabPage("Home Assistant");
        InitializeHomeAssistantTab(haTab);
        _tabControl.TabPages.Add(haTab);

        // Teams Tab
        var teamsTab = new TabPage("Teams");
        InitializeTeamsTab(teamsTab);
        _tabControl.TabPages.Add(teamsTab);

        // Logs Tab
        var logsTab = new TabPage("Logs");
        InitializeLogsTab(logsTab);
        _tabControl.TabPages.Add(logsTab);

        // Message Label (positioned above buttons)
        _messageLabel.Dock = DockStyle.Top;
        _messageLabel.Height = 30;
        _messageLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
        _messageLabel.Padding = new Padding(10, 0, 0, 0);
        _messageLabel.ForeColor = System.Drawing.Color.Green;
        _messageLabel.Visible = false;

        // Bottom Panel with Buttons
        var buttonPanel = new Panel { Dock = DockStyle.Bottom, Height = 50, BackColor = System.Drawing.Color.WhiteSmoke };
        
        _saveButton.Text = "Save";
        _saveButton.Location = new System.Drawing.Point(280, 10);
        _saveButton.Size = new System.Drawing.Size(90, 30);
        _saveButton.Click += SaveButton_Click;

        _cancelButton.Text = "Cancel";
        _cancelButton.Location = new System.Drawing.Point(380, 10);
        _cancelButton.Size = new System.Drawing.Size(90, 30);
        _cancelButton.Click += (s, e) => this.Close();

        buttonPanel.Controls.Add(_saveButton);
        buttonPanel.Controls.Add(_cancelButton);

        this.Controls.Add(_tabControl);
        this.Controls.Add(buttonPanel);
        this.Controls.Add(_messageLabel);
        this.Controls.Add(headerPanel);
    }

    private void InitializeSettingsTab(TabPage tab)
    {
        tab.Padding = new Padding(10);

        var settingsLabel = new Label 
        { 
            Text = "Application Settings", 
            Font = new System.Drawing.Font("Arial", 11, System.Drawing.FontStyle.Bold),
            Location = new System.Drawing.Point(10, 10),
            AutoSize = true
        };

        _startOnWindowsStartupCheckBox.Text = "Start OmniPresence when Windows starts";
        _startOnWindowsStartupCheckBox.Location = new System.Drawing.Point(10, 40);
        _startOnWindowsStartupCheckBox.AutoSize = true;
        _startOnWindowsStartupCheckBox.Checked = _configService.StartOnWindowsStartup;

        tab.Controls.Add(settingsLabel);
        tab.Controls.Add(_startOnWindowsStartupCheckBox);
    }

    private void InitializeHomeAssistantTab(TabPage tab)
    {
        tab.Padding = new Padding(10);

        _homeAssistantEnabledCheckBox.Text = "Enable Home Assistant Integration";
        _homeAssistantEnabledCheckBox.Location = new System.Drawing.Point(10, 10);
        _homeAssistantEnabledCheckBox.AutoSize = true;
        _homeAssistantEnabledCheckBox.Checked = _configService.HomeAssistantEnabled;
        _homeAssistantEnabledCheckBox.CheckedChanged += HomeAssistantEnabledCheckBox_CheckedChanged;

        var urlLabel = new Label { Text = "Home Assistant URL:", Location = new System.Drawing.Point(10, 40), AutoSize = true };
        _haUrlTextBox.Location = new System.Drawing.Point(10, 60);
        _haUrlTextBox.Size = new System.Drawing.Size(450, 25);
        _haUrlTextBox.BorderStyle = BorderStyle.FixedSingle;
        _haUrlTextBox.Enabled = _configService.HomeAssistantEnabled;

        var tokenLabel = new Label { Text = "Long-Lived Access Token:", Location = new System.Drawing.Point(10, 95), AutoSize = true };
        _haTokenTextBox.Location = new System.Drawing.Point(10, 115);
        _haTokenTextBox.Size = new System.Drawing.Size(450, 25);
        _haTokenTextBox.BorderStyle = BorderStyle.FixedSingle;
        _haTokenTextBox.UseSystemPasswordChar = true;
        _haTokenTextBox.Enabled = _configService.HomeAssistantEnabled;

        var statusSensorLabel = new Label { Text = "Status Sensor Name:", Location = new System.Drawing.Point(10, 150), AutoSize = true };
        _statusSensorNameTextBox.Location = new System.Drawing.Point(10, 170);
        _statusSensorNameTextBox.Size = new System.Drawing.Size(450, 25);
        _statusSensorNameTextBox.BorderStyle = BorderStyle.FixedSingle;
        _statusSensorNameTextBox.Text = "sensor.teams_status";
        _statusSensorNameTextBox.Enabled = _configService.HomeAssistantEnabled;

        var activitySensorLabel = new Label { Text = "Activity Sensor Name:", Location = new System.Drawing.Point(10, 205), AutoSize = true };
        _activitySensorNameTextBox.Location = new System.Drawing.Point(10, 225);
        _activitySensorNameTextBox.Size = new System.Drawing.Size(450, 25);
        _activitySensorNameTextBox.BorderStyle = BorderStyle.FixedSingle;
        _activitySensorNameTextBox.Text = "sensor.teams_activity";
        _activitySensorNameTextBox.Enabled = _configService.HomeAssistantEnabled;

        _testHaConnectionButton.Text = "Test Connection";
        _testHaConnectionButton.Location = new System.Drawing.Point(10, 260);
        _testHaConnectionButton.Size = new System.Drawing.Size(120, 30);
        _testHaConnectionButton.Click += TestHaConnection_Click;
        _testHaConnectionButton.Enabled = _configService.HomeAssistantEnabled;

        _haStatusLabel.Text = "Status: Not tested";
        _haStatusLabel.Location = new System.Drawing.Point(10, 300);
        _haStatusLabel.AutoSize = true;
        _haStatusLabel.ForeColor = System.Drawing.Color.Gray;

        tab.Controls.Add(_homeAssistantEnabledCheckBox);
        tab.Controls.Add(urlLabel);
        tab.Controls.Add(_haUrlTextBox);
        tab.Controls.Add(tokenLabel);
        tab.Controls.Add(_haTokenTextBox);
        tab.Controls.Add(statusSensorLabel);
        tab.Controls.Add(_statusSensorNameTextBox);
        tab.Controls.Add(activitySensorLabel);
        tab.Controls.Add(_activitySensorNameTextBox);
        tab.Controls.Add(_testHaConnectionButton);
        tab.Controls.Add(_haStatusLabel);
    }

    private void HomeAssistantEnabledCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        _haUrlTextBox.Enabled = _homeAssistantEnabledCheckBox.Checked;
        _haTokenTextBox.Enabled = _homeAssistantEnabledCheckBox.Checked;
        _statusSensorNameTextBox.Enabled = _homeAssistantEnabledCheckBox.Checked;
        _activitySensorNameTextBox.Enabled = _homeAssistantEnabledCheckBox.Checked;
        _testHaConnectionButton.Enabled = _homeAssistantEnabledCheckBox.Checked;
    }

    private void InitializeLogsTab(TabPage tab)
    {
        tab.Padding = new Padding(10);

        _logsTextBox.Dock = DockStyle.Fill;
        _logsTextBox.ReadOnly = true;
        _logsTextBox.BackColor = System.Drawing.Color.Black;
        _logsTextBox.ForeColor = System.Drawing.Color.LimeGreen;
        _logsTextBox.Font = new System.Drawing.Font("Consolas", 9);

        var buttonPanel = new Panel { Dock = DockStyle.Bottom, Height = 40, Padding = new Padding(10) };
        _clearLogsButton.Text = "Clear Logs";
        _clearLogsButton.Size = new System.Drawing.Size(100, 30);
        _clearLogsButton.Click += (s, e) =>
        {
            _logsTextBox.Clear();
            _guiLogSink.Clear();
        };
        buttonPanel.Controls.Add(_clearLogsButton);

        tab.Controls.Add(_logsTextBox);
        tab.Controls.Add(buttonPanel);
    }

    private void InitializeTeamsTab(TabPage tab)
    {
        tab.Padding = new Padding(10);
        var yPos = 10;
        
        // Polling Interval
        var pollingLabel = new Label { Text = "Polling Interval (seconds):", Location = new System.Drawing.Point(10, yPos), AutoSize = true };
        yPos += 25;
        _pollingIntervalNumeric.Location = new System.Drawing.Point(10, yPos);
        _pollingIntervalNumeric.Size = new System.Drawing.Size(100, 25);
        _pollingIntervalNumeric.Minimum = 1;
        _pollingIntervalNumeric.Maximum = 300;
        _pollingIntervalNumeric.Value = _configService.TeamsPollingIntervalSeconds;
        _pollingIntervalNumeric.BorderStyle = BorderStyle.FixedSingle;
        yPos += 35;
        
        // Auto-detect Log Path
        _autoDetectPathCheckBox.Text = "Auto-detect Teams log path";
        _autoDetectPathCheckBox.Location = new System.Drawing.Point(10, yPos);
        _autoDetectPathCheckBox.AutoSize = true;
        _autoDetectPathCheckBox.Checked = _configService.TeamsAutoDetectLogPath;
        _autoDetectPathCheckBox.CheckedChanged += AutoDetectPathCheckBox_CheckedChanged;
        yPos += 30;
        
        // Log Path TextBox
        var logPathLabel = new Label { Text = "Teams Log Path:", Location = new System.Drawing.Point(10, yPos), AutoSize = true };
        yPos += 25;
        _teamsLogPathTextBox.Location = new System.Drawing.Point(10, yPos);
        _teamsLogPathTextBox.Size = new System.Drawing.Size(450, 25);
        _teamsLogPathTextBox.BorderStyle = BorderStyle.FixedSingle;
        _teamsLogPathTextBox.Text = _configService.TeamsLogPath;
        _teamsLogPathTextBox.ReadOnly = _configService.TeamsAutoDetectLogPath;
        _teamsLogPathTextBox.BackColor = _configService.TeamsAutoDetectLogPath ? System.Drawing.Color.LightGray : System.Drawing.Color.White;
        yPos += 35;
        
        // Status Mappings
        var mappingsLabel = new Label { Text = "Status Mappings:", Location = new System.Drawing.Point(10, yPos), AutoSize = true };
        yPos += 25;
        _statusMappingsGrid.Location = new System.Drawing.Point(10, yPos);
        _statusMappingsGrid.Size = new System.Drawing.Size(450, 100);
        _statusMappingsGrid.ColumnCount = 2;
        _statusMappingsGrid.Columns[0].Name = "Teams Status";
        _statusMappingsGrid.Columns[0].Width = 225;
        _statusMappingsGrid.Columns[1].Name = "Display Name";
        _statusMappingsGrid.Columns[1].Width = 225;
        _statusMappingsGrid.AllowUserToAddRows = false;
        _statusMappingsGrid.AllowUserToDeleteRows = false;
        _statusMappingsGrid.ReadOnly = false;
        _statusMappingsGrid.RowHeadersVisible = false;
        
        // Populate status mappings
        foreach (var mapping in _configService.TeamsStatusMappings)
        {
            _statusMappingsGrid.Rows.Add(mapping.Key, mapping.Value);
        }
        yPos += 110;
        
        // Debug Mode
        _teamsDebugModeCheckBox.Text = "Enable Teams debug mode (verbose logging)";
        _teamsDebugModeCheckBox.Location = new System.Drawing.Point(10, yPos);
        _teamsDebugModeCheckBox.AutoSize = true;
        _teamsDebugModeCheckBox.Checked = _configService.TeamsDebugMode;
        
        tab.Controls.Add(pollingLabel);
        tab.Controls.Add(_pollingIntervalNumeric);
        tab.Controls.Add(_autoDetectPathCheckBox);
        tab.Controls.Add(logPathLabel);
        tab.Controls.Add(_teamsLogPathTextBox);
        tab.Controls.Add(mappingsLabel);
        tab.Controls.Add(_statusMappingsGrid);
        tab.Controls.Add(_teamsDebugModeCheckBox);
    }

    private void AutoDetectPathCheckBox_CheckedChanged(object? sender, EventArgs e)
    {
        _teamsLogPathTextBox.ReadOnly = _autoDetectPathCheckBox.Checked;
        _teamsLogPathTextBox.BackColor = _autoDetectPathCheckBox.Checked ? System.Drawing.Color.LightGray : System.Drawing.Color.White;
        
        if (_autoDetectPathCheckBox.Checked)
        {
            _teamsLogPathTextBox.Text = _configService.TeamsLogPath;
        }
    }

    private void LoadCurrentConfiguration()
    {
        _startOnWindowsStartupCheckBox.Checked = _configService.StartOnWindowsStartup;
        _homeAssistantEnabledCheckBox.Checked = _configService.HomeAssistantEnabled;
        _haUrlTextBox.Text = _configService.HAUrl;
        _haTokenTextBox.Text = _configService.HAToken;
        _statusSensorNameTextBox.Text = _configService.StatusSensorName;
        _activitySensorNameTextBox.Text = _configService.ActivitySensorName;
    }

    private async void TestHaConnection_Click(object? sender, EventArgs e)
    {
        _testHaConnectionButton.Enabled = false;
        _haStatusLabel.Text = "Testing connection...";
        _haStatusLabel.ForeColor = System.Drawing.Color.Blue;

        try
        {
            // Test with the values currently in the form, not the saved config
            var isConnected = await _haClient.TestConnectionAsync(
                _haUrlTextBox.Text,
                _haTokenTextBox.Text,
                CancellationToken.None);

            if (isConnected)
            {
                _haStatusLabel.Text = "Status: Connected (Success)";
                _haStatusLabel.ForeColor = System.Drawing.Color.Green;
            }
            else
            {
                _haStatusLabel.Text = "Status: Connection failed";
                _haStatusLabel.ForeColor = System.Drawing.Color.Red;
            }
        }
        catch (Exception ex)
        {
            _haStatusLabel.Text = $"Status: Error - {ex.Message}";
            _haStatusLabel.ForeColor = System.Drawing.Color.Red;
        }
        finally
        {
            _testHaConnectionButton.Enabled = true;
        }
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        try
        {
            if (_homeAssistantEnabledCheckBox.Checked && 
                (string.IsNullOrWhiteSpace(_haUrlTextBox.Text) || string.IsNullOrWhiteSpace(_haTokenTextBox.Text) ||
                string.IsNullOrWhiteSpace(_statusSensorNameTextBox.Text) || string.IsNullOrWhiteSpace(_activitySensorNameTextBox.Text)))
            {
                ShowMessage("Please fill in all Home Assistant fields", System.Drawing.Color.Red);
                return;
            }

            System.Diagnostics.Debug.WriteLine("[CONFIG SAVE] Starting configuration save...");
            
            // Save Application settings
            System.Diagnostics.Debug.WriteLine($"[CONFIG SAVE] Saving application settings: StartOnWindowsStartup={_startOnWindowsStartupCheckBox.Checked}");
            _configService.SaveApplicationSettings(_startOnWindowsStartupCheckBox.Checked);
            System.Diagnostics.Debug.WriteLine("[CONFIG SAVE] Application settings saved");
            
            // Save Home Assistant configuration
            System.Diagnostics.Debug.WriteLine("[CONFIG SAVE] Saving Home Assistant configuration...");
            _configService.SaveConfiguration(
                _haUrlTextBox.Text,
                _haTokenTextBox.Text,
                _statusSensorNameTextBox.Text,
                _activitySensorNameTextBox.Text,
                _homeAssistantEnabledCheckBox.Checked);
            System.Diagnostics.Debug.WriteLine("[CONFIG SAVE] Home Assistant configuration saved");
            
            // Collect Teams status mappings from grid
            var statusMappings = new Dictionary<string, string>();
            foreach (DataGridViewRow row in _statusMappingsGrid.Rows)
            {
                if (row.Cells[0].Value != null && row.Cells[1].Value != null)
                {
                    statusMappings[row.Cells[0].Value.ToString()!] = row.Cells[1].Value.ToString()!;
                }
            }
            
            // Save Teams configuration
            System.Diagnostics.Debug.WriteLine($"[CONFIG SAVE] Saving Teams configuration: PollingInterval={_pollingIntervalNumeric.Value}s, AutoDetect={_autoDetectPathCheckBox.Checked}, DebugMode={_teamsDebugModeCheckBox.Checked}");
            _configService.SaveTeamsConfiguration(
                (int)_pollingIntervalNumeric.Value,
                _autoDetectPathCheckBox.Checked,
                _teamsLogPathTextBox.Text,
                statusMappings,
                _teamsDebugModeCheckBox.Checked);
            System.Diagnostics.Debug.WriteLine("[CONFIG SAVE] Teams configuration saved");
            
            // Verify the values were saved by checking the service properties
            System.Diagnostics.Debug.WriteLine($"[CONFIG SAVE] Verification - PollingInterval: {_configService.TeamsPollingIntervalSeconds}s, AutoDetect: {_configService.TeamsAutoDetectLogPath}, DebugMode: {_configService.TeamsDebugMode}");
            
            // Send a test update to Home Assistant with new credentials if enabled
            if (_homeAssistantEnabledCheckBox.Checked)
            {
                _ = SendTestUpdateAsync(_haUrlTextBox.Text, _haTokenTextBox.Text);
            }
            
            ShowMessage("Configuration saved successfully", System.Drawing.Color.Green);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CONFIG SAVE] Exception: {ex}");
            ShowMessage($"Error saving configuration: {ex.Message}", System.Drawing.Color.Red);
        }
    }

    private async Task SendTestUpdateAsync(string haUrl, string haToken)
    {
        try
        {
            // Send current status to Home Assistant to verify it's working
            await _haClient.SendStatusUpdateAsync(_statusService.CurrentStatus, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // Log but don't show error to user - save was successful even if update failed
            System.Diagnostics.Debug.WriteLine($"Failed to send test update: {ex.Message}");
        }
    }

    private void ShowMessage(string message, System.Drawing.Color color)
    {
        _messageLabel.Text = message;
        _messageLabel.ForeColor = color;
        _messageLabel.Visible = true;
    }

    private void SubscribeToStatusChanges()
    {
        _statusService.StatusChanged += (s, e) =>
        {
            _currentStatusLabel.Text = $"Status: {e.NewStatus}";
            _lastUpdateLabel.Text = $"Last update: {DateTime.Now:HH:mm:ss}";
            _lastUpdate = DateTime.Now;
        };

        _statusService.CallActivityChanged += (s, e) =>
        {
            _currentActivityLabel.Text = $"Activity: {(e.IsNowInCall ? "In a call" : "Not in a call")}";
            _lastUpdateLabel.Text = $"Last update: {DateTime.Now:HH:mm:ss}";
            _lastUpdate = DateTime.Now;
        };
    }

    private void SubscribeToLogs()
    {
        // Load existing logs
        var existingLogs = _guiLogSink.GetAllLogs();
        foreach (var log in existingLogs)
        {
            AppendLogEntry(log);
        }

        // Subscribe to new logs
        _guiLogSink.LogAdded += (s, logEntry) =>
        {
            if (this.InvokeRequired)
            {
                this.Invoke(() => AppendLogEntry(logEntry));
            }
            else
            {
                AppendLogEntry(logEntry);
            }
        };
    }

    private void AppendLogEntry(GuiLogEntry logEntry)
    {
        // Color code by log level
        var color = logEntry.Level switch
        {
            "ERR" => System.Drawing.Color.Red,
            "WRN" => System.Drawing.Color.Yellow,
            "INF" => System.Drawing.Color.LimeGreen,
            _ => System.Drawing.Color.White
        };

        _logsTextBox.SelectionColor = color;
        _logsTextBox.AppendText(logEntry.ToString() + Environment.NewLine);
        
        // Auto-scroll to bottom
        _logsTextBox.SelectionStart = _logsTextBox.Text.Length;
        _logsTextBox.ScrollToCaret();
    }
}
