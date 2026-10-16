using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeShotAvailabilityTests
{
    private static string GameDirectory => TestResources.GameDirectory;

    [Fact]
    public void OriginalTemplateAndKernelProduceOneSavedShotByte()
    {
        using GameAssets assets = new(GameDirectory);
        NativeShotAvailabilityState state = assets.ReadNewGameShotAvailability();
        Assert.Equal((byte)1, state.Flags770);
        Assert.Equal(new byte[] { 101, 102, 103, 104, 105, 106, 107, 108 }, state.AmmoItemIds);
        Assert.Equal("4D0D23473AB54AC078B0C77A1672D970BF51E7824BAC1724027B5766B706C9EA", state.InitOutSha256);
        Assert.Equal("E378FB8F198EDE3DAE858F0DED6670F9BA423AA79ABFFF7237E701DFC7F9E7F6", state.KernelSha256);
        state.Validate();
    }

    [Fact]
    public void NativeMenuDiscoveryPreservesAvailabilityAndOnlyReadsInventory()
    {
        using GameAssets assets = new(GameDirectory);
        NativeShotAvailabilityState initial = assets.ReadNewGameShotAvailability();
        NativeEconomy economy = assets.ReadNewGameEconomy().AddItem(108, 1, out _).AddItem(102, 2, out _).AddItem(190, 1, out _);
        string before = JsonSerializer.Serialize(economy);
        NativeShotAvailabilityState found = initial.Discover(economy);
        Assert.Equal((byte)0x83, found.Flags770);
        Assert.Equal((byte)1, initial.Flags770);
        Assert.Equal(before, JsonSerializer.Serialize(economy));
        Assert.Equal((byte)0x83, found.Discover(assets.ReadNewGameEconomy()).Flags770);

        //Declared physical-byte inputs exercise MOVSX and count!=0, not an item grant or a research save.
        NativeItemStock[] stocks = assets.ReadNewGameEconomy().Items.ToArray();
        stocks[0] = new(101, 255);
        stocks[1] = new(103, 0);
        stocks[2] = new(255, 255);
        stocks[3] = new(108, 255);
        Assert.Equal((byte)0x81, initial.Discover(economy with { Items = stocks }).Flags770);
    }

    [Fact]
    public void CurrentOwnerRestoresAndRefusesMissingTablesOrMixedProducers()
    {
        using GameAssets assets = new(GameDirectory);
        NativeShotAvailabilityState state = assets.ReadNewGameShotAvailability().Discover(assets.ReadNewGameEconomy().AddItem(108, 1, out _));
        string before = JsonSerializer.Serialize(state);
        NativeShotAvailabilityState restored = JsonSerializer.Deserialize<NativeShotAvailabilityState>(before)!;
        restored.Validate();
        Assert.Equal(before, JsonSerializer.Serialize(restored.Discover(assets.ReadNewGameEconomy())));
        foreach (string property in new[] { nameof(state.Flags770), nameof(state.AmmoItemIds), nameof(state.InitOutSha256), nameof(state.KernelSha256) })
        {
            JsonObject missing = JsonNode.Parse(before)!.AsObject();
            missing.Remove(property);
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeShotAvailabilityState>(missing.ToJsonString()));
        }
        Assert.Throws<InvalidDataException>(() => (state with { AmmoItemIds = ImmutableArray<byte>.Empty }).Validate());
        NativeEconomy mismatched = assets.ReadNewGameEconomy() with { InitOutSha256 = new string('A', 64) };
        Assert.Throws<InvalidDataException>(() => state.Discover(mismatched));
        Assert.Equal(before, JsonSerializer.Serialize(state));
    }
}
