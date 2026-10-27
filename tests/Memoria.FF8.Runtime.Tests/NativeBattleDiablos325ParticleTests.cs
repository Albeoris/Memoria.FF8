using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDiablos325ParticleTests
{
    [Fact]
    public void Bank_constructor_clears_previous_shape_bytes_then_links_five_exact_programmes_and_workspaces()
    {
        NativeBattleDiablos325Frame source = Fixture();
        var preserved = NativeBattleStorageSnapshots.Capture(source);
        string original = NativeBattleTestSnapshots.Json(preserved);
        NativeBattleDiablos325Frame frame = NativeBattleDiablos325Particles.Initialize6548B9(source);
        NativeBattleEffectTaskPool pool = Pool(frame);
        Assert.Equal(0x20DFAB8u + 0x3505C, pool.BankAddress);
        Assert.Equal((ushort)0xE8, pool.Stride);
        Assert.Equal(16, pool.Slots.Length);
        Assert.Equal(0, pool.Head);
        Assert.Equal(4, pool.Tail);
        short[] x = [-500, -200, 0, 200, 500];
        uint[] programme = [0x4AFAC, 0x4BBC4, 0x4B558, 0x4BBC4, 0x4AFAC];
        for (int index = 0; index < 5; index++)
        {
            NativeBattleModulePoolTask task = pool.Slots[index];
            Assert.Equal((ushort)1, task.Flags);
            Assert.Equal(x[index], unchecked((short)task.ReadWord(0xC)));
            Assert.Equal((short)-8492, unchecked((short)task.ReadWord(0xE)));
            Assert.Equal((short)-128, unchecked((short)task.ReadWord(0x12)));
            Assert.Equal(0x20DFAB8u + programme[index], task.ReadDword(0x18));
            Assert.Equal(0u, task.ReadDword(0x1C));
            Assert.Equal(NativeBattleStorageSnapshots.Capture(task).Payload, frame.Memory.Arena!.Read(0x3505C + index * 0xE8 + 12, 0xDC).ToImmutableArray());
            Assert.Equal((ushort)1, BinaryPrimitives.ReadUInt16LittleEndian(frame.Memory.Arena.Read(0x3505C + index * 0xE8, 2)));
            Assert.Equal(0x659960u, BinaryPrimitives.ReadUInt32LittleEndian(frame.Memory.Arena.Read(0x3505C + index * 0xE8 + 8, 4)));
        }
        Assert.All(frame.Memory.Arena!.Read(0x3505C + 5 * 0xE8, 11 * 0xE8), value => Assert.Equal((byte)0, value));
        Assert.Equal(original, NativeBattleTestSnapshots.Json(preserved));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Frozen_programmes_hold_clock_and_Y_and_completion_frees_before_motion_without_needing_unused_selectors(bool pause)
    {
        NativeBattleDiablos325Frame frame = NativeBattleDiablos325Particles.Initialize6548B9(Fixture());
        frame = frame with { Memory = frame.Memory with { Diablos325 = frame.Memory.Diablos325! with { Pause5180 = pause ? 1u : 0u } } };
        NativeBattleCameraProjection projection = new(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096), 288, 160, 108);
        NativeBattleSummonColors colors = new(0, 0, 1f / 4096, 4503601774854144.0);
        uint cursor = frame.Memory.Diablos325!.PacketCursor51D0;
        for (int index = 0; index < 3; index++)
        {
            frame = NativeBattleDiablos325Particles.Advance659960(frame, projection, colors, (_, _) => throw new InvalidOperationException("A zero-node programme must not submit packets."));
        }
        NativeBattleEffectTaskPool pool = Pool(frame);
        Assert.Equal(pause ? 0u : 2u, pool.Slots[0].ReadDword(0x1C));
        Assert.Equal(pause ? (short)-8492 : (short)-8468, unchecked((short)pool.Slots[0].ReadWord(0xE)));
        if (pause)
        {
            Assert.Equal(0, pool.Head);
            Assert.Equal(4, pool.Tail);
            Assert.All(pool.Slots.Take(5), task => Assert.Equal((ushort)1, task.Flags));
        }
        else
        {
            Assert.Null(pool.Head);
            Assert.Null(pool.Tail);
            Assert.All(pool.Slots.Take(5), task => Assert.Equal((ushort)0, task.Flags));
        }
        Assert.Equal(cursor, Assert.IsType<NativeBattleDiablos325State>(frame.Memory.Diablos325).PacketCursor51D0);
    }

    [Fact]
    public void Full_bank_birth_returns_same_frame_without_source_access_or_extra_mutation()
    {
        NativeBattleDiablos325Frame frame = NativeBattleDiablos325Particles.Initialize6548B9(Fixture());
        for (int index = 5; index < 16; index++)
        {
            frame = NativeBattleDiablos325Particles.Spawn659900(frame, new(1, 2, 3), -128, 0x20DFAB8u + 0x4AFAC);
        }
        Assert.Same(frame, NativeBattleDiablos325Particles.Spawn659900(frame, new(4, 5, 6), 7, 0));
    }

    private static NativeBattleEffectTaskPool Pool(NativeBattleDiablos325Frame frame) => frame.Effect.Pools.Single(item => item.DescriptorAddress == 0x2505190);

    private static NativeBattleDiablos325Frame Fixture()
    {
        var original = NativeBattleDiablos325Tests.Fixture();
        NativeBattleDiablos325Frame frame = NativeBattleDiablos325Entry.Initialize6541E0(original.Frame, original.Packets, original.Models);
        NativeBattleEffectArena arena = frame.Memory.Arena!.Write(0x3505C, Enumerable.Repeat((byte)0xA5, 0xE80).ToArray());
        byte[] programme = new byte[16];
        BinaryPrimitives.WriteInt32LittleEndian(programme.AsSpan(4), 8);
        BinaryPrimitives.WriteInt32LittleEndian(programme.AsSpan(12), 2);
        foreach (int offset in new[] { 0x4AFAC, 0x4B558, 0x4BBC4 })
        {
            arena = arena.Write(offset, programme);
        }
        NativeBattleDiablos325Resources resources = new(new(0xFD8584, new byte[0x3890].ToImmutableArray()), new(new string('0', 64), [0], [0, 0, 0, 0])) { Files = [new(730, "effect-data", new byte[0x171E8].ToImmutableArray())] };
        return frame with { Memory = frame.Memory with { Arena = arena }, Effect = frame.Effect with { Binding = frame.Effect.Binding with { Diablos325Resources = resources } } };
    }
}
