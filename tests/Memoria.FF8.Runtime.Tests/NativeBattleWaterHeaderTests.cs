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

    [Fact]
    public void Hosted_capture_preserves_flags_fields_and_deduplicates_full_occurrence_stream()
    {
        ImmutableArray<NativeBattleGraphicsModel> old = HostedModels();
        NativeBattleWaterHeader initial = HostedHeader(old);
        NativeBattleWaterHeader header = NativeBattleWaterHeaderCapture.CacheRowsAA4B60(initial with { PresentationActors = initial.PresentationActors!.WithSavedActors([7, 24, 7]) }, old);
        NativeBattleCinematicActorContext previous = header.PresentationActors!;
        ImmutableArray<NativeBattleGraphicsModel> changed = old.Select(model => model with
        {
            Flags0 = (ushort)(0xAB00 + model.Index),
            Rotation14 = -200,
            Transform = model.Transform is { } transform ? transform with { Position28 = new(10, 20, 30) } : null
        }).ToImmutableArray();
        byte[] order = [24, 7, .. Enumerable.Range(0, 24).Where(row => row != 7).Select(row => (byte)row)];
        ImmutableArray<byte> contacts = Enumerable.Range(0, 300).Select(index => order[index % order.Length]).Append(byte.MaxValue).ToImmutableArray();
        var result = NativeBattleWaterHeaderCapture.CaptureAA4E90(header, Arena(), Bank, 1, 7, 5, 16, contacts, 300, changed);
        NativeBattleCinematicActorContext actual = result.Header.PresentationActors!;

        Assert.Equal(5, result.CurrentHeaderIndex);
        Assert.Equal((byte)7, result.Header.Read(0x44, 1)[0]);
        Assert.Equal((byte)25, result.Header.Read(0x41, 1)[0]);
        Assert.Equal(order[..6], result.Header.Read(0x48, 6).ToArray());
        Assert.Equal((byte)24, result.Arena.Read(Slot(1) + 0x1B, 1)[0]);
        Assert.Equal(order, actual.CurrentActors.ToArray());
        Assert.Equal((byte)24, actual.ReadFirstActor());
        Assert.Equal(previous.SavedActors, actual.SavedActors);
        Assert.Equal(previous.ContactCursor, actual.ContactCursor);
        for (int row = 0; row < changed.Length; row++)
        {
            Assert.Equal(previous.ReadCachedActor(row) with { Flags0 = changed[row].Flags0 }, actual.ReadCachedActor(row));
            Assert.Equal((byte)Array.IndexOf(order, (byte)row), actual.ReadActorOrdinal(row));
            if (row < 7)
            {
                Assert.Equal(changed[row].Flags0, U16(result.Header, 0x4E + row * 2));
                Assert.Equal(actual.ReadActorOrdinal(row), result.Header.Read(0xCC + row, 1)[0]);
            }
        }
        Assert.Equal(header.Bytes[0xD3], result.Header.Bytes[0xD3]);
        Assert.Equal(header.Bytes[0x60..0xC0].ToArray(), result.Header.Bytes[0x60..0xC0].ToArray());
        Assert.Equal(header.Bytes[0xD8..].ToArray(), result.Header.Bytes[0xD8..].ToArray());

        string json = NativeBattleTestSnapshots.Json(result.Header);
        NativeBattleWaterHeader restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterHeader>(json)!;
        Assert.Equal(json, NativeBattleTestSnapshots.Json(restored));
        NativeBattleWaterHeader refreshed = NativeBattleWaterHeaderCapture.CacheRowsAA4B60(restored, changed);
        NativeBattleCinematicActorContext updated = refreshed.PresentationActors!;
        Assert.Equal(new NativeBattleVector(10, 20, 30), updated.ReadCachedActor(24).Position28);
        Assert.Equal((short)-200, updated.ReadCachedActor(24).Rotation14);
        Assert.Equal(actual.CurrentActors.ToArray(), updated.CurrentActors.ToArray());
        Assert.Equal(actual.ActorOrdinals.ToArray(), updated.ActorOrdinals.ToArray());
        Assert.Equal(actual.ReadSavedActors().ToArray(), updated.ReadSavedActors().ToArray());
        Assert.Equal(actual.ContactCursor, updated.ContactCursor);
        Assert.Equal(new short[] { 10, 20, 30 }, Words(refreshed, 0x84, 3));
        Assert.Equal(restored.Bytes[0x7C], refreshed.Bytes[0x7C]);
    }

    [Fact]
    public void Hosted_zero_contacts_use_explicit_first_actor_without_changing_ordinals_or_saved_list()
    {
        ImmutableArray<NativeBattleGraphicsModel> models = HostedModels();
        NativeBattleWaterHeader header = HostedHeader(models);
        var result = NativeBattleWaterHeaderCapture.CaptureAA4E90(header, Arena(), Bank, 0, 7, 0, 24, default, 0, models);
        Assert.Equal((byte)0, result.Header.Read(0x41, 1)[0]);
        Assert.Equal((byte)24, result.Header.Read(0x48, 1)[0]);
        Assert.Equal((byte)24, result.Arena.Read(Slot(0) + 0x1B, 1)[0]);
        NativeBattleCinematicActorContext actual = result.Header.PresentationActors!;
        Assert.Empty(actual.CurrentActors);
        Assert.Equal((byte)24, actual.ReadFirstActor());
        Assert.Null(actual.SavedActors);
        Assert.Equal(header.PresentationActors!.ActorOrdinals, actual.ActorOrdinals);
        Assert.Equal(header.Bytes[0xCC..0xD3].ToArray(), result.Header.Bytes[0xCC..0xD3].ToArray());
    }

    [Fact]
    public void Capture_rejects_invalid_counts_and_hosted_unbound_rows()
    {
        ImmutableArray<NativeBattleGraphicsModel> models = HostedModels();
        NativeBattleWaterHeader header = HostedHeader(models, hole: 6);
        Assert.Throws<InvalidDataException>(() => NativeBattleWaterHeaderCapture.CaptureAA4E90(header, Arena(), Bank, 0, 6, 0, 24, [], 0, models));
        Assert.Throws<InvalidDataException>(() => NativeBattleWaterHeaderCapture.CaptureAA4E90(header, Arena(), Bank, 0, 7, 0, 6, [], 0, models));
        Assert.Throws<InvalidDataException>(() => NativeBattleWaterHeaderCapture.CaptureAA4E90(header, Arena(), Bank, 0, 7, 0, 24, [6], 1, models));
        Assert.Throws<InvalidDataException>(() => NativeBattleWaterHeaderCapture.CaptureAA4E90(header, Arena(), Bank, 0, 7, 0, 24, [24], 2, models));
        Assert.Throws<InvalidDataException>(() => NativeBattleWaterHeaderCapture.CaptureAA4E90(header, Arena(), Bank, 0, 7, 0, 24, [], -1, models));
        Assert.Throws<InvalidDataException>(() => NativeBattleWaterHeaderCapture.CaptureAA4E90(header, Arena(), Bank, 0, 7, 0, 24, [], NativeBattleContactAddress.MaximumRowCount + 1, models));
        Assert.Throws<InvalidDataException>(() => NativeBattleWaterHeaderCapture.CaptureAA4E90(header, Arena(), Bank, 0, 7, 0, 24, [], 0, models.RemoveAt(24)));
        Assert.Throws<InvalidDataException>(() => NativeBattleWaterHeaderCapture.CacheRowsAA4B60(header, models.SetItem(24, new(23, 0))));
        Assert.Throws<NotSupportedException>(() => NativeBattleWaterHeaderCapture.CaptureAA4E90(Warm(), Arena(), Bank, 0, 0, 0, 0, [], -1, Models()));
        Assert.Throws<NotSupportedException>(() => NativeBattleWaterHeaderCapture.CaptureAA4E90(Warm(), Arena(), Bank, 0, 0, 0, 0, Enumerable.Repeat((byte)0, 256).ToImmutableArray(), 256, Models()));
    }

    private static ImmutableArray<NativeBattleGraphicsModel> HostedModels()
    {
        ImmutableArray<NativeBattleGraphicsModel> native = Models();
        return [.. native, .. Enumerable.Range(7, 18).Select(row => native[row % native.Length] with { Index = row })];
    }

    private static NativeBattleWaterHeader HostedHeader(ImmutableArray<NativeBattleGraphicsModel> models, int? hole = null)
    {
        NativeBattleGraphicsRoster roster = new(Enumerable.Range(0, models.Length).Select(row => row == hole ? null : new NativeBattleGraphicsActor($"actor-{row}", row % 2 == 0)).ToImmutableArray());
        NativeBattleCinematicActorContext actors = NativeBattleCinematicActorContext.FromGraphics(roster, models).CaptureCurrentContacts([24, 7], 0).WithContactCursor(49);
        return Warm() with { PresentationActors = actors };
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
