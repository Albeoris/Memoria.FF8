using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Tests.NativeBattleTypedFixture;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWaterHeaderTests
{
    [Fact]
    public void Capture_deduplicates_contacts_in_order_and_preserves_unwritten_raw_fields()
    {
        NativeBattleGraphicsState graphics = Graphics();
        NativeBattleWaterHeader header = Warm();
        NativeBattleEffectArena arena = Arena();
        var result = NativeBattleWaterHeaderCapture.CaptureAA4E90(header, arena, Bank, 1, OriginalOwner(5), OriginalOwner(2), Contacts(3, 1, 3, 6), graphics.ActorModels, graphics.NativeBindings, BattleContactTaskStorageContract.OriginalArena);
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
        Assert.Equal(Contacts(3, 1, 6), result.Header.PresentationActors!.CurrentActors);
        for (int slot = 0; slot < 7; slot++)
        {
            Assert.Equal(Model(slot).Flags0, U16(result.Header, 0x4E + slot * 2));
        }
    }

    [Theory]
    [InlineData(BattleContactTaskStorageContract.OriginalArena, 0x1206)]
    [InlineData(BattleContactTaskStorageContract.ExpandableArena, 0x1234)]
    public void Seven_recipients_share_the_raw_flags_byte_in_the_original_arena_only(BattleContactTaskStorageContract storage, ushort expectedFlags)
    {
        NativeBattleGraphicsState graphics = Graphics();
        var result = NativeBattleWaterHeaderCapture.CaptureAA4E90(Warm(), Arena(), Bank, 0, OriginalOwner(2), OriginalOwner(3), Contacts(0, 1, 2, 3, 4, 5, 6), graphics.ActorModels, graphics.NativeBindings, storage);
        Assert.Equal(expectedFlags, U16(result.Header, 0x4E));
        Assert.Equal(expectedFlags, result.Header.PresentationActors!.ReadCachedActor(OriginalOwner(0)).Flags0);
        Assert.Equal((ushort)0x1235, U16(result.Header, 0x50));
        Assert.Equal((byte)7, result.Header.Read(0x41, 1)[0]);
        Assert.Equal((byte)0, result.Arena.Read(Slot(0) + 0x1B, 1)[0]);
        Assert.Equal(new byte[] { 0, 1, 2, 3, 4, 5, 6 }, result.Header.Read(0xCC, 7));
    }

    [Fact]
    public void Zero_contacts_keep_the_explicit_first_owner_and_retain_ordinals()
    {
        NativeBattleGraphicsState graphics = Graphics();
        NativeBattleWaterHeader header = Warm();
        var result = NativeBattleWaterHeaderCapture.CaptureAA4E90(header, Arena(), Bank, 0, OriginalOwner(6), OriginalOwner(4), [], graphics.ActorModels, graphics.NativeBindings, BattleContactTaskStorageContract.OriginalArena);
        Assert.Equal((byte)4, result.Header.Read(0x48, 1)[0]);
        Assert.Equal((byte)0, result.Header.Read(0x41, 1)[0]);
        Assert.Equal((byte)6, result.Header.Read(0x44, 1)[0]);
        Assert.Equal((byte)4, result.Arena.Read(Slot(0) + 0x1B, 1)[0]);
        Assert.Equal(OriginalOwner(4), result.Header.PresentationActors!.ReadFirstActor());
        Assert.Equal(header.Bytes.Skip(0xCC).Take(7), result.Header.Read(0xCC, 7));
        Assert.Equal(header.Bytes.Skip(0x49).Take(3), result.Header.Read(0x49, 3));
    }

    [Fact]
    public void Cache_copies_known_original_model_facts_and_leaves_unknown_fields_unknown()
    {
        NativeBattleGraphicsState graphics = Graphics();
        BattleActorModels models = graphics.ActorModels.Set(OriginalOwner(2), new(0) { Rotation12 = -9, Rotation16 = 12 });
        NativeBattleWaterHeader header = Warm() with { PresentationActors = NativeBattleCinematicActorContext.FromGraphics(models) };
        NativeBattleWaterHeader cached = NativeBattleWaterHeaderCapture.CacheRowsAA4B60(header, models, graphics.NativeBindings);
        for (int slot = 0; slot < 7; slot++)
        {
            Assert.Equal(0x1D972C0u + 0x9Cu * (uint)slot, U32(cached, 0x60 + slot * 4));
            Assert.False(cached.Known[0xDE + slot * 8]);
            Assert.False(cached.Known[0xDF + slot * 8]);
            if (slot == 2)
            {
                continue;
            }
            NativeBattleGraphicsModel model = models.Get(OriginalOwner(slot));
            NativeBattleModelTransform transform = model.Transform!;
            Assert.Equal(new short[] { transform.Position28.X, transform.Position28.Y, transform.Position28.Z, transform.Vertical34 }, Words(cached, 0x84 + slot * 8, 4));
            Assert.Equal(new[] { model.Rotation12!.Value, model.Rotation14!.Value, model.Rotation16!.Value }, Words(cached, 0xD8 + slot * 8, 3));
            Assert.Equal(new short[] { transform.Scale148.X, transform.Scale148.Y, transform.Scale148.Z, transform.Scale154 }, Words(cached, 0x120 + slot * 8, 4));
        }
        Assert.All(cached.Known.Skip(0x84 + 16).Take(8), known => Assert.False(known));
        Assert.All(cached.Known.Skip(0x120 + 16).Take(8), known => Assert.False(known));
        Assert.Equal((short)-9, I16(cached, 0xD8 + 16));
        Assert.Equal((short)12, I16(cached, 0xD8 + 20));
        Assert.False(cached.Known[0xD8 + 18]);
        Assert.False(cached.Known[0xD8 + 19]);
        Assert.Throws<NotSupportedException>(() => cached.Read(0x84 + 16, 2));
    }

    [Fact]
    public void Expanded_capture_keeps_full_owner_order_occurrences_and_the_live_cached_state()
    {
        NativeBattleGraphicsState graphics = Graphics();
        BattleModelOwner[] extras = Enumerable.Range(7, 260).Select(index => (BattleModelOwner)new BattleEnemyActorModel(new($"extra-{index}"))).ToArray();
        BattleActorModels models = graphics.ActorModels;
        foreach (BattleModelOwner owner in extras)
        {
            models = models.Add(owner, Model(7));
        }
        BattleModelOwner[] order = [extras[^1], .. graphics.ActorModels.Owners, .. extras[..^1]];
        ImmutableArray<BattleModelOwner> repeated = [.. Enumerable.Range(0, 600).Select(index => order[index % order.Length])];
        NativeBattleWaterHeader header = Warm() with { PresentationActors = NativeBattleCinematicActorContext.FromGraphics(models).WithSavedActors([extras[0], extras[^1], extras[0]]).WithContactsProcessed(49) };
        BattleActorModels changed = models.Set(extras[^1], Model(9) with { Rotation14 = -200 });
        var result = NativeBattleWaterHeaderCapture.CaptureAA4E90(header, Arena(), Bank, 1, extras[0], OriginalOwner(5), repeated, changed, graphics.NativeBindings, BattleContactTaskStorageContract.ExpandableArena);
        NativeBattleCinematicActorContext actual = result.Header.PresentationActors!;

        Assert.False(result.Header.Known[0x44]);
        Assert.False(result.Header.Known[0x41]);
        Assert.False(result.Header.Known[0x48]);
        Assert.Equal(order, actual.CurrentActors);
        Assert.Equal(extras[^1], actual.ReadFirstActor());
        Assert.Equal(header.PresentationActors!.SavedActors, actual.SavedActors);
        Assert.Equal(49, actual.ContactsProcessed);
        Assert.Equal(266, actual.ReadActorOrdinal(extras[^2]));
        Assert.Equal(models.Get(extras[^1]).Rotation14, actual.ReadCachedActor(extras[^1]).Rotation14);

        NativeBattleWaterHeader refreshed = NativeBattleWaterHeaderCapture.CacheRowsAA4B60(result.Header, changed, graphics.NativeBindings);
        Assert.Equal((short)-200, refreshed.PresentationActors!.ReadCachedActor(extras[^1]).Rotation14);
        Assert.Equal(order, refreshed.PresentationActors.CurrentActors);
        string json = NativeBattleTestSnapshots.Json(refreshed);
        NativeBattleWaterHeader restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterHeader>(json)!;
        Assert.Null(restored.PresentationActors);
        Assert.Equal(refreshed.Bytes.ToArray(), restored.Bytes.ToArray());
        Assert.Equal(refreshed.Known.ToArray(), restored.Known.ToArray());
    }

    [Fact]
    public void Capture_rejects_missing_current_owners()
    {
        NativeBattleGraphicsState graphics = Graphics();
        BattleModelOwner missing = new BattlePartyActorModel(new("retired"));
        Assert.Throws<InvalidDataException>(() => NativeBattleWaterHeaderCapture.CaptureAA4E90(Warm(), Arena(), Bank, 0, missing, OriginalOwner(0), [], graphics.ActorModels, graphics.NativeBindings, BattleContactTaskStorageContract.OriginalArena));
        Assert.Throws<InvalidDataException>(() => NativeBattleWaterHeaderCapture.CaptureAA4E90(Warm(), Arena(), Bank, 0, OriginalOwner(0), missing, [missing], graphics.ActorModels, graphics.NativeBindings, BattleContactTaskStorageContract.OriginalArena));
    }

    private static NativeBattleGraphicsState Graphics() => OriginalGraphics(Enumerable.Range(0, 7).Select(slot => (slot, OriginalOwner(slot), Model(slot))));

    private static readonly NativeBattleWaterSlotBank Bank = new(0x1000, 0x1000, 8, 0x1058);
    private static int Slot(int index) => 0x1000 + 0x58 + index * 0x100;
    private static NativeBattleEffectArena Arena()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(0x1000, Enumerable.Repeat((byte)0xAE, 0x1000).ToArray());
        return arena.Write(0x101C, new byte[] { 0x58, 0, 0, 0 });
    }

    private static NativeBattleWaterHeader Warm() => new(Enumerable.Range(0, 0x17C).Select(index => unchecked((byte)(index + 31))).ToImmutableArray(), Enumerable.Repeat(true, 0x17C).ToImmutableArray());

    private static ImmutableArray<BattleModelOwner> Contacts(params int[] slots) => slots.Select(OriginalOwner).ToImmutableArray();

    private static NativeBattleGraphicsModel Model(int index) => new((ushort)(0x1234 + index))
    {
        Rotation12 = (short)(-100 - index),
        Rotation14 = (short)(200 + index),
        Rotation16 = (short)(-300 - index),
        Transform = new(new(111, 222, 333), new((short)(10 + index), (short)(-20 - index), (short)(30 + index)), (short)(-40 - index), 777, 888)
        {
            Scale148 = new((short)(1000 + index), (short)(-2000 - index), (short)(3000 + index)),
            Scale154 = (short)(-4000 - index)
        }
    };

    private static ushort U16(NativeBattleWaterHeader header, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(header.Read(offset, 2).ToArray());
    private static short I16(NativeBattleWaterHeader header, int offset) => BinaryPrimitives.ReadInt16LittleEndian(header.Read(offset, 2).ToArray());
    private static uint U32(NativeBattleWaterHeader header, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(header.Read(offset, 4).ToArray());
    private static short[] Words(NativeBattleWaterHeader header, int offset, int count) => Enumerable.Range(0, count).Select(index => I16(header, offset + index * 2)).ToArray();
}
