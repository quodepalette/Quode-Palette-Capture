using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using ScreenRecorderLib;

namespace QuodePaletteCapture
{
    public class MainForm : Form
    {
        // Recording state
        private Recorder? _recorder;
        private string _outputFolder;
        private string? _currentFilePath;
        private DateTime _recordStartTime;
        private TimeSpan _pausedDuration;
        private DateTime? _pauseStartTime;
        private bool _isRecording;
        private bool _isPaused;
        private bool _allowClose;

        // UI controls
        private NotifyIcon _trayIcon = null!;
        private ComboBox _monitorCombo = null!;
        private ComboBox _qualityCombo = null!;
        private ThemedCheckBox _audioCheck = null!;
        private ThemedCheckBox _cursorCheck = null!;
        private CursorHighlight? _cursorHighlight;
        private bool _highlightRequested;
        private Label _statusLabel = null!;
        private Label _timerLabel = null!;
        private TextBox _folderText = null!;
        private Button _startButton = null!;
        private Button _pauseButton = null!;
        private Button _stopButton = null!;
        private Button _browseButton = null!;
        private Button _openFolderButton = null!;
        private System.Windows.Forms.Timer _uiTimer = null!;
        private ToolStripMenuItem _trayStartItem = null!;
        private ToolStripMenuItem _trayPauseItem = null!;
        private ToolStripMenuItem _trayStopItem = null!;

        private const string QualityLow = "Low  (smaller file, 30 fps)";
        private const string QualityMedium = "Medium  (balanced, 30 fps)";
        private const string QualityHigh = "High  (best quality, 60 fps)";

        public MainForm()
        {
            _outputFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                "Quode Palette Capture");

            Text = "Quode Palette Capture";
            ClientSize = new Size(460, 530);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = AppTheme.BgWindow;
            Font = AppFonts.Make(AppFonts.Medium, 9.5f);

            AppTheme.ApplyDarkTitleBar(this);

            try
            {
                string iconPath = Path.Combine(AppContext.BaseDirectory, "AppIcon.ico");
                if (File.Exists(iconPath))
                {
                    Icon = new Icon(iconPath);
                }
            }
            catch
            {
                // If the icon can't be loaded, the default Windows icon is used instead.
            }

            BuildUi();
            BuildTrayIcon();
            PopulateMonitors();

            _folderText.Text = FormatPathForDisplay(_outputFolder);

            FormClosing += MainForm_FormClosing;
            Resize += MainForm_Resize;
        }

        // ---- UI construction ----
        private void BuildUi()
        {
            var headerPanel = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(460, 68),
                BackColor = AppTheme.BgPanel
            };

            try
            {
                string pngPath = Path.Combine(AppContext.BaseDirectory, "AppIcon.png");
                if (File.Exists(pngPath))
                {
                    var logo = new PictureBox
                    {
                        Location = new Point(18, 14),
                        Size = new Size(40, 40),
                        SizeMode = PictureBoxSizeMode.Zoom,
                        Image = Image.FromFile(pngPath),
                        BackColor = Color.Transparent
                    };
                    headerPanel.Controls.Add(logo);
                }
            }
            catch
            {
                // The logo is cosmetic, so a load failure is ignored.
            }

            var titleLabel = new Label
            {
                Text = "Quode Palette Capture",
                Font = AppFonts.Make(AppFonts.SemiBold, 14),
                ForeColor = AppTheme.TextPrimary,
                BackColor = Color.Transparent,
                Location = new Point(68, 12),
                AutoSize = true
            };
            var subtitleLabel = new Label
            {
                Text = "Lightweight screen recorder",
                Font = AppFonts.Make(AppFonts.Medium, 8.5f),
                ForeColor = AppTheme.TextSecondary,
                BackColor = Color.Transparent,
                Location = new Point(69, 38),
                AutoSize = true
            };
            headerPanel.Controls.Add(titleLabel);
            headerPanel.Controls.Add(subtitleLabel);
            Controls.Add(headerPanel);

            int y = 86;

            Controls.Add(MakeSectionLabel("MONITOR", 18, y));
            _monitorCombo = MakeDarkComboBox(18, y + 20, 424);
            Controls.Add(_monitorCombo);

            y += 62;
            Controls.Add(MakeSectionLabel("QUALITY", 18, y));
            _qualityCombo = MakeDarkComboBox(18, y + 20, 424);
            _qualityCombo.Items.AddRange(new object[] { QualityLow, QualityMedium, QualityHigh });
            _qualityCombo.SelectedIndex = 1;
            Controls.Add(_qualityCombo);

            y += 62;
            _audioCheck = new ThemedCheckBox
            {
                Text = "Record system / headphone audio (no microphone)",
                Font = AppFonts.Make(AppFonts.Medium, 9.5f),
                ForeColor = AppTheme.TextPrimary,
                BackColor = AppTheme.BgWindow,
                Location = new Point(18, y),
                AutoSize = true,
                Checked = true
            };
            Controls.Add(_audioCheck);

            y += 30;
            _cursorCheck = new ThemedCheckBox
            {
                Text = "Highlight mouse pointer with a blue ring",
                Font = AppFonts.Make(AppFonts.Medium, 9.5f),
                ForeColor = AppTheme.TextPrimary,
                BackColor = AppTheme.BgWindow,
                Location = new Point(18, y),
                AutoSize = true,
                Checked = true
            };
            Controls.Add(_cursorCheck);

            y += 36;
            Controls.Add(MakeSectionLabel("SAVE TO", 18, y));
            _folderText = new TextBox
            {
                Location = new Point(18, y + 20),
                Size = new Size(338, 26),
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = AppTheme.BgControl,
                ForeColor = AppTheme.TextPrimary,
                Font = AppFonts.Make(AppFonts.Medium, 9.5f)
            };
            _browseButton = MakeSecondaryButton("Browse...", 364, y + 19, 78, 28);
            _browseButton.Click += BrowseButton_Click;
            Controls.Add(_folderText);
            Controls.Add(_browseButton);

            y += 68;
            var line = new Panel
            {
                Location = new Point(18, y),
                Size = new Size(424, 1),
                BackColor = AppTheme.Border
            };
            Controls.Add(line);

            y += 16;
            _statusLabel = new Label
            {
                Text = "Ready",
                Font = AppFonts.Make(AppFonts.SemiBold, 10),
                ForeColor = AppTheme.TextPrimary,
                BackColor = Color.Transparent,
                Location = new Point(18, y),
                AutoSize = true
            };
            _timerLabel = new Label
            {
                Text = "00:00:00",
                Font = AppFonts.Make(AppFonts.SemiBold, 18),
                ForeColor = AppTheme.TextPrimary,
                BackColor = Color.Transparent,
                Location = new Point(18, y + 24),
                AutoSize = true
            };
            Controls.Add(_statusLabel);
            Controls.Add(_timerLabel);

            y += 72;
            _startButton = new ThemedButton
            {
                Text = "●   Start Recording",
                Font = AppFonts.Make(AppFonts.SemiBold, 10.5f),
                Location = new Point(18, y),
                Size = new Size(424, 42),
                BackColor = AppTheme.Accent,
                ForeColor = AppTheme.AccentText,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _startButton.FlatAppearance.BorderSize = 0;
            _startButton.Click += StartButton_Click;
            Controls.Add(_startButton);

            y += 52;
            _pauseButton = MakeSecondaryButton("Pause", 18, y, 206, 38);
            _pauseButton.Enabled = false;
            _pauseButton.Click += PauseButton_Click;

            _stopButton = MakeSecondaryButton("Stop", 236, y, 206, 38);
            _stopButton.Enabled = false;
            _stopButton.Click += StopButton_Click;
            Controls.Add(_pauseButton);
            Controls.Add(_stopButton);

            y += 50;
            _openFolderButton = MakeSecondaryButton("Open output folder", 18, y, 424, 32);
            _openFolderButton.Click += (s, e) => OpenOutputFolder();
            Controls.Add(_openFolderButton);

            _uiTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _uiTimer.Tick += UiTimer_Tick;
        }

        private static Label MakeSectionLabel(string text, int x, int y)
        {
            return new Label
            {
                Text = text,
                Location = new Point(x, y),
                AutoSize = true,
                Font = AppFonts.Make(AppFonts.SemiBold, 8f),
                ForeColor = AppTheme.TextMuted,
                BackColor = Color.Transparent
            };
        }

        /// <summary>
        /// A ComboBox that is owner-drawn to match the dark theme. Both the closed box and
        /// the open dropdown list use a dark background with light text, because the default
        /// WinForms ComboBox ignores BackColor for the dropdown.
        /// </summary>
        private static ComboBox MakeDarkComboBox(int x, int y, int width)
        {
            var combo = new ComboBox
            {
                Location = new Point(x, y),
                Size = new Size(width, 28),
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = AppTheme.BgControl,
                ForeColor = AppTheme.TextPrimary,
                Font = AppFonts.Make(AppFonts.Medium, 9.5f),
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 24
            };
            combo.DrawItem += DarkComboBox_DrawItem;
            return combo;
        }

        private static void DarkComboBox_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (sender is not ComboBox combo)
            {
                return;
            }

            bool isSelectedInList = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Color background = isSelectedInList ? AppTheme.BgControlHover : AppTheme.BgControl;

            using (var backBrush = new SolidBrush(background))
            {
                e.Graphics.FillRectangle(backBrush, e.Bounds);
            }

            if (e.Index >= 0 && e.Index < combo.Items.Count)
            {
                string text = combo.Items[e.Index]?.ToString() ?? string.Empty;
                TextRenderer.DrawText(
                    e.Graphics,
                    text,
                    combo.Font,
                    new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 16, e.Bounds.Height),
                    AppTheme.TextPrimary,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            }
        }

        /// <summary>A dark, flat, bordered button used for every non-primary action.</summary>
        private static Button MakeSecondaryButton(string text, int x, int y, int width, int height)
        {
            var button = new ThemedButton
            {
                Text = text,
                Font = AppFonts.Make(AppFonts.Medium, 9.5f),
                Location = new Point(x, y),
                Size = new Size(width, height),
                BackColor = AppTheme.BgControl,
                ForeColor = AppTheme.TextPrimary,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = AppTheme.Border;
            button.FlatAppearance.MouseOverBackColor = AppTheme.BgControlHover;
            button.FlatAppearance.MouseDownBackColor = AppTheme.BgControlHover;
            return button;
        }

        private void BuildTrayIcon()
        {
            var menu = new ContextMenuStrip
            {
                Renderer = new ToolStripProfessionalRenderer(new DarkMenuColorTable()),
                Font = AppFonts.Make(AppFonts.Medium, 9.5f),
                BackColor = AppTheme.BgPanel,
                ForeColor = AppTheme.TextPrimary,
                ShowImageMargin = false
            };

            var showItem = new ToolStripMenuItem("Show Quode Palette Capture") { ForeColor = AppTheme.TextPrimary };
            showItem.Click += (s, e) => RestoreFromTray();
            menu.Items.Add(showItem);
            menu.Items.Add(new ToolStripSeparator());

            _trayStartItem = new ToolStripMenuItem("Start Recording") { ForeColor = AppTheme.TextPrimary };
            _trayStartItem.Click += (s, e) => StartRecording();
            menu.Items.Add(_trayStartItem);

            _trayPauseItem = new ToolStripMenuItem("Pause") { Enabled = false, ForeColor = AppTheme.TextPrimary };
            _trayPauseItem.Click += (s, e) => PauseOrResume();
            menu.Items.Add(_trayPauseItem);

            _trayStopItem = new ToolStripMenuItem("Stop Recording") { Enabled = false, ForeColor = AppTheme.TextPrimary };
            _trayStopItem.Click += (s, e) => StopRecording();
            menu.Items.Add(_trayStopItem);

            menu.Items.Add(new ToolStripSeparator());

            var openFolderItem = new ToolStripMenuItem("Open output folder") { ForeColor = AppTheme.TextPrimary };
            openFolderItem.Click += (s, e) => OpenOutputFolder();
            menu.Items.Add(openFolderItem);

            var exitItem = new ToolStripMenuItem("Exit") { ForeColor = AppTheme.TextPrimary };
            exitItem.Click += (s, e) => ExitApplication();
            menu.Items.Add(exitItem);

            _trayIcon = new NotifyIcon
            {
                Icon = Icon,
                Text = "Quode Palette Capture",
                Visible = true,
                ContextMenuStrip = menu
            };
            _trayIcon.DoubleClick += (s, e) => RestoreFromTray();
        }

        // ---- Monitor enumeration ----
        private void PopulateMonitors()
        {
            _monitorCombo.Items.Clear();
            foreach (var screen in Screen.AllScreens)
            {
                _monitorCombo.Items.Add(new MonitorItem(screen));
            }
            if (_monitorCombo.Items.Count > 0)
            {
                _monitorCombo.SelectedIndex = 0;
            }
        }

        private string GetSelectedMonitorDeviceName()
        {
            if (_monitorCombo.SelectedItem is MonitorItem item)
            {
                return item.DeviceName;
            }
            return Screen.PrimaryScreen?.DeviceName ?? string.Empty;
        }

        private sealed class MonitorItem
        {
            public string DeviceName { get; }
            private readonly string _label;

            public MonitorItem(Screen screen)
            {
                DeviceName = screen.DeviceName;
                string primaryTag = screen.Primary ? " (Primary)" : "";
                _label = $"{screen.DeviceName}{primaryTag} | {screen.Bounds.Width}x{screen.Bounds.Height}";
            }

            public override string ToString() => _label;
        }

        // ---- Recording control ----
        private RecorderOptions BuildOptions(string monitorDeviceName, string quality, bool audioEnabled)
        {
            int bitrate;
            int framerate;

            if (quality == QualityLow)
            {
                bitrate = 3_000_000;
                framerate = 30;
            }
            else if (quality == QualityHigh)
            {
                bitrate = 12_000_000;
                framerate = 60;
            }
            else
            {
                bitrate = 6_000_000;
                framerate = 30;
            }

            var options = new RecorderOptions
            {
                VideoEncoderOptions = new VideoEncoderOptions
                {
                    Bitrate = bitrate,
                    Framerate = framerate,
                    IsFixedFramerate = false,
                    IsHardwareEncodingEnabled = true,
                    IsLowLatencyEnabled = true,
                    IsMp4FastStartEnabled = true,
                    Encoder = new H264VideoEncoder
                    {
                        BitrateMode = H264BitrateControlMode.CBR,
                        EncoderProfile = H264Profile.High
                    }
                },
                AudioOptions = new AudioOptions
                {
                    IsAudioEnabled = audioEnabled,
                    Bitrate = AudioBitrate.bitrate_128kbps,
                    Channels = AudioChannels.Stereo,
                    // An empty device name selects the current Windows default playback
                    // device, so recording follows whatever output the user has chosen
                    // (speakers, wired headphones, Bluetooth and so on) without having to
                    // enumerate or hard-code a device.
                    AudioOutputDevice = string.Empty,
                    InputVolume = 1.0f,
                    OutputVolume = 1.0f
                },
                MouseOptions = new MouseOptions
                {
                    IsMousePointerEnabled = true,
                    IsMouseClicksDetected = false
                },
                SourceOptions = new SourceOptions()
            };

            options.SourceOptions.RecordingSources.Add(new DisplayRecordingSource { DeviceName = monitorDeviceName });

            // ScreenRecorderLib has renamed some AudioOptions members between major versions
            // (for example IsOutputDeviceEnabled and IsInputDeviceEnabled in v5 and v6, and a
            // simpler surface in v7 and later). These are set through reflection against a short
            // list of known names, so a library update can't break the build. If none of the
            // names exist on the installed version, the call does nothing.
            TrySetProperty(options.AudioOptions,
                new[] { "IsOutputDeviceEnabled", "IsSystemAudioEnabled", "IsSystemAudioRecordingEnabled", "RecordSystemAudio" },
                audioEnabled);
            TrySetProperty(options.AudioOptions,
                new[] { "IsInputDeviceEnabled", "IsMicrophoneEnabled", "IsInputAudioEnabled", "RecordMicrophone" },
                false); // The microphone is never enabled, whatever the library version.

            return options;
        }

        /// <summary>
        /// Sets the first matching writable property found among <paramref name="candidateNames"/>
        /// on <paramref name="target"/>, if any. Safe no-op if none exist. Used to stay compatible
        /// with different ScreenRecorderLib versions without hard-coding an exact API surface.
        /// </summary>
        private static void TrySetProperty(object target, string[] candidateNames, object value)
        {
            var type = target.GetType();
            foreach (var name in candidateNames)
            {
                var prop = type.GetProperty(name);
                if (prop != null && prop.CanWrite)
                {
                    try
                    {
                        prop.SetValue(target, value);
                        return;
                    }
                    catch
                    {
                        // Try the next candidate name.
                    }
                }
            }
        }

        private void StartRecording()
        {
            if (_isRecording)
            {
                return;
            }

            try
            {
                Directory.CreateDirectory(_outputFolder);
                string fileName = $"QuodePaletteCapture_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
                _currentFilePath = Path.Combine(_outputFolder, fileName);

                string monitorDeviceName = GetSelectedMonitorDeviceName();
                string quality = _qualityCombo.SelectedItem?.ToString() ?? QualityMedium;
                bool audioEnabled = _audioCheck.Checked;

                var options = BuildOptions(monitorDeviceName, quality, audioEnabled);

                _recorder = Recorder.CreateRecorder(options);
                _recorder.OnRecordingComplete += (s, e) => Recorder_OnRecordingComplete(s, e);
                _recorder.OnRecordingFailed += (s, e) => Recorder_OnRecordingFailed(s, e);
                _recorder.OnStatusChanged += (s, e) => Recorder_OnStatusChanged(s, e);

                _recorder.Record(_currentFilePath);

                _isRecording = true;
                _isPaused = false;
                _recordStartTime = DateTime.Now;
                _pausedDuration = TimeSpan.Zero;
                _pauseStartTime = null;

                _uiTimer.Start();
                _highlightRequested = _cursorCheck.Checked;
                ShowCursorHighlight();
                UpdateButtonsForRecording();
                _statusLabel.Text = "Recording...";
                _trayIcon.Text = "Quode Palette Capture - Recording";
            }
            catch (Exception ex)
            {
                ReportError("Could not start recording:", ex);
                ResetToIdle();
            }
        }

        private void PauseOrResume()
        {
            if (_recorder == null || !_isRecording)
            {
                return;
            }

            try
            {
                if (!_isPaused)
                {
                    _recorder.Pause();
                    _isPaused = true;
                    HideCursorHighlight();
                    _pauseStartTime = DateTime.Now;
                    _statusLabel.Text = "Paused";
                    _pauseButton.Text = "Resume";
                    _trayPauseItem.Text = "Resume";
                }
                else
                {
                    _recorder.Resume();
                    _isPaused = false;
                    ShowCursorHighlight();
                    if (_pauseStartTime.HasValue)
                    {
                        _pausedDuration += DateTime.Now - _pauseStartTime.Value;
                        _pauseStartTime = null;
                    }
                    _statusLabel.Text = "Recording...";
                    _pauseButton.Text = "Pause";
                    _trayPauseItem.Text = "Pause";
                }
            }
            catch (Exception ex)
            {
                ReportError("Pause/Resume failed:", ex);
            }
        }

        private void StopRecording()
        {
            if (_recorder == null || !_isRecording)
            {
                return;
            }

            _statusLabel.Text = "Finishing...";
            HideCursorHighlight();
            _startButton.Enabled = false;
            _pauseButton.Enabled = false;
            _stopButton.Enabled = false;
            _trayStartItem.Enabled = false;
            _trayPauseItem.Enabled = false;
            _trayStopItem.Enabled = false;

            try
            {
                _recorder.Stop();
            }
            catch (Exception ex)
            {
                ReportError("Could not stop recording cleanly:", ex);
                ResetToIdle();
            }
        }

        // ---- Recorder events (may fire on a background thread) ----
        // The event-arg parameters are typed as `dynamic` on purpose. ScreenRecorderLib's
        // event-args class names can differ slightly between versions, while the members
        // used below (Error and FilePath) have stayed stable, so a minor version bump
        // won't break the build.
        //
        // Every handler body is wrapped in try/catch so that an unexpected failure produces
        // a specific error message and a log entry instead of a generic one.
        private void Recorder_OnStatusChanged(object? sender, dynamic e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => Recorder_OnStatusChanged(sender, e)));
                return;
            }

            try
            {
                // Status text is already driven by the button and pause logic. This hook is
                // kept so status-specific handling is easy to add later.
            }
            catch (Exception ex)
            {
                ReportError("A status update failed:", ex);
            }
        }

        private void Recorder_OnRecordingFailed(object? sender, dynamic e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => Recorder_OnRecordingFailed(sender, e)));
                return;
            }

            try
            {
                string errorText;
                try { errorText = e.Error?.ToString() ?? "Unknown error"; }
                catch { errorText = "Unknown error"; }

                MessageBox.Show(
                    this,
                    "Recording failed:\n" + errorText,
                    "Quode Palette Capture",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                ReportError("Recording failed, and the failure could not be fully described:", ex);
            }
            finally
            {
                ResetToIdle();
            }
        }

        private void Recorder_OnRecordingComplete(object? sender, dynamic e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => Recorder_OnRecordingComplete(sender, e)));
                return;
            }

            try
            {
                _statusLabel.Text = "Saved";
                try
                {
                    string savedPath = e.FilePath?.ToString() ?? _currentFilePath ?? "";
                    _trayIcon.ShowBalloonTip(
                        3000,
                        "Quode Palette Capture",
                        "Recording saved:\n" + Path.GetFileName(savedPath),
                        ToolTipIcon.Info);
                }
                catch
                {
                    // Balloon tips are cosmetic, so failures are ignored.
                }
            }
            catch (Exception ex)
            {
                ReportError("The recording finished, but wrapping up failed:", ex);
            }
            finally
            {
                ResetToIdle();
            }
        }

        /// <summary>
        /// Shows a contextual error message with the exception type, message and
        /// inner-exception chain, and appends the same details plus the stack trace to
        /// QuodePaletteCapture_error.log next to the exe.
        /// </summary>
        private void ReportError(string context, Exception ex)
        {
            var sb = new StringBuilder();
            var current = ex;
            int depth = 0;
            while (current != null && depth < 5)
            {
                if (depth > 0)
                {
                    sb.Append("\n\nCaused by: ");
                }
                sb.Append(current.GetType().FullName).Append(": ");
                sb.Append(string.IsNullOrEmpty(current.Message) ? "(no message)" : current.Message);
                current = current.InnerException;
                depth++;
            }

            try
            {
                string logPath = Path.Combine(AppContext.BaseDirectory, "QuodePaletteCapture_error.log");
                File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}\n{ex}\n\n");
            }
            catch
            {
                // Logging is best effort. A failed write should never hide the original error.
            }

            MessageBox.Show(
                this,
                context + "\n\n" + sb + "\n\nDetails were also saved to QuodePaletteCapture_error.log next to the exe.",
                "Quode Palette Capture",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        // ---- Helpers ----
        /// <summary>
        /// Shows the blue pointer highlight if it was requested when recording started.
        /// The highlight is cosmetic, so any failure is ignored and recording continues.
        /// </summary>
        private void ShowCursorHighlight()
        {
            if (!_highlightRequested)
            {
                return;
            }

            try
            {
                _cursorHighlight ??= new CursorHighlight(DeviceDpi);
                _cursorHighlight.Begin();
            }
            catch
            {
                // Cosmetic only. Recording must never fail because of the highlight.
            }
        }

        private void HideCursorHighlight()
        {
            try
            {
                _cursorHighlight?.End();
            }
            catch
            {
                // Cosmetic only.
            }
        }

        private void UpdateButtonsForRecording()
        {
            _startButton.Enabled = false;
            _pauseButton.Enabled = true;
            _stopButton.Enabled = true;
            _monitorCombo.Enabled = false;
            _qualityCombo.Enabled = false;
            _audioCheck.Enabled = false;
            _cursorCheck.Enabled = false;
            _browseButton.Enabled = false;

            _trayStartItem.Enabled = false;
            _trayPauseItem.Enabled = true;
            _trayStopItem.Enabled = true;
        }

        private void UpdateButtonsForIdle()
        {
            _startButton.Enabled = true;
            _pauseButton.Enabled = false;
            _stopButton.Enabled = false;
            _monitorCombo.Enabled = true;
            _qualityCombo.Enabled = true;
            _audioCheck.Enabled = true;
            _cursorCheck.Enabled = true;
            _browseButton.Enabled = true;

            _trayStartItem.Enabled = true;
            _trayPauseItem.Enabled = false;
            _trayStopItem.Enabled = false;
        }

        private void ResetToIdle()
        {
            _isRecording = false;
            _isPaused = false;
            _uiTimer.Stop();
            HideCursorHighlight();
            _highlightRequested = false;
            _timerLabel.Text = "00:00:00";
            if (_statusLabel.Text != "Saved")
            {
                _statusLabel.Text = "Ready";
            }
            _pauseButton.Text = "Pause";
            _trayPauseItem.Text = "Pause";
            UpdateButtonsForIdle();
            _trayIcon.Text = "Quode Palette Capture";

            if (_recorder != null)
            {
                // Handlers subscribed to the recorder are released together with the recorder
                // instance, so no separate unsubscribe step is needed.
                try
                {
                    _recorder.Dispose();
                }
                catch (Exception ex)
                {
                    ReportError("Cleaning up the recorder after finishing failed (recording itself was likely fine):", ex);
                }
                _recorder = null;
            }
        }

        private void UiTimer_Tick(object? sender, EventArgs e)
        {
            if (!_isRecording)
            {
                return;
            }

            TimeSpan elapsed = DateTime.Now - _recordStartTime - _pausedDuration;
            if (_isPaused && _pauseStartTime.HasValue)
            {
                elapsed -= DateTime.Now - _pauseStartTime.Value;
            }
            if (elapsed < TimeSpan.Zero)
            {
                elapsed = TimeSpan.Zero;
            }
            _timerLabel.Text = elapsed.ToString(@"hh\:mm\:ss");
        }

        /// <summary>
        /// Returns a version of the path that is safe to show on screen. A path inside the
        /// user's profile folder has that prefix replaced with "..", so a recording or
        /// screenshot of the window doesn't reveal the Windows account name. Only the
        /// display text is shortened. The real path is still used for saving files.
        /// </summary>
        private static string FormatPathForDisplay(string fullPath)
        {
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                .TrimEnd(Path.DirectorySeparatorChar);

            if (profile.Length > 0 &&
                fullPath.StartsWith(profile + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return ".." + fullPath.Substring(profile.Length);
            }

            return fullPath;
        }

        private void OpenOutputFolder()
        {
            try
            {
                Directory.CreateDirectory(_outputFolder);
                Process.Start(new ProcessStartInfo
                {
                    FileName = _outputFolder,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not open folder:\n" + ex.Message, "Quode Palette Capture",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RestoreFromTray()
        {
            Show();
            WindowState = FormWindowState.Normal;
            ShowInTaskbar = true;
            Activate();
        }

        private void ExitApplication()
        {
            if (_isRecording)
            {
                var result = MessageBox.Show(
                    this,
                    "A recording is in progress. Stop it and exit?",
                    "Quode Palette Capture",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result != DialogResult.Yes)
                {
                    return;
                }

                StopRecording();
            }

            _allowClose = true;
            _trayIcon.Visible = false;
            Close();
        }

        // ---- Window events ----
        private void MainForm_Resize(object? sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                ShowInTaskbar = false;
                Hide();
            }
        }

        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (_allowClose)
            {
                return;
            }

            // Closing the window minimizes to the tray instead of exiting, so an
            // in-progress recording is never interrupted by accident.
            e.Cancel = true;
            WindowState = FormWindowState.Minimized;
        }

        // ---- Button handlers ----
        private void StartButton_Click(object? sender, EventArgs e) => StartRecording();
        private void PauseButton_Click(object? sender, EventArgs e) => PauseOrResume();
        private void StopButton_Click(object? sender, EventArgs e) => StopRecording();

        private void BrowseButton_Click(object? sender, EventArgs e)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Choose where Quode Palette Capture saves recordings",
                SelectedPath = Directory.Exists(_outputFolder) ? _outputFolder : Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                UseDescriptionForTitle = true
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _outputFolder = dialog.SelectedPath;
                _folderText.Text = FormatPathForDisplay(_outputFolder);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _recorder?.Dispose();
                _trayIcon?.Dispose();
                _uiTimer?.Dispose();
                _cursorHighlight?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
