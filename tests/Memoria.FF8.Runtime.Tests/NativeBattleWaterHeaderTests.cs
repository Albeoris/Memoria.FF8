using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWaterHeaderTests
{
    [Fact]
    public void Capture_preserves_order_deduplicates_targets_and_retains_unselected_warm_map_and_tail()
    {
        NativeBattleWaterHeader header = Warm();
        NativeBattleEffectArena arena = Arena();
        ImmutableArray<NativeBattleGraphicsModel> models = Models();
        string before = NativeBattleTestSnapshots.Json(models);
        var result = NativeBattleWaterHeaderCapture.CaptureAA4E90(header, arena, Bank, 1, 5, 7, 2, Contacts(3, 1, 3, 6), 4, models);
        Assert.Equal(7, result.CurrentHeaderIndex);
        Assert.Equal((byte)5, result.Header.Read(0x44, 1)[0]);
        Assert.Equal((byte)3, result.Header.Read(0x41, 1)[0]);
        Assert.Equal(new byte[] { 3, 1, 6 }, result.Header.Read(0x48, 3));
        Assert.Equal(header.Bytes[0x4B], result.Header.Read(0x4B, 1)[0]);
        Assert.Equal((byte)0, result.Header.Read(0xCC + 3, 1)[0]);
        Assert.Equal((byte)1, result.Header.Read(0xCC + 1, 1)[0]);
        Assert.Equal((byte)2, result.Header.Read(0xCC + 6, 1)[0]);
        Assert.Equal(header.Bytes[0xCC + 2], result.Header.Read(0xCC + 2, 1)[0]);
        Assert.Equal((byte)3, result.Arena.Read(Slot(1) + 0x1B, 1)[0]);
        Assert.Equal(arena.Read(Slot(0), 0x100), result.Arena.Read(Slot(0), 0x100));
        Assert.Equal(before, NativeBattleTestSnapshots.Json(models));
        for (int index = 0; index < 7; index++)
        {
            Assert.Equal(models[index].Flags0, U16(result.Header, 0x4E + index * 2));
        }
    }

    [Fact]
    public void Seven_unique_contacts_overwrite_first_flags_low_byte_after_the_flags_capture()
    {
        var captured = NativeBattleWaterHeaderCapture.CaptureAA4E90(Warm(), Arena(), Bank, 0, 2, 3, 5, Contacts(0, 1, 2, 3, 4, 5, 6), 7, Models());
        Assert.Equal(new byte[] { 0, 1, 2, 3, 4, 5, 6 }, captured.Header.Read(0x48, 7));
        Assert.Equal((ushort)0x1206, U16(captured.Header, 0x4E));
        Assert.Equal((ushort)0x1235, U16(captured.Header, 0x50));
        Assert.Equal((byte)7, captured.Header.Read(0x41, 1)[0]);
        Assert.Equal((byte)0, captured.Arena.Read(Slot(0) + 0x1B, 1)[0]);
        Assert.Equal(new byte[] { 0, 1, 2, 3, 4, 5, 6 }, captured.Header.Read(0xCC, 7));
    }

    [Fact]
    public void Zero_count_still_reads_explicit_first_header_actor_and_writes_slot_target()
    {
        NativeBattleWaterHeader header = Warm();
        var result = NativeBattleWaterHeaderCapture.CaptureAA4E90(header, Arena(), Bank, 0, 6, 11, 4, Contacts(255), 0, Models());
        Assert.Equal(11, result.CurrentHeaderIndex);
        Assert.Equal((byte)4, result.Header.Read(0x48, 1)[0]);
        Assert.Equal((byte)0, result.Header.Read(0x41, 1)[0]);
        Assert.Equal((byte)6, result.Header.Read(0x44, 1)[0]);
        Assert.Equal((byte)4, result.Arena.Read(Slot(0) + 0x1B, 1)[0]);
        Assert.Equal(header.Bytes.Skip(0xCC).Take(7), result.Header.Read(0xCC, 7));
        Assert.Equal(header.Bytes.Skip(0x49).Take(3), result.Header.Read(0x49, 3));
    }

    [Theory]
    [InlineData(128)]
    [InlineData(255)]
    public void High_contact_count_is_unsigned_and_requires_its_actual_contact_owner(byte count)
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleWaterHeaderCapture.CaptureAA4E90(Warm(), Arena(), Bank, 0, 0, 0, 0, [], count, Models()));
        ImmutableArray<byte> contacts = Enumerable.Repeat((byte)3, count).ToImmutableArray();
        var result = NativeBattleWaterHeaderCapture.CaptureAA4E90(Warm(), Arena(), Bank, 0, 0, 0, 0, contacts, count, Models());
        Assert.Equal((byte)1, result.Header.Read(0x41, 1)[0]);
        Assert.Equal((byte)3, result.Arena.Read(Slot(0) + 0x1B, 1)[0]);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(31)]
    [InlineData(255)]
    public void Positive_count_rejects_actor_outside_actual_seven_physical_rows(byte actor)
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleWaterHeaderCapture.CaptureAA4E90(Warm(), Arena(), Bank, 0, 0, 0, 0, Contacts(actor), 1, Models()));
    }

    [Fact]
    public void Cache_copies_all_physical_rows_with_position_vertical_direct_Euler_and_both_scales()
    {
        ImmutableArray<NativeBattleGraphicsModel> models = Models();
        string before = NativeBattleTestSnapshots.Json(models);
        NativeBattleWaterHeader header = NativeBattleWaterHeaderCapture.CacheRowsAA4B60(Warm(), models);
        for (int index = 0; index < 7; index++)
        {
            Assert.Equal(0x1D972C0u + 0x9Cu * (uint)index, U32(header, 0x60 + index * 4));
            NativeBattleModelTransform transform = models[index].Transform!;
            Assert.Equal(new short[] { transform.Position28.X, transform.Position28.Y, transform.Position28.Z, transform.Vertical34 }, Words(header, 0x84 + index * 8, 4));
            Assert.Equal(new short[] { models[index].Rotation12!.Value, models[index].Rotation14!.Value, models[index].Rotation16!.Value }, Words(header, 0xD8 + index * 8, 3));
            Assert.False(header.Known[0xD8 + index * 8 + 6]);
            Assert.False(header.Known[0xD8 + index * 8 + 7]);
            Assert.Throws<NotSupportedException>(() => header.Read(0xD8 + index * 8 + 6, 2));
            Assert.Equal(new short[] { transform.Scale148.X, transform.Scale148.Y, transform.Scale148.Z, transform.Scale154 }, Words(header, 0x120 + index * 8, 4));
        }
        Assert.Equal(before, NativeBattleTestSnapshots.Json(models));
    }

    [Fact]
    public void Unknown_unloaded_fields_clear_previous_destination_knowledge_without_filling_from_pose_or_padding()
    {
        ImmutableArray<NativeBattleGraphicsModel> models = Models().SetItem(2, new(2, 0) { Rotation12 = -9, Rotation14 = null, Rotation16 = 12 });
        NativeBattleWaterHeader cached = NativeBattleWaterHeaderCapture.CacheRowsAA4B60(Warm(), models);
        Assert.All(cached.Known.Skip(0x84 + 16).Take(8), known => Assert.False(known));
        Assert.All(cached.Known.Skip(0x120 + 16).Take(8), known => Assert.False(known));
        Assert.Equal((short)-9, I16(cached, 0xD8 + 16));
        Assert.Equal((short)12, I16(cached, 0xD8 + 20));
        Assert.False(cached.Known[0xD8 + 18]);
        Assert.False(cached.Known[0xD8 + 19]);
        Assert.False(cached.Known[0xD8 + 22]);
        Assert.False(cached.Known[0xD8 + 23]);
        Assert.Throws<NotSupportedException>(() => cached.Read(0x84 + 16, 2));
        var captured = NativeBattleWaterHeaderCapture.CaptureAA4E90(cached, Arena(), Bank, 0, 0, 0, 0, [], 0, models);
        Assert.Equal((ushort)0, U16(captured.Header, 0x4E + 4));
        Assert.Equal((ushort)0x1237, U16(captured.Header, 0x4E + 6));
    }

    [Fact]
    public void Cold_is_known_zero_imported_unknown_remains_unknown_and_JSON_next_capture_is_identical()
    {
        NativeBattleWaterHeader cold = NativeBattleWaterHeader.FromColdProcess();
        cold.Validate();
        Assert.Equal(new byte[0x17C], cold.Read(0, 0x17C));
        NativeBattleWaterHeader unknown = NativeBattleWaterHeader.Unknown();
        unknown.Validate();
        Assert.Throws<NotSupportedException>(() => unknown.Read(0, 1));
        NativeBattleWaterHeader source = NativeBattleWaterHeaderCapture.CacheRowsAA4B60(unknown, Models());
        NativeBattleWaterHeader restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterHeader>(NativeBattleTestSnapshots.Json(source))!;
        restored.Validate();
        Assert.Equal(source.Known.ToArray(), restored.Known.ToArray());
        var expected = NativeBattleWaterHeaderCapture.CaptureAA4E90(source, Arena(), Bank, 0, 2, 5, 3, Contacts(6, 6, 0), 3, Models());
        var actual = NativeBattleWaterHeaderCapture.CaptureAA4E90(restored, Arena(), Bank, 0, 2, 5, 3, Contacts(6, 6, 0), 3, Models());
        Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(actual));
        Assert.Throws<InvalidDataException>(() => (cold with { Bytes = [] }).Validate());
        Assert.Throws<InvalidDataException>(() => (cold with { Known = [] }).Validate());
    }

    private static readonly NativeBattleWaterSlotBank Bank = new(0x1000, 0x1000, 8, 0x1058);
    private static int Slot(int index) => 0x1000 + 0x58 + index * 0x100;
    private static NativeBattleEffectArena Arena()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(0x1000, Enumerable.Repeat((byte)0xAE, 0x1000).ToArray());
        return arena.Write(0x101C, new byte[] { 0x58, 0, 0, 0 });
    }

    private static NativeBattleWaterHeader Warm() => new(Enumerable.Range(0, 0x17C).Select(index => unchecked((byte)(index + 31))).ToImmutableArray(), Enumerable.Repeat(true, 0x17C).ToImmutableArray());

    private static ImmutableArray<byte> Contacts(params byte[] actors) => actors.ToImmutableArray();

    private static ImmutableArray<NativeBattleGraphicsModel> Models() => Enumerable.Range(0, 7).Select(index => new NativeBattleGraphicsModel(index, (ushort)(0x1234 + index))
    {
        Rotation12 = (short)(-100 - index),
        Rotation14 = (short)(200 + index),
        Rotation16 = (short)(-300 - index),
        Transform = new(new(111, 222, 333), new((short)(10 + index), (short)(-20 - index), (short)(30 + index)), (short)(-40 - index), 777, 888)
        {
            Scale148 = new((short)(1000 + index), (short)(-2000 - index), (short)(3000 + index)),
            Scale154 = (short)(-4000 - index)
        }
    }).ToImmutableArray();

    private static ushort U16(NativeBattleWaterHeader header, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(header.Read(offset, 2).ToArray());
    private static short I16(NativeBattleWaterHeader header, int offset) => BinaryPrimitives.ReadInt16LittleEndian(header.Read(offset, 2).ToArray());
    private static uint U32(NativeBattleWaterHeader header, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(header.Read(offset, 4).ToArray());
    private static short[] Words(NativeBattleWaterHeader header, int offset, int count) => Enumerable.Range(0, count).Select(index => I16(header, offset + index * 2)).ToArray();
}
