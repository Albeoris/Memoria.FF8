using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeWorldMusicFileTests
{
    [Theory]
    [InlineData(0, 41)]
    [InlineData(1, 24)]
    [InlineData(2, 81)]
    [InlineData(3, 35)]
    [InlineData(4, 59)]
    [InlineData(5, 89)]
    public void OriginalExecutableTableAndArchiveSupplyCompletedAkaoPayload(int index, byte music)
    {
        using WorldAssets assets = new(LaunchOptions.DefaultGameDirectory);
        NativeWorldMusicResource resource = assets.ReadMusicFileResource(index);
        resource.Validate();
        Assert.Equal(music, resource.MusicId);
        Assert.Equal($"dat/music{index}.obj", resource.File!.RelativePath);
        Assert.Equal(resource.Length, resource.File.PayloadEnd - resource.File.PayloadOffset);
        Assert.True(resource.File.PayloadEnd <= resource.File.Length);
        NativeWorldMusicResource restored = JsonSerializer.Deserialize<NativeWorldMusicResource>(JsonSerializer.Serialize(resource))!;
        Assert.Equal(resource, restored);
    }

    [Fact]
    public void Actual827RagnarokDepartureCompletesOriginalLoaderAndColdRestores()
    {
        GameSession session = new(GameSession.Load(RootFile("../fixtures/external-fixture.json")));
        using GameWorld world = new(session, LaunchOptions.DefaultGameDirectory);
        world.EnsureField();
        NativeMusicProcessState before = world.Events.Scene.MusicProcess!;
        Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 60)).Accepted);
        Assert.Null(world.Events.LastFault);
        Assert.Equal(SceneKind.WorldMap, session.State.Scene);
        NativeWorldMusicLoad load = Assert.IsType<NativeWorldMusicLoad>(world.Events.Scene.WorldMusicLoad);
        Assert.Equal((byte)89, load.Music);
        Assert.Equal(5, load.Resource!.File!.Index);
        Assert.Equal(13, load.StartPoint);
        Assert.False(JsonSerializer.SerializeToElement(load).TryGetProperty(nameof(NativeWorldMusicLoad.StartPoint), out _));
        Assert.Equal(NativeWorldMusicOperation.Play, load.Operation);
        Assert.Null(load.InitialVolume);
        Assert.Equal(127, load.TargetVolume);
        Assert.Equal(60, load.DurationMilliseconds);
        Assert.Equal(before with { Bank201 = (byte)(before.Bank201!.Value ^ 1) }, world.Events.Scene.MusicProcess);
        session.SetDepartingFieldState(world.Events.Capture());
        JsonElement receipt = world.Events.Capture();
        foreach (string property in new[] { "Index", "PayloadEnd", "InitialVolume" })
        {
            JsonNode malformed = JsonNode.Parse(receipt.GetRawText())!;
            if (property == "InitialVolume")
            {
                malformed["Scene"]!["WorldMusicLoad"]![property] = 0;
            }
            else
            {
                malformed["Scene"]!["WorldMusicLoad"]!["Resource"]!["File"]![property] = 0;
            }
            Assert.Throws<InvalidDataException>(() => world.Events.Restore(JsonSerializer.SerializeToElement(malformed)));
            Assert.True(JsonElement.DeepEquals(receipt, world.Events.Capture()));
        }
        GameSession restoredSession = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        using GameWorld restored = new(restoredSession, LaunchOptions.DefaultGameDirectory);
        restored.EnsureField();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.True(restored.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State.World), JsonSerializer.SerializeToElement(restoredSession.State.World)));
    }

    [Fact]
    public void CurrentOrphanageFootSaveDerivesNativeSkipWithoutSerializedMigration()
    {
        string source = RootFile("../fixtures/external-fixture.json");
        GameSession session = new(GameSession.Load(source));
        GameSession restoredSession = new(GameSession.Load(source));
        using GameWorld world = new(session, LaunchOptions.DefaultGameDirectory);
        using GameWorld restored = new(restoredSession, LaunchOptions.DefaultGameDirectory);
        world.EnsureField();
        restored.EnsureField();
        NativeWorldMusicLoad load = Assert.IsType<NativeWorldMusicLoad>(world.Events.Scene.WorldMusicLoad);
        Assert.Equal(5, load.Resource!.File!.Index);
        Assert.Equal(13, load.StartPoint);
        Assert.False(JsonSerializer.SerializeToElement(load).TryGetProperty(nameof(NativeWorldMusicLoad.StartPoint), out _));
        Assert.True(world.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.True(restored.Apply(new(PlayerActionKind.Wait)).Accepted);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State.World), JsonSerializer.SerializeToElement(restoredSession.State.World)));
    }

    private static string RootFile(string relative)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Actual standalone departure source is absent.", relative);
    }
}
