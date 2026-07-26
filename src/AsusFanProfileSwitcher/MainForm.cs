using AsusFanProfileSwitcher.Controls;
using AsusFanProfileSwitcher.Dialogs;
using AsusFanProfileSwitcher.Models;
using AsusFanProfileSwitcher.Services;

namespace AsusFanProfileSwitcher;

internal sealed class MainForm : Form
{
    private static readonly Color Background = Color.FromArgb(11, 13, 16);
    private static readonly Color Surface = Color.FromArgb(21, 24, 29);
    private static readonly Color SurfaceRaised = Color.FromArgb(28, 31, 37);
    private static readonly Color Accent = Color.FromArgb(229, 37, 53);
    private static readonly Color Muted = Color.FromArgb(132, 140, 151);

    private readonly ProfileCatalog _catalog = new();
    private readonly AsusFanXpertAdapter _adapter = new();
    private readonly SettingsStore _settingsStore = new();
    private readonly HardwareMonitorService _hardwareMonitor = new();
    private readonly System.Windows.Forms.Timer _monitorTimer = new() { Interval = 1500 };
    private readonly TableLayoutPanel _root = new();
    private readonly FlowLayoutPanel _profilesPanel = new();
    private readonly FlowLayoutPanel _fanReadingsPanel = new();
    private readonly CurveChart _curveChart = new();
    private readonly Label _connectionLabel = new();
    private readonly Label _pathLabel = new();
    private readonly Label _statusLabel = new();
    private readonly Label _monitorProfileLabel = new();
    private readonly Label _monitorStateLabel = new();
    private readonly Button _monitorButton = new();
    private readonly Button _refreshButton = new();
    private readonly Button _browseButton = new();
    private readonly Button _newProfileButton = new();

    private AppSettings _settings;
    private IReadOnlyList<FanProfile> _profiles = [];
    private IReadOnlyList<FanReading> _latestFanReadings = [];
    private FanProfile? _selectedProfile;
    private string? _selectedFanId;
    private FanXpertConnection? _connection;
    private string _profileDirectory = ProfileCatalog.DefaultProfileDirectory;
    private bool _monitorVisible;
    private bool _monitorReading;
    private bool _busy;

    public MainForm()
    {
        _settings = _settingsStore.Load();
        Text = "ASUS Fan Profile Switcher";
        MinimumSize = new Size(1080, 680);
        Size = new Size(1400, 820);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Background;
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9.5F);
        BuildInterface();
        Shown += OnShown;
        FormClosed += (_, _) =>
        {
            _monitorTimer.Stop();
            _hardwareMonitor.Dispose();
        };
    }

    private async void OnShown(object? sender, EventArgs e)
    {
        WindowsTheme.ApplyDark(this, _profilesPanel, _fanReadingsPanel);
        RefreshProfiles();
        _monitorStateLabel.Text = "INITIALIZING SENSORS…";
        await Task.Run(_hardwareMonitor.Open);
        _monitorTimer.Tick += async (_, _) => await RefreshMonitorAsync();
        _monitorTimer.Start();
        await RefreshMonitorAsync();
    }

    private void BuildInterface()
    {
        _root.Dock = DockStyle.Fill;
        _root.ColumnCount = 3;
        _root.RowCount = 1;
        _root.BackColor = Background;
        _root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
        _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0));

        _root.Controls.Add(BuildNavigation(), 0, 0);
        _root.Controls.Add(BuildProfilePage(), 1, 0);
        _root.Controls.Add(BuildMonitorPanel(), 2, 0);
        Controls.Add(_root);
    }

    private Control BuildNavigation()
    {
        var navigation = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(16, 18, 22),
            RowCount = 4,
            ColumnCount = 1,
            Padding = new Padding(0, 18, 0, 16)
        };
        navigation.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        navigation.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        navigation.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        navigation.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));

        var logo = new Label
        {
            Text = "FX",
            Dock = DockStyle.Fill,
            Font = new Font("Bahnschrift SemiBold", 18F),
            ForeColor = Accent,
            TextAlign = ContentAlignment.TopCenter
        };
        navigation.Controls.Add(logo, 0, 0);

        var profileTab = CreateRailButton("▰", "PROFILES", true);
        navigation.Controls.Add(profileTab, 0, 1);

        _monitorButton.Text = "◉\nMONITOR";
        StyleRailButton(_monitorButton, false);
        _monitorButton.Click += (_, _) => ToggleMonitor();
        navigation.Controls.Add(_monitorButton, 0, 3);
        return navigation;
    }

    private Control BuildProfilePage()
    {
        var page = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(40, 30, 34, 24),
            BackColor = Background,
            RowCount = 5,
            ColumnCount = 1
        };
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

        page.Controls.Add(BuildHeader(), 0, 0);
        page.Controls.Add(BuildConnectionStrip(), 0, 1);

        var sectionTitle = new Label
        {
            AutoSize = true,
            Text = "COOLING PROFILES",
            Font = new Font("Segoe UI Semibold", 9F),
            ForeColor = Color.FromArgb(188, 193, 201),
            Margin = new Padding(1, 26, 0, 16)
        };
        page.Controls.Add(sectionTitle, 0, 2);

        _profilesPanel.Dock = DockStyle.Fill;
        _profilesPanel.AutoScroll = true;
        _profilesPanel.WrapContents = true;
        _profilesPanel.BackColor = Background;
        _profilesPanel.Padding = new Padding(0, 0, 16, 0);
        _profilesPanel.Margin = new Padding(0);
        page.Controls.Add(_profilesPanel, 0, 3);

        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.ForeColor = Muted;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusLabel.Text = "READY";
        page.Controls.Add(_statusLabel, 0, 4);
        return page;
    }

    private Control BuildHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 76,
            ColumnCount = 2,
            Margin = new Padding(0, 0, 0, 12)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 326));

        var titles = new Panel { Dock = DockStyle.Fill };
        titles.Controls.Add(new Label
        {
            Text = "FAN XPERT CONTROL",
            Font = new Font("Bahnschrift SemiBold", 22F),
            ForeColor = Color.FromArgb(239, 241, 244),
            AutoSize = true,
            Location = new Point(0, 0)
        });
        titles.Controls.Add(new Label
        {
            Text = "PROFILE MANAGEMENT  /  LIVE COOLING STATUS",
            Font = new Font("Segoe UI Semibold", 8F),
            ForeColor = Muted,
            AutoSize = true,
            Location = new Point(2, 41)
        });
        header.Controls.Add(titles, 0, 0);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0, 5, 0, 0),
            Padding = new Padding(0)
        };
        ConfigureActionButton(_browseButton, "FOLDER", false, 82);
        ConfigureActionButton(_refreshButton, "REFRESH", false, 86);
        ConfigureActionButton(_newProfileButton, "+ NEW PROFILE", true, 132);
        _browseButton.Click += (_, _) => ChooseFolder();
        _refreshButton.Click += (_, _) => RefreshProfiles();
        _newProfileButton.Click += (_, _) => CreateProfile();
        actions.Controls.AddRange([_browseButton, _refreshButton, _newProfileButton]);
        header.Controls.Add(actions, 1, 0);
        return header;
    }

    private Control BuildConnectionStrip()
    {
        var strip = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Padding = new Padding(17, 11, 17, 9),
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0, 0, 0, 0)
        };
        _connectionLabel.AutoSize = true;
        _connectionLabel.Font = new Font("Segoe UI Semibold", 9.5F);
        _connectionLabel.Text = "●  DETECTING ASUS SERVICE";
        strip.Controls.Add(_connectionLabel, 0, 0);
        _pathLabel.AutoEllipsis = true;
        _pathLabel.Dock = DockStyle.Fill;
        _pathLabel.ForeColor = Muted;
        _pathLabel.Font = new Font("Consolas", 8.5F);
        _pathLabel.Text = _profileDirectory;
        strip.Controls.Add(_pathLabel, 0, 1);
        return strip;
    }

    private Control BuildMonitorPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(17, 19, 23),
            Padding = new Padding(24, 26, 24, 22),
            RowCount = 6,
            ColumnCount = 1
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 48));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 52));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));

        var heading = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        heading.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 35));
        heading.Controls.Add(new Label
        {
            Text = "PERFORMANCE",
            Font = new Font("Bahnschrift SemiBold", 16F),
            ForeColor = Color.White,
            AutoSize = true,
            Anchor = AnchorStyles.Left
        }, 0, 0);
        var close = new Button
        {
            Text = "×",
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            ForeColor = Muted,
            Font = new Font("Segoe UI", 16F),
            Cursor = Cursors.Hand
        };
        close.FlatAppearance.BorderSize = 0;
        close.Click += (_, _) => ToggleMonitor();
        heading.Controls.Add(close, 1, 0);
        panel.Controls.Add(heading, 0, 0);

        panel.Controls.Add(SectionLabel("LIVE FAN TELEMETRY"), 0, 1);
        _fanReadingsPanel.Dock = DockStyle.Fill;
        _fanReadingsPanel.AutoScroll = true;
        _fanReadingsPanel.FlowDirection = FlowDirection.TopDown;
        _fanReadingsPanel.WrapContents = false;
        _fanReadingsPanel.BackColor = Color.FromArgb(17, 19, 23);
        _fanReadingsPanel.Margin = new Padding(0);
        _fanReadingsPanel.Resize += (_, _) =>
        {
            foreach (var card in _fanReadingsPanel.Controls.OfType<FanReadingCard>())
            {
                card.Width = Math.Max(280, _fanReadingsPanel.ClientSize.Width - 22);
            }
        };
        panel.Controls.Add(_fanReadingsPanel, 0, 2);

        panel.Controls.Add(SectionLabel("SELECTED FAN CURVE"), 0, 3);
        _curveChart.Dock = DockStyle.Fill;
        panel.Controls.Add(_curveChart, 0, 4);

        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _monitorProfileLabel.AutoSize = true;
        _monitorProfileLabel.ForeColor = Color.FromArgb(194, 199, 207);
        _monitorProfileLabel.Font = new Font("Segoe UI Semibold", 8F);
        footer.Controls.Add(_monitorProfileLabel, 0, 0);
        _monitorStateLabel.AutoSize = true;
        _monitorStateLabel.ForeColor = Muted;
        _monitorStateLabel.Font = new Font("Segoe UI", 7.5F);
        footer.Controls.Add(_monitorStateLabel, 1, 0);
        panel.Controls.Add(footer, 0, 5);
        return panel;
    }

    private void RefreshProfiles()
    {
        _connection = _adapter.Discover();
        _profiles = _catalog.Load(_profileDirectory)
            .Select(profile => profile with
            {
                DisplayName = SettingsStore.GetProfileDisplayName(
                    _settings,
                    profile.Name,
                    profile.Name)
            })
            .ToArray();
        var activeHash = GetActiveHash();

        _connectionLabel.Text = _connection.IsConnected
            ? $"●  CONNECTED  /  {_connection.ServiceName}"
            : $"●  LIMITED MODE  /  {_connection.Summary}";
        _connectionLabel.ForeColor = _connection.IsConnected
            ? Color.FromArgb(62, 205, 151)
            : Color.FromArgb(242, 173, 62);
        _pathLabel.Text = _profileDirectory;

        if (_selectedProfile is not null)
        {
            _selectedProfile = _profiles.FirstOrDefault(profile =>
                string.Equals(profile.Name, _selectedProfile.Name, StringComparison.OrdinalIgnoreCase));
        }
        _selectedProfile ??= _profiles.FirstOrDefault(profile =>
            string.Equals(profile.Hash, activeHash, StringComparison.Ordinal));
        _selectedProfile ??= _profiles.FirstOrDefault();

        _profilesPanel.SuspendLayout();
        _profilesPanel.Controls.Clear();
        foreach (var profile in _profiles)
        {
            var card = new ProfileCard(
                profile,
                string.Equals(profile.Hash, activeHash, StringComparison.Ordinal))
            {
                Margin = new Padding(0, 0, 20, 20)
            };
            card.Invoked += async (_, _) => await SelectAndApplyProfileAsync(profile);
            card.EditInvoked += (_, _) => EditProfileName(profile);
            _profilesPanel.Controls.Add(card);
        }

        if (_profiles.Count == 0)
        {
            _profilesPanel.Controls.Add(new Label
            {
                AutoSize = true,
                ForeColor = Muted,
                Text = Directory.Exists(_profileDirectory)
                    ? "NO VALID XML PROFILES FOUND"
                    : "PROFILE DIRECTORY NOT FOUND",
                Margin = new Padding(2, 16, 0, 0)
            });
        }
        _profilesPanel.ResumeLayout();
        ShowSelectedCurve();
        _statusLabel.Text = $"{_profiles.Count} PROFILE{(_profiles.Count == 1 ? "" : "S")} AVAILABLE";
    }

    private async Task SelectAndApplyProfileAsync(FanProfile profile)
    {
        _selectedProfile = profile;
        ShowSelectedCurve();

        if (_busy || _connection is null)
        {
            return;
        }
        if (!_connection.IsConnected)
        {
            MessageBox.Show(
                $"{_connection.Summary}\n\nThe curve can still be inspected, but the app will not modify ASUS files without a compatible service and active FanStore.xml.",
                "Fan Xpert connection unavailable",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }
        if (string.Equals(profile.Hash, GetActiveHash(), StringComparison.Ordinal))
        {
            _statusLabel.Text = $"{profile.DisplayName.ToUpperInvariant()} IS ALREADY ACTIVE";
            return;
        }

        var confirmation = MessageBox.Show(
            $"Apply “{profile.DisplayName}” now?\n\nThe ASUS fan-control service will briefly restart and the current configuration will be backed up.",
            "Apply cooling profile",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);
        if (confirmation != DialogResult.Yes)
        {
            return;
        }

        SetBusy(true);
        _statusLabel.Text = $"APPLYING {profile.DisplayName.ToUpperInvariant()}…";
        try
        {
            var result = await _adapter.ApplyAsync(profile, _connection);
            RefreshProfiles();
            _statusLabel.Text =
                $"{profile.DisplayName.ToUpperInvariant()} ACTIVE  /  BACKUP: {result.BackupPath}";
        }
        catch (Exception exception)
        {
            _statusLabel.Text = "PROFILE SWITCH FAILED";
            MessageBox.Show(
                $"The app attempted to restore the previous configuration.\n\n{exception.Message}",
                "Profile switch failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ShowSelectedCurve()
    {
        if (_selectedProfile is null)
        {
            _curveChart.Curves = [];
            _monitorProfileLabel.Text = "NO PROFILE SELECTED";
            return;
        }

        var curves = _catalog.LoadCurves(_selectedProfile.FilePath);
        var selectedReading = _latestFanReadings.FirstOrDefault(reading =>
            string.Equals(reading.Id, _selectedFanId, StringComparison.OrdinalIgnoreCase));
        var selectedIndex = selectedReading is null
            ? -1
            : _latestFanReadings.ToList().IndexOf(selectedReading);
        var curve = MatchCurve(curves, selectedReading, selectedIndex);
        _curveChart.Curves = curve is null ? [] : [curve];

        var fanName = selectedReading is null
            ? "NO LIVE FAN SELECTED"
            : SettingsStore.GetFanAlias(
                _settings,
                selectedReading.Id,
                selectedReading.DefaultName).ToUpperInvariant();
        _monitorProfileLabel.Text =
            $"{_selectedProfile.DisplayName.ToUpperInvariant()}  /  {fanName}";
    }

    private async Task RefreshMonitorAsync()
    {
        if (_monitorReading)
        {
            return;
        }
        _monitorReading = true;
        try
        {
            var readings = await Task.Run(_hardwareMonitor.Read);
            if (IsDisposed)
            {
                return;
            }
            UpdateFanCards(readings);
            _monitorStateLabel.Text = _hardwareMonitor.Error is null
                ? $"UPDATED {DateTime.Now:HH:mm:ss}"
                : "SENSOR ACCESS LIMITED";
        }
        finally
        {
            _monitorReading = false;
        }
    }

    private void UpdateFanCards(IReadOnlyList<FanReading> readings)
    {
        _latestFanReadings = readings;
        if (_selectedFanId is null ||
            !readings.Any(reading =>
                string.Equals(reading.Id, _selectedFanId, StringComparison.OrdinalIgnoreCase)))
        {
            _selectedFanId = readings.FirstOrDefault()?.Id;
        }

        var existing = _fanReadingsPanel.Controls
            .OfType<FanReadingCard>()
            .ToDictionary(card => card.SensorId, StringComparer.OrdinalIgnoreCase);

        foreach (var reading in readings)
        {
            var alias = SettingsStore.GetFanAlias(
                _settings,
                reading.Id,
                reading.DefaultName);
            if (existing.Remove(reading.Id, out var card))
            {
                card.UpdateReading(reading, alias);
                card.IsSelected = string.Equals(
                    reading.Id,
                    _selectedFanId,
                    StringComparison.OrdinalIgnoreCase);
                continue;
            }

            card = new FanReadingCard(reading, alias)
            {
                Width = Math.Max(280, _fanReadingsPanel.ClientSize.Width - 22),
                Margin = new Padding(0, 0, 0, 12),
                IsSelected = string.Equals(
                    reading.Id,
                    _selectedFanId,
                    StringComparison.OrdinalIgnoreCase)
            };
            card.Invoked += (_, _) => SelectFan(card.SensorId);
            card.RenameInvoked += (_, _) => RenameFan(card);
            _fanReadingsPanel.Controls.Add(card);
        }

        foreach (var stale in existing.Values)
        {
            _fanReadingsPanel.Controls.Remove(stale);
            stale.Dispose();
        }

        if (readings.Count == 0)
        {
            if (_fanReadingsPanel.Controls.Find("EmptyMonitorLabel", false).Length == 0)
            {
                _fanReadingsPanel.Controls.Add(new Label
                {
                    Name = "EmptyMonitorLabel",
                    AutoSize = true,
                    MaximumSize = new Size(320, 0),
                    ForeColor = Muted,
                    Text = _hardwareMonitor.Error is null
                        ? "No motherboard fan sensors were exposed."
                        : $"Telemetry unavailable:\n{_hardwareMonitor.Error}",
                    Margin = new Padding(2, 12, 0, 0)
                });
            }
        }
        else
        {
            var empty = _fanReadingsPanel.Controls.Find("EmptyMonitorLabel", false).FirstOrDefault();
            if (empty is not null)
            {
                _fanReadingsPanel.Controls.Remove(empty);
                empty.Dispose();
            }
        }
        ShowSelectedCurve();
    }

    private void SelectFan(string sensorId)
    {
        _selectedFanId = sensorId;
        foreach (var card in _fanReadingsPanel.Controls.OfType<FanReadingCard>())
        {
            card.IsSelected = string.Equals(
                card.SensorId,
                sensorId,
                StringComparison.OrdinalIgnoreCase);
        }
        ShowSelectedCurve();
    }

    private void RenameFan(FanReadingCard card)
    {
        var name = TextEntryDialog.Show(
            this,
            "Rename fan",
            "Friendly name shown in the performance monitor",
            card.DisplayName);
        if (name is null)
        {
            return;
        }
        _settings.FanAliases[card.SensorId] = name;
        _settingsStore.Save(_settings);
        ShowSelectedCurve();
        _ = RefreshMonitorAsync();
    }

    private void EditProfileName(FanProfile profile)
    {
        var name = TextEntryDialog.Show(
            this,
            "Profile display name",
            "Display name used by this application",
            profile.DisplayName);
        if (name is null)
        {
            return;
        }
        _settings.ProfileDisplayNames[profile.Name] = name;
        _settingsStore.Save(_settings);
        RefreshProfiles();
    }

    private void CreateProfile()
    {
        var source = _selectedProfile ?? _profiles.FirstOrDefault();
        if (source is null)
        {
            MessageBox.Show(
                "Save at least one profile in Fan Xpert before creating a copy.",
                "No source profile",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var values = TextEntryDialog.ShowNewProfile(
            this,
            $"Copy of {source.DisplayName}",
            $"{source.Name}-copy");
        if (values is null)
        {
            return;
        }

        try
        {
            var created = _catalog.Duplicate(
                source,
                _profileDirectory,
                values.Value.FileName,
                values.Value.DisplayName);
            _settings.ProfileDisplayNames[created.Name] = values.Value.DisplayName;
            _settingsStore.Save(_settings);
            _selectedProfile = created;
            RefreshProfiles();
            _statusLabel.Text = $"{created.DisplayName.ToUpperInvariant()} CREATED";
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                exception.Message,
                "Could not create profile",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void ToggleMonitor()
    {
        _monitorVisible = !_monitorVisible;
        if (_monitorVisible && Width < 1280)
        {
            Width = 1280;
        }
        _root.ColumnStyles[2].Width = _monitorVisible ? 440 : 0;
        _monitorButton.ForeColor = _monitorVisible ? Accent : Color.FromArgb(154, 160, 169);
        _monitorButton.BackColor = _monitorVisible ? Color.FromArgb(35, 25, 29) : Color.Transparent;
        if (_monitorVisible)
        {
            _ = RefreshMonitorAsync();
        }
    }

    private void ChooseFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select the ASUS Fan Xpert Profiles folder",
            InitialDirectory = Directory.Exists(_profileDirectory)
                ? _profileDirectory
                : Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _profileDirectory = dialog.SelectedPath;
            _selectedProfile = null;
            RefreshProfiles();
        }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        _refreshButton.Enabled = !busy;
        _browseButton.Enabled = !busy;
        _newProfileButton.Enabled = !busy;
        foreach (Control control in _profilesPanel.Controls)
        {
            control.Enabled = !busy;
        }
    }

    private string? GetActiveHash()
    {
        try
        {
            return _connection?.ActiveStorePath is { } path && File.Exists(path)
                ? ProfileCatalog.CalculateHash(path)
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static Label SectionLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = Color.FromArgb(151, 158, 168),
        Font = new Font("Segoe UI Semibold", 8F),
        Anchor = AnchorStyles.Left
    };

    private Button CreateRailButton(string icon, string label, bool active)
    {
        var button = new Button { Text = $"{icon}\n{label}" };
        StyleRailButton(button, active);
        return button;
    }

    private void StyleRailButton(Button button, bool active)
    {
        button.Dock = DockStyle.Fill;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = active ? Color.FromArgb(35, 25, 29) : Color.Transparent;
        button.ForeColor = active ? Accent : Color.FromArgb(154, 160, 169);
        button.Font = new Font("Segoe UI Semibold", 7.5F);
        button.Cursor = Cursors.Hand;
        button.Margin = new Padding(0);
    }

    private static FanCurve? MatchCurve(
        IReadOnlyList<FanCurve> curves,
        FanReading? reading,
        int readingIndex)
    {
        if (curves.Count == 0)
        {
            return null;
        }
        if (reading is null)
        {
            return curves[0];
        }

        var sensorName = NormalizeFanName(reading.DefaultName);
        var byName = curves.FirstOrDefault(curve =>
        {
            var curveName = NormalizeFanName(curve.Name);
            return curveName == sensorName ||
                   (curveName.Length >= 4 && sensorName.Contains(curveName)) ||
                   (sensorName.Length >= 4 && curveName.Contains(sensorName));
        });
        if (byName is not null)
        {
            return byName;
        }

        var numberText = new string(reading.DefaultName.Reverse()
            .TakeWhile(char.IsDigit)
            .Reverse()
            .ToArray());
        if (int.TryParse(numberText, out var fanNumber) &&
            fanNumber > 0 &&
            fanNumber <= curves.Count)
        {
            return curves[fanNumber - 1];
        }

        return readingIndex >= 0 && readingIndex < curves.Count
            ? curves[readingIndex]
            : curves[0];
    }

    private static string NormalizeFanName(string value) =>
        new(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());

    private static void ConfigureActionButton(
        Button button,
        string text,
        bool primary,
        int width)
    {
        button.Text = text;
        button.AutoSize = false;
        button.Size = new Size(width, 38);
        button.Padding = new Padding(5, 0, 5, 0);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = primary ? Accent : Color.FromArgb(61, 66, 75);
        button.BackColor = primary ? Accent : SurfaceRaised;
        button.ForeColor = Color.White;
        button.Font = new Font("Segoe UI Semibold", 8F);
        button.Cursor = Cursors.Hand;
        button.Margin = new Padding(8, 0, 0, 0);
    }
}
