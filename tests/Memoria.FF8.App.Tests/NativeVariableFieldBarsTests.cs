using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeVariableFieldBarsTests
{
    [Theory]
    [InlineData(120, 50, 0, 100, 60)]
    [InlineData(65536, 50, 0, 100, -32768)]
    [InlineData(120, 50, 65536, 65636, 0)]
    [InlineData(-120, 50, 0, 100, -60)]
    [InlineData(120, 9, 10, 10, 0)]
    [InlineData(120, 10, 10, 10, 120)]
    public void VariableBarUsesFullDwordInitialScaleAndRetainsSignedWords(int width, int value, int minimum, int maximum, int fill)
    {
        NativeFieldBarsState bars = NativeFieldBarsState.CreateHidden().DisplayWidth(1, 65544, -65374, 65539, minimum, maximum, value, width);
        bars.Validate();
        Assert.Equal((short)2, bars.Second!.Mirror.Mode);
        Assert.Equal(unchecked((short)width), bars.Second.Mirror.WidthWord5A);
        Assert.Equal(unchecked((short)width), bars.Second.Display.Width);
        Assert.Equal(unchecked((short)fill), bars.Second.Display.Fill);
        Assert.Equal((short)8, bars.Second.Display.X);
        Assert.Equal((short)162, bars.Second.Display.Y);
        Assert.Equal((byte)131, bars.Second.Display.Flags);
        NativeFieldBarsState hidden = bars.Hide(1);
        hidden.Validate();
        Assert.False(hidden.Second!.Display.Visible);
        Assert.Equal(bars.Second.Mirror.WidthWord5A, hidden.Second.Mirror.WidthWord5A);
        Assert.Equal(hidden, hidden.Advance(2));
        NativeFieldBarsState fixedWidth = bars.Display(1, 0, 0, 0, 0, 100, 100);
        fixedWidth.Validate();
        Assert.Equal((short)96, fixedWidth.Second!.Display.Width);
        Assert.Equal(bars.Second.Mirror.WidthWord5A, fixedWidth.Second.Mirror.WidthWord5A);
        Assert.Throws<NotSupportedException>(() => bars.DisplayWidth(3, 0, 0, 0, 0, 100, 0, 120));
    }

    [Fact]
    public void Original948BuildsBoth120PixelBarsWithoutYieldingAndRestoresItsNextCallback()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original field948 is required.");
        string source = Path.Combine(Repository(), "../fixtures/external-fixture.json");
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, directory);
        world.EnsureField();
        for (int callback = 0; session.FieldBars?.First?.Mirror.Mode != 2 && callback < 100; callback++)
        {
            world.Apply(new PlayerAction(PlayerActionKind.Wait));
        }
        Assert.Null(world.Events.LastFault);
        Assert.Equal(948, world.Events.FieldId);
        JsonElement firstDisplay = world.Events.Capture();
        JsonElement issuing = firstDisplay.GetProperty("Fibers").EnumerateArray().Single(fiber => fiber.GetProperty("ObjectId").GetInt32() == 14 && fiber.GetProperty("ScriptId").GetInt32() == 75);
        Assert.Equal("None", issuing.GetProperty("WaitReason").GetString());
        for (int callback = 0; session.State.EventMemory.Read(1029, 1).Values[0] != 2 && callback < 10; callback++)
        {
            world.Apply(new PlayerAction(PlayerActionKind.Wait));
        }
        Assert.NotNull(session.FieldBars);
        foreach (NativeFieldBarSlot slot in new[] { session.FieldBars!.First!, session.FieldBars.Second! })
        {
            Assert.Equal((short)2, slot.Mirror.Mode);
            Assert.Equal((short)120, slot.Mirror.WidthWord5A);
            Assert.Equal((short)120, slot.Display.Width);
            Assert.Equal((short)120, slot.Display.Value);
            Assert.InRange(slot.Display.Fill, (short)4, (short)12);
            Assert.True(slot.Display.Visible);
        }
        Assert.Equal((short)8, session.FieldBars.First!.Display.X);
        Assert.Equal((short)170, session.FieldBars.Second!.Display.X);
        Assert.Equal(2, session.State.EventMemory.Read(1029, 1).Values[0]);
        JsonElement saved = world.Events.Capture();
        JsonNode bad = JsonNode.Parse(saved.GetRawText())!;
        bad["FieldBars"]!["First"]!["Mirror"]!.AsObject().Remove("WidthWord5A");
        Assert.Throws<InvalidDataException>(() => world.Events.Restore(JsonSerializer.SerializeToElement(bad)));
        Assert.True(JsonElement.DeepEquals(saved, world.Events.Capture()));
        GameSession restoredSession = new(session.State with { EventState = saved });
        using GameWorld restored = new(restoredSession, directory);
        restored.EnsureField();
        Assert.True(JsonElement.DeepEquals(saved, restored.Events.Capture()));
        for (int callback = 0; callback < 3; callback++)
        {
            Assert.Equal(world.Apply(new PlayerAction(PlayerActionKind.Wait)), restored.Apply(new PlayerAction(PlayerActionKind.Wait)));
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        }
    }

    private static string Repository()
    {
        DirectoryInfo? directory = new(TestResources.FixtureDirectory);
        return directory!.FullName;
    }
}
