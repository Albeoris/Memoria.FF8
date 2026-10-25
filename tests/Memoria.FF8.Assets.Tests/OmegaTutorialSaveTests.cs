using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class OmegaTutorialSaveTests
{
    [Fact]
    public void Current_tutorial_owner_replaces_retained_bits_and_preserves_adjacent_bytes()
    {
        GameState state = Retained();
        JsonElement scene = JsonSerializer.SerializeToElement(new { Scene = new { UnlockedTutorials = new[] { 0, 127 } } });
        byte[] block = NativePcSaveCodec.Decode(NativePcSaveConverter.Export(state with { EventState = scene })).Block;
        Assert.Equal((byte)1, block[NativePcSaveData.MainOffset + 3283]);
        Assert.Equal((byte)128, block[NativePcSaveData.MainOffset + 3298]);
        Assert.All(block.AsSpan(NativePcSaveData.MainOffset + 3284, 14).ToArray(), value => Assert.Equal((byte)0, value));
        Assert.Equal((byte)0xA4, block[NativePcSaveData.MainOffset + 3282]);
        Assert.Equal((byte)0x5A, block[NativePcSaveData.MainOffset + 3299]);
    }

    [Fact]
    public void Missing_owner_retains_original_bits_but_explicit_empty_owner_clears_them()
    {
        GameState state = Retained();
        byte[] retained = NativePcSaveCodec.Decode(NativePcSaveConverter.Export(state)).Block;
        Assert.All(retained.AsSpan(NativePcSaveData.MainOffset + 3283, 16).ToArray(), value => Assert.Equal((byte)255, value));
        state = state with { EventState = JsonSerializer.SerializeToElement(new { Scene = new { UnlockedTutorials = Array.Empty<int>() } }) };
        state = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(state))!;
        byte[] cleared = NativePcSaveCodec.Decode(NativePcSaveConverter.Export(state)).Block;
        Assert.All(cleared.AsSpan(NativePcSaveData.MainOffset + 3283, 16).ToArray(), value => Assert.Equal((byte)0, value));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(128)]
    public void Invalid_tutorial_id_is_rejected(int id)
    {
        GameState state = Retained() with { EventState = JsonSerializer.SerializeToElement(new { Scene = new { UnlockedTutorials = new[] { id } } }) };
        Assert.Throws<InvalidDataException>(() => NativePcSaveConverter.Export(state));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[\"127\"]")]
    [InlineData("[2147483648]")]
    public void Malformed_tutorial_owner_is_rejected(string json)
    {
        using JsonDocument snapshot = JsonDocument.Parse("{\"Scene\":{\"UnlockedTutorials\":" + json + "}}");
        GameState state = Retained() with { EventState = snapshot.RootElement.Clone() };
        Assert.Throws<InvalidDataException>(() => NativePcSaveConverter.Export(state));
    }

    private static GameState Retained()
    {
        GameState state = Defaults();
        NativePcSaveData original = NativePcSaveCodec.Decode(NativePcSaveConverter.Export(state));
        original.Block.AsSpan(NativePcSaveData.MainOffset + 3283, 16).Fill(255);
        original.Block[NativePcSaveData.MainOffset + 3282] = 0xA4;
        original.Block[NativePcSaveData.MainOffset + 3299] = 0x5A;
        return state with { OriginalSave = original };
    }

    private static GameState Defaults()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        string hash = NativeInputConfiguration.OriginalInitHash;
        NativeStatCurve curve = new(100, 0, 100, 255, 200);
        NativeStatModifier modifier = new(0, 0, 0);
        NativeCharacterVitals vitals = new()
        {
            Version = 1,
            Rule = NativeCharacterVitals.InitializationRule,
            InitOutSha256 = hash,
            KernelSha256 = new string('0', 64),
            Records = Enumerable.Range(0, 8).Select(id => new NativeCharacterVital((byte)id, new NativeHpWord(100, 100), 0, new NativeCharacterJunction(0, 0, 0, new byte[20]), 0, 0, 0, new byte[6], Enumerable.Repeat(new NativeMagicStock(0, 0), 32).ToArray(), 1, 0)).ToArray(),
            CombatParty = [0, 255, 255],
            DreamEnabled = false,
            GuardianForces = Enumerable.Range(0, 16).Select(_ => new NativeGuardianForceVital(0, 1, 100, new NativeGuardianForceLearnedBank(0, 0), null, ImmutableArray.CreateRange(new byte[21]), 0, 0)).ToArray(),
            Definitions = new NativeStatDefinitions(Enumerable.Repeat(curve, 11).ToImmutableArray(), ImmutableArray.CreateRange(new byte[57]), Enumerable.Repeat(modifier, 19).ToImmutableArray(), Enumerable.Repeat(curve, 16).ToImmutableArray(), Enumerable.Repeat(modifier, 9).ToImmutableArray()),
            MenuDefinitions = new NativeGuardianForceMenuDefinitions(Enumerable.Range(0, 16).Select(_ => Enumerable.Repeat(new NativeGuardianForceLearningRule(0, 255, 0), 21).ToImmutableArray()).ToImmutableArray(), ImmutableArray.CreateRange(new byte[116]), ImmutableArray.CreateRange(new byte[19]), ImmutableArray.CreateRange(new byte[5])),
            DreamWeapons = new byte[3],
            JunctionPartyBackup = new byte[6]
        };
        NativeEconomy economy = new() { Version = 1, Rule = NativeEconomy.InitializationRule, InitOutSha256 = hash, Items = Enumerable.Repeat(new NativeItemStock(0, 0), NativeEconomy.ItemSlotCount).ToArray(), ItemOrder = Enumerable.Range(0, 32).Select(rank => (byte)rank).ToArray(), Gil = 5000, PastGil = 0, EventBankGil72 = 0, EventBankPastGil76 = 0 };
        return session.State with { CharacterVitals = vitals, Economy = economy, FieldArrival = new FieldTransition(74, 10, 20, 0, 0) };
    }
}
