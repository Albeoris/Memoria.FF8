using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect70LowerTests
{
    [Fact]
    public void Header1000_takes_priority_over200_and_uses_camera_anchor_retained_rotation_offset_then_actual_camera_translation()
    {
        NativeBattlePackedMatrix32 camera = Packed(new([8192, 0, 0, 0, 12288, 0, 0, 0, 16384], 1000, -2000, 3000, 4096));
        NativeBattleActionMatrix retained = new([0, -4096, 0, 4096, 0, 0, 0, 0, 4096], 90000, 80000, 70000, 4096);
        NativeBattleGfAnimationNode node = Node(0x1200, new(10, 20, 30));
        NativeBattleEffect70ProgrammeDrawingStep result = Draw(node, Context(new(100, 200, 300)), camera, Packed(retained));
        Assert.Equal(new int?[] { 1180, -1390, 4230 }, result.Memory.GeometryRegisters!.Translation.ToArray());
        AssertRotation(retained, result);
        Assert.Null(result.Memory.GeometryRegisters.Rotation20[18]);
        Assert.Null(result.Memory.GeometryRegisters.Rotation20[19]);
        NativeBattleEffect70ProgrammeDrawingStep without200 = Draw(Node(0x1000, new(10, 20, 30)), Context(new(100, 200, 300)), camera, Packed(retained));
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(without200)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(result)));
    }

    [Fact]
    public void Header200_keeps_full_MAC32_anchor_then_adds_camera_space_offset_and_camera_translation()
    {
        NativeBattleActionMatrix view = new([32767, 0, 0, 0, 4096, 0, 0, 0, 4096], 50000, -3, 2000, 4096);
        NativeBattleEffect70ProgrammeDrawingStep result = Draw(Node(0x200, new(10000, 0, 0)), Context(new(10000, 0, 0)), Packed(view));
        Assert.Equal(new int?[] { 139998, -3, 2000 }, result.Memory.GeometryRegisters!.Translation.ToArray());
        AssertRotation(NativeBattleCuragaMatrices.Identity8DD770(), result);
        Assert.False(result.Memory.ScriptedColorRegisters!.NormalInputKnown!.Value[6]);
        Assert.False(result.Memory.ScriptedColorRegisters.NormalInputKnown.Value[7]);
    }

    [Fact]
    public void Header80000_uses_x_times_y_then_z_and40000_retains_its_native_priority()
    {
        NativeBattleVector angles = new(300, 500, 700);
        NativeBattleActionMatrix x = NativeBattleGfMatrices.Euler701310(new(angles.X, 0, 0));
        NativeBattleActionMatrix y = NativeBattleGfMatrices.Euler701310(new(0, angles.Y, 0));
        NativeBattleActionMatrix z = NativeBattleGfMatrices.Euler701310(new(0, 0, angles.Z));
        NativeBattleActionMatrix xyz = NativeBattleGfMatrices.Multiply56C090(NativeBattleGfMatrices.Multiply56C090(x, y), z);
        NativeBattleActionMatrix yxz = NativeBattleGfMatrices.Multiply56C090(NativeBattleGfMatrices.Multiply56C090(y, x), z);
        Assert.NotEqual(xyz.Rotation.ToArray(), yxz.Rotation.ToArray());
        AssertRotation(xyz, Draw(Node(0x80200, angles: angles)));
        AssertRotation(yxz, Draw(Node(0xC0200, angles: angles)));
    }

    [Fact]
    public void Header100_right_multiplies_scale_with_native_rounding_and_preserves_translation()
    {
        NativeBattleVector angles = new(300, 500, 700);
        NativeBattleVector scale = new(2047, 3071, 1023);
        NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler701630(angles);
        NativeBattleActionMatrix diagonal = new([scale.X, 0, 0, 0, scale.Y, 0, 0, 0, scale.Z], 0, 0, 0, 4096);
        NativeBattleActionMatrix rounded = NativeBattleGfMatrices.Multiply56C090(rotation, diagonal);
        NativeBattleActionMatrix floored = NativeBattlePoseMatrices.Scale56BEF0(rotation, scale.X, scale.Y, scale.Z);
        Assert.NotEqual(rounded.Rotation.ToArray(), floored.Rotation.ToArray());
        NativeBattleEffect70ProgrammeDrawingStep result = Draw(Node(0x80300, new(12, 23, 34), angles, scale), Context(new(3, 4, 5)));
        AssertRotation(rounded, result);
        Assert.Equal(new int?[] { 15, 27, 2039 }, result.Memory.GeometryRegisters!.Translation.ToArray());
        NativeBattleEffect70ProgrammeDrawingStep ordinary = Draw(Node(0x80200, new(12, 23, 34), angles, scale), Context(new(3, 4, 5)));
        AssertRotation(floored, ordinary);
        Assert.Equal(result.Memory.GeometryRegisters.Translation.ToArray(), ordinary.Memory.GeometryRegisters!.Translation.ToArray());
    }

    [Fact]
    public void Morph80_writes_XYZ_only_retains_W_unknown_and_keeps_adjacent_packet_bytes()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().MarkUnknown(MorphOffset, MorphBytes).Write(MorphOffset + MorphBytes, [1, 2, 3, 4]);
        NativeBattleGfAnimationNode node = Node(blend: 2048, second: 1);
        NativeBattleEffect70ProgrammeDrawingStep result = Draw(node, memory: Memory(arena), count: 80);
        Assert.Equal((short)50, BinaryPrimitives.ReadInt16LittleEndian(result.Memory.Arena!.Read(MorphOffset + 79 * 8, 2)));
        Assert.Throws<NotSupportedException>(() => result.Memory.Arena.Read(MorphOffset + 79 * 8 + 6, 2));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, result.Memory.Arena.Read(MorphOffset + MorphBytes, 4));
        Assert.Equal(MorphAddress, U32(result.Temporary88, 4));
        Assert.Throws<InvalidDataException>(() => Draw(node, count: 81));
    }

    [Fact]
    public void Direct114_variant_ignores_unknown_morph_pointer_and_does_not_apply80_vertex_workspace_cap()
    {
        NativeBattleEffect70ProgrammeDrawingStep result = Draw(Node(), Context().MarkUnknown(0x20, 4), count: 114, variants: 1);
        Assert.Equal(SourceAddress + 28u, U32(result.Temporary88, 4));
        Assert.Empty(result.Batch.Attempts);
    }

    [Fact]
    public void Header4000_sets_exact_culling_flags_and_signed_context_depth_without_inventing_far_color()
    {
        NativeBattleEffectArena retained = NativeBattleEffectArena.Clear().MarkUnknown(0, 88).Write(8, [0xAA, 0xBB, 0xCC]);
        NativeBattleEffect70ProgrammeDrawingStep plain = Draw(Node(0x4000), temporary: retained);
        Assert.Equal(0x2000u, U32(plain.Temporary88, 0x1C));
        Assert.Equal(new byte[] { 0xAA, 0xBB, 0xCC }, plain.Temporary88.Read(8, 3));
        Assert.Throws<NotSupportedException>(() => plain.Temporary88.Read(11, 1));
        Assert.Equal(unchecked((uint)-1024), U32(plain.Temporary88, 0x18));
        NativeBattleEffect70ProgrammeDrawingStep faded = Draw(Node(0x4000, cue: 2048, color: 0x12345678));
        Assert.Equal(0x20C0u, U32(faded.Temporary88, 0x1C));
        Assert.Equal(0x12345678u, U32(faded.Temporary88, 8));
        Assert.Equal(2048u, U32(faded.Temporary88, 12));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0x200)]
    [InlineData(0x1000)]
    public void Caller36_scale_mode_scales_local_position_before_translation_and_rotation(uint header)
    {
        NativeBattleEffectArena caller = Context(new(10, 20, 30)).Write(8, BitConverter.GetBytes(8192)).Write(12, BitConverter.GetBytes(-4096)).Write(16, BitConverter.GetBytes(2048)).Write(0x14, BitConverter.GetBytes(1));
        NativeBattleEffect70ProgrammeDrawingStep step = Draw(Node(header, new(5, 7, 10)), caller);
        Assert.Equal(new int?[] { 20, 13, 2035 }, step.Memory.GeometryRegisters!.Translation.ToArray());
        AssertRotation(new([8192, 0, 0, 0, -4096, 0, 0, 0, 2048], 0, 0, 0, 4096), step);
    }

    [Fact]
    public void Zero_caller_scale_does_not_add_a_second_visibility_test()
    {
        NativeBattleEffectArena caller = Context().Write(8, new byte[12]).Write(0x14, BitConverter.GetBytes(1));
        NativeBattleEffect70ProgrammeDrawingStep step = Draw(Node(), caller);
        Assert.Equal(new int?[] { 0, 0, 2000 }, step.Memory.GeometryRegisters!.Translation.ToArray());
        AssertRotation(new([0, 0, 0, 0, 0, 0, 0, 0, 0], 0, 0, 0, 4096), step);
    }

    private const uint SourceAddress = 0x112015C;
    private const int MorphOffset = 0x241C;
    private const int MorphBytes = 640;
    private static uint MorphAddress => NativeBattleEffectNodeMemory.ArenaAddress + MorphOffset;
    private static NativeBattleCameraProjection Projection => new(null, 100, 160, 108);
    private static NativeBattleSummonColors Constants => NativeBattleGteColorInterpolation.CodeConstants45F270;
    private static NativeBattlePackedMatrix32 Packed(NativeBattleActionMatrix matrix) => NativeBattlePackedMatrix32.FromMatrix(matrix, null);

    private static NativeBattleEffectMemory Memory(NativeBattleEffectArena? arena = null) => NativeBattleEffectMemory.FromColdProcess() with { Arena = arena ?? NativeBattleEffectArena.Clear(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };

    private static NativeBattleEffect70ProgrammeDrawingStep Draw(NativeBattleGfAnimationNode node, NativeBattleEffectArena? context = null, NativeBattlePackedMatrix32? camera = null, NativeBattlePackedMatrix32? retained = null, NativeBattleEffectMemory? memory = null, NativeBattleEffectArena? temporary = null, int count = 4, int variants = 2)
    {
        NativeBattlePackedMatrix32 view = camera ?? Packed(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 });
        return NativeBattleEffect70Programme.Draw(Source(count, variants), node, context ?? Context(), view, retained ?? view, temporary ?? NativeBattleEffectArena.Clear().MarkUnknown(0, 88), memory ?? Memory(), MorphAddress, MorphBytes, (byte)'1', Projection, Constants);
    }

    private static NativeBattleEffectArena Context(NativeBattleVector anchor = default) => NativeBattleEffectArena.Clear().MarkUnknown(0, 36).Write(0x14, BitConverter.GetBytes(0)).Write(0, BitConverter.GetBytes(anchor.X)).Write(2, BitConverter.GetBytes(anchor.Y)).Write(4, BitConverter.GetBytes(anchor.Z)).Write(0x1C, BitConverter.GetBytes((short)-1024)).Write(0x20, BitConverter.GetBytes(MorphAddress));
    private static uint U32(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));

    private static void AssertRotation(NativeBattleActionMatrix expected, NativeBattleEffect70ProgrammeDrawingStep actual)
    {
        byte[] bytes = new byte[18];
        for (int index = 0; index < 9; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 2), expected.Rotation[index]);
        }
        Assert.Equal(bytes.Select(value => (byte?)value).ToArray(), actual.Memory.GeometryRegisters!.Rotation20.Take(18).ToArray());
    }

    private static NativeBattleGfAnimationNode Node(uint header = 0, NativeBattleVector position = default, NativeBattleVector angles = default, NativeBattleVector? scale = null, short blend = 0, short second = 0, short cue = 0, uint color = 0)
    {
        byte[] bytes = new byte[44];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), header);
        NativeBattleVector[] vectors = [position, angles, scale ?? new(4096, 4096, 4096)];
        for (int index = 0; index < vectors.Length; index++)
        {
            int at = 8 + index * 8;
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(at), vectors[index].X);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(at + 2), vectors[index].Y);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(at + 4), vectors[index].Z);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(32), color);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(36), cue);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(38), blend);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(42), second);
        return new(0, 0, bytes.ToImmutableArray());
    }

    private static NativeBattleGfAnimationSource Source(int count, int variants, bool face = false)
    {
        int faces = 12 + count * variants * 8;
        byte[] bytes = new byte[16 + faces + 32 + (face ? 24 : 0)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 16);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), faces);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), count);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), variants);
        short[] xy = [-400, -400, 400, -400, -400, 400, 400, 400];
        for (int variant = 0; variant < variants; variant++)
        {
            for (int vertex = 0; vertex < count; vertex++)
            {
                int at = 28 + (variant * count + vertex) * 8;
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(at), (short)((vertex < 4 ? xy[vertex * 2] : 0) + variant * 100));
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(at + 2), (short)(vertex < 4 ? xy[vertex * 2 + 1] : 0));
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(at + 6), 0x1234);
            }
        }
        if (face)
        {
            int group = 16 + faces + 3 * 4;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(group), 1);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(group + 4), 0x2C332211);
            for (int vertex = 0; vertex < 4; vertex++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(group + 8 + vertex * 2), (ushort)(vertex * 2));
            }
        }
        return new(SourceAddress, bytes.ToImmutableArray());
    }
}
