using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text.Json;
using HidSharp;

namespace AuraUnlock;

internal class Program
{
    private static AppConfig _config = new();
    private const string ConfigPath = "settings.json";

    // Hardware Settings
    private const byte HardwareLimit = 200;
    private const int VendorId = 0x0B05;
    private const int ProductId = 0x1AC8;
    private const float SmoothFactor = 0.2f;
    private const int CaptureW = 200;
    private const int CaptureH = 150;

    private static bool _isRunning = true;
    private static NotifyIcon? _trayIcon;
    public static Mutex? SingleInstanceMutex { get; private set; }

    private static UiManager? _uiManager;

    /// <summary>
    /// Sets the process-wide DPI awareness level for the application. This determines
    /// how the application handles DPI scaling on high-resolution displays.
    /// </summary>
    /// <param name="processDpiAwareness">
    /// An integer representing the desired DPI awareness level of the process.
    /// Values typically include:
    /// - 0: DPI unaware.
    /// - 1: System DPI aware.
    /// - 2: Per-monitor DPI aware.
    /// </param>
    /// <returns>
    /// Returns an integer indicating the success or failure of the operation.
    /// A return value of 0 typically indicates success, while non-zero values
    /// indicate an error.
    /// </returns>
    [DllImport("shcore.dll")]
    private static extern int SetProcessDpiAwareness(int processDpiAwareness);

    /// <summary>
    /// Entry point for the application that initializes and starts its major components.
    /// Configures the application settings, establishes a single-instance mutex to prevent
    /// multiple instances from running simultaneously, and sets up the application UI and
    /// system tray functionalities. Manages core tasks such as loading configuration,
    /// running the background service worker, and handling the application lifecycle.
    /// </summary>
    [STAThread]
    private static void Main()
    {
        LoadConfig();
        try
        {
            SetProcessDpiAwareness(2);
        }
        catch
        {
            // ignored
        }

        const string appName = "Global\\ASUS_ALB01_Driver";
        SingleInstanceMutex = new Mutex(true, appName, out var createdNew);
        if (!createdNew) return;

        ApplicationConfiguration.Initialize();

        // Init UI Manager
        _uiManager = new UiManager(
            onSaveConfig: SaveConfig,
            onToggleService: ToggleService,
            onExit: ExitApp,
            captureW: CaptureW,
            captureH: CaptureH
        );

        // === TRAY MENU ===
        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("Exit", null, (_, _) => ExitApp());

        _trayIcon = new NotifyIcon
        {
            Icon = CreateIcon(Color.LimeGreen),
            Visible = true,
            Text = $"ASUS LightBar: ON ({_config.BrightnessPercent}%)",
            ContextMenuStrip = contextMenu
        };

        // By clicking, we ask UI Manager to open/close the window
        _trayIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                _uiManager.ToggleSettingsWindow(_config, _isRunning);
            }
        };

        Task.Run(ServiceWorker);
        Application.Run();
    }

    // === LOGIC HANDLERS ===

    /// <summary>
    /// Toggles the operational state of the service between running and paused.
    /// Updates the system tray icon to visually represent the service state
    /// using a green icon for running and a red icon for paused.
    /// Adjusts the tooltip text of the tray icon to display the current service
    /// status and brightness percentage, if applicable.
    /// </summary>
    private static void ToggleService()
    {
        _isRunning = !_isRunning;
        if (_trayIcon == null) return;

        _trayIcon.Icon = CreateIcon(_isRunning ? Color.LimeGreen : Color.Red);
        _trayIcon.Text = $"ASUS LightBar: {(_isRunning ? "ON" : "OFF")} ({_config.BrightnessPercent}%)";
    }

    /// <summary>
    /// Saves the current application configuration to a file.
    /// Writes the serialized configuration to the designated settings file path.
    /// Updates the system tray icon tooltip to reflect the brightness percentage if applicable.
    /// Catches and suppresses any exceptions that occur during the save process.
    /// </summary>
    private static void SaveConfig()
    {
        try
        {
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(_config));
            _trayIcon?.Text = $"ASUS LightBar: ON ({_config.BrightnessPercent}%)";
        }
        catch
        {
            /* ignored */
        }
    }

    /// <summary>
    /// Loads the application configuration from a JSON file located at a predefined path.
    /// If the configuration file does not exist, it invokes the method responsible for
    /// creating and saving a default configuration file. This ensures that the application
    /// can always initialize with valid configuration settings. Silently handles any exceptions
    /// that may occur during the loading process.
    /// </summary>
    private static void LoadConfig()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                _config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigPath))!;
            }
            else SaveConfig();
        }
        catch
        {
            /* ignored */
        }
    }

    private static void ExitApp()
    {
        _trayIcon!.Visible = false;
        Environment.Exit(0);
    }

    /// <summary>
    /// Creates a system tray icon with a specified color. The icon is represented
    /// as a 16x16 bitmap rendered with a filled circle using the given color.
    /// </summary>
    /// <param name="color">The color used to fill the circle in the icon.</param>
    /// <returns>An <see cref="Icon"/> object created from the rendered bitmap.</returns>
    private static Icon CreateIcon(Color color)
    {
        using var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);
        using (Brush b = new SolidBrush(color)) g.FillEllipse(b, 2, 2, 12, 12);
        return Icon.FromHandle(bmp.GetHicon());
    }

    /// <summary>
    /// Continuously executes the Ambilight logic in a loop, which captures screen data
    /// and processes it for a lighting effect. If an exception occurs during the
    /// execution of the logic, the method sleeps for 3 seconds before trying again.
    /// Ensures the lighting effect remains active without manual intervention.
    /// </summary>
    private static void ServiceWorker()
    {
        while (true)
        {
            try
            {
                RunAmbilightLogic();
            }
            catch
            {
                Thread.Sleep(3000);
            }
        }
    }

    /// <summary>
    /// Executes the core logic for managing the Ambilight effect.
    /// Captures a subsection of the screen, processes pixel color data, and adjusts the RGB lighting of a connected HID device.
    /// The method includes adaptive smoothing for color transitions and brightness adjustments based on user-defined settings.
    /// Ensures the connected device's state is periodically updated with calculated color values, and transitions to black
    /// when the service is paused. Utilizes hardware constraints and user-configured brightness limits to determine effective lighting levels.
    /// Manages resource cleanup for graphics and bitmap objects and handles device disconnection or errors gracefully.
    /// </summary>
    private static void RunAmbilightLogic()
    {
        var device = DeviceList.Local.GetHidDevices(VendorId, ProductId).FirstOrDefault();
        if (device == null || !device.TryOpen(out var stream))
        {
            Thread.Sleep(2000);
            return;
        }

        var p35 = new byte[65];
        p35[0] = 0xEC;
        p35[1] = 0x35;
        p35[5] = 0x01;
        p35[8] = 0x01;
        var p40 = new byte[65];
        p40[0] = 0xEC;
        p40[1] = 0x40;
        p40[2] = 0x84;
        p40[4] = 0x04;

        var bmp = new Bitmap(CaptureW, CaptureH);
        var g = Graphics.FromImage(bmp);
        float curR = 0, curG = 0, curB = 0;

        var blackPacket = new byte[65];
        Array.Copy(p40, blackPacket, 65);
        for (var i = 5; i < 62; i++) blackPacket[i] = 0;

        try
        {
            stream.Write(p35);
            while (true)
            {
                if (!_isRunning)
                {
                    stream.Write(blackPacket);
                    Thread.Sleep(500);
                    continue;
                }

                var sW = _config.ScreenWidth;
                var sH = _config.ScreenHeight;
                var startX = Math.Max(0, (sW - CaptureW) / 2);
                var startY = Math.Max(0, (sH - CaptureH) / 2);

                g.CopyFromScreen(startX, startY, 0, 0, new Size(CaptureW, CaptureH));

                long r = 0, gr = 0, b = 0;
                var count = 0;
                var data = bmp.LockBits(new Rectangle(0, 0, CaptureW, CaptureH), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                unsafe
                {
                    var ptr = (byte*)data.Scan0;
                    for (var y = 0; y < CaptureH; y += 10)
                    for (var x = 0; x < CaptureW; x += 10)
                    {
                        b += ptr[y * data.Stride + x * 3];
                        gr += ptr[y * data.Stride + x * 3 + 1];
                        r += ptr[y * data.Stride + x * 3 + 2];
                        count++;
                    }
                }

                bmp.UnlockBits(data);
                if (count > 0)
                {
                    r /= count;
                    gr /= count;
                    b /= count;
                }

                curR = Lerp(curR, r, SmoothFactor);
                curG = Lerp(curG, gr, SmoothFactor);
                curB = Lerp(curB, b, SmoothFactor);

                var userFactor = _config.BrightnessPercent / 100.0f;
                var effectiveLimit = (byte)(HardwareLimit * userFactor);
                var finalR = (byte)Math.Min(curR, effectiveLimit);
                var finalG = (byte)Math.Min(curG, effectiveLimit);
                var finalB = (byte)Math.Min(curB, effectiveLimit);

                for (var i = 5; i < 62; i += 3)
                {
                    p40[i] = finalR;
                    p40[i + 1] = finalG;
                    p40[i + 2] = finalB;
                }

                p35[9] = finalR;
                p35[10] = finalG;
                p35[11] = finalB;

                stream.Write(p35);
                stream.Write(p40);
                Thread.Sleep(10);
            }
        }
        catch
        {
            stream.Close();
            throw;
        }
        finally
        {
            g.Dispose();
            bmp.Dispose();
        }
    }

    /// <summary>
    /// Linearly interpolates between two values based on a given factor.
    /// </summary>
    /// <param name="a">The starting value of the interpolation.</param>
    /// <param name="b">The ending value of the interpolation.</param>
    /// <param name="t">The interpolation factor, typically between 0 and 1. A value of 0 returns <paramref name="a"/> and a value of 1 returns <paramref name="b"/>.</param>
    /// <returns>The interpolated value between <paramref name="a"/> and <paramref name="b"/> based on the factor <paramref name="t"/>.</returns>
    private static float Lerp(float a, float b, double t) => a + (float)((b - a) * t);
}