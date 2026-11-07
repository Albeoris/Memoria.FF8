using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Tests;

public sealed class DiagnosticPocketStationPreparationTests
{
    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)1)]
    [InlineData((byte)2)]
    [InlineData((byte)3)]
    public void Explicit_saved_prerequisites_preserve_every_unrelated_byte_and_cold_state(byte variant)
    {
        NativePocketStationState source = Source();
        string before = JsonSerializer.Serialize(source);
        DiagnosticPocketStationPreparationResult result = GameWorld.PrepareDiagnosticPocketStation(source, new(true, false, 1, variant));
        Assert.Equal((byte)((source.Flags | 1) & ~2), result.State.Flags);
        Assert.Equal((byte)1, result.State.Level);
        Assert.Equal(variant, result.State.AttackVariant);
        Assert.Equal(source.InitOutSha256, result.State.InitOutSha256);
        Assert.Equal(source.ChocoboName, result.State.ChocoboName);
        for (int index = 0; index < 64; index++)
        {
            if (index is not (0 or 1 or 45))
            {
                Assert.Equal(source.Raw[index], result.State.Raw[index]);
            }
        }
        Assert.Equal("pocket-station", Assert.Single(result.Changes).Owner);
        Assert.Equal(before, JsonSerializer.Serialize(source));
        GameSession session = new(GameSession.CreateColdProcess().State with { PocketStation = source });
        session.SetDiagnosticPocketStation(result.State);
        Assert.Equal(JsonSerializer.Serialize(result.State), JsonSerializer.Serialize(session.State.PocketStation));
        GameSession restored = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        Assert.Equal(JsonSerializer.Serialize(session.State), JsonSerializer.Serialize(restored.State));
        DiagnosticPocketStationPreparationResult unchanged = GameWorld.PrepareDiagnosticPocketStation(result.State, new(true, false, 1, variant));
        Assert.Empty(unchanged.Changes);
    }

    [Fact]
    public void Preparation_rejects_unsupported_selectors_and_undeclared_saved_byte_changes()
    {
        NativePocketStationState source = Source();
        Assert.Throws<InvalidDataException>(() => GameWorld.PrepareDiagnosticPocketStation(source, new(true, false, 0, 0)));
        Assert.Throws<InvalidDataException>(() => GameWorld.PrepareDiagnosticPocketStation(source, new(true, false, 101, 0)));
        Assert.Throws<InvalidDataException>(() => GameWorld.PrepareDiagnosticPocketStation(source, new(true, false, 1, 4)));
        GameSession session = new(GameSession.CreateColdProcess().State with { PocketStation = source });
        NativePocketStationState prepared = GameWorld.PrepareDiagnosticPocketStation(source, new(true, false, 1, 0)).State;
        Assert.Throws<InvalidDataException>(() => session.SetDiagnosticPocketStation(prepared with { Raw = prepared.Raw.SetItem(12, (byte)(prepared.Raw[12] ^ 1)) }));
        Assert.Throws<InvalidDataException>(() => session.SetDiagnosticPocketStation(prepared with { Raw = prepared.Raw.SetItem(0, (byte)(prepared.Flags ^ 4)) }));
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(session.State.PocketStation));
    }

    private static NativePocketStationState Source()
    {
        byte[] raw = Enumerable.Range(0, 64).Select(index => (byte)(index * 3)).ToArray();
        raw[0] = 0xA6;
        raw[1] = 17;
        raw[45] = 3;
        return new(NativeInputConfiguration.OriginalInitHash, raw.ToImmutableArray()) { ChocoboName = new byte[12].ToImmutableArray() };
    }
}
