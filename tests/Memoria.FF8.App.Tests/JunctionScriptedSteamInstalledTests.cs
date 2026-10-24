using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class JunctionScriptedSteamInstalledTests
{
    [Fact]
    public void ActualCarriageEmitsSteamResolvesItsReplacementAndContinuesAfterSnapshot()
    {
        GameSession session = new(GameSession.Load(TestResources.FixturePath("junction-scripted-steam-source")));
        string archive = TestResources.FixturePath("junction-world-textures");
        string mods = Directory.GetParent(Path.GetDirectoryName(archive)!)!.FullName;
        using GameResourceHost host = new(TestResources.GameDirectory, Path.Combine(mods, "test-application"), watch: false, modsDirectory: mods);
        using WorldMapScene scene = new(session, TestResources.GameDirectory, host.Resources);
        Assert.Equal(0, scene.State.Frame);
        Assert.Equal(24, scene.Apply(new(PlayerActionKind.Wait, Frames: 24)).FramesAdvanced);
        NativeScriptedWorldRuntimeState scripted = scene.State.NativeScriptedRuntime!;
        Assert.Contains(scripted.Forest!.Particles, particle => particle.Type == 0 && particle.Visible);
        byte[] image = scene.Render(640, 448);
        Assert.NotEmpty(image);
        using WorldAssets replacedAssets = new(TestResources.GameDirectory, host.Resources);
        using WorldAssets originalAssets = new(TestResources.GameDirectory);
        Assert.True(replacedAssets.GetForestTexture(0, 0).Width > originalAssets.GetForestTexture(0, 0).Width);
        GameSession restored = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using WorldMapScene cold = new(restored, TestResources.GameDirectory, host.Resources);
        Assert.Equal(scene.Apply(new(PlayerActionKind.Wait)), cold.Apply(new(PlayerActionKind.Wait)));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restored.State)));
        Assert.Equal(scene.Render(640, 448), cold.Render(640, 448));
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_JUNCTION_OUTPUT") ?? TestResources.ArtifactDirectory;
        Directory.CreateDirectory(output);
        session.Save(Path.Combine(output, "scripted-steam-frame25.json"));
        Assert.Equal((byte)0, WorldMapScene.MapDisplayMode(scene.State));
        foreach (byte expected in new byte[] { 1, 2, 0 })
        {
            Assert.Equal(scene.Apply(new(PlayerActionKind.Wait)), cold.Apply(new(PlayerActionKind.Wait)));
            Assert.Equal(scene.Apply(new(PlayerActionKind.Wait, WorldButtons: 0x100)), cold.Apply(new(PlayerActionKind.Wait, WorldButtons: 0x100)));
            Assert.Equal(expected, WorldMapScene.MapDisplayMode(scene.State));
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restored.State)));
            session.Save(Path.Combine(output, $"scripted-steam-map-mode{expected}.json"));
        }
    }
}
