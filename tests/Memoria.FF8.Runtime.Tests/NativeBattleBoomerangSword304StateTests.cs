using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleBoomerangSword304Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBoomerangSword304StateTests
{
    [Fact]
    public void Cold_process_has_exact_scalar_matrices_and_root_owners()
    {
        var state = NativeBattleBoomerangSword304State.FromColdProcess();
        state.Validate();
        Assert.Equal(new byte[16], state.Read(BaseAddress, 16));
        Assert.Equal(new byte[4], state.Read(CursorGlobal, 4));
        Assert.Equal(new byte[20], state.RootBankStorage!.Read(0, 20));
        Assert.Equal(new[] { 20, 172, 48, 484, 36 }, Pools.Select(pool => (int)pool.Stride));
        Assert.Equal(new[] { 1, 1, 32, 3, 64 }, Pools.Select(pool => (int)pool.Capacity));
    }

    [Fact]
    public void Unknown_coalesced_globals_and_root_transfer_round_trip_without_reseeding()
    {
        var state = NativeBattleBoomerangSword304State.FromUnknown().WriteWord(BaseAddress, 4096) with { RootBankStorage = null };
        state.Validate();
        var restored = JsonSerializer.Deserialize<NativeBattleBoomerangSword304State>(JsonSerializer.Serialize(state))!;
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
        var state = NativeBattleBoomerangSword304State.FromColdProcess();
        foreach (uint address in new[] { BaseAddress + 16, RootBank, RootDescriptor, ArenaAddress, ArenaAddress + ScratchOffset, ArenaAddress + FirstPacketOffset, CursorGlobal + 4, BaseAddress + 16 })
        {
            Assert.Throws<InvalidDataException>(() => state.ReadWord(address));
        }
        Assert.Throws<InvalidDataException>(() => (state with { Globals = state.Globals.MarkUnknown(16, 1) }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { RootBankStorage = NativeBattleEffectArena.Clear().MarkUnknown(20, 1) }).Validate());
    }

    [Fact]
    public void Partial_scalar_views_preserve_neighbor_knownness_without_DEFAULT_fields()
    {
        var state = NativeBattleBoomerangSword304State.FromUnknown();
        var view = NativeBattleEffectArena.Clear().MarkUnknown(0, 12).Write(4, new byte[] { 1, 2, 3, 4 });
        state = state.WriteView(BaseAddress, view, 12);
        Assert.Equal(view.ReadSnapshot(0, 12).Known, state.ReadView(BaseAddress, 12).ReadSnapshot(0, 12).Known);
        Assert.Throws<NotSupportedException>(() => state.ReadDword(BaseAddress));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(BaseAddress + 8));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(ArenaGlobal));
        var resources = Resources();
        var retained = new NativeBattleGfAnimationWorkspace(Enumerable.Repeat((byte)0xA5, 484).ToImmutableArray());
        var initialized = retained.Initialize7016B0(resources.Programme, ProgrammeWorkspaceLength);
        Assert.Equal(0, initialized.Frame);
        Assert.Equal(ProgrammeAddress, initialized.ResourceAddress);
        Assert.All(initialized.Data.Skip(ProgrammeWorkspaceLength), value => Assert.Equal((byte)0xA5, value));
        Assert.Equal(ScratchOffset, 0xC58 + 64 * 36);
        Assert.Equal(FirstPacketOffset, ScratchOffset + ScratchLength);
    }

    internal static NativeBattleBoomerangSword304Resources Resources()
    {
        byte[] camera = new byte[CameraLength];
        BinaryPrimitives.WriteUInt16LittleEndian(camera, 2);
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(2), 8);
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(4), 32);
        BinaryPrimitives.WriteUInt16LittleEndian(camera.AsSpan(6), CameraLength);
        var resources = new NativeBattleBoomerangSword304Resources(Source(ProgrammeAddress, ProgrammeLength, 4, 6, 14), Mesh(MeshAddress, MeshLength, 52, [0, 0, 63, 15, 0, 0, 0, 0]), Mesh(TrailMeshAddress, TrailMeshLength, 14, [0, 0, 0, 0, 16, 0, 0, 0]), Sprite(SpriteAddress, SpriteLength, 10, 8), new("FF8_EN:10312CC", camera.ToImmutableArray()), 0x641AC);
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

        static NativeBattleStaticMeshResource Mesh(uint address, int length, int vertices, int[] counts)
        {
            byte[] bytes = new byte[length];
            Put(bytes, 0, (uint)(8 + vertices * 8));
            Put(bytes, 4, (uint)vertices);
            int cursor = 8 + vertices * 8;
            int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
            for (int group = 0; group < 8; group++)
            {
                Put(bytes, cursor, (uint)counts[group]);
                cursor += 4 + counts[group] * strides[group];
            }
            return new(0, address, bytes.ToImmutableArray());
        }

        static NativeBattleModuleResource Sprite(uint address, int length, int frames, byte kind)
        {
            byte[] bytes = new byte[length];
            Put(bytes, 0, (uint)kind << 24 | 0x010524);
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
