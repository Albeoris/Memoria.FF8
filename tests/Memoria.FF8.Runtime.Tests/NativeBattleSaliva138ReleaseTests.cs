using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleSaliva138Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSaliva138ReleaseTests
{
    [Fact]
    public void Native_completed_root_transfers_undrained_children_without_clearing_raw_flags_links_or_unknown_payload()
    {
        NativeBattleSaliva138Frame frame = Frame();
        NativeBattleSaliva138Frame restored = JsonSerializer.Deserialize<NativeBattleSaliva138Frame>(JsonSerializer.Serialize(frame))!;
        NativeBattleSaliva138Frame released = NativeBattleSaliva138Controller.ReleaseReturnedBanks(frame);
        NativeBattleSaliva138Frame other = NativeBattleSaliva138Controller.ReleaseReturnedBanks(restored);
        Assert.Equal(JsonSerializer.Serialize(released), JsonSerializer.Serialize(other));
        Assert.Equal(RootDescriptor, Assert.Single(released.Effect.Pools).DescriptorAddress);
        Assert.Equal(0u, released.Effect.ReturnedDescriptor);
        Assert.Same(frame.State, released.State);
        Assert.Equal(frame.Random, released.Random);
        int curve = CurveOffset;
        NativeBattleEffectArena arena = released.Memory.Arena!;
        Assert.Equal((ushort)1, BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(curve, 2)));
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + CurveOffset + CurveStride, BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(curve + 4, 4)));
        Assert.Equal(CurveCallback, BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(curve + 8, 4)));
        Assert.Equal((ushort)0x1234, BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(curve + 12, 2)));
        Assert.Throws<NotSupportedException>(() => arena.Read(curve + 14, 2));
        Assert.Equal((ushort)1, BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(MovingSpriteOffset, 2)));
        Assert.Equal(MovingSpriteCallback, BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(MovingSpriteOffset + 8, 4)));
        Assert.Throws<NotSupportedException>(() => frame.Memory.Arena!.Read(curve, 2));
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void Returned_root_or_required_survivors_cannot_be_ignored(bool rootReturned, bool mainActive, bool simpleActive)
    {
        NativeBattleSaliva138Frame frame = Frame();
        ImmutableArray<NativeBattleEffectTaskPool> pools = frame.Effect.Pools;
        if (mainActive)
        {
            (NativeBattleEffectTaskPool pool, _) = pools[1].Append(MainCallback);
            pools = pools.SetItem(1, pool);
        }
        if (simpleActive)
        {
            (NativeBattleEffectTaskPool pool, _) = pools[4].Append(SimpleSpriteCallback);
            pools = pools.SetItem(4, pool);
        }
        frame = frame with { Effect = frame.Effect with { Pools = pools, ReturnedDescriptor = rootReturned ? RootDescriptor : 0u } };
        string before = JsonSerializer.Serialize(frame);
        Assert.Throws<InvalidDataException>(() => NativeBattleSaliva138Controller.ReleaseReturnedBanks(frame));
        Assert.Equal(before, JsonSerializer.Serialize(frame));
    }

    private static NativeBattleSaliva138Frame Frame()
    {
        uint working = NativeBattleEffectNodeMemory.ArenaAddress;
        NativeBattleEffectTaskPool[] pools =
        [
            NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, RootStride, 1),
            NativeBattleEffectTaskPool.Initialize(MainDescriptor, working + MainOffset, MainStride, 1),
            NativeBattleEffectTaskPool.Initialize(CurveDescriptor, working + CurveOffset, CurveStride, CurveCount),
            NativeBattleEffectTaskPool.Initialize(MovingSpriteDescriptor, working + MovingSpriteOffset, MovingSpriteStride, MovingSpriteCount),
            NativeBattleEffectTaskPool.Initialize(SimpleSpriteDescriptor, working, SimpleSpriteStride, SimpleSpriteCount)
        ];
        (pools[2], int curve) = pools[2].Append(CurveCallback);
        (pools[2], _) = pools[2].Append(CurveCallback);
        pools[2] = pools[2] with { Slots = pools[2].Slots.SetItem(curve, pools[2].Slots[curve].WriteWord(12, 0x1234).MarkPayloadUnknown(14, 2)) };
        (pools[3], _) = pools[3].Append(MovingSpriteCallback);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().MarkUnknown(0, PointsOffset) };
        NativeBattleOriginalEffectState effect = new(138, 137, new(138, Entry, Initializer), new(0, 0, 0, 0, 0, 0), 0) { Pools = pools.ToImmutableArray() };
        return new(memory, effect, new(55), NativeBattleSaliva138State.FromColdProcess().WriteDword(WorkingGlobal, working));
    }
}
