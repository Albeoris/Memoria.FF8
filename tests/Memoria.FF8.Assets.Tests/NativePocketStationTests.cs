using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativePocketStationTests
{
    [Fact]
    public void OriginalNewGameClearsTheSavedPocketStationTailBeyondItsCopiedTemplate()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativePocketStationState saved = assets.ReadNewGamePocketStation();
        saved.Validate();
        Assert.Equal(64, saved.Raw.Length);
        Assert.All(saved.Raw, value => Assert.Equal((byte)0, value));
        Assert.Equal((byte)0, saved.Level);
        Assert.Equal("", NativePocketStationText.ChocoboName(saved));
        Assert.Equal(12, saved.ChocoboName!.Value.Length);
        Assert.Equal(new NativeGlobalRandomState(0), assets.ReadNewGameGlobalRandom());
        Assert.Equal(saved.Raw, JsonSerializer.Deserialize<NativePocketStationState>(JsonSerializer.Serialize(saved))!.Raw);
    }

    [Fact]
    public void PocketStationPreservesEveryByteAndUsesTheOriginalFieldWidths()
    {
        NativePocketStationState saved = new(NativeInputConfiguration.OriginalInitHash, Enumerable.Range(0, 64).Select(value => (byte)value).ToImmutableArray());
        saved.Validate();
        Assert.Equal((byte)0, saved.Flags);
        Assert.Equal((byte)1, saved.Level);
        Assert.Equal((byte)45, saved.AttackVariant);
        Assert.Throws<InvalidDataException>(() => (saved with { Raw = saved.Raw.RemoveAt(0) }).Validate());
        Assert.Null(NativePocketStationText.ChocoboName(saved));
        Assert.Throws<InvalidDataException>(() => (saved with { ChocoboName = ImmutableArray.CreateRange(new byte[11]) }).Validate());
    }
}
