using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect218Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect218Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect218RadialTests
{
    [Fact]
    public void Radial_births_skip_nonzero_inactive_headers_consume_forty_draws_and_preserve_unwritten_WORDs()
    {
        NativeBattleEffect218Frame frame = Fixture();
        uint bank = ArenaAddress + ParticleAOffset;
        frame = WriteBytes(frame, bank, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(2u)), 4);
        frame = WriteBytes(frame, bank + 24 + 14, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((ushort)0xAABB)), 2);
        frame = WriteBytes(frame, bank + 24 + 18, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((ushort)0xCCDD)), 2);
        NativeCrtRandomState expected = frame.Random;
        for (int index = 0; index < 40; index++)
        {
            expected = expected.Draw(out _);
        }
        NativeBattleEffect218Frame actual = SpawnRadial634300(frame, bank);
        Assert.Equal(expected, actual.Random);
        Assert.Equal(2u, Dword(ReadBytes(actual, bank, 4), 0));
        for (int index = 1; index <= 8; index++)
        {
            NativeBattleEffectArena row = ReadBytes(actual, bank + (uint)(index * 24), 24);
            Assert.Equal(1u, Dword(row, 0));
            Assert.Equal((ushort)0, Word(row, 4));
            Assert.InRange(Word(row, 6), (ushort)2560, (ushort)5631);
            Assert.InRange(unchecked((short)Word(row, 10)), (short)-59, (short)0);
        }
        Assert.Equal((ushort)0xAABB, Word(ReadBytes(actual, bank + 24, 24), 14));
        Assert.Equal((ushort)0xCCDD, Word(ReadBytes(actual, bank + 24, 24), 18));
        Assert.Equal(0u, Dword(ReadBytes(actual, bank + 9 * 24, 24), 0));
    }

    [Fact]
    public void Full_radial_bank_does_not_consume_RNG_or_replace_retained_rows()
    {
        NativeBattleEffect218Frame frame = Fixture();
        uint bank = ArenaAddress + ParticleAOffset;
        for (int index = 0; index < 200; index++)
        {
            frame = WriteBytes(frame, bank + (uint)(index * 24), NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(2u)), 4);
        }
        NativeBattleEffect218Frame actual = SpawnRadial634300(frame, bank);
        Assert.Equal(frame.Random, actual.Random);
        Assert.Equal(2u, Dword(ReadBytes(actual, bank + 199 * 24, 4), 0));
    }

    [Fact]
    public void Captured218_reused_radial_rows_get_new_births_without_changing_native_W_OR_CRT()
    {
        NativeBattleEffect218Frame native = Fixture();
        NativeBattleEffect218Frame captured = Fixture() with { CapturePresentation = true };
        uint bank = ArenaAddress + ParticleAOffset;
        long previous = 0;
        for (int pass = 0; pass < 2; pass++)
        {
            native = SpawnRadial634300(native, bank);
            captured = SpawnRadial634300(captured, bank);
            Assert.Equal(NativeBattleTestSnapshots.Json(native), NativeBattleTestSnapshots.Json(captured));
            NativeBattlePilotVisualIdentity identity = NativeBattlePilotVisualLifetimes.Identity(218, captured.State.ParticleGlobals, bank, 0);
            Assert.True(identity.Generation > previous);
            previous = identity.Generation;
            for (int index = 0; index < 8; index++)
            {
                uint address = bank + (uint)(index * 24);
                native = WriteBytes(native, address, NativeBattleEffectArena.Clear().Write(0, new byte[4]), 4);
                captured = WriteBytes(captured, address, NativeBattleEffectArena.Clear().Write(0, new byte[4]), 4);
            }
        }
    }

    private static NativeBattleEffect218Frame Fixture()
    {
        NativeBattleOriginalEffectState effect = new(218, 218, new(218, Entry, Initializer), new(0, 0, 0, 0, 0, 0), RootDescriptor);
        return new(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, effect, new(0xBEEF), NativeBattleEffect218State.FromColdProcess());
    }
    private static ushort Word(NativeBattleEffectArena row, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(row.Read(offset, 2));
    private static uint Dword(NativeBattleEffectArena row, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(row.Read(offset, 4));
}
