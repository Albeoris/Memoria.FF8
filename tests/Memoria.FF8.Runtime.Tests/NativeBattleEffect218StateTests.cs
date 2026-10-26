using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect218StateTests
{
    [Fact]
    public void Flare_anchor_keeps_its_real_retained_six_bytes_and_unknown_imports_do_not_use_cold_values()
    {
        uint address = NativeBattleEffect218State.FlareAnchorAddress;
        NativeBattleEffect218State cold = NativeBattleEffect218State.FromColdProcess();
        Assert.Equal(new byte[6], cold.ReadView(address, 6).Read(0, 6));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect218State.FromUnknown().ReadWord(address));
        NativeBattleEffect218State current = cold.WriteWord(address, 0x8123).WriteWord(address + 2, 0x4567).WriteWord(address + 4, 0x89AB);
        NativeBattleEffect218State restored = JsonSerializer.Deserialize<NativeBattleEffect218State>(JsonSerializer.Serialize(current))!;
        restored.Validate();
        Assert.Equal(new byte[] { 0x23, 0x81, 0x67, 0x45, 0xAB, 0x89 }, restored.ReadView(address, 6).Read(0, 6));
        Assert.Throws<InvalidDataException>(() => restored.ReadView(address, 8));
    }

    [Fact]
    public void Snapshot_without_the_actual_flare_anchor_requires_explicit_migration()
    {
        string json = JsonSerializer.Serialize(NativeBattleEffect218State.FromColdProcess());
        Dictionary<string, JsonElement> fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        Assert.True(fields.Remove(nameof(NativeBattleEffect218State.FlareAnchor)));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeBattleEffect218State>(JsonSerializer.Serialize(fields)));
    }
}
