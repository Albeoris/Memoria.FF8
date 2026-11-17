using Avalonia;
using Avalonia.Headless;

namespace Memoria.Launcher.Tests;

public sealed class LauncherHeadlessFixture : IDisposable
{
    public HeadlessUnitTestSession Session { get; } = HeadlessUnitTestSession.StartNew(typeof(LauncherHeadlessFixture));

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<Application>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });

    public void Dispose()
    {
        Session.Dispose();
    }
}
