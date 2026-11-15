using System.Numerics;
using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.App.Tests;

public sealed class Ff7ChocoboHudTests
{
    [Fact]
    public void OriginalHudResourcesSubmitWithoutAdvancingNativeRaceState()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF7_CHOCOBO_ENTRY_SAVE");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory), "Select original FFVII resources.");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(source) && File.Exists(source), "Select an original Chocobo departure save.");
        Ff7Installation installation = Ff7Installation.Discover(directory!);
        using FileStream stream = File.OpenRead(source!);
        CombinedSessionSave saved = CombinedSessionCodec.Read(stream);
        using Ff7Session session = Ff7Session.Restore(installation, Assert.IsType<Ff7SessionSave>(saved.Ff7));
        for (int tick = 0; tick < 64; tick++)
        {
            session.AdvanceTick();
        }
        Ff7ChocoboRuntime race = Assert.IsType<Ff7ChocoboRuntime>(session.ChocoboRace);
        Ff7ChocoboHud hud = new(installation);
        string before = JsonSerializer.Serialize(race.Capture());
        Capture capture = new();
        hud.Render(capture, race, 320, 240);
        Assert.True(capture.Triangles > 10);
        Assert.Equal(before, JsonSerializer.Serialize(race.Capture()));
        Ff7ChocoboHudData data = Ff7PcExecutableData.LoadChocoboHudData(installation);
        Assert.Equal(11, data.Textures.Count);
        Assert.Equal(2, data.Crowns.Count);
        Assert.Equal(2, data.Controls.Count);
        Assert.Equal(25, data.Fade.Slot);
        Assert.Equal(320, data.Fade.Width);
        Assert.Equal(240, data.Fade.Height);
    }

    private sealed class Capture : ITriangleRenderer
    {
        public int Triangles { get; private set; }

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth)
        {
            throw new InvalidOperationException("HUD must use the active scene pass.");
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Assert.True(float.IsFinite(a.Position.X) && float.IsFinite(a.Position.Y));
            Assert.True(float.IsFinite(b.Position.X) && float.IsFinite(b.Position.Y));
            Assert.True(float.IsFinite(c.Position.X) && float.IsFinite(c.Position.Y));
            Assert.False(material.DepthWrite);
            Triangles++;
        }

        public void End()
        {
            throw new InvalidOperationException("HUD must leave the scene pass active.");
        }
    }
}
