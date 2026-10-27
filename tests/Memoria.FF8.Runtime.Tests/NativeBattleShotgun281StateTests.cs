using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleShotgun281Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShotgun281StateTests
{
    [Fact]
    public void Cold_process_has_exact_scalar_matrices_and_root_owners()
    {
        var state = NativeBattleShotgun281State.FromColdProcess();
        state.Validate();
        Assert.Equal(new byte[12], state.Read(BaseAddress, 12));
        Assert.Equal(new byte[72], state.Read(CursorGlobal, 72));
        Assert.Equal(new byte[20], state.RootBankStorage!.Read(0, 20));
        Assert.Equal(new[] { 20, 40, 332, 544 }, Pools.Select(pool => (int)pool.Stride));
        Assert.Equal(new[] { 1, 128, 6, 1 }, Pools.Select(pool => (int)pool.Capacity));
    }

    [Fact]
    public void Unknown_coalesced_globals_and_root_transfer_round_trip_without_reseeding()
    {
        var state = NativeBattleShotgun281State.FromUnknown().WriteWord(BasisAAddress, 4096) with { RootBankStorage = null };
        state.Validate();
        var restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleShotgun281State>(NativeBattleTestSnapshots.Json(state))!;
        restored.Validate();
        Assert.Null(restored.RootBankStorage);
        Assert.Equal((ushort)4096, restored.ReadWord(BasisAAddress));
        Assert.Throws<NotSupportedException>(() => restored.ReadWord(BasisAAddress + 2));
        Assert.Throws<NotSupportedException>(() => restored.ReadDword(ArenaGlobal));
        Assert.Equal(NativeBattleTestSnapshots.Json(state), NativeBattleTestSnapshots.Json(restored));
    }

    [Fact]
    public void COMMON_pools_scratch_packets_and_BSS_gaps_are_excluded()
    {
        var state = NativeBattleShotgun281State.FromColdProcess();
        foreach (uint address in new[] { BaseAddress + 12, RootBank, RootDescriptor, ArenaAddress, ArenaAddress + ScratchOffset, ArenaAddress + FirstPacketOffset, ArenaGlobal + 4 })
        {
            Assert.Throws<InvalidDataException>(() => state.ReadWord(address));
        }
        Assert.Throws<InvalidDataException>(() => (state with { Globals = state.Globals.MarkUnknown(12, 1) }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { RootBankStorage = NativeBattleEffectArena.Clear().MarkUnknown(20, 1) }).Validate());
    }

    [Fact]
    public void Partial_matrix_views_preserve_padding_and_independent_bases()
    {
        var state = NativeBattleShotgun281State.FromUnknown();
        var point = NativeBattleEffectArena.Clear().MarkUnknown(0, 32).Write(20, new byte[] { 1, 2, 3, 4, 5, 6 });
        state = state.WriteView(BasisAAddress, point, 32);
        Assert.Equal(point.ReadSnapshot(0, 32).Known, state.ReadView(BasisAAddress, 32).ReadSnapshot(0, 32).Known);
        Assert.Throws<NotSupportedException>(() => state.ReadWord(BasisAAddress + 18));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(BasisBAddress + 20));
    }

    internal static NativeBattleShotgun281Resources Resources()
    {
        byte[] camera = new byte[CameraLength];
        BinaryPrimitives.WriteUInt16LittleEndian(camera, 2);
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(2), 8);
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(4), 52);
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(6), CameraLength);
        byte[] point = new byte[8];
        BinaryPrimitives.WriteInt16LittleEndian(point.AsSpan(6), 23);
        var resources = new NativeBattleShotgun281Resources(Source(ProgrammeAAddress, ProgrammeALength, 5, 6, 14), Source(ProgrammeBAddress, ProgrammeBLength, 3, 4, 32), Sprite(SpriteAAddress, SpriteALength, 8), Sprite(SpriteBAddress, SpriteBLength, 10), new("FF8_EN:10A7824", camera.ToImmutableArray()), 0x61B74, point.ToImmutableArray());
        resources.Validate();
        return resources;

        static NativeBattleGfAnimationSource Source(uint address, int length, int geometries, int nodes, int frames)
        {
            byte[] bytes = new byte[length];
            Put(bytes, 0, (uint)geometries);
            Put(bytes, 4, 0x100);
            for (int geometry = 0; geometry < geometries; geometry++)
            {
                Put(bytes, 8 + geometry * 4, 64);
            }
            Put(bytes, 64, 20);
            Put(bytes, 68, 1);
            Put(bytes, 72, 1);
            Put(bytes, 0x100, (uint)nodes);
            Put(bytes, 0x104, (uint)frames);
            for (int node = 0; node < nodes; node++)
            {
                Put(bytes, 0x108 + node * 4, (uint)(8 + nodes * 4 + node * 8));
            }
            return new(address, bytes.ToImmutableArray());
        }

        static NativeBattleModuleResource Sprite(uint address, int length, int frames)
        {
            byte[] bytes = new byte[length];
            Put(bytes, 0, 0x08010524);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), (ushort)frames);
            for (int frame = 0; frame <= frames; frame++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + frame * 2), 40);
            }
            return new(0, address, 0, 0, bytes.ToImmutableArray());
        }

        static void Put(byte[] bytes, int offset, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
        }
    }
}
