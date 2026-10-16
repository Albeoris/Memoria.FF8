using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class UiCheckpointTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { Converters = { new JsonStringEnumConverter() } };

    [Fact]
    public void CurrentUiBuildPreservesAuthenticRoofStatePixelsAndNextOrdinaryInputs()
    {
        string sourcePath = RequiredCheckpoint("MEMORIA_UI_SOURCE");
        string rebuiltPath = RequiredCheckpoint("MEMORIA_UI_REBUILT");
        string gameDirectory = InstalledGameDirectory();
        GameState source = GameSession.Load(sourcePath);
        GameState recreated = GameSession.Load(rebuiltPath);
        AssertRoofCheckpoint(source);
        AssertRoofCheckpoint(recreated);
        AssertStateEqual(source, recreated, "Complete source and snapshot restore state before loading the field");
        GameSession sourceSession = new(source);
        GameSession rebuiltSession = new(recreated);
        using GameWorld sourceWorld = new(sourceSession, gameDirectory);
        using GameWorld rebuiltWorld = new(rebuiltSession, gameDirectory);
        sourceWorld.EnsureField();
        rebuiltWorld.EnsureField();
        sourceWorld.SetRenderSize(640, 448);
        rebuiltWorld.SetRenderSize(640, 448);
        AssertEquivalent(sourceSession, sourceWorld, rebuiltSession, rebuiltWorld, "After loading at 640x448");
        AssertNext(new PlayerAction(PlayerActionKind.Wait), sourceSession, sourceWorld, rebuiltSession, rebuiltWorld);
        Assert.True(sourceWorld.Events.IsUserControlEnabled, "Roof Move requires the actual current user-control permission.");
        Assert.False(sourceWorld.Events.IsDialogueBlocking);
        float previousX = sourceSession.State.X;
        float previousY = sourceSession.State.Y;
        AssertNext(new PlayerAction(PlayerActionKind.Move, Horizontal: 1), sourceSession, sourceWorld, rebuiltSession, rebuiltWorld);
        Assert.True(previousX != sourceSession.State.X || previousY != sourceSession.State.Y, "The compared ordinary roof Move must change the actual player position.");
        Console.WriteLine($"UI checkpoint validation: preserved source {sourcePath}; current-code snapshot restore {rebuiltPath}; complete state all persisted owners, 640x448 CPU pixels, next Wait and genuine Move equal. Both implementations are current Memoria; original FF8 GPU/audio/menu pixel parity is not claimed.");
    }

    [Fact]
    public void AllDemoAndReadOnlyLiveMenuViewsPreserveAttachedAuthenticCheckpoint()
    {
        string sourcePath = RequiredCheckpoint("MEMORIA_UI_SOURCE");
        GameState source = GameSession.Load(sourcePath);
        AssertRoofCheckpoint(source);
        GameSession session = new(source);
        JsonElement original = JsonSerializer.SerializeToElement(session.State, JsonOptions);
        int nativeActionCalls = 0;
        MenuController controller = new(() => LiveMenuData.Build(session.State), _ => nativeActionCalls++);
        string[] screens = controller.ScreenIds.ToArray();
        Assert.NotEmpty(screens);
        RecordingCanvas canvas = new();
        UiTheme theme = new();
        foreach (bool demo in new[] { true, false })
        {
            foreach (string screen in screens)
            {
                controller.Open(screen, demo);
                Draw(controller, canvas, theme);
                foreach (MenuInput input in new[] { MenuInput.Down, MenuInput.Right, MenuInput.Left, MenuInput.Up, MenuInput.PageRight, MenuInput.PageLeft, MenuInput.Confirm, MenuInput.Cancel })
                {
                    controller.Navigate(input);
                    Draw(controller, canvas, theme);
                }
                Assert.True(JsonElement.DeepEquals(original, JsonSerializer.SerializeToElement(session.State, JsonOptions)), $"{(demo ? "Synthetic demo" : "Read-only live")} screen {screen} changed its attached native checkpoint.");
            }
        }
        controller.Open();
        controller.Execute(new MenuAction("unsupported-native-operation"));
        Assert.Contains("Unsupported", controller.Message, StringComparison.OrdinalIgnoreCase);
        Draw(controller, canvas, theme);
        Assert.Equal(0, nativeActionCalls);
        Assert.True(JsonElement.DeepEquals(original, JsonSerializer.SerializeToElement(session.State, JsonOptions)));
        Assert.True(canvas.TextCalls > 0);
        Assert.Equal(0, canvas.ClipDepth);
        Console.WriteLine($"UI checkpoint isolation: {screens.Length} registered screens, synthetic demo and read-only live drawing/navigation; preserved source {sourcePath}. Canvas command checks do not establish original FF8 menu pixel parity.");
    }

    private static string RequiredCheckpoint(string variable)
    {
        string? path = Environment.GetEnvironmentVariable(variable);
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(path), $"Set {variable} to the explicitly selected authentic current checkpoint.");
        Assert.True(File.Exists(path), $"Explicit checkpoint {variable} does not exist: {path}");
        return Path.GetFullPath(path!);
    }

    private static string InstalledGameDirectory()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original field resources are required for the CPU comparison.");
        return directory;
    }

    private static void AssertRoofCheckpoint(GameState state)
    {
        Assert.Equal(SceneKind.Field, state.Scene);
        Assert.Equal(930, state.FieldId);
        Assert.Equal(5, state.EventState!.Value.GetProperty("Frame").GetInt32());
    }

    private static void AssertNext(PlayerAction action, GameSession sourceSession, GameWorld sourceWorld, GameSession rebuiltSession, GameWorld rebuiltWorld)
    {
        PlayerActionResult direct = sourceWorld.Apply(action);
        PlayerActionResult recreated = rebuiltWorld.Apply(action);
        Assert.True(direct.Accepted, direct.Detail);
        Assert.Equal(direct, recreated);
        AssertEquivalent(sourceSession, sourceWorld, rebuiltSession, rebuiltWorld, $"After ordinary {action.Kind}");
    }

    private static void AssertEquivalent(GameSession sourceSession, GameWorld sourceWorld, GameSession rebuiltSession, GameWorld rebuiltWorld, string phase)
    {
        AssertStateEqual(sourceSession.State, rebuiltSession.State, phase);
        Assert.True(JsonElement.DeepEquals(sourceWorld.Events.Capture(), rebuiltWorld.Events.Capture()), $"{phase}: complete current VM differs.");
        byte[] direct = sourceWorld.Render(TimeSpan.Zero);
        byte[] recreated = rebuiltWorld.Render(TimeSpan.Zero);
        Assert.Equal(640 * 448 * 4, direct.Length);
        Assert.Equal(direct, recreated);
    }

    private static void AssertStateEqual(GameState source, GameState recreated, string phase)
    {
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(source, JsonOptions), JsonSerializer.SerializeToElement(recreated, JsonOptions)), $"{phase}: complete current GameState including all persisted owners differs.");
    }

    private static void Draw(MenuController controller, RecordingCanvas canvas, UiTheme theme)
    {
        controller.BuildView().Draw(canvas, theme);
        Assert.Equal(0, canvas.ClipDepth);
    }

    private sealed class RecordingCanvas : IUiCanvas
    {
        public int TextCalls { get; private set; }
        public int ClipDepth { get; private set; }

        public void Fill(UiRect bounds, UiColor color)
        {
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
            TextCalls++;
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment)
        {
            Text(text, fontSize, bounds, color);
        }

        public void MenuPointer(UiRect bounds)
        {
        }

        public void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness)
        {
        }

        public void PushClip(UiRect bounds)
        {
            ClipDepth++;
        }

        public void PopClip()
        {
            ClipDepth--;
            Assert.True(ClipDepth >= 0, "UI canvas popped a clip without a matching push.");
        }
    }
}
