using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeWorldTextureAnimationStateTests
{
    [Theory]
    [InlineData(0, 0, 2)]
    [InlineData(48, 0, 2)]
    [InlineData(50, 0, 3)]
    [InlineData(50, 12, 2)]
    [InlineData(50, 1, 3)]
    public void CallbackUsesPrecedingEntityAndScriptMode(int entityType, byte scriptMode, int step)
    {
        NativeWorldTextureAnimationState state = new(383, 382);
        Assert.Equal(new NativeWorldTextureAnimationState((383 + step) % 384, (382 + step) % 384), state.AdvanceWorldCallback(entityType, scriptMode));
    }

    [Theory]
    [InlineData(false, true, 17, 31)]
    [InlineData(true, false, 20, 28)]
    [InlineData(false, false, 17, 28)]
    public void IndependentNativeGatesRetainDisabledClock(bool textureEnabled, bool paletteEnabled, int texture, int palette)
    {
        Assert.Equal(new NativeWorldTextureAnimationState(texture, palette), new NativeWorldTextureAnimationState(17, 28).AdvanceWorldCallback(50, 0, textureEnabled, paletteEnabled));
    }

    [Fact]
    public void MainLoopCallbacksDoNotAdvanceWorldAnimation()
    {
        NativeWorldProcess process = NativeWorldProcess.FromColdProcess() with { TextureAnimation = new(6, 31) };
        Assert.Equal(process.TextureAnimation, process.AdvanceMainLoopCallbacks(800).TextureAnimation);
        Assert.False(process.IsColdProcess);
        Assert.Equal(new NativeWorldTextureAnimationState(0, 0), NativeWorldProcess.FromColdProcess().TextureAnimation);
    }

    [Fact]
    public void ClocksSerializeAndMissingLegacyStateRequiresExplicitMigration()
    {
        NativeWorldProcess process = NativeWorldProcess.FromColdProcess() with { TextureAnimation = new(383, 5) };
        NativeWorldProcess restored = JsonSerializer.Deserialize<NativeWorldProcess>(JsonSerializer.Serialize(process))!;
        restored.Validate();
        Assert.Equal(process.TextureAnimation, restored.TextureAnimation);
        JsonObject legacy = JsonNode.Parse(JsonSerializer.Serialize(process))!.AsObject();
        legacy.Remove("TextureAnimation");
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeWorldProcess>(legacy.ToJsonString()));
        GameState departure = new() { WorldProcess = process with { TextureAnimation = null } };
        Assert.Throws<NotSupportedException>(() => NativeWorldSalaryState.Begin(departure).AdvanceTextureAnimation(0, 0));
    }

    [Fact]
    public void SnapshotCompletionDoesNotInventHistoricalAnimationOrFillPartialClocks()
    {
        JsonObject saved = JsonSerializer.SerializeToNode(GameSession.CreateColdProcess().State)!.AsObject();
        saved[nameof(GameState.WorldProcess)]!.AsObject().Remove(nameof(NativeWorldProcess.TextureAnimation));
        using MemoryStream stream = new(System.Text.Encoding.UTF8.GetBytes(saved.ToJsonString()));
        Assert.Throws<JsonException>(() => SnapshotDefaults.Deserialize(stream, new JsonSerializerOptions()));
        saved[nameof(GameState.WorldProcess)]![nameof(NativeWorldProcess.TextureAnimation)] = new JsonObject { [nameof(NativeWorldTextureAnimationState.TextureClock)] = 3 };
        using MemoryStream partial = new(System.Text.Encoding.UTF8.GetBytes(saved.ToJsonString()));
        Assert.Throws<JsonException>(() => SnapshotDefaults.Deserialize(partial, new JsonSerializerOptions()));
    }

    [Fact]
    public void CallbackTransactionPublishesExactClocksAndDoesNotMutateDeparture()
    {
        WorldState world = new();
        NativeWorldProcess process = NativeWorldProcess.FromColdProcess() with { TextureAnimation = new(382, 383) };
        GameState departure = new() { Scene = SceneKind.WorldMap, World = world, WorldProcess = process };
        NativeWorldSalaryState transaction = NativeWorldSalaryState.Begin(departure).AdvanceTextureAnimation(50, 0).Advance(null, null, null, 0, 0);
        WorldState finalWorld = world with { Frame = 1 };
        GameState result = transaction.ApplyTo(departure with { World = finalWorld, WorldProcess = GameSession.PrepareWorldProcess(departure, finalWorld) }, finalWorld);
        Assert.Equal(new NativeWorldTextureAnimationState(1, 2), result.WorldProcess!.TextureAnimation);
        Assert.Equal(new NativeWorldTextureAnimationState(382, 383), departure.WorldProcess!.TextureAnimation);
        Assert.Equal(result.WorldProcess.TextureAnimation, NativeWorldSalaryState.Begin(result).TextureAnimation);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(384, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 384)]
    public void RejectsInvalidClocks(int texture, int palette)
    {
        Assert.Throws<InvalidDataException>(() => new NativeWorldTextureAnimationState(texture, palette).Validate());
    }
}
