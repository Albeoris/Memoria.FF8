using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleActContext40PolicyTests
{
    private const int MorphOffset = 0x4000;
    private const uint MorphAddress = NativeBattleEffectNodeMemory.ArenaAddress + MorphOffset;

    [Fact]
    public void Hypnotize_ignores_alternate_Euler_and_header_scaling_and_uses_row_scale()
    {
        NativeBattleGfAnimationNode node = RotatedScaled(0x40300);
        NativeBattleDefaultActContext40Step result = Draw(NativeBattleActContext40Policy.Hypnotize133, Source(4, 1), node);
        NativeBattleActionMatrix expected = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler701310(new(300, 500, 700)), 2048, 1024, 4096);
        AssertRotation(expected, result);
        NativeBattleDefaultActContext40Step unflagged = Draw(NativeBattleActContext40Policy.Hypnotize133, Source(4, 1), RotatedScaled(0x200));
        Assert.Equal(NativeBattleTestSnapshots.Json(unflagged), NativeBattleTestSnapshots.Json(result));
    }

    [Theory]
    [InlineData(0x40200)]
    [InlineData(0x40300)]
    public void BeamLaser_ignores_alternate_Euler_but_honors_only_its_header100_scale_policy(uint header)
    {
        NativeBattleDefaultActContext40Step result = Draw(NativeBattleActContext40Policy.BeamLaser134, Source(4, 1), RotatedScaled(header));
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Euler701310(new(300, 500, 700));
        NativeBattleActionMatrix expected = (header & 0x100) != 0 ? NativeBattleGfMatrices.Multiply56C090(matrix, new([2048, 0, 0, 0, 1024, 0, 0, 0, 4096], 0, 0, 0, 4096)) : NativeBattlePoseMatrices.Scale56BEF0(matrix, 2048, 1024, 4096);
        AssertRotation(expected, result);
    }

    [Theory]
    [InlineData(133, 0)]
    [InlineData(133, 2048)]
    [InlineData(134, 0)]
    [InlineData(134, 2048)]
    public void Draw_flags_ignore_header4000_and2000_and_follow_cue_only(int effect, int cue)
    {
        NativeBattleActContext40Policy policy = effect == 133 ? NativeBattleActContext40Policy.Hypnotize133 : NativeBattleActContext40Policy.BeamLaser134;
        NativeBattleDefaultActContext40Step result = Draw(policy, Source(4, 1), Node(header: 0x6200, cue: (short)cue, color: 0x7FC08040));
        Assert.Equal(cue == 0 ? 0x2030u : 0x20F0u, U32(result.Temporary88, 0x1C));
        Assert.Equal(unchecked((uint)-128), U32(result.Temporary88, 0x18));
        if (cue == 0)
        {
            Assert.Throws<NotSupportedException>(() => result.Temporary88.Read(8, 4));
        }
        else
        {
            Assert.Equal(0x7FC08040u, U32(result.Temporary88, 8));
        }
    }

    [Fact]
    public void Hypnotize_direct112_vertices_requires_no_Arena_or_unused_morph_pointer_but_interpolation_fails()
    {
        NativeBattleEffectMemory memory = Memory() with { Arena = null };
        NativeBattleDefaultActContext40Step result = Draw(NativeBattleActContext40Policy.Hypnotize133, Source(112, 1), Node(), Context().MarkUnknown(36, 4), memory);
        Assert.Null(result.Memory.Arena);
        Assert.Equal(Source(112, 1).Address + 28u, U32(result.Temporary88, 4));
        Assert.Throws<InvalidDataException>(() => Draw(NativeBattleActContext40Policy.Hypnotize133, Source(4, 2), Node(ratio: 2048, second: 1)));
    }

    [Fact]
    public void BeamLaser_49row_morph_preserves_W_and_adjacent_packet_bytes_while_direct64_needs_no_scratch()
    {
        NativeBattleEffectMemory memory = Memory() with { Arena = Memory().Arena!.Write(MorphOffset + 392, [1, 2, 3, 4]) };
        NativeBattleDefaultActContext40Step result = Draw(NativeBattleActContext40Policy.BeamLaser134, Source(49, 2), Node(ratio: 2048, second: 1), memory: memory);
        Assert.Equal((short)50, BinaryPrimitives.ReadInt16LittleEndian(result.Memory.Arena!.Read(MorphOffset, 2)));
        Assert.Equal((short)98, BinaryPrimitives.ReadInt16LittleEndian(result.Memory.Arena.Read(MorphOffset + 48 * 8, 2)));
        Assert.Throws<NotSupportedException>(() => result.Memory.Arena.Read(MorphOffset + 48 * 8 + 6, 2));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, result.Memory.Arena.Read(MorphOffset + 392, 4));
        Assert.Throws<InvalidDataException>(() => Draw(NativeBattleActContext40Policy.BeamLaser134, Source(50, 2), Node(ratio: 2048, second: 1)));
        NativeBattleDefaultActContext40Step direct = Draw(NativeBattleActContext40Policy.BeamLaser134, Source(64, 1), Node(), Context().MarkUnknown(36, 4), Memory() with { Arena = null });
        Assert.Null(direct.Memory.Arena);
        NativeBattleDefaultActContext40Step restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleDefaultActContext40Step>(NativeBattleTestSnapshots.Json(result))!;
        Assert.Equal(result.Memory.Arena.ReadSnapshot(MorphOffset, 392).Known, restored.Memory.Arena!.ReadSnapshot(MorphOffset, 392).Known);
    }

    [Theory]
    [InlineData((short)0, -400)]
    [InlineData((short)4096, -200)]
    public void Hypnotize133_capture_preserves_native_selected_rows_W_registers_and_actual_depths(short ratio, int expectedX)
    {
        NativeBattleGfAnimationSource source = Source(4, 2, 4);
        byte[] bytes = source.Data.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(60), -200);
        source = source with { Data = bytes.ToImmutableArray() };
        NativeBattleGfAnimationNode node = Node(ratio: ratio, second: 1);
        NativeBattleCameraProjection projection = new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108);
        NativeBattleDefaultActContext40Step native = NativeBattleDefaultActContext40.Draw(Memory(), source, node, Context(), NativeBattleEffectArena.Clear().MarkUnknown(0, 88), projection, NativeBattleGteColorInterpolation.CodeConstants45F270, MorphAddress, NativeBattleActContext40Policy.Hypnotize133);
        string frozen = NativeBattleTestSnapshots.Json(native);
        NativeBattleDefaultActContext40Step captured = NativeBattleDefaultActContext40.Draw(Memory(), source, node, Context(), NativeBattleEffectArena.Clear().MarkUnknown(0, 88), projection, NativeBattleGteColorInterpolation.CodeConstants45F270, MorphAddress, NativeBattleActContext40Policy.Hypnotize133, pilotIdentity: new(133, 1, 2, 3, 4) { ResourceGeneration = source.Address });
        captured = captured with { Batch = NativeBattleContextBodyPresentation.CaptureDepths(captured.Batch) };
        Assert.Equal(frozen, NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(frozen, NativeBattleTestSnapshots.Json(native));
        NativeBattleGeometryPrimitive primitive = Assert.Single(captured.Batch.Attempts).Primitive!;
        NativeBattlePilotVisualGeometry visual = Assert.IsType<NativeBattlePilotVisualGeometry>(primitive.PilotGeometry);
        Assert.Equal(expectedX, visual.A.X);
        Assert.Equal((ushort)133, visual.Identity.Effect);
        Assert.True(visual.UseSourceDepths);
        Assert.Equal(new System.Numerics.Vector4(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]), visual.SourceDepths);
        Assert.Equal(bytes, source.Data.ToArray());
    }

    private static NativeBattleGfAnimationNode RotatedScaled(uint header)
    {
        NativeBattleGfAnimationNode node = Node(header);
        byte[] bytes = node.Data.ToArray();
        short[] angles = [300, 500, 700];
        for (int axis = 0; axis < 3; axis++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(16 + axis * 2, 2), angles[axis]);
        }
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(24, 2), 2048);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(26, 2), 1024);
        return node with { Data = bytes.ToImmutableArray() };
    }

    private static void AssertRotation(NativeBattleActionMatrix expected, NativeBattleDefaultActContext40Step result)
    {
        byte[] rotation = new byte[18];
        for (int index = 0; index < 9; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(rotation.AsSpan(index * 2, 2), expected.Rotation[index]);
        }
        Assert.Equal(rotation.Select(value => (byte?)value), result.Memory.GeometryRegisters!.Rotation20.Take(18));
        Assert.Null(result.Memory.GeometryRegisters.Rotation20[18]);
    }

    private static NativeBattleDefaultActContext40Step Draw(NativeBattleActContext40Policy policy, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena? context = null, NativeBattleEffectMemory? memory = null) => NativeBattleDefaultActContext40.Draw(memory ?? Memory(), source, node, context ?? Context(), NativeBattleEffectArena.Clear().MarkUnknown(0, 88), new(null, 100, 160, 108), NativeBattleGteColorInterpolation.CodeConstants45F270, MorphAddress, policy);
    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().MarkUnknown(MorphOffset, 392), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
    private static NativeBattleEffectArena Context(NativeBattleActionMatrix? matrix = null)
    {
        NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(matrix ?? NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, null);
        return NativeBattleEffectArena.Clear().MarkUnknown(0, 40).Write(0, packed.Bytes.AsSpan()).MarkUnknown(18, 2).Write(32, BitConverter.GetBytes(-128)).Write(36, BitConverter.GetBytes(MorphAddress));
    }
    private static uint U32(NativeBattleEffectArena memory, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Read(offset, 4));
    private static NativeBattleGfAnimationNode Node(uint header = 0, short x = 0, short ratio = 0, short second = 0, short cue = 0, uint color = 0)
    {
        byte[] bytes = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), header);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), x);
        foreach (int offset in new[] { 24, 26, 28 })
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), 4096);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(32), color);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(36), cue);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(38), ratio);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(42), second);
        return new(0, 0, bytes.ToImmutableArray());
    }
    private static NativeBattleGfAnimationSource Source(int count, int variants, int group = -1)
    {
        const int geometry = 16;
        int faceStart = geometry + 12 + count * variants * 8;
        byte[] bytes = new byte[faceStart + 32 + (group == 4 ? 20 : 0)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), geometry);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(geometry), faceStart - geometry);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(geometry + 4), count);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(geometry + 8), variants);
        for (int variant = 0; variant < variants; variant++)
        {
            for (int vertex = 0; vertex < count; vertex++)
            {
                int offset = geometry + 12 + (variant * count + vertex) * 8;
                short x = (short)(vertex + variant * 100);
                short y = 0;
                if (group == 4)
                {
                    x = vertex == 1 ? (short)400 : (short)-400;
                    y = vertex >= 2 ? (short)400 : (short)-400;
                }
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), x);
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 2), y);
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 6), 1234);
            }
        }
        if (group == 4)
        {
            int face = faceStart + 20;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(face - 4), 1);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face), 0x30808080);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 6), 2);
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 8), 4);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 12), 0x808080);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face + 16), 0x808080);
        }
        return new(0x1000224, bytes.ToImmutableArray());
    }
}
