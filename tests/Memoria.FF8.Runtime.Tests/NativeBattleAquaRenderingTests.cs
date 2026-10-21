using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAquaRenderingTests
{
    [Fact]
    public void Wave_context_retains_its_distinct_horizontal_scroll_and_128_by_256_window()
    {
        NativeBattleModulePoolTask task = Task().WriteDword(0x40, 0x123456).WriteWord(0x50, 1234).WriteWord(0x52, 0xFFF0).WriteWord(0x5E, 120);
        NativeBattleEsunaMeshContext context = NativeBattleAquaRendering.WaveContext(task);
        Assert.Equal(0xF0u, context.Flags);
        Assert.Equal(0x123456u, context.FarColor);
        Assert.Equal(1234, context.Fade);
        Assert.Equal(-16, context.DepthBias);
        Assert.Equal(120, context.UOffset);
        Assert.Equal(0, context.VOffset);
        Assert.Equal(new NativeBattleTextureWindow(0, 0, 256, 256), context.WindowBefore);
        Assert.Equal(new NativeBattleTextureWindow(0, 0, 128, 256), context.WindowAfter);
        Assert.Equal(17, context.TableBaseBucket);
        Assert.Equal(2, context.DepthShift);
    }

    [Fact]
    public void Billboard_uses_actual_camera_times_transpose_instead_of_substituting_identity()
    {
        NativeBattleActionMatrix camera = new([2048, 0, 0, 0, 2048, 0, 0, 0, 2048], 10, 20, 30, 4096);
        NativeBattleActionMatrix result = NativeBattleAquaRendering.ComposeBillboard(camera, new(2, 4, 8));
        Assert.Equal(new short[] { 1024, 0, 0, 0, 1024, 0, 0, 0, 1024 }, result.Rotation);
        Assert.Equal((11, 22, 34), (result.X, result.Y, result.Z));
    }

    [Fact]
    public void Sprite_uses_signed_bias_before_depth_shift_and_preserves_payload_and_packet_padding_masks()
    {
        NativeBattleModulePoolTask task = SpriteTask(2000);
        NativeBattleAquaSpriteComposition result = NativeBattleAquaRendering.ComposeSprite(task, Resources(), Identity, Projection, 0);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(result.Batch.Attempts);
        NativeBattleGeometryPrimitive primitive = Assert.IsType<NativeBattleGeometryPrimitive>(attempt.Primitive);
        Assert.Equal(138, primitive.Bucket);
        Assert.Equal(0x2E404040u, primitive.Words[1]);
        Assert.Equal(0x00C01E14u, primitive.Words[3]);
        Assert.Equal(new[] { 2000, 2000, 2000, 2000 }, primitive.VertexPayload4);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[38]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[39]);
    }

    [Fact]
    public void Rejected_sprite_publishes_triple_and_partial_packet_without_fourth_average_or_color_writes()
    {
        List<NativeBattleAquaOperation> operations = [];
        NativeBattleAquaFrame frame = new(NativeBattleEffectMemory.FromColdProcess() with { Aqua55 = NativeBattleAquaState.FromColdProcess(55), SharedEffectView2793E58 = NativeBattlePackedMatrix32.FromMatrix(Identity, null) }, new(55, 54, new(55, 0x808820, 0x808800), null, 0), NativeCrtRandomState.FromColdProcess());
        NativeBattleAquaInputs inputs = new(NativeBattleActionPacketsState.Fresh(), Identity, Projection, 0, new(0, 0, 1f / 4096, 4503601774854144d), _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException(), (current, operation) =>
        {
            operations.Add(operation);
            return current;
        });
        NativeBattleAquaRendering.DrawSprite(frame, SpriteTask(-2000), Resources(), inputs);
        Assert.Equal(new NativeBattleAquaTemporaryBegin(216), operations[0]);
        NativeBattleAquaProjection projected = Assert.Single(operations.OfType<NativeBattleAquaProjection>());
        Assert.False(projected.FinalCue);
        Assert.Equal(3, projected.Points.Length);
        Assert.Equal(0x44, projected.TemporaryVectorOffset);
        Assert.Equal(new NativeBattleAquaTemporaryFlags(0x40), Assert.Single(operations.OfType<NativeBattleAquaTemporaryFlags>()));
        Assert.DoesNotContain(operations.OfType<NativeBattleAquaTemporaryPatch>(), patch => patch.Offset == 0x40);
        Assert.Empty(operations.OfType<NativeBattleAquaAverageDepth>());
        Assert.Empty(operations.OfType<NativeBattleAquaColor>());
        NativeBattleGfGeometryAttempt attempt = Assert.Single(Assert.Single(operations.OfType<NativeBattleAquaPackets>()).Batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[4]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[8]);
        Assert.DoesNotContain(new NativeBattleAquaTemporaryUnknown(0xCA, 2), operations);
        Assert.DoesNotContain(new NativeBattleAquaTemporaryUnknown(0x96, 2), operations);
        Assert.Contains(new NativeBattleAquaTemporaryCopy(0xCA, 0x96, 2), operations);
        Assert.Equal(new int?[] { 0xB8, 0x64 }, operations.OfType<NativeBattleAquaMatrix>().Select(value => value.TemporaryMatrixOffset));
        Assert.Equal(2, operations.OfType<NativeBattleAquaMatrixProduct>().Count());
        Assert.Equal(2, operations.OfType<NativeBattleAquaLongTransform>().Count());
        Assert.All(operations.OfType<NativeBattleAquaLongTransform>(), value => Assert.False(value.LoadRotation));
        Assert.DoesNotContain(new NativeBattleAquaTemporaryUnknown(0x76, 2), operations);
        Assert.Equal(new NativeBattleAquaTemporaryEnd(216), operations[^1]);
    }

    [Fact]
    public void Wave_projection_publishes_source_fourth_words_and_reads_retained_flags_at_the_original_site()
    {
        byte[] mesh = new byte[10600];
        BinaryPrimitives.WriteInt32LittleEndian(mesh, 2504);
        BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(4), 312);
        for (int index = 0; index < 4; index++)
        {
            int offset = 8 + index * 8;
            BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(offset), (short)((index & 1) == 0 ? -64 : 64));
            BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(offset + 2), (short)(index < 2 ? -64 : 64));
            BinaryPrimitives.WriteInt16LittleEndian(mesh.AsSpan(offset + 4), 2000);
            BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(offset + 6), (ushort)(0x8001 + index));
        }
        BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(2516), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(2520), 0x2E808080);
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(2524 + index * 2), (ushort)(index * 2));
        }
        NativeBattleAquaState retained = NativeBattleAquaState.FromColdProcess(55) with { WaveMesh = mesh.ToImmutableArray() };
        NativeBattleAquaFrame frame = new(NativeBattleEffectMemory.FromColdProcess() with { Aqua55 = retained }, new(55, 54, new(55, 0x808820, 0x808800), null, 0), NativeCrtRandomState.FromColdProcess());
        List<NativeBattleAquaOperation> operations = [];
        NativeBattleAquaInputs inputs = new(NativeBattleActionPacketsState.Fresh(), Identity, Projection, 0, new(0, 0, 1f / 4096, 4503601774854144d), _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), (_, _, _) => throw new InvalidOperationException(), (current, operation) =>
        {
            operations.Add(operation);
            return current;
        });
        NativeBattleModulePoolTask task = Task().WriteDword(0x4C, frame.Profile.MeshAddress).WriteDword(0x30, 4096).WriteDword(0x34, 4096).WriteDword(0x38, 4096).WriteWord(0x50, 4096);
        NativeBattleAquaRendering.DrawWave(frame, task, Resources(), inputs);
        NativeBattleAquaProjection triple = operations.OfType<NativeBattleAquaProjection>().First();
        Assert.Equal(new short?[] { -32767, -32766, -32765 }, triple.Ws);
        Assert.Null(triple.TemporaryVectorOffset);
        Assert.Equal(new NativeBattleAquaTemporaryFlags(0x3C), Assert.Single(operations.OfType<NativeBattleAquaTemporaryFlags>()));
        Assert.DoesNotContain(operations.OfType<NativeBattleAquaTemporaryPatch>(), patch => patch.Offset == 0x3C);
    }

    [Fact]
    public void Unsupported_source_quad_rotation_fails_at_the_resource_boundary()
    {
        NativeBattleAquaResources source = Resources();
        byte[] bytes = source.Sprites[0x15A38DC].ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(32), 1);
        NativeBattleAquaResources changed = source with { Sprites = source.Sprites.SetItem(0x15A38DC, bytes.ToImmutableArray()) };
        Assert.Throws<NotSupportedException>(() => NativeBattleAquaRendering.ComposeSprite(SpriteTask(2000), changed, Identity, Projection, 0));
    }

    private static NativeBattleActionMatrix Identity => NativeBattleCuragaMatrices.Identity8DD770();
    private static NativeBattleCameraProjection Projection => new(Identity, 256, 160, 108);
    private static NativeBattleModulePoolTask Task() => new(1, 0, null, 1, new byte[328].ToImmutableArray());
    private static NativeBattleModulePoolTask SpriteTask(short depth) => Task().WriteDword(0x4C, 0x15A38DC).WriteWord(0x20, unchecked((ushort)depth)).WriteWord(0x56, 0xFFF0);

    private static NativeBattleAquaResources Resources()
    {
        byte[] bytes = new byte[40];
        bytes[4] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 0xFFFF);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), 1);
        bytes[20] = 8;
        bytes[21] = 64;
        bytes[22] = 8;
        bytes[23] = 0x2E;
        bytes[24] = 20;
        bytes[25] = 30;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(26), 128);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(28), -4);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(30), -4);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(34), 0x400);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(36), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(38), 4096);
        return new(55, new("aqua-camera", []), ImmutableDictionary<uint, ImmutableArray<byte>>.Empty.Add(0x15A38DC, bytes.ToImmutableArray()), [], [], 0, null);
    }
}
