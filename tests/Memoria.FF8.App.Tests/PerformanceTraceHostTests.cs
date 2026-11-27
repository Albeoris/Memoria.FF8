using System.Reflection;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class PerformanceTraceHostTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void F2OpensDebugRoomAndStopsRecordingAfterAnExplicitStart(bool preserveSettingsMenu)
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable("MEMORIA_FF8_GRAPHICS_HOST_TEST") == "1", "An installed-game graphics host is required.");
        string gameDirectory = TestResources.GameDirectory;
        string directory = Directory.CreateTempSubdirectory("memoria-fps-host-").FullName;
        PerformanceTrace? trace = null;
        try
        {
            LaunchOptions options = new()
            {
                GameDirectory = gameDirectory,
                DebugUiScreen = "config-graphics",
                MuteAudio = true,
                AchievementsLocalOnly = true,
                Width = 640,
                Height = 480,
                ResolutionSpecified = true,
                SettingsDirectory = directory,
                ControlDirectory = Path.Combine(directory, "controls"),
                SavePath = Path.Combine(directory, "unused.json")
            };
            GameSession session = GameSession.CreateColdProcess();
            using RecreateGame game = new(options, session);
            KeyboardState keys = default;
            HostPhysicalInput input = new(true, () => true, () => keys, () => default, () => default);
            HostField("physicalInput").SetValue(game, input);

            void Frame(params Keys[] pressed)
            {
                keys = new KeyboardState(pressed);
                game.RunOneFrame();
            }

            Frame();
            MenuController menu = Read<MenuController>(game, "gameMenus");
            trace = Read<PerformanceTrace>(game, "performanceTrace");
            string? originalSelection = menu.SelectedEntry?.Id;
            if (!preserveSettingsMenu)
            {
                menu.Close();
            }
            PresentationTiming timing = Read<PresentationTiming>(game, "presentationTiming");
            Keys[] openKeys = preserveSettingsMenu ? [Keys.F2, Keys.X] : [Keys.F2];
            Frame(openKeys);
            Frame(openKeys);

            Assert.True(Read<bool>(game, "uiMovieSuspended"));
            Assert.Equal(SceneKind.Title, session.State.Scene);
            Assert.False(trace.IsRecording);
            Assert.Equal("debug-room", menu.ScreenId);
            menu.SelectEntry("debug-room", "start-fps-trace");

            Frame();
            Frame(Keys.X);
            Assert.True(trace.IsRecording);
            string path = Assert.IsType<string>(trace.FilePath);
            Frame(Keys.X);
            Frame();
            Frame();
            Assert.Equal(path, trace.FilePath);
            Assert.True(trace.BytesWritten > 0);
            Assert.Contains("F2", Read<string>(game, "traceLabel"));

            Frame(Keys.F2);
            Assert.False(trace.IsRecording);
            Assert.False(menu.IsDebugRoomOpen);
            ErrorReportPresenter report = Read<ErrorReportPresenter>(game, "traceReport");
            Assert.Contains("Viewport", report.Details);
            long length = new FileInfo(path).Length;
            Assert.Equal(length, trace.BytesWritten);
            string[] rows = File.ReadAllLines(path);
            Assert.StartsWith("frame,elapsed_ms,scene,", rows[0]);
            Assert.True(rows.Length >= 2, "Expected at least one presented-frame interval in the CSV.");

            TimeSpan reportPause = timing.SimulationTotal;
            Frame(Keys.F2);
            Assert.Same(report, Read<ErrorReportPresenter>(game, "traceReport"));
            Assert.Equal(reportPause, timing.SimulationTotal);
            Frame();
            Frame(Keys.C);
            Assert.Null(HostField("traceReport").GetValue(game));
            Assert.Equal(preserveSettingsMenu, menu.IsOpen);
            if (preserveSettingsMenu)
            {
                Assert.Equal("config-graphics", menu.ScreenId);
                Assert.Equal(originalSelection, menu.SelectedEntry?.Id);
            }
        }
        finally
        {
            if (trace?.FilePath is { } path)
            {
                File.Delete(path);
            }
            Directory.Delete(directory, recursive: true);
        }
    }

    private static FieldInfo HostField(string name) => typeof(RecreateGame).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;

    private static T Read<T>(RecreateGame game, string name) => Assert.IsType<T>(HostField(name).GetValue(game));
}
