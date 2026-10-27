using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleZantetsukenRenderingTests
{
    [Theory]
    [InlineData(-1, 0, 0, 1, 64)]
    [InlineData(0, -1, 0, 2, 64)]
    [InlineData(0, 0, -1, 4, 64)]
    [InlineData(1, -1, 1, 3, 72)]
    [InlineData(-1, 1, 1, 5, 72)]
    [InlineData(1, 1, -1, 6, 72)]
    public void Every_native_fan_case_including_zero_endpoints_emits_exact_shape_count_and_restores_main(int a, int b, int c, uint mask, int allocated)
    {
        Fixture fixture = Create([a, b, c]);
        NativeBattleZantetsukenRenderStep result = Draw(fixture);
        Assert.Equal(mask, Dword(result.Temporary1D8, 0x1C));
        Assert.Equal(allocated, result.Batch.AllocationBytes);
        Assert.Equal(2, result.Batch.Attempts.Length);
        Assert.Equal(allocated == 72 ? new[] { 32, 40 } : new[] { 32, 32 }, result.Batch.Attempts.Select(attempt => attempt.PacketLength));
        Assert.All(result.Batch.Attempts, attempt =>
        {
            Assert.NotNull(attempt.Primitive);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[30]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[31]);
            Assert.Equal(4, attempt.Primitive!.VertexPayload4.Length);
        });
        Assert.Equal(new int?[] { 0, 0, 2000 }, result.Memory.GeometryRegisters!.Translation);
        Assert.Throws<NotSupportedException>(() => result.Temporary1D8.Read(0x34, 4));
        Assert.Throws<NotSupportedException>(() => result.Temporary1D8.Read(0x2C, 4));
        Assert.Throws<NotSupportedException>(() => result.Temporary1D8.Read(0, 4));
        for (int index = 0; index < 3; index++)
        {
            Assert.Throws<NotSupportedException>(() => result.VertexScratch.Read(index * 8 + 6, 2));
        }
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void Unsplit_original_side_keeps_plane_MAC_IR_but_alternate_side_reprojects(int sign, bool alternate)
    {
        Fixture fixture = Create([sign, sign, sign]);
        NativeBattleZantetsukenRenderStep result = Draw(fixture);
        Assert.Equal(32, result.Batch.AllocationBytes);
        Assert.Equal(0u, Dword(result.Temporary1D8, 0x1C));
        Assert.Equal(0u, result.Memory.ScriptedColorRegisters!.Flags);
        if (alternate)
        {
            Assert.All(result.Memory.ScriptedColorRegisters.Mac, value => Assert.NotNull(value));
            Assert.Equal(1u, Dword(result.Temporary1D8, 0x24) & 0xFFFF);
        }
        else
        {
            Assert.Equal(new int?[] { sign * 20, null, null }, result.Memory.ScriptedColorRegisters.Mac);
            Assert.Equal(new int?[] { sign * 20, null, null }, result.Memory.ScriptedColorRegisters.Ir);
        }
        Assert.Equal(0x07000000u, BinaryPrimitives.ReadUInt32LittleEndian(result.Batch.Attempts[0].Data.AsSpan()));
        Assert.Equal(new int?[] { 0, 0, 2000 }, result.Memory.GeometryRegisters!.Translation);
    }

    [Fact]
    public void Stage_quad_shades_once_and_second_triple_preserves_new_plane_outputs()
    {
        Fixture fixture = Create([-1, -1, -1, -1], true, true);
        NativeBattleZantetsukenRenderStep result = NativeBattleZantetsukenRendering.DrawStage(fixture.Memory, fixture.Temporary, fixture.Scratch, fixture.Objects, fixture.Inputs, 0, 2048);
        Assert.Equal(64, result.Batch.AllocationBytes);
        Assert.Equal(2, result.Batch.Attempts.Length);
        NativeBattleScriptedColorRegisters colors = result.Memory.ScriptedColorRegisters!;
        Assert.Equal(new int?[] { -20, null, null }, colors.Mac);
        Assert.Equal(0u, colors.Ir0);
        Assert.Equal(new uint?[] { null, null, 0x02030201 }, colors.Rgb);
        Assert.Equal(0x02060402u, colors.Material);
        Assert.Equal(0x02030201u, Dword(result.Temporary1D8, 0x20));
        Assert.Equal(new ushort[] { 0, 0, 0, 1 }, Enumerable.Range(0, 4).Select(index => unchecked((ushort)Short(result.Temporary1D8, 8 + index * 2))));
        Assert.Equal(0u, result.Memory.PrimitiveProcess!.ModelRendererRegisters!.FarRed);
    }

    [Fact]
    public void Masked_object_does_not_load_bones_or_camera_and_preserves_scratch()
    {
        Fixture fixture = Create([-1, -1, -1]);
        NativeBattleZantetsukenRenderStep result = Draw(fixture, objectMask: 0);
        Assert.Empty(result.Batch.Attempts);
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.Same(fixture.Memory.GeometryRegisters, result.Memory.GeometryRegisters);
        Assert.Same(fixture.Memory.ScriptedColorRegisters, result.Memory.ScriptedColorRegisters);
        Assert.Equal(NativeBattleTestSnapshots.Json(fixture.Scratch), NativeBattleTestSnapshots.Json(result.VertexScratch));
        Assert.NotNull(result.Memory.ScriptedLighting!.LightMatrix);
        Assert.Throws<NotSupportedException>(() => result.Temporary1D8.Read(0, 4));
    }

    [Fact]
    public void Bone_projection_saturates_IR_and_retains_packed_scratch_W()
    {
        Fixture fixture = Create([-1, -1, -1]);
        NativeBattleRawMesh original = fixture.Objects[0].Mesh;
        byte[] vertices = original.PackedVertexGroups!.Value[0].Bytes.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(vertices, 20000);
        NativeBattleRawMesh mesh = original with { PackedVertexGroups = [new(0, 3, vertices.ToImmutableArray())] };
        NativeBattleActionMatrix scale = Identity with { Rotation = [8192, 0, 0, 0, 4096, 0, 0, 0, 4096] };
        fixture = fixture with { Objects = [new(mesh, null)], Inputs = fixture.Inputs with { Bones = [scale] } };
        NativeBattleZantetsukenRenderStep result = Draw(fixture);
        Assert.Equal(short.MaxValue, Short(result.VertexScratch, 0));
        Assert.Throws<NotSupportedException>(() => result.VertexScratch.Read(6, 2));
    }

    [Fact]
    public void Physical_pointer_publications_are_exact_and_cold_next_reuses_the_same_canonical_owners()
    {
        Fixture fixture = Create([1, -1, 1]);
        fixture = fixture with { Inputs = fixture.Inputs with { PacketAddress = 0x12000000, OrderingAddress = 0x13000044 }, Objects = [fixture.Objects[0] with { FaceAddress = 0x14000020 }] };
        NativeBattleZantetsukenRenderStep first = Draw(fixture);
        Assert.Equal(0x12000048u, Dword(first.Temporary1D8, 0x34));
        Assert.Equal(0x13000044u, Dword(first.Temporary1D8, 0x2C));
        Assert.Equal(0x14000020u, Dword(first.Temporary1D8, 0));
        Assert.Equal(0x15000000u, Dword(first.Temporary1D8, 4));
        Assert.Equal(NativeBattleTemporaryMemory.Address + 0x48, Dword(first.Temporary1D8, 0xB8));
        NativeBattleZantetsukenRenderStep restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleZantetsukenRenderStep>(NativeBattleTestSnapshots.Json(first))!;
        NativeBattleZantetsukenRenderStep Next(NativeBattleZantetsukenRenderStep current) => NativeBattleZantetsukenRendering.DrawBody(current.Memory, current.Temporary1D8, current.VertexScratch, fixture.Objects, fixture.Inputs, 1, 0x808080);
        Assert.Equal(NativeBattleTestSnapshots.Json(Next(first)), NativeBattleTestSnapshots.Json(Next(restored)));
    }

    [Fact]
    public void Plane_preparation_uses_wrapped_dot_then_SAR_negation_and_stage_preserves_unknown_anchor_W()
    {
        NativeBattleEffectArena retained = NativeBattleEffectArena.Clear().MarkUnknown(0, 472);
        NativeBattleEffectArena result = NativeBattleZantetsukenRendering.PreparePlane(retained, new(1, 0, 0, 123), new(-1, 0, 0), false);
        Assert.Equal((short)1, Short(result, 0xAE));
        Assert.Throws<NotSupportedException>(() => result.Read(0x4E, 2));
        Assert.Equal((short)1, Short(result, 0x48));
        Assert.Throws<NotSupportedException>(() => result.Read(0x50, 8));
    }

    private sealed record Fixture(NativeBattleEffectMemory Memory, NativeBattleEffectArena Temporary, NativeBattleEffectArena Scratch, ImmutableArray<NativeBattleZantetsukenRenderObject> Objects, NativeBattleZantetsukenRenderInputs Inputs);
    private static NativeBattleActionMatrix Identity => NativeBattleCuragaMatrices.Identity8DD770();
    private static NativeBattleZantetsukenRenderStep Draw(Fixture fixture, uint objectMask = 1) => NativeBattleZantetsukenRendering.DrawBody(fixture.Memory, fixture.Temporary, fixture.Scratch, fixture.Objects, fixture.Inputs, objectMask, 0x808080);
    private static Fixture Create(int[] signs, bool quad = false, bool stage = false)
    {
        byte[] packed = new byte[signs.Length * 6 + 6];
        var vertices = ImmutableArray.CreateBuilder<NativeBattleRawVertex>();
        short[] xs = [-200, 200, -200, 200];
        short[] ys = [-200, -200, 200, 200];
        for (int index = 0; index < signs.Length; index++)
        {
            short z = (short)(signs[index] * 20);
            BinaryPrimitives.WriteInt16LittleEndian(packed.AsSpan(index * 6), xs[index]);
            BinaryPrimitives.WriteInt16LittleEndian(packed.AsSpan(index * 6 + 2), ys[index]);
            BinaryPrimitives.WriteInt16LittleEndian(packed.AsSpan(index * 6 + 4), z);
            vertices.Add(new(new(xs[index], ys[index], z), 0));
        }
        byte[] face = new byte[(quad ? 20 : 16) + (stage ? 4 : 0)];
        for (int index = 0; index < signs.Length; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(index * 2), (ushort)index);
        }
        if (!quad)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(6), 0x0303);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(8), 0x00200001);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(12), 0x00AA0202);
        if (quad)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(16), 0x04040303);
        }
        if (stage)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(quad ? 20 : 16), 0x02060402);
        }
        NativeBattleRawMesh mesh = new(vertices.ToImmutable(), [new(quad, face.ToImmutableArray())]) { PackedVertexGroups = [new(0, (short)signs.Length, packed.ToImmutableArray())] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedLighting = null, ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        NativeBattleEffectArena temporary = NativeBattleZantetsukenRendering.PreparePlane(NativeBattleEffectArena.Clear().MarkUnknown(0, 472), new(0, 0, 0, 17), new(0, 0, 4096));
        uint[] table = [0, 1, 2, 0, 0, 1, 2, 0, 0, 3, 2, 3, 3, 1, 2, 0, 0, 1, 4, 4, 0, 4, 2, 0, 3, 1, 4, 4, 0, 3, 2, 4, 5, 1, 2, 5, 0, 1, 5, 0, 0, 3, 5, 3, 5, 3, 2, 1, 5, 4, 2, 5, 0, 1, 5, 4];
        NativeBattleZantetsukenRenderInputs inputs = new(NativeBattlePackedMatrix32.FromMatrix(Identity with { Z = 2000 }, 0x1234), NativeBattlePackedMatrix32.FromMatrix(Identity with { X = 50, Z = 2000 }, null), [Identity], _ => null, new(null, 288, 160, 108), new(0, 0, 1f / 4096f, BitConverter.Int64BitsToDouble(0x4330000080000000L)), NativeBattleTemporaryMemory.Address, 0x15000000, null, null, 17, 2, table.ToImmutableArray());
        return new(memory, temporary, NativeBattleEffectArena.Clear().MarkUnknown(0, 64), [new(mesh, null)], inputs);
    }
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static short Short(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
}
