using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect218Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect218Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect218ClosureTests
{
    [Fact]
    public void Delayed_fragment_decrements_only_when_unpaused_and_never_draws_on_the_transition_to_zero()
    {
        var (frame, address) = Fragment(0, 1);
        int draws = 0;
        NativeBattleEffect218Frame Draw(NativeBattleEffect218Frame current, uint _)
        {
            draws++;
            return current;
        }
        var paused = AdvanceFragment635110(frame, address, 0x200, Draw);
        Assert.Equal((ushort)1, Read(paused.Frame, address).ReadWord(22));
        var transition = AdvanceFragment635110(paused.Frame, address, 0, Draw);
        Assert.Equal((ushort)0, Read(transition.Frame, address).ReadWord(22));
        Assert.Equal((ushort)0, Read(transition.Frame, address).ReadWord(12));
        Assert.Equal(0, draws);
        var activePaused = AdvanceFragment635110(transition.Frame, address, 1, Draw);
        Assert.Equal(1, draws);
        Assert.Equal((ushort)0, Read(activePaused.Frame, address).ReadWord(12));
    }

    [Fact]
    public void Active_fragment_draws_before_WORD_wrapped_motion_and_retires_on_new_age46()
    {
        var (frame, address) = Fragment(45, 0);
        NativeBattleEffectArena row = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)32760)).Write(2, BitConverter.GetBytes((short)-32760)).Write(4, BitConverter.GetBytes((short)17)).Write(6, BitConverter.GetBytes((ushort)0xBEEF)).Write(8, BitConverter.GetBytes((short)20)).Write(10, BitConverter.GetBytes((short)-20)).Write(12, BitConverter.GetBytes((short)-3)).Write(14, BitConverter.GetBytes((ushort)0xCAFE));
        frame = WriteBytes(frame, 0x24F3378, row, 16);
        var actual = AdvanceFragment635110(frame, address, 0, (current, _) =>
        {
            Assert.Equal((ushort)45, Read(current, address).ReadWord(12));
            Assert.Equal((short)32760, Word(ReadBytes(current, 0x24F3378, 16), 0));
            return current;
        });
        NativeBattleEffectArena result = ReadBytes(actual.Frame, 0x24F3378, 16);
        Assert.Equal(2, actual.Result);
        Assert.Equal((ushort)46, Read(actual.Frame, address).ReadWord(12));
        Assert.Equal((short)-32756, Word(result, 0));
        Assert.Equal((short)32756, Word(result, 2));
        Assert.Equal((short)14, Word(result, 4));
        Assert.Equal(unchecked((short)0xBEEF), Word(result, 6));
        Assert.Equal(unchecked((short)0xCAFE), Word(result, 14));
    }

    [Fact]
    public void Empty_child_banks_retire_root_and_transfer_current_task_payload_back_to_retained_storage_even_while_paused()
    {
        NativeBattleEffect218Frame frame = Frame();
        ImmutableArray<NativeBattleEffectTaskPool> pools = Pools.Select(profile => NativeBattleEffectTaskPool.Initialize(profile.Descriptor, profile.Bank, profile.Stride, profile.Capacity)).ToImmutableArray();
        (NativeBattleEffectTaskPool root, int slot) = pools[0].Append(RootCallback);
        root = root with { Slots = root.Slots.SetItem(slot, root.Slots[slot].WriteWord(12, 0x1234)) };
        frame = frame with { Effect = frame.Effect with { Pools = pools.SetItem(0, root) } };
        NativeBattleEffect218RuntimeInputs inputs = new(null!, null!, false, _ => 1, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);
        NativeBattleEffect218Frame actual = Advance(frame, null!, inputs);
        Assert.Equal(0u, actual.Effect.ReturnedDescriptor);
        Assert.Empty(actual.Effect.Pools);
        Assert.Equal((ushort)0x1235, actual.State.ReadWord(root.BankAddress + (uint)(slot * root.Stride) + 12));
    }

    [Fact]
    public void Capture_matrix_uses_signed_division_focal_WORD_and_independent_translation_rotation()
    {
        NativeBattlePackedMatrix32 packed = NativeBattleEffect218Capture.Matrix634E40(new(-18, 30000, -18), -123);
        NativeBattleActionMatrix matrix = packed.ReadMatrix();
        NativeBattleActionMatrix first = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CD50(new(1024, 0, 0)) with { X = 5, Y = -4, Z = -123 }, 252, 0, 246);
        NativeBattleActionMatrix turn = NativeBattleGfMatrices.Euler56CD50(new(0, 0, -1024));
        var expected = NativeBattleGfMatrices.RotateLong56C600(turn, 5, -4, -123);
        Assert.Equal((expected.Item1, expected.Item2, expected.Item3), (matrix.X, matrix.Y, matrix.Z));
        Assert.Equal(NativeBattleGfMatrices.Multiply56C090(turn, first).Rotation.ToArray(), matrix.Rotation.ToArray());
        Assert.DoesNotContain(true, packed.Known.Skip(18).Take(2));
    }

    [Fact]
    public void Capture_static_mask_preserves_unwritten_tag_bytes_and_uses_original_static_owner()
    {
        NativeBattleEffect218State state = NativeBattleEffect218State.FromColdProcess().WriteView(0x24F3538, NativeBattleEffectArena.Clear().Write(0, [0xA1, 0xB2, 0xC3, 0xD4, 0, 0, 0, 0]), 8);
        NativeBattleGfGeometryBatch batch = NativeBattleEffect218Capture.Mask634E40(state.ReadView(0x24F3538, 8));
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Attempts);
        Assert.Equal(new byte[] { 0xA1, 0xB2, 0xC3, 1, 0, 0, 0, 0xE6 }, attempt.Data.ToArray());
        Assert.All(attempt.WriteMask.Take(3), value => Assert.Equal(NativeBattleGfPacketByteState.Untouched, value));
        Assert.All(attempt.WriteMask.Skip(3), value => Assert.Equal(NativeBattleGfPacketByteState.Known, value));
        NativeBattleEffect218StaticPacketStep stored = NativeBattleEffect218StaticPackets.Store(state, NativeBattleLogicalOrdering.Clear(), 0x24F3538, batch, 0);
        NativeBattlePrimitiveReference reference = new(NativeBattleEffect218StaticPackets.GlobalsBank, 0, checked((int)(0x24F3538 - NativeBattleEffect218State.GlobalsAddress)));
        Assert.Equal(0xE6000000u, BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleEffect218StaticPackets.Read(stored.State, reference, 8).AsSpan(4)));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffect218StaticPackets.Read(stored.State, reference with { ByteOffset = reference.ByteOffset + 4 }, 8));
    }

    [Fact]
    public void Mesh_particle_births_consume_nine_draws_per_row_preserve_position_padding_and_skip_nonzero_inactive_rows()
    {
        NativeBattleEffect218Frame frame = Frame() with { Staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown()) };
        uint bank = ArenaAddress + ParticleBOffset;
        frame = WriteBytes(frame, bank, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(2u)), 4);
        for (int index = 0; index < 24; index++)
        {
            frame = WriteBytes(frame, checked(0x24F3378u + (uint)(index * 16)), NativeBattleEffectArena.Clear().Write(6, BitConverter.GetBytes((ushort)0xBEEF)).Write(10, BitConverter.GetBytes((short)4096)), 16);
        }
        NativeCrtRandomState expected = frame.Random;
        for (int draw = 0; draw < 216; draw++)
        {
            expected = expected.Draw(out _);
        }
        (frame, uint temporary) = Allocate(frame, 104);
        NativeBattleEffect218Frame actual = SpawnMeshParticles636EF1(frame, bank, temporary);
        Assert.Equal(expected, actual.Random);
        Assert.Equal(2u, BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(actual, bank, 4).Read(0, 4)));
        for (int index = 1; index <= 24; index++)
        {
            NativeBattleEffectArena row = ReadBytes(actual, bank + (uint)(index * 32), 32);
            Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(row.Read(0, 4)));
            Assert.Equal(unchecked((short)0xBEEF), Word(row, 14));
            Assert.InRange(Word(row, 6), (short)128, (short)1151);
            Assert.InRange(Word(row, 24), (short)0, (short)1023);
        }
    }

    [Fact]
    public void Debris_births_consume_four_draws_per_row_and_keep_unwritten_velocity_WORDs()
    {
        NativeBattleEffect218Frame frame = Frame();
        uint bank = ArenaAddress + ParticleAOffset;
        frame = WriteBytes(frame, bank + 16, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((ushort)0xCAFE)), 2);
        NativeCrtRandomState expected = frame.Random;
        for (int draw = 0; draw < 96; draw++)
        {
            expected = expected.Draw(out _);
        }
        NativeBattleEffect218Frame actual = SpawnDebrisSprites636CE1(frame, bank);
        Assert.Equal(expected, actual.Random);
        Assert.Equal(unchecked((short)0xCAFE), Word(ReadBytes(actual, bank, 24), 16));
        Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(actual, bank + 23 * 24, 4).Read(0, 4)));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(actual, bank + 24 * 24, 4).Read(0, 4)));
    }

    private static (NativeBattleEffect218Frame Frame, uint Address) Fragment(ushort age, ushort delay)
    {
        NativeBattleCureFamilyPool profile = Pools[1];
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(profile.Descriptor, profile.Bank, profile.Stride, profile.Capacity);
        (pool, int slot) = pool.Append(0x635110);
        pool = pool with { Slots = pool.Slots.SetItem(slot, pool.Slots[slot].WriteWord(12, age).WriteWord(14, 0).WriteWord(22, delay)) };
        NativeBattleEffect218Frame frame = Frame();
        return (frame with { Effect = frame.Effect with { Pools = [pool] } }, pool.BankAddress + (uint)(slot * pool.Stride));
    }

    private static NativeBattleEffect218Frame Frame() => new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, new(218, 218, new(218, Entry, Initializer), new(0, 0, 0, 0, 0, 0), RootDescriptor), new(0xBEEF), NativeBattleEffect218State.FromColdProcess());
    private static short Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
}
