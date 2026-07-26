using Avalonia;

namespace SteamShuffle;

internal static class Program
{
    // Avalonia needs a plain, non-async Main so its own message loop can take over.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // This is also reused by Avalonia's design-time previewer in the IDE.
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()   // picks the right windowing backend for Windows/Linux/macOS automatically
        .WithInterFont()
        .LogToTrace();
}
