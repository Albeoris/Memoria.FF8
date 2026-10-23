using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleSonic139Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSonic139StateTests
{
    [Theory]
    [InlineData(0u, 32u)]
    [InlineData(1u, 31u)]
    [InlineData(25u, 27u)]
    [InlineData(0xFFFFFFFFu, 32u)]
    [InlineData(0xFFFFFFFEu, 31u)]
    [InlineData(0x80000000u, 1u)]
    public void Native_leading_sign_publication_preserves_unrelated_registers_and_JSON(uint value, uint expected)
    {
        NativeBattleGeometryRegisters before = NativeBattleGeometryRegisters.Unknown() with { Mac0 = 123, Otz = 456, Sxy = [11u, 22u, 33u] };
        NativeBattleGeometryRegisters after = NativeBattleSonic139Preprocessing.PublishLeading45DCA0(before, value);
        Assert.Equal(value, after.Lzcs);
        Assert.Equal(expected, after.Lzcr);
        Assert.Equal(before.Mac0, after.Mac0);
        Assert.Equal(before.Otz, after.Otz);
        Assert.Equal(before.Sxy, after.Sxy);
        Assert.Equal(before.Rotation20, after.Rotation20);
        Assert.Equal(JsonSerializer.Serialize(after), JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleGeometryRegisters>(JsonSerializer.Serialize(after))));
        Assert.Null(NativeBattleGeometryRegisters.Unknown().Lzcs);
        Assert.DoesNotContain("Lzcs", JsonSerializer.Serialize(NativeBattleGeometryRegisters.Unknown()));
    }

    [Fact]
    public void Cold_publication_does_not_invent_original_PE_bytes_or_copy_imported_unknowns()
    {
        NativeBattleSonic139State cold = NativeBattleSonic139State.FromColdProcess();
        NativeBattleSonic139State unknown = NativeBattleSonic139State.FromUnknown();
        cold.Validate();
        unknown.Validate();
        Assert.True(cold.PendingColdSources);
        Assert.Equal(0u, cold.ReadDword(WorkingGlobal));
        Assert.Throws<NotSupportedException>(() => cold.ReadDword(MutableFramesAddress));
        Assert.Throws<NotSupportedException>(() => unknown.ReadDword(WorkingGlobal));
        Assert.Throws<NotSupportedException>(() => unknown.ReadDword(MutableFramesAddress));
        Assert.Throws<InvalidDataException>(() => cold.ReadDword(RootBank));
        Assert.Throws<InvalidDataException>(() => cold.ReadDword(MainDescriptor));
        Assert.Throws<InvalidDataException>(() => cold.ReadDword(0x20DFAB8));
    }

    [Fact]
    public void Warm_entry_copies_current_mutable_frame_bytes_and_preserves_current_frame_owner()
    {
        NativeBattleSonic139Resources resources = Resources();
        NativeBattleSonic139State state = NativeBattleSonic139State.FromColdProcess().CopyEntrySnapshot6A8860(resources).WriteDword(MutableFramesAddress + 12, 0xCAFEBABE);
        NativeBattleSonic139State next = state.CopyEntrySnapshot6A8860(resources);
        Assert.Equal(0xCAFEBABEu, next.ReadDword(MutableFramesAddress + 12));
        Assert.Equal(0xCAFEBABEu, next.ReadDword(CopiedMeshAddress + MutableFramesAddress - SourceMeshAddress + 12));
        NativeBattleSonic139State restored = JsonSerializer.Deserialize<NativeBattleSonic139State>(JsonSerializer.Serialize(next))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(next), JsonSerializer.Serialize(restored));
        Assert.Throws<NotSupportedException>(() => resources.Read(MutableFramesAddress, 4));
        Assert.Equal(VertexCount, BitConverter.ToInt32(resources.Read(Frame0Address + 4, 4).AsSpan()));
    }

    [Fact]
    public void Normalization_publishes_actual_last_nonzero_LZ_and_preserves_zero_vertex_registers()
    {
        NativeBattleSonic139Resources resources = Resources();
        NativeBattleSonic139State state = NativeBattleSonic139State.FromColdProcess().CopyEntrySnapshot6A8860(resources).WriteWord(CopiedMeshAddress + 8, 3).WriteWord(CopiedMeshAddress + 10, 4);
        NativeBattleSonic139Frame frame = new(NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown() with { Mac0 = 99 } }, new(139, 138, new(139, Entry, Initializer), null, 0), new(0), state);
        NativeBattleSonic139Frame after = NativeBattleSonic139Preprocessing.Normalize6A9C80(frame, CopiedMeshAddress, resources);
        Assert.Equal(25u, after.Memory.GeometryRegisters!.Lzcs);
        Assert.Equal(27u, after.Memory.GeometryRegisters.Lzcr);
        Assert.Equal(99u, after.Memory.GeometryRegisters.Mac0);
        Assert.Equal(0x200000u | 614u | 819u << 10, after.State.ReadDword(CopiedMeshAddress + 12));
        Assert.Equal(0u, after.State.ReadDword(CopiedMeshAddress + 20));
        Assert.Equal((ushort)3, after.State.ReadWord(CopiedMeshAddress + 8));
        Assert.Equal((ushort)4, after.State.ReadWord(CopiedMeshAddress + 10));
    }

    internal static NativeBattleSonic139Resources Resources()
    {
        byte[] seed = new byte[EntryCopyLength];
        foreach (int offset in new[] { 0, 0x2420, 0x2A70, 0x30C0, 0x3710 })
        {
            BitConverter.GetBytes(FrameLength).CopyTo(seed, offset);
            BitConverter.GetBytes(VertexCount).CopyTo(seed, offset + 4);
        }
        byte[] camera = new byte[0x11C];
        BitConverter.GetBytes((ushort)0x11C).CopyTo(camera, 6);
        ushort[] sqrt = new ushort[256];
        sqrt[100] = 5120;
        short[] sine = new short[4096];
        sine[1023] = 4096;
        return new(new("FF8_EN:11D4E90", seed.ToImmutableArray()), new("FF8_EN:11D7900", seed.AsSpan(0x2A70, MutableFramesLength).ToArray().ToImmutableArray()), new(MainActAddress, new byte[0x26AC].ToImmutableArray()), new(TargetActAddress, new byte[0x1F80].ToImmutableArray()), new("FF8_EN:11DD21C", camera.ToImmutableArray()), new byte[8].ToImmutableArray(), sqrt.ToImmutableArray(), sine.ToImmutableArray(), [0x4040FF, 0xFF40FF, 0xFF4040, 0x3167616D], 0x641C8);
    }
}
