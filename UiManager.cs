using System.Drawing.Drawing2D;

namespace AuraUnlock;

public class UiManager(Action onSaveConfig, Action onToggleService, Action onExit, int captureW, int captureH)
{
    private Form? _activeSettingsForm;
    private Form? _overlayForm;

    private bool _isDebugVisible;

    // Colors
    private readonly Color _colorBg = Color.FromArgb(32, 32, 32);
    private readonly Color _colorText = Color.White;
    private readonly Color _colorAccent = Color.FromArgb(255, 50, 50); // ROG Red
    private readonly Color _colorControlBg = Color.FromArgb(50, 50, 50);

    /// <summary>
    /// Toggles the visibility of the settings window. If the settings window is not currently active
    /// or has been disposed, it will open and display configuration settings. If the settings
    /// window is currently active, it will close the window.
    /// </summary>
    /// <param name="config">An instance of <see cref="AppConfig"/> that contains application configuration data.</param>
    /// <param name="isRunning">A boolean indicating whether the service is currently running.</param>
    public void ToggleSettingsWindow(AppConfig config, bool isRunning)
    {
        if (_activeSettingsForm == null || _activeSettingsForm.IsDisposed)
        {
            ShowFlyoutSettings(config, isRunning);
        }
        else
        {
            _activeSettingsForm.Close();
        }
    }

    /// <summary>
    /// Displays the flyout settings menu. If the flyout menu is already open, it updates
    /// the displayed configuration settings and status.
    /// </summary>
    /// <param name="config">An instance of <see cref="AppConfig"/> that provides the current configuration data for the application.</param>
    /// <param name="isRunning">A boolean indicating the current operational status of the service.</param>
    private void ShowFlyoutSettings(AppConfig config, bool isRunning)
    {
        _activeSettingsForm = new Form();
        var f = _activeSettingsForm;

        // Window Style
        f.FormBorderStyle = FormBorderStyle.None;
        f.BackColor = _colorBg;
        f.ForeColor = _colorText;
        f.Size = new Size(280, 290);
        f.StartPosition = FormStartPosition.Manual;
        f.ShowInTaskbar = false;
        f.TopMost = true;

        // Position near cursor
        var x = Cursor.Position.X - f.Width / 2;
        var y = Cursor.Position.Y - f.Height - 10;

        var screen = Screen.FromPoint(Cursor.Position).WorkingArea;
        if (x + f.Width > screen.Right) x = screen.Right - f.Width - 10;
        if (y + f.Height > screen.Bottom) y = screen.Bottom - f.Height - 10;
        if (y < screen.Top) y = screen.Top + 10;
        f.Location = new Point(x, y);

        f.Deactivate += (_, _) => f.Close(); // Close on blur

        // === CONTROLS ===
        var fontTitle = new Font("Segoe UI", 12, FontStyle.Bold);
        var fontNormal = new Font("Segoe UI", 9, FontStyle.Regular);
        var fontSmall = new Font("Segoe UI", 8, FontStyle.Regular);

        // 1. Title
        var lblTitle = new Label { Text = "ASUS LightBar", Location = new Point(15, 20), AutoSize = true, Font = fontTitle, ForeColor = _colorAccent };

        // 2. Toggle Button
        var btnToggle = new Button
        {
            Text = isRunning ? "ON" : "OFF",
            Location = new Point(200, 18),
            Size = new Size(60, 28),
            FlatStyle = FlatStyle.Flat,
            BackColor = isRunning ? Color.FromArgb(0, 100, 0) : Color.FromArgb(100, 0, 0),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnToggle.FlatAppearance.BorderSize = 0;
        btnToggle.Click += (_, _) =>
        {
            onToggleService.Invoke();
            isRunning = !isRunning;
            btnToggle.Text = isRunning ? "ON" : "OFF";
            btnToggle.BackColor = isRunning ? Color.FromArgb(0, 100, 0) : Color.FromArgb(100, 0, 0);
        };

        // 3. Resolution Section
        var lblRes = new Label { Text = "Monitor Resolution:", Location = new Point(20, 70), AutoSize = true, Font = fontNormal, ForeColor = Color.Gray };

        var txtW = CreateStyledBox(config.ScreenWidth.ToString(), 20, 95, 70);
        var lblX = new Label { Text = "x", Location = new Point(95, 97), AutoSize = true, Font = fontNormal };
        var txtH = CreateStyledBox(config.ScreenHeight.ToString(), 115, 95, 70);

        var btnApplyRes = new Button
        {
            Text = "✓",
            Location = new Point(200, 95),
            Size = new Size(60, 33),
            FlatStyle = FlatStyle.Flat,
            BackColor = _colorControlBg,
            ForeColor = _colorAccent,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 10, FontStyle.Bold)
        };
        btnApplyRes.FlatAppearance.BorderSize = 0;
        btnApplyRes.Click += (_, _) =>
        {
            if (!int.TryParse(txtW.Text, out var w) || !int.TryParse(txtH.Text, out var h)) return;

            config.ScreenWidth = w;
            config.ScreenHeight = h;
            onSaveConfig.Invoke();
            UpdateOverlayPosition(config.ScreenWidth, config.ScreenHeight);

            // Blink effect
            btnApplyRes.BackColor = Color.Green;
            btnApplyRes.ForeColor = Color.White;
            Task.Delay(300).ContinueWith(_ => btnApplyRes.Invoke(() =>
            {
                btnApplyRes.BackColor = _colorControlBg;
                btnApplyRes.ForeColor = _colorAccent;
            }));
        };

        // 4. Brightness Section
        var lblBright = new Label
            { Text = $"Brightness: {config.BrightnessPercent}%", Location = new Point(20, 140), AutoSize = true, Font = fontNormal, ForeColor = Color.Gray };

        var trackBar = new DarkTrackBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = config.BrightnessPercent,
            Location = new Point(10, 180),
            Width = 260,
            Height = 25,
            ThumbColor = _colorAccent,
            TrackColor = Color.FromArgb(80, 80, 80)
        };

        trackBar.ValueChanged += (_, _) =>
        {
            lblBright.Text = $"Brightness: {trackBar.Value}%";
            config.BrightnessPercent = trackBar.Value;
            onSaveConfig.Invoke();
        };

        // 5. Debug Checkbox
        var chkDebug = new CheckBox
        {
            Text = "Show Capture Zone (Debug)",
            Checked = _isDebugVisible,
            Location = new Point(20, 210),
            AutoSize = true,
            Font = fontSmall,
            ForeColor = Color.Gray,
            Cursor = Cursors.Hand
        };
        chkDebug.CheckedChanged += (_, _) => ToggleDebugOverlay(chkDebug.Checked, config.ScreenWidth, config.ScreenHeight);

        // 6. Footer (Exit)
        var lblExit = new Label
        {
            Text = "Exit",
            Location = new Point(10, f.Height - 30),
            Size = new Size(f.Width - 20, 28),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = fontNormal,
            ForeColor = Color.Gray,
            Cursor = Cursors.Hand,
        };

        lblExit.MouseEnter += (_, _) => lblExit.ForeColor = _colorAccent;
        lblExit.MouseLeave += (_, _) => lblExit.ForeColor = Color.Gray;
        lblExit.Click += (_, _) => onExit.Invoke();

        f.Controls.AddRange(lblTitle, btnToggle, lblRes, txtW, lblX, txtH, btnApplyRes, lblBright, trackBar, lblExit, chkDebug);

        // Draw Border
        f.Paint += (_, e) => { e.Graphics.DrawRectangle(new Pen(Color.FromArgb(60, 60, 60)), 0, 0, f.Width - 1, f.Height - 1); };

        f.Show();
        f.Activate();
    }

    /// <summary>
    /// Creates a styled text box with specified properties such as positioning, width, and initial text.
    /// The text box will have a predefined background color, text color, font, and fixed border style.
    /// </summary>
    /// <param name="text">The initial text to display in the text box.</param>
    /// <param name="x">The x-coordinate of the text box on the UI.</param>
    /// <param name="y">The y-coordinate of the text box on the UI.</param>
    /// <param name="w">The width of the text box.</param>
    /// <returns>A styled <see cref="TextBox"/> instance with the configured properties.</returns>
    private TextBox CreateStyledBox(string text, int x, int y, int w)
    {
        return new TextBox
        {
            Text = text,
            Location = new Point(x, y),
            Width = w,
            BackColor = _colorControlBg,
            ForeColor = _colorText,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 9),
            TextAlign = HorizontalAlignment.Center
        };
    }

    /// <summary>
    /// Toggles the visibility of the debug overlay. When enabled, the overlay displays a transparent,
    /// bordered form useful for debugging UI layouts. When disabled, the overlay is hidden.
    /// </summary>
    /// <param name="show">A boolean indicating whether the debug overlay should be displayed.
    /// Set to <c>true</c> to show the overlay, or <c>false</c> to hide it.</param>
    /// <param name="screenW">The width of the screen in pixels, used for positioning and scaling the overlay.</param>
    /// <param name="screenH">The height of the screen in pixels, used for positioning and scaling the overlay.</param>
    private void ToggleDebugOverlay(bool show, int screenW, int screenH)
    {
        _isDebugVisible = show;
        if (show)
        {
            if (_overlayForm == null || _overlayForm.IsDisposed)
            {
                _overlayForm = new Form();
                _overlayForm.FormBorderStyle = FormBorderStyle.None;
                _overlayForm.ShowInTaskbar = false;
                _overlayForm.TopMost = true;
                _overlayForm.BackColor = Color.Magenta;
                _overlayForm.TransparencyKey = Color.Magenta;
                _overlayForm.StartPosition = FormStartPosition.Manual;

                _overlayForm.Paint += (_, e) =>
                {
                    using var p = new Pen(Color.Red, 4);
                    p.DashStyle = DashStyle.Dash;
                    e.Graphics.DrawRectangle(p, 2, 2, _overlayForm.Width - 4, _overlayForm.Height - 4);
                };
            }

            UpdateOverlayPosition(screenW, screenH);
            _overlayForm.Show();
        }
        else
        {
            _overlayForm?.Hide();
        }
    }

    /// <summary>
    /// Updates the position and size of the overlay form based on the given screen dimensions.
    /// Centers the overlay on the screen if possible and adjusts its location and size accordingly.
    /// </summary>
    /// <param name="screenW">The width of the available screen area.</param>
    /// <param name="screenH">The height of the available screen area.</param>
    private void UpdateOverlayPosition(int screenW, int screenH)
    {
        if (_overlayForm == null || _overlayForm.IsDisposed) return;

        var startX = Math.Max(0, (screenW - captureW) / 2);
        var startY = Math.Max(0, (screenH - captureH) / 2);

        _overlayForm.Location = new Point(startX, startY);
        _overlayForm.Size = new Size(captureW, captureH);
        _overlayForm.Invalidate();
    }
}