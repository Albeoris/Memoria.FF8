using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleScriptedEffectMemoryTests
{
    private const uint Base = NativeBattleScriptedEffectMemory.Address;

    [Fact]
    public void Genuine_cold_window_is_known_zero_and_imported_unknown_does_not_hydrate()
    {
        NativeBattleScriptedEffectMemory cold = NativeBattleScriptedEffectMemory.FromColdProcess();
        cold.Validate();
        Assert.Equal(0x2797068u, Base);
        Assert.Equal(0x1BB0, NativeBattleScriptedEffectMemory.Length);
        Assert.Equal(new byte[NativeBattleScriptedEffectMemory.Length], cold.Read(Base, NativeBattleScriptedEffectMemory.Length));
        NativeBattleScriptedEffectMemory unknown = NativeBattleScriptedEffectMemory.Unknown();
        unknown.Validate();
        Assert.Throws<NotSupportedException>(() => unknown.Read(Base, 1));
        Assert.Throws<NotSupportedException>(() => unknown.Read(Base + NativeBattleScriptedEffectMemory.Length - 1, 1));
        NativeBattleWaterHeader header = unknown.ReadHeader();
        Assert.All(header.Known, known => Assert.False(known));
        Assert.Throws<NotSupportedException>(() => header.Read(0, 2));
    }

    [Fact]
    public void Header_and_core_writes_are_one_alias_and_partial_writes_only_establish_written_bytes()
    {
        NativeBattleScriptedEffectMemory source = NativeBattleScriptedEffectMemory.Unknown();
        NativeBattleScriptedEffectMemory written = source.Write(Base + 0x44, [3, 4, 5]);
        NativeBattleWaterHeader header = written.ReadHeader();
        Assert.Equal(new byte[] { 3, 4, 5 }, header.Read(0x44, 3));
        Assert.False(header.Known[0x43]);
        Assert.False(header.Known[0x47]);
        Assert.Throws<NotSupportedException>(() => source.Read(Base + 0x44, 1));
        NativeBattleScriptedEffectMemory unknownAgain = written.MarkUnknown(Base + 0x45, 1);
        Assert.Equal((byte)4, unknownAgain.ReadHeader().Bytes[0x45]);
        Assert.False(unknownAgain.ReadHeader().Known[0x45]);
        Assert.Throws<NotSupportedException>(() => unknownAgain.Read(Base + 0x45, 1));
        Assert.Equal(new byte[] { 3 }, unknownAgain.Read(Base + 0x44, 1));
        NativeBattleScriptedEffectMemory repaired = unknownAgain.Write(Base + 0x45, [8]);
        Assert.Equal(new byte[] { 3, 8, 5 }, repaired.Read(Base + 0x44, 3));
        Assert.False(repaired.ReadHeader().Known[0x47]);
    }

    [Fact]
    public void WithHeader_copies_all_raw_bytes_and_validity_without_touching_other_window_storage()
    {
        NativeBattleScriptedEffectMemory source = NativeBattleScriptedEffectMemory.FromColdProcess().Write(Base + 0x17C, [0xCE, 0xDF]).Write(Base + NativeBattleScriptedEffectMemory.Length - 1, [0xA7]);
        NativeBattleWaterHeader header = new(Enumerable.Range(0, 0x17C).Select(index => unchecked((byte)(index * 17 + 3))).ToImmutableArray(), Enumerable.Range(0, 0x17C).Select(index => index % 3 != 0).ToImmutableArray());
        NativeBattleScriptedEffectMemory result = source.WithHeader(header);
        NativeBattleWaterHeader actual = result.ReadHeader();
        Assert.Equal(header.Bytes.ToArray(), actual.Bytes.ToArray());
        Assert.Equal(header.Known.ToArray(), actual.Known.ToArray());
        Assert.Equal(new byte[] { 0xCE, 0xDF }, result.Read(Base + 0x17C, 2));
        Assert.Equal(new byte[] { 0xA7 }, result.Read(Base + NativeBattleScriptedEffectMemory.Length - 1, 1));
        Assert.Throws<NotSupportedException>(() => result.Read(Base, 1));
        Assert.Equal(new byte[0x17C], source.Read(Base, 0x17C));
        NativeBattleScriptedEffectMemory reknown = result.WithHeader(NativeBattleWaterHeader.FromColdProcess());
        Assert.Equal(new byte[0x17C], reknown.Read(Base, 0x17C));
        Assert.Equal(new byte[] { 0xCE, 0xDF }, reknown.Read(Base + 0x17C, 2));
    }

    [Fact]
    public void Disjoint_copy_transfers_unknown_raw_padding_and_clears_destination_knowledge_without_consuming_source()
    {
        NativeBattleScriptedEffectMemory source = NativeBattleScriptedEffectMemory.FromColdProcess().Write(Base + 0x80, [1, 2, 0xAC, 0xBD, 5, 6, 7, 8]).MarkUnknown(Base + 0x82, 2).Write(Base + 0x120, Enumerable.Repeat((byte)0xEE, 8).ToArray());
        Assert.Throws<NotSupportedException>(() => source.Read(Base + 0x80, 8));
        NativeBattleScriptedEffectMemory copied = source.Copy(Base + 0x80, Base + 0x120, 8);
        NativeBattleWaterHeader header = copied.ReadHeader();
        Assert.Equal(new byte[] { 1, 2, 0xAC, 0xBD, 5, 6, 7, 8 }, header.Bytes.Skip(0x120).Take(8));
        Assert.Equal(new[] { true, true, false, false, true, true, true, true }, header.Known.Skip(0x120).Take(8));
        Assert.Throws<NotSupportedException>(() => copied.Read(Base + 0x120, 8));
        Assert.Equal(new byte[] { 1, 2 }, copied.Read(Base + 0x120, 2));
        NativeBattleScriptedEffectMemory repaired = copied.Write(Base + 0x122, [0x11, 0x22]);
        Assert.Equal(new byte[] { 1, 2, 0x11, 0x22, 5, 6, 7, 8 }, repaired.Read(Base + 0x120, 8));
        Assert.Throws<NotSupportedException>(() => repaired.Read(Base + 0x82, 2));
        Assert.Equal(Enumerable.Repeat((byte)0xEE, 8), source.Read(Base + 0x120, 8));
    }

    [Fact]
    public void JSON_preserves_masks_raw_unknown_bytes_and_next_copy_and_write_results()
    {
        NativeBattleScriptedEffectMemory source = NativeBattleScriptedEffectMemory.Unknown().Write(Base + 0x70, [0xA1, 0xB2, 0xC3, 0xD4]).MarkUnknown(Base + 0x71, 1).Write(Base + 0x1BAF, [0x57]);
        string json = JsonSerializer.Serialize(source);
        NativeBattleScriptedEffectMemory restored = JsonSerializer.Deserialize<NativeBattleScriptedEffectMemory>(json)!;
        restored.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Equal(source.ReadHeader().Bytes.ToArray(), restored.ReadHeader().Bytes.ToArray());
        Assert.Equal(source.ReadHeader().Known.ToArray(), restored.ReadHeader().Known.ToArray());
        NativeBattleScriptedEffectMemory expected = source.Copy(Base + 0x70, Base + 0x100, 4).Write(Base + 0x101, [0xEF]);
        NativeBattleScriptedEffectMemory actual = restored.Copy(Base + 0x70, Base + 0x100, 4).Write(Base + 0x101, [0xEF]);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
        Assert.Equal(new byte[] { 0xA1, 0xEF, 0xC3, 0xD4 }, actual.Read(Base + 0x100, 4));
        Assert.Equal(new byte[] { 0x57 }, actual.Read(Base + 0x1BAF, 1));
        Assert.Throws<NotSupportedException>(() => actual.Read(Base + 0x71, 1));
    }

    [Fact]
    public void Bounded_access_and_copy_reject_cross_window_ranges_and_overlap_without_changing_source()
    {
        NativeBattleScriptedEffectMemory source = NativeBattleScriptedEffectMemory.FromColdProcess().Write(Base + 0x80, [1, 2, 3, 4, 5, 6, 7, 8]);
        string before = JsonSerializer.Serialize(source);
        Assert.Throws<InvalidDataException>(() => source.Read(Base - 1, 1));
        Assert.Throws<InvalidDataException>(() => source.Read(Base + NativeBattleScriptedEffectMemory.Length, 1));
        Assert.Throws<InvalidDataException>(() => source.Read(Base, -1));
        Assert.Throws<InvalidDataException>(() => source.Write(Base + NativeBattleScriptedEffectMemory.Length - 1, new byte[2]));
        Assert.Throws<InvalidDataException>(() => source.MarkUnknown(uint.MaxValue, 1));
        Assert.Throws<InvalidDataException>(() => source.Copy(Base + 0x80, Base + NativeBattleScriptedEffectMemory.Length - 1, 2));
        Assert.Throws<NotSupportedException>(() => source.Copy(Base + 0x80, Base + 0x84, 8));
        Assert.Throws<NotSupportedException>(() => source.Copy(Base + 0x84, Base + 0x80, 8));
        Assert.Equal(before, JsonSerializer.Serialize(source));
        Assert.Empty(source.Read(Base + NativeBattleScriptedEffectMemory.Length, 0));
    }

    [Fact]
    public void Core_validation_rejects_regions_and_unknown_masks_outside_its_physical_window()
    {
        NativeBattleScriptedEffectMemory region = new(NativeBattleEffectArena.Clear().Write(NativeBattleScriptedEffectMemory.Length, [1]));
        Assert.Throws<InvalidDataException>(() => region.Validate());
        NativeBattleScriptedEffectMemory unknown = new(NativeBattleEffectArena.Clear().MarkUnknown(NativeBattleScriptedEffectMemory.Length, 1));
        Assert.Throws<InvalidDataException>(() => unknown.Validate());
        NativeBattleScriptedEffectMemory crossing = new(NativeBattleEffectArena.Clear().Write(NativeBattleScriptedEffectMemory.Length - 1, [1, 2]));
        Assert.Throws<InvalidDataException>(() => crossing.Validate());
    }
}
