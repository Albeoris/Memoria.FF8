using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed partial class NativeBattlePandemona291EntryTests
{
    [Fact]
    public void ConstructorSortsWholeTargetRecordsAndPreservesUnknownCopiedRotationPadding()
    {
        var fixture = Fixture();
        uint[] addresses = [0x13A0E58, 0x13A4F88, 0x13A6508, 0x13A8508, 0x13AB5C0, 0x13AC1B8, 0x13ACDB0];
        ImmutableArray<NativeBattlePandemona291Animation> animations = addresses.Select(address =>
        {
            byte[] bytes = new byte[16];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 1);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 0xFF01);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 0x1234);
            return new NativeBattlePandemona291Animation(address, bytes.ToImmutableArray());
        }).ToImmutableArray();
        NativeBattlePandemona291Resources resources = fixture.Resources with { AnimationSources = animations };
        NativeBattlePandemona291Frame source = fixture.Frame with { Effect = fixture.Frame.Effect with { Binding = fixture.Frame.Effect.Binding with { Pandemona291Resources = resources } } };
        source = NativeBattlePandemona291Entry.Initialize6ED250(source, resources, fixture.Packets, Model);
        int vibrations = 0;
        NativeBattlePandemona291ConstructionInputs inputs = new(fixture.Packets, Model, (frame, actor) => new(frame, new((short)(100 + actor), (short)(-200 - actor))), frame =>
        {
            vibrations++;
            return new(frame with { Memory = frame.Memory with { Pandemona291 = frame.Memory.Pandemona291! with { Scratch25562EC = 77 } } }, 456);
        });
        NativeBattlePandemona291Frame result = NativeBattlePandemona291Construction.Initialize6ED3B6(source, inputs);
        Assert.Equal(1, vibrations);
        Assert.Equal(6, result.Effect.Pools.Length);
        NativeBattleEffectTaskPool creature = result.Effect.Pools.Single(pool => pool.DescriptorAddress == 0x25562A8);
        Assert.Equal(0x6ED900u, creature.Slots[0].Callback);
        Assert.Equal(456u, creature.Slots[0].ReadDword(0x10));
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 0x21A4u + 0x42Cu, creature.Slots[0].ReadDword(0x102C));
        NativeBattleEffectArena arena = result.Memory.Arena!;
        int record = 0x21A4 + 0x14;
        Assert.Equal(0x1D972C0u + 156, BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(record, 4)));
        Assert.Equal((ushort)101, BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(record + 4, 2)));
        Assert.Equal(unchecked((ushort)-201), BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(record + 6, 2)));
        Assert.Equal((ushort)16, BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(record + 8, 2)));
        Assert.Equal((ushort)0x7FF, BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(record + 0x16, 2)));
        Assert.Equal(new byte[] { 0, 1, 1, 0 }, arena.Read(record + 0x1C, 4));
        Assert.Throws<NotSupportedException>(() => arena.Read(record + 0x18, 4));
        Assert.Equal(new bool[] { true, true, false, false }, arena.ReadSnapshot(record + 0x18, 4).Known);
        Assert.Equal(77u, result.Memory.Pandemona291!.Scratch25562EC);
        Assert.Equal(0xF01, BinaryPrimitives.ReadUInt16LittleEndian(result.Memory.Pandemona291.Animations!.Value[0].Bytes.AsSpan().Slice(14, 2)));
        Assert.Equal(0x1234, BinaryPrimitives.ReadUInt16LittleEndian(result.Memory.Pandemona291.Animations.Value[3].Bytes.AsSpan().Slice(14, 2)));
        Assert.Equal(0x231D4, result.Memory.EffectIo!.BaseOffset);
        Assert.Equal(new byte[] { 0, 0, 1, 0, 0 }, result.Memory.Arena!.Read(0x231D4, 5));
        NativeBattlePandemona291Frame restored = JsonSerializer.Deserialize<NativeBattlePandemona291Frame>(JsonSerializer.Serialize(result))!;
        Assert.Equal(arena.ReadSnapshot(record, 32).Known, restored.Memory.Arena!.ReadSnapshot(record, 32).Known);
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(restored));

        static NativeBattleGraphicsModel Model(int actor) => new(actor, 2)
        {
            HasLinkedModelRing140 = false,
            Rotation12 = 3,
            Rotation14 = -1,
            Rotation16 = 5,
            Transform = new(new(0, 0, 0), new((short)(actor == 0 ? -10 : 20), 3, (short)(actor == 0 ? -103 : 100)), 7, 0, 0)
        };
    }
}
