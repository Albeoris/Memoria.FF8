using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleBlaster186Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBlaster186MorphTests
{
    [Fact]
    public void Sixteen_native_frames_write_all_102_XYZ_rows_and_retain_unknown_W_warm_W_matrix_and_task_gaps()
    {
        NativeBattleBlaster186Resources resources = Sources(0, 4096, 8192, 12288);
        NativeBattleBlaster186State state = NativeBattleBlaster186State.FromUnknown().Write(MatrixAddress, Enumerable.Repeat((byte)0xA5, 32).ToArray()).WriteWord(MutableFrameAddresses[14] + 101 * 8 + 6, 0xCAFE);
        string root = JsonSerializer.Serialize(state.RootBankStorage);
        string child = JsonSerializer.Serialize(state.ChildBankStorage);
        NativeBattleBlaster186State result = NativeBattleBlaster186Morph.Initialize(state, resources);
        result.Validate();
        Assert.Equal((ushort)819, result.ReadWord(MutableFrameAddresses[0]));
        Assert.Equal((ushort)819, result.ReadWord(MutableFrameAddresses[0] + 101 * 8));
        Assert.Equal((ushort)2460, result.ReadWord(MutableFrameAddresses[15] + 101 * 8 + 4));
        foreach (uint address in MutableFrameAddresses)
        {
            for (uint vertex = 0; vertex < VertexCount; vertex++)
            {
                Assert.Equal(6, result.Read(address + vertex * 8, 6).Length);
            }
            Assert.Throws<NotSupportedException>(() => result.ReadWord(address + 6));
        }
        Assert.Equal((ushort)0xCAFE, result.ReadWord(MutableFrameAddresses[14] + 101 * 8 + 6));
        Assert.Equal(Enumerable.Repeat((byte)0xA5, 32).ToArray(), result.Read(MatrixAddress, 32));
        Assert.Equal(root, JsonSerializer.Serialize(result.RootBankStorage));
        Assert.Equal(child, JsonSerializer.Serialize(result.ChildBankStorage));
        NativeBattleBlaster186State restored = JsonSerializer.Deserialize<NativeBattleBlaster186State>(JsonSerializer.Serialize(result))!;
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(restored));
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(NativeBattleBlaster186Morph.Initialize(restored, resources)));
    }

    [Fact]
    public void Cold_destination_W_remains_BSS_zero_and_never_copies_immutable_source_W()
    {
        NativeBattleBlaster186Resources resources = Sources(0, 4096, 8192, 12288);
        Assert.Equal((ushort)0x7EEF, BinaryPrimitives.ReadUInt16LittleEndian(resources.SourceShapes[0].Bytes.AsSpan().Slice(6)));
        NativeBattleBlaster186State state = NativeBattleBlaster186Morph.Initialize(NativeBattleBlaster186State.FromColdProcess(), resources);
        foreach (uint address in MutableFrameAddresses)
        {
            for (uint vertex = 0; vertex < VertexCount; vertex++)
            {
                Assert.Equal((ushort)0, state.ReadWord(address + vertex * 8 + 6));
            }
        }
    }

    [Theory]
    [InlineData(-2, -3, 2048, -3)]
    [InlineData(-32768, 32767, int.MaxValue, 32752)]
    [InlineData(10, 20, -4096, 0)]
    [InlineData(10, 20, 8192, 30)]
    public void Signed_axes_low32_multiplication_arithmetic_shift_and_WORD_wrap_match_native(int left, int right, int scale, int expected)
    {
        NativeBattleBlaster186Resources resources = Sources((short)left, (short)right, 0, 0);
        NativeBattleBlaster186State state = NativeBattleBlaster186Morph.Interpolate(NativeBattleBlaster186State.FromUnknown(), resources, MutableFrameAddresses[0], SourceAAddress, SourceBAddress, scale);
        Assert.Equal(unchecked((ushort)expected), state.ReadWord(MutableFrameAddresses[0]));
        Assert.Throws<NotSupportedException>(() => state.ReadWord(MutableFrameAddresses[0] + 6));
    }

    [Fact]
    public void Morph_rejects_task_matrix_gap_destinations_and_mutable_or_unselected_input_sources()
    {
        NativeBattleBlaster186Resources resources = Sources(0, 1, 2, 3);
        NativeBattleBlaster186State state = NativeBattleBlaster186State.FromColdProcess();
        Assert.Throws<NotSupportedException>(() => NativeBattleBlaster186Morph.Interpolate(state, resources, MatrixAddress, SourceAAddress, SourceBAddress, 1));
        Assert.Throws<NotSupportedException>(() => NativeBattleBlaster186Morph.Interpolate(state, resources, ChildBank, SourceAAddress, SourceBAddress, 1));
        Assert.Throws<NotSupportedException>(() => NativeBattleBlaster186Morph.Interpolate(state, resources, MutableFrameAddresses[0], MutableFrameAddresses[1], SourceBAddress, 1));
        Assert.Throws<NotSupportedException>(() => resources.ReadSourceVertices(SourceAAddress - 8));
    }

    private static NativeBattleBlaster186Resources Sources(short a, short b, short c, short d)
    {
        short[] values = [a, b, c, d];
        ImmutableArray<NativeBattleBlaster186VertexShape> shapes = Enumerable.Range(0, 4).Select(index =>
        {
            byte[] bytes = new byte[VertexFrameBytes];
            for (int vertex = 0; vertex < VertexCount; vertex++)
            {
                for (int axis = 0; axis < 3; axis++)
                {
                    BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(vertex * 8 + axis * 2), values[index]);
                }
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(vertex * 8 + 6), 0x7EEF);
            }
            return new NativeBattleBlaster186VertexShape(SourceAddresses[index] - 8, SourceAddresses[index], 0x338, VertexCount, bytes.ToImmutableArray());
        }).ToImmutableArray();
        return new(null!, null!, null!, null!, null!, null!, null!, null!, 0, shapes, [], [], [], []);
    }
}
