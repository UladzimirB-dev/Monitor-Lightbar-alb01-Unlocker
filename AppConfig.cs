namespace AuraUnlock;

/// <summary>
/// Represents the configuration settings for the application.
/// Provides properties to manage screen resolution and brightness levels.
/// </summary>
public class AppConfig
{
    public int ScreenWidth { get; set; } = 3840;
    public int ScreenHeight { get; set; } = 2160;
    public int BrightnessPercent { get; set; } = 80;
}