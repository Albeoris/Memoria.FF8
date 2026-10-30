using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAnchorActPreparationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Group16_anchor_capture_forwards_exact_selected_or_morphed_source_without_native_changes(bool morph)
    {
        NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with
        {
            Arena = NativeBattleEffectArena.Clear().Write(MorphOffset, Enumerable.Repeat((byte)0xA5, 384).ToArray()),
            ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0()
        };
        NativeBattlePilotVisualIdentity identity = new(217, 88, 0, 48, 32) { ResourceGeneration = 0x44 };
        NativeBattleCameraProjection projection = new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108);
        var native = NativeBattleAnchorDepthAct16.Draw(Memory(), Source(4), Node(morph), Context(-128), Camera(), Temporary(), projection, Constants, MorphAddress, 384);
        var captured = NativeBattleAnchorDepthAct16.Draw(Memory(), Source(4), Node(morph), Context(-128), Camera(), Temporary(), projection, Constants, MorphAddress, 384, pilotIdentity: identity);
        Assert.Equal(NativeBattleTestSnapshots.Json(native), NativeBattleTestSnapshots.Json(captured));
        Assert.Null(Assert.Single(native.Batch.Attempts).Primitive!.PilotGeometry);
        var visual = Assert.IsType<NativeBattlePilotVisualGeometry>(Assert.Single(captured.Batch.Attempts).Primitive!.PilotGeometry);
        Assert.Equal(identity.Generation, visual.Identity.Generation);
        Assert.Equal(identity.ResourceGeneration, visual.Identity.ResourceGeneration);
        Assert.Equal(new System.Numerics.Vector3(morph ? -350 : -400, -400, 2000), visual.A);
        Assert.Equal(new System.Numerics.Vector3(morph ? 450 : 400, 400, 2000), visual.D);
        Assert.Equal(unchecked((uint)-128), U32(captured.Temporary88, 0x18));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Effect101_anchor_capture_keeps_selected_morph_W_registers_and_exact_packets(bool morph)
    {
        NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with
        {
            Arena = NativeBattleEffectArena.Clear().Write(MorphOffset, Enumerable.Repeat((byte)0xA5, 384).ToArray()),
            ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0()
        };
        NativeBattlePilotVisualIdentity identity = new(101, 88, 0, 48, 32) { ResourceGeneration = 0x44 };
        NativeBattleCameraProjection projection = new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108);
        var native = NativeBattleAnchorDepthAct16.Draw(Memory(), Source(4), Node(morph), Context(-128), Camera(), Temporary(), projection, Constants, MorphAddress, 384);
        string detachedNative = NativeBattleTestSnapshots.Json(native);
        var captured = NativeBattleAnchorDepthAct16.Draw(Memory(), Source(4), Node(morph), Context(-128), Camera(), Temporary(), projection, Constants, MorphAddress, 384, pilotIdentity: identity);
        Assert.Equal(detachedNative, NativeBattleTestSnapshots.Json(captured));
        Assert.Null(Assert.Single(native.Batch.Attempts).Primitive!.PilotGeometry);
        var visual = Assert.IsType<NativeBattlePilotVisualGeometry>(Assert.Single(captured.Batch.Attempts).Primitive!.PilotGeometry);
        Assert.Equal(identity.Generation, visual.Identity.Generation);
        Assert.Equal(identity.ResourceGeneration, visual.Identity.ResourceGeneration);
        Assert.Equal(new System.Numerics.Vector3(morph ? -350 : -400, -400, 2000), visual.A);
        Assert.Equal(new System.Numerics.Vector3(morph ? 450 : 400, 400, 2000), visual.D);
        Assert.Equal(unchecked((uint)-128), U32(captured.Temporary88, 0x18));
    }

    private const int MorphOffset = 0x4000;
    private const uint MorphAddress = NativeBattleEffectNodeMemory.ArenaAddress + MorphOffset;
    private static NativeBattleSummonColors Constants => NativeBattleGteColorInterpolation.CodeConstants45F270;

    [Theory]
    [InlineData(-128)]
    [InlineData(0)]
    [InlineData(1024)]
    public void Context16_copies_signed_depth_and_uses_morph_pointer_at_C(int depth)
    {
        NativeBattleAnchorActPreparationStep prepared = Prepare(Source(4), Node(), Context(depth));
        Assert.Equal(unchecked((uint)depth), U32(prepared.Temporary88, 0x18));
        Assert.Throws<NotSupportedException>(() => prepared.Temporary88.Read(8, 3));
        Assert.Equal(0x2030u, U32(prepared.Temporary88, 0x1C));
        Assert.Null(prepared.Registers.Geometry.Rotation20[18]);
        Assert.Null(prepared.Registers.Geometry.Rotation20[19]);
    }

    [Fact]
    public void Electrocute_keeps_retained_depth_and_unconditional_direct_capacity_guard()
    {
        NativeBattleEffectArena temporary = Temporary().Write(0x18, BitConverter.GetBytes(-77));
        NativeBattleEffectArena context = Context(0).Write(8, BitConverter.GetBytes(MorphAddress));
        NativeBattleAnchorActPreparationStep prepared = Prepare(Source(4), Node(), context, NativeBattleAnchorActContextPolicy.Electrocute275, temporary);
        Assert.Equal(unchecked((uint)-77), U32(prepared.Temporary88, 0x18));
        Assert.Throws<InvalidDataException>(() => Prepare(Source(114), Node(), context, NativeBattleAnchorActContextPolicy.Electrocute275));
    }

    [Fact]
    public void Direct114_vertex_shape_does_not_consume_48_vertex_morph_pointer_or_scratch()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().MarkUnknown(MorphOffset, 384);
        NativeBattleAnchorActPreparationStep prepared = Prepare(Source(114), Node(), Context(0).MarkUnknown(12, 4), arena: arena);
        Assert.Equal((short)0x1234, prepared.ReadVertex(113 * 8).W);
        Assert.Throws<NotSupportedException>(() => prepared.Arena.Read(MorphOffset, 1));
        Assert.Throws<InvalidDataException>(() => prepared.ReadVertex(114 * 8));
    }

    [Fact]
    public void Interpolation48_preserves_W_and_adjacent_bytes_and_requires_actual_C_pointer()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().MarkUnknown(MorphOffset, 384).Write(MorphOffset + 384, [1, 2, 3, 4]);
        NativeBattleAnchorActPreparationStep prepared = Prepare(Source(48), Node(true), Context(-1), arena: arena);
        Assert.Null(prepared.ReadVertex(47 * 8).W);
        Assert.Equal((short)50, prepared.ReadVertex(47 * 8).Vertex.X);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, prepared.Arena.Read(MorphOffset + 384, 4));
        Assert.Throws<InvalidDataException>(() => Prepare(Source(49), Node(true), Context(0)));
        Assert.Throws<InvalidDataException>(() => Prepare(Source(48), Node(true), Context(0).Write(12, BitConverter.GetBytes(MorphAddress + 8))));
    }

    [Theory]
    [InlineData(-128)]
    [InlineData(128)]
    public void Context16_uses_default_backend_and_publishes_current_memory_controls(int depth)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        memory = memory with { Arena = NativeBattleEffectArena.Clear(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), PrimitiveProcess = memory.PrimitiveProcess! with { ModelRendererRegisters = new(1, 2, 3, 4, 5) } };
        NativeBattleAnchorDepthAct16Step result = NativeBattleAnchorDepthAct16.Draw(memory, Source(4), Node(), Context(depth), Camera(), Temporary(), new(null, 100, 160, 108), Constants, MorphAddress, 384);
        Assert.Equal(unchecked((uint)depth), U32(result.Temporary88, 0x18));
        Assert.Single(result.Batch.Attempts);
        Assert.NotNull(result.Memory.GeometryRegisters);
        Assert.NotNull(result.Memory.ScriptedColorRegisters);
        Assert.Null(result.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
    }

    [Fact]
    public void Unsupported_context_policy_fails_before_preparing_any_native_fields()
    {
        Assert.Throws<NotSupportedException>(() => Prepare(Source(4), Node(), Context(0), new(8, 8, true)));
    }

    private static NativeBattleAnchorActPreparationStep Prepare(NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena context, NativeBattleAnchorActContextPolicy? policy = null, NativeBattleEffectArena? temporary = null, NativeBattleEffectArena? arena = null)
    {
        return NativeBattleAnchorActPreparation.Prepare(source, node, context, Camera(), temporary ?? Temporary(), arena ?? NativeBattleEffectArena.Clear(), MorphAddress, 384, new(NativeBattleGeometryRegisters.Unknown(), NativeBattleScriptedColorRegisters.Unknown()), Constants, policy ?? NativeBattleAnchorActContextPolicy.AnchorDepth16);
    }

    private static NativeBattlePackedMatrix32 Camera() => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 }, null);
    private static NativeBattleEffectArena Temporary() => NativeBattleEffectArena.Clear().MarkUnknown(0, 88);
    private static NativeBattleEffectArena Context(int depth) => NativeBattleEffectArena.Clear().MarkUnknown(0, 16).Write(0, new byte[8]).Write(8, BitConverter.GetBytes(depth)).Write(12, BitConverter.GetBytes(MorphAddress));

    private static NativeBattleGfAnimationNode Node(bool interpolate = false)
    {
        byte[] node = new byte[44];
        for (int axis = 0; axis < 3; axis++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(node.AsSpan(24 + axis * 2), 4096);
        }
        if (interpolate)
        {
            BinaryPrimitives.WriteInt16LittleEndian(node.AsSpan(38), 2048);
            BinaryPrimitives.WriteInt16LittleEndian(node.AsSpan(42), 1);
        }
        return new(0, 0, node.ToImmutableArray());
    }

    private static NativeBattleGfAnimationSource Source(int count)
    {
        int faces = 12 + count * 16;
        byte[] bytes = new byte[16 + faces + 68];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 16);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), faces);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(20), count);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), 2);
        short[] coordinates = [-400, -400, 400, -400, -400, 400, 400, 400];
        for (int shape = 0; shape < 2; shape++)
        {
            for (int vertex = 0; vertex < count; vertex++)
            {
                int at = 28 + (shape * count + vertex) * 8;
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(at), (short)((vertex < 4 ? coordinates[vertex * 2] : 0) + shape * 100));
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(at + 2), (short)(vertex < 4 ? coordinates[vertex * 2 + 1] : 0));
                BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(at + 6), 0x1234);
            }
        }
        int group = 16 + faces + 28;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(group), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(group + 4), 0x3C332211);
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(group + 8 + index * 2), (ushort)(index * 2));
        }
        return new(0x1000224, bytes.ToImmutableArray());
    }

    private static uint U32(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
}
