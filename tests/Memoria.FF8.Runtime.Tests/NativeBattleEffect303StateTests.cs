using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect303Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect303StateTests
{
    [Fact]
    public void Cold_process_separates_sparse_scalars_root_and_COMMON_banks()
    {
        var state = NativeBattleEffect303State.FromColdProcess();
        state.Validate();
        Assert.Equal(new byte[16], state.Read(BaseAddress, 16));
        Assert.Equal(new byte[8], state.Read(CursorGlobal, 8));
        Assert.Equal(new byte[4], state.Read(ArenaGlobal, 4));
        Assert.Equal(new byte[20], state.RootBankStorage!.Read(0, 20));
        Assert.Equal(new[] { 20, 10176, 312, 36, 20 }, Pools.Select(pool => (int)pool.Stride));
        Assert.Equal(new[] { 1, 1, 4, 64, 8 }, Pools.Select(pool => (int)pool.Capacity));
        Assert.Equal(MainLength, MainProgrammeWorkspaceOffset + MainProgrammeWorkspaceLength);
        Assert.Equal(MainProgrammeWorkspaceOffset, FragmentOffset + FragmentCount * FragmentStride);
        Assert.Equal(FirstPacketOffset, IoOffset + IoLength + BodyProjectionCapacity);
        Assert.Equal(SecondPacketOffset, FirstPacketOffset + PacketBankBytes);
    }

    [Fact]
    public void JSON_preserves_partial_knownness_and_exclusive_active_root_transfer()
    {
        var state = NativeBattleEffect303State.FromColdProcess().WriteDword(TargetGlobal, 0x1D97530).WriteDword(SeedGlobal, 0x7FFF) with { RootBankStorage = null };
        var partial = NativeBattleEffectArena.Clear().MarkUnknown(0, 4).Write(1, new byte[] { 9, 8 });
        state = state.WriteView(CursorGlobal, partial, 4);
        var restored = JsonSerializer.Deserialize<NativeBattleEffect303State>(JsonSerializer.Serialize(state))!;
        restored.Validate();
        Assert.Null(restored.RootBankStorage);
        Assert.Equal(0x1D97530u, restored.ReadDword(TargetGlobal));
        Assert.Equal(0x7FFFu, restored.ReadDword(SeedGlobal));
        Assert.Throws<NotSupportedException>(() => restored.ReadDword(CursorGlobal));
        var view = restored.ReadView(CursorGlobal, 4);
        Assert.Equal(new[] { false, true, true, false }, view.ReadSnapshot(0, 4).Known);
        Assert.Equal(new byte[] { 9, 8 }, restored.Read(CursorGlobal + 1, 2));
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void Unknown_import_requires_actual_scalar_and_root_writers()
    {
        var state = NativeBattleEffect303State.FromUnknown();
        state.Validate();
        Assert.Throws<NotSupportedException>(() => state.ReadDword(CasterGlobal));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(SeedGlobal));
        Assert.Throws<NotSupportedException>(() => state.RootBankStorage!.Read(0, 20));
        state = state.WriteDword(CasterGlobal, 0x1D97494).WriteWord(SeedGlobal, 0);
        Assert.Equal(0x1D97494u, state.ReadDword(CasterGlobal));
        Assert.Throws<NotSupportedException>(() => state.ReadDword(SeedGlobal));
        Assert.Equal((ushort)0, state.ReadWord(SeedGlobal));
        state = state.WriteDword(SeedGlobal, 0);
        Assert.Equal(0u, state.ReadDword(SeedGlobal));
    }

    [Fact]
    public void Descriptors_gaps_taskbanks_and_cross_owner_reads_are_rejected()
    {
        var state = NativeBattleEffect303State.FromColdProcess();
        foreach (uint address in new[] { BaseAddress + 16, RootDescriptor, RootBank, CursorGlobal - 1, CursorGlobal + 8, ArenaGlobal - 1, ArenaGlobal + 4, ArenaAddress, ArenaAddress + ScratchOffset })
        {
            Assert.Throws<InvalidDataException>(() => state.ReadWord(address));
        }
        Assert.Throws<InvalidDataException>(() => state.Read(TargetGlobal, 5));
        Assert.Throws<InvalidDataException>(() => (state with { Globals = state.Globals.MarkUnknown(16, 1) }).Validate());
        Assert.Throws<InvalidDataException>(() => (state with { RootBankStorage = NativeBattleEffectArena.Clear().MarkUnknown(20, 1) }).Validate());
        var resources = Resources();
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(VibrationAddress + VibrationLength - 1, 2));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(ArenaAddress + ScratchOffset, 8));
        Assert.Throws<InvalidDataException>(() => (resources with { MuzzlePoints16 = resources.MuzzlePoints16.SetItem(6, 4) }).Validate());
    }

    internal static NativeBattleEffect303Resources Resources()
    {
        byte[] camera = new byte[CameraLength];
        W(camera, 0, 2);
        W(camera, 2, 8);
        W(camera, 4, 24);
        W(camera, 6, CameraLength);
        byte[] vibration = new byte[VibrationLength];
        D(vibration, 4, 256);
        W(vibration, 256, 14);
        byte[] points = new byte[16];
        W(points, 6, 3);
        W(points, 14, 39);
        byte[] completion = new byte[33824];
        D(completion, 0, 16);
        D(completion, 4, 8);
        D(completion, 8, 1036);
        W(completion, 12, 624);
        W(completion, 14, 224);
        W(completion, 16, 16);
        W(completion, 18, 32);
        D(completion, 1044, 32780);
        W(completion, 1048, 704);
        W(completion, 1050, 256);
        W(completion, 1052, 64);
        W(completion, 1054, 256);
        var resources = new NativeBattleEffect303Resources(Source(MainProgrammeAddress, MainProgrammeLength, 5, 22), Source(ChildProgrammeAddress, ChildProgrammeLength, 4, 16), Enumerable.Range(0, 4).Select(Mesh).ToImmutableArray(), SpriteSource(ParticleSpriteAddress, ParticleSpriteLength, 10, 10), SpriteSource(SpriteAddress, SpriteLength, 12, 7), new("FF8_EN:10340AC", camera.ToImmutableArray()), 0x6419D, new(VibrationAddress, new(new string('0', 64), [0, 256], vibration.ToImmutableArray())), new(CompletionFileId, "MA8DEF_0.TIM", completion.ToImmutableArray()), new byte[32].ToImmutableArray(), points.ToImmutableArray());
        resources.Validate();
        return resources;

        static NativeBattleGfAnimationSource Source(uint address, int length, int nodes, int frames)
        {
            byte[] bytes = new byte[length];
            D(bytes, 0, 3);
            D(bytes, 4, 256);
            for (int geometry = 0; geometry < 3; geometry++)
            {
                D(bytes, 8 + geometry * 4, 64);
            }
            D(bytes, 64, 20);
            D(bytes, 68, 1);
            D(bytes, 72, 1);
            D(bytes, 256, (uint)nodes);
            D(bytes, 260, (uint)frames);
            for (int node = 0; node < nodes; node++)
            {
                D(bytes, 264 + node * 4, (uint)(8 + nodes * 4 + node * 8));
            }
            return new(address, bytes.ToImmutableArray());
        }

        static NativeBattleStaticMeshResource Mesh(int index)
        {
            byte[] bytes = new byte[MeshLengths[index]];
            int vertices = MeshVertexCounts[index];
            D(bytes, 0, (uint)(8 + vertices * 8));
            D(bytes, 4, (uint)vertices);
            int cursor = 8 + vertices * 8;
            for (int group = 0; group < 8; group++)
            {
                int count = group == 2 ? new[] { 76, 30, 30, 10 }[index] : group == 3 && index == 0 ? 2 : 0;
                D(bytes, cursor, (uint)count);
                cursor += 4;
                int size = group == 3 ? 24 : 20;
                for (int face = 0; face < count; face++)
                {
                    bytes[cursor + face * size + 3] = group == 3 ? (byte)0x2C : (byte)0x24;
                }
                cursor += count * size;
            }
            return new(0, MeshAddresses[index], bytes.ToImmutableArray());
        }

        static NativeBattleModuleResource SpriteSource(uint address, int length, int frames, byte kind)
        {
            byte[] bytes = new byte[length];
            D(bytes, 0, (uint)kind << 24 | 0x010524);
            W(bytes, 8, frames);
            for (int phase = 0; phase <= frames; phase++)
            {
                W(bytes, 10 + phase * 2, 64);
            }
            return new(0, address, 0, 0, bytes.ToImmutableArray());
        }
    }

    private static void W(byte[] bytes, int offset, int value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), checked((ushort)value));
    private static void D(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
}
