using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleMiniPulseCannon286Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleMiniPulseCannon286StateTests
{
    [Fact]
    public void Cold_process_has_exact_scalar_matrices_and_root_owners()
    {
        var state = NativeBattleMiniPulseCannon286State.FromColdProcess();
        state.Validate();
        Assert.Equal(new byte[12], state.Read(BaseAddress, 12));
        Assert.Equal(new byte[8], state.Read(CursorGlobal, 8));
        Assert.Equal(new byte[20], state.RootBankStorage!.Read(0, 20));
        Assert.Equal(new[] { 20, 36, 1748 }, Pools.Select(pool => (int)pool.Stride));
        Assert.Equal(new[] { 1, 32, 1 }, Pools.Select(pool => (int)pool.Capacity));
    }

    [Fact]
    public void Unknown_coalesced_globals_and_root_transfer_round_trip_without_reseeding()
    {
        var state = NativeBattleMiniPulseCannon286State.FromUnknown().WriteWord(BaseAddress, 4096) with { RootBankStorage = null };
        state.Validate();
        var restored = JsonSerializer.Deserialize<NativeBattleMiniPulseCannon286State>(JsonSerializer.Serialize(state))!;
        restored.Validate();
        Assert.Null(restored.RootBankStorage);
        Assert.Equal((ushort)4096, restored.ReadWord(BaseAddress));
        Assert.Throws<NotSupportedException>(() => restored.ReadWord(BaseAddress + 2));
        Assert.Throws<NotSupportedException>(() => restored.ReadDword(ArenaGlobal));
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void COMMON_pools_scratch_packets_and_BSS_gaps_are_excluded()
    {
        var state = NativeBattleMiniPulseCannon286State.FromColdProcess();
        foreach (uint address in new[] { BaseAddress + 12, RootBank, RootDescriptor, ArenaAddress, ArenaAddress + ScratchOffset, ArenaAddress + FirstPacketOffset, ArenaGlobal + 4, BaseAddress + 12 })
        {
            Assert.Throws<InvalidDataException>(() => state.ReadWord(address));
        }
        Assert.Throws<InvalidDataException>(() => (state with { Globals = state.Globals.MarkUnknown(12, 1) }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { RootBankStorage = NativeBattleEffectArena.Clear().MarkUnknown(20, 1) }).Validate());
    }

    [Fact]
    public void Partial_scalar_views_preserve_neighbor_knownness_without_DEFAULT_fields()
    {
        var state = NativeBattleMiniPulseCannon286State.FromUnknown();
        var view = NativeBattleEffectArena.Clear().MarkUnknown(0, 12).Write(4, new byte[] { 1, 2, 3, 4 });
        state = state.WriteView(BaseAddress, view, 12);
        Assert.Equal(view.ReadSnapshot(0, 12).Known, state.ReadView(BaseAddress, 12).ReadSnapshot(0, 12).Known);
        Assert.Throws<NotSupportedException>(() => state.ReadDword(BaseAddress));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(BaseAddress + 8));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(ArenaGlobal));
    }

    internal static NativeBattleMiniPulseCannon286Resources Resources()
    {
        byte[] camera = new byte[CameraLength];
        BinaryPrimitives.WriteUInt16LittleEndian(camera, 2);
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(2), 8);
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(4), 52);
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(6), CameraLength);
        byte[] point = new byte[8];
        BinaryPrimitives.WriteInt16LittleEndian(point.AsSpan(6), 2);
        var resources = new NativeBattleMiniPulseCannon286Resources(Source(ProgrammeAAddress, ProgrammeALength, 5, 7, 42), Source(ProgrammeBAddress, ProgrammeBLength, 3, 3, 42), Source(ProgrammeCAddress, ProgrammeCLength, 7, 9, 20), Source(ProgrammeDAddress, ProgrammeDLength, 2, 2, 18), Sprite(SpriteAddress, SpriteLength, 10), new("FF8_EN:10847B0", camera.ToImmutableArray()), 0x641C5, point.ToImmutableArray());
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
            Put(bytes, 0, 0x09010524);
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
