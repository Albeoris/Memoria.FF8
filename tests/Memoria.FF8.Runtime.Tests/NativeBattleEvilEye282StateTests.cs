using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEvilEye282Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEvilEye282StateTests
{
    [Fact]
    public void Cold_process_has_exact_scalar_matrices_and_root_owners()
    {
        var state = NativeBattleEvilEye282State.FromColdProcess();
        state.Validate();
        Assert.Equal(new byte[12], state.Read(BaseAddress, 12));
        Assert.Equal(new byte[16], state.Read(CursorGlobal, 16));
        Assert.Equal(new byte[20], state.RootBankStorage!.Read(0, 20));
        Assert.Equal(new byte[32], state.Read(NormalizedViewAddress, 32));
        Assert.Equal(new[] { 20, 20, 408, 1380 }, Pools.Select(pool => (int)pool.Stride));
        Assert.Equal(new[] { 1, 128, 3, 1 }, Pools.Select(pool => (int)pool.Capacity));
    }

    [Fact]
    public void Unknown_coalesced_globals_and_root_transfer_round_trip_without_reseeding()
    {
        var state = NativeBattleEvilEye282State.FromUnknown().WriteWord(NormalizedViewAddress, 4096) with { RootBankStorage = null };
        state.Validate();
        var restored = JsonSerializer.Deserialize<NativeBattleEvilEye282State>(JsonSerializer.Serialize(state))!;
        restored.Validate();
        Assert.Null(restored.RootBankStorage);
        Assert.Equal((ushort)4096, restored.ReadWord(NormalizedViewAddress));
        Assert.Throws<NotSupportedException>(() => restored.ReadWord(NormalizedViewAddress + 2));
        Assert.Throws<NotSupportedException>(() => restored.ReadDword(ArenaGlobal));
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void COMMON_pools_scratch_packets_and_BSS_gaps_are_excluded()
    {
        var state = NativeBattleEvilEye282State.FromColdProcess();
        foreach (uint address in new[] { BaseAddress + 12, RootBank, RootDescriptor, ArenaAddress, ArenaAddress + ScratchOffset, ArenaAddress + FirstPacketOffset, ArenaGlobal + 4, NormalizedViewAddress + 32 })
        {
            Assert.Throws<InvalidDataException>(() => state.ReadWord(address));
        }
        Assert.Throws<InvalidDataException>(() => (state with { Globals = state.Globals.MarkUnknown(12, 1) }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { RootBankStorage = NativeBattleEffectArena.Clear().MarkUnknown(20, 1) }).Validate());
    }

    [Fact]
    public void Partial_matrix_views_preserve_padding_and_independent_bases()
    {
        var state = NativeBattleEvilEye282State.FromUnknown();
        var point = NativeBattleEffectArena.Clear().MarkUnknown(0, 32).Write(20, new byte[] { 1, 2, 3, 4, 5, 6 });
        state = state.WriteView(NormalizedViewAddress, point, 32);
        Assert.Equal(point.ReadSnapshot(0, 32).Known, state.ReadView(NormalizedViewAddress, 32).ReadSnapshot(0, 32).Known);
        Assert.Throws<NotSupportedException>(() => state.ReadWord(NormalizedViewAddress + 18));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(CursorGlobal));
    }

    internal static NativeBattleEvilEye282Resources Resources()
    {
        byte[] camera = new byte[CameraLength];
        BinaryPrimitives.WriteUInt16LittleEndian(camera, 2);
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(2), 8);
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(4), 24);
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(6), CameraLength);
        byte[] point = new byte[8];
        BinaryPrimitives.WriteInt16LittleEndian(point.AsSpan(6), 13);
        var resources = new NativeBattleEvilEye282Resources(Source(ProgrammeAAddress, ProgrammeALength, 3, 12, 21), Source(ProgrammeBAddress, ProgrammeBLength, 3, 5, 32), Sprite(SpriteAddress, SpriteLength, 16), new("FF8_EN:10A2F54", camera.ToImmutableArray()), 0x641AF, point.ToImmutableArray());
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
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + frame * 2), 64);
            }
            return new(0, address, 0, 0, bytes.ToImmutableArray());
        }

        static void Put(byte[] bytes, int offset, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
        }
    }
}
