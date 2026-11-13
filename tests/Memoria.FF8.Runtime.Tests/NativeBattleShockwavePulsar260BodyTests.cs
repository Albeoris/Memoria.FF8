using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShockwavePulsar260BodyTests
{
    private sealed record Material : INativeBattleForeignMaterial;
    private static NativeBattleActionMatrix Identity => NativeBattleCuragaMatrices.Identity8DD770();
    private static NativeBattleCameraProjection Projection => new(null, 256, 160, 108);
    private static NativeBattleSummonColors Constants => NativeBattleGteColorInterpolation.CodeConstants45F270;

    [Fact]
    public void Body92_preserves_unwritten_depth_cue_and_pins_the_rotated_scaled_pivot()
    {
        NativeBattleEffectArena retained = NativeBattleEffectArena.Clear().Write(0x54, BitConverter.GetBytes(0x12345678u));
        NativeBattleShockwavePulsar260BodyPreparationStep actual = NativeBattleShockwavePulsar260BodyPreparation.Prepare693170(retained, Identity with { X = 100, Y = 200, Z = 300 }, NativeBattlePackedMatrix32.FromMatrix(Identity with { Z = 2000 }, null), Vector(new(0, 0, 0)), Vector(new(10, 20, 30)), 2048, 0x196A018, NativeBattleEffectArena.Clear().MarkUnknown(0, 4), 0x808080, 3);
        Assert.Equal(new[] { 105, 210, 2315 }, new[] { actual.Matrix.X, actual.Matrix.Y, actual.Matrix.Z });
        Assert.Equal(new short[] { 2048, 0, 0, 0, 2048, 0, 0, 0, 2048 }, actual.Matrix.Rotation);
        Assert.Equal(0x12345678u, Dword(actual.Temporary92, 0x54));
        Assert.Equal(0u, Dword(actual.Temporary92, 0x58));
        Assert.Equal(0x196A018u, Dword(actual.Temporary92, 0x34));
        Assert.Throws<NotSupportedException>(() => actual.Temporary92.Read(0x40, 4));
        Assert.Throws<NotSupportedException>(() => actual.Temporary92.Read(18, 2));
    }

    [Fact]
    public void Bounds_uses_saturated_signed_IR2_with_zero_W_and_retains_partial_temporary_bytes()
    {
        byte[] source = Geometry(false);
        NativeBattleEffectMemory memory = Memory();
        var actual = NativeBattleShockwavePulsar260BoundsCapture.Capture693000(memory, NativeBattleEffectArena.Clear().MarkUnknown(0, 24), [NativeBattlePackedMatrix32.FromMatrix(Identity with { Y = 32700 }, null)], (offset, count) => source.AsSpan(offset, count).ToArray(), 1);
        Assert.Equal((short)32767, actual.Maximum);
        Assert.Equal((short)32300, actual.Minimum);
        Assert.Equal((int?)0x7FFF, actual.Memory.ScriptedColorRegisters!.Ir[1]);
        Assert.Equal(new byte[] { 0, 0 }, actual.Memory.ScriptedColorRegisters!.NormalInput!.Value.Skip(6));
        Assert.Equal(32767u, Dword(actual.Temporary24, 4));
        Assert.Throws<NotSupportedException>(() => actual.Temporary24.Read(0x12, 2));
        Assert.Throws<NotSupportedException>(() => actual.Temporary24.Read(0xE, 2));
    }

    [Fact]
    public void Foreign_bounds_walk_actual_indexed_vertices_with_native_ground_basis_and_register_effects()
    {
        NativeBattleForeignModelGeometry foreign = ForeignGeometry() with { World = Identity with { Y = 12345 } };
        NativeBattleEffectArena temporary = NativeBattleEffectArena.CreateUnknown(24);
        NativeBattleShockwavePulsar260BoundsStep actual = NativeBattleShockwavePulsar260BoundsCapture.CaptureForeign(Memory(), temporary, foreign);
        byte[] bytes = Geometry(false);
        NativeBattleShockwavePulsar260BoundsStep native = NativeBattleShockwavePulsar260BoundsCapture.Capture693000(Memory(), temporary, [NativeBattlePackedMatrix32.FromMatrix(Identity, null)], (offset, count) => bytes.AsSpan(offset, count).ToArray(), 1);
        Assert.Equal(native.Maximum, actual.Maximum);
        Assert.Equal(native.Minimum, actual.Minimum);
        Assert.Equal(NativeBattleTestSnapshots.Json(native.Memory), NativeBattleTestSnapshots.Json(actual.Memory));
        Assert.Equal((short)400, actual.Maximum);
        Assert.Equal((short)-400, actual.Minimum);
        NativeBattleShockwavePulsar260BoundsStep restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleShockwavePulsar260BoundsStep>(NativeBattleTestSnapshots.Json(actual))!;
        Assert.Equal(NativeBattleTestSnapshots.Json(NativeBattleShockwavePulsar260BoundsCapture.CaptureForeign(actual.Memory, actual.Temporary24, foreign)), NativeBattleTestSnapshots.Json(NativeBattleShockwavePulsar260BoundsCapture.CaptureForeign(restored.Memory, restored.Temporary24, foreign)));
    }

    [Fact]
    public void Foreign_body_uses_native_pivot_scale_and_retains_original_host_surface_without_native_resources()
    {
        NativeBattleForeignModelGeometry foreign = ForeignGeometry();
        NativeBattleActionMatrix view = Identity with { Z = 2000 };
        NativeBattleCameraProjection projection = Projection with { View = view };
        NativeBattleShockwavePulsar260BodyPreparationStep prepared = NativeBattleShockwavePulsar260BodyPreparation.Prepare693170(NativeBattleEffectArena.CreateUnknown(92), foreign.World, NativeBattlePackedMatrix32.FromMatrix(view, null), Vector(default), Vector(default), 2048, 0x1000, NativeBattleEffectArena.Clear(), 0x808080, uint.MaxValue);
        NativeBattleEffectArena context = NativeBattleModelContext44.CopyContext(prepared.Temporary92, 0x30, NativeBattleEffectArena.CreateUnknown(44), 0);
        NativeBattleEffectMemory before = Memory();
        NativeBattleModelContext44Step actual = NativeBattleForeignModelDrawing.DrawContext44(before, context, foreign, prepared.Matrix, NativeBattleEffectArena.CreateUnknown(24), projection, Constants, false, new(260, 0x693170, 3, 1, 0));
        Assert.NotEmpty(actual.Batch.Attempts);
        Assert.All(actual.Batch.Attempts, attempt => Assert.Same(foreign.Faces[0].Surface, attempt.Primitive!.PilotGeometry!.ForeignSurface));
        Assert.Equal(new Vector3(-200, -200, 0), actual.Batch.Attempts[0].Primitive!.PilotGeometry!.A);
        Assert.Equal(NativeBattleTestSnapshots.Json(before.ScriptedColorRegisters), NativeBattleTestSnapshots.Json(actual.Memory.ScriptedColorRegisters));
        Assert.Throws<NotSupportedException>(() => actual.Context44.Read(0x24, 2));
    }

    [Fact]
    public void Masked_bounds_and_body_read_only_the_directory_and_keep_empty_sentinels()
    {
        byte[] directory = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(directory, 1);
        BinaryPrimitives.WriteInt32LittleEndian(directory.AsSpan(4), 123456);
        byte[] Read(int offset, int count) => directory.AsSpan(offset, count).ToArray();
        var bounds = NativeBattleShockwavePulsar260BoundsCapture.Capture693000(Memory(), NativeBattleEffectArena.Clear().MarkUnknown(0, 24), [], Read, 0);
        Assert.Equal((short)0, bounds.Maximum);
        Assert.Equal((short)0, bounds.Minimum);
        Assert.Equal(0x10000u, Dword(bounds.Temporary24, 0));
        Assert.Equal(0xFFFF0000u, Dword(bounds.Temporary24, 4));
        NativeBattleModelContext44Step body = Draw(directory, Context(0));
        Assert.Empty(body.Batch.Attempts);
        Assert.Equal(0, body.Batch.AllocationBytes);
    }

    [Fact]
    public void Plain_body_preserves_unknown_depth_and_CPU_projection_does_not_change_GTE_data()
    {
        var actual = Draw(Geometry(false), Context(1));
        Assert.Single(actual.Batch.Attempts);
        Assert.Throws<NotSupportedException>(() => actual.Context44.Read(0x24, 4));
        Assert.Equal(NativeBattleTestSnapshots.Json(Memory().ScriptedColorRegisters), NativeBattleTestSnapshots.Json(actual.Memory.ScriptedColorRegisters));
        Assert.Equal(Memory().GeometryRegisters!.Sxy, actual.Memory.GeometryRegisters!.Sxy);
        Assert.Equal(new int?[] { 0, 0, 2000 }, actual.Memory.GeometryRegisters.Translation);
        Assert.Equal(0x1939C30u + 52, Dword(actual.Context44, 0));
    }

    [Fact]
    public void Colored_body_requires_current_depth_and_cold_JSON_next_retains_its_high_word_mask()
    {
        Assert.Throws<NotSupportedException>(() => Draw(Geometry(true), Context(1)));
        NativeBattleEffectArena context = Context(1).Write(0x24, BitConverter.GetBytes((short)1024));
        var first = Draw(Geometry(true), context);
        Assert.Null(first.Memory.ScriptedColorRegisters!.Ir0);
        var restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleModelContext44Step>(NativeBattleTestSnapshots.Json(first))!;
        var next = Draw(Geometry(true), first.Context44, first.Memory, first.ProjectedScratch);
        var resumed = Draw(Geometry(true), restored.Context44, restored.Memory, restored.ProjectedScratch);
        Assert.Equal(NativeBattleTestSnapshots.Json(next), NativeBattleTestSnapshots.Json(resumed));
    }

    private static NativeBattleModelContext44Step Draw(byte[] bytes, NativeBattleEffectArena context, NativeBattleEffectMemory? memory = null, NativeBattleEffectArena? scratch = null) => NativeBattleModelContext44.Draw(memory ?? Memory(), context, [NativeBattlePackedMatrix32.FromMatrix(Identity with { Z = 2000 }, null)], (offset, count) => bytes.AsSpan(offset, count).ToArray(), 0x1939C30, scratch ?? NativeBattleEffectArena.Clear().MarkUnknown(0, 64), 64, Projection, Constants, false, 17, 4);
    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown() with { Sxy = [1, 2, 3] }, ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };
    private static NativeBattleEffectArena Context(uint mask) => NativeBattleEffectArena.Clear().MarkUnknown(0, 44).Write(0x14, BitConverter.GetBytes((short)0)).Write(0x16, BitConverter.GetBytes((short)0)).Write(0x18, BitConverter.GetBytes((short)320)).Write(0x1A, BitConverter.GetBytes((short)216)).Write(0x1C, BitConverter.GetBytes(0x808080u)).Write(0x20, BitConverter.GetBytes(mask));
    private static NativeBattleEffectArena Vector(NativeBattleVector point) => NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes(point.X)).Write(2, BitConverter.GetBytes(point.Y)).Write(4, BitConverter.GetBytes(point.Z));
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static NativeBattleForeignModelGeometry ForeignGeometry()
    {
        ImmutableArray<NativeBattleVector> vertices = [new(-400, -400, 0), new(400, -400, 0), new(-400, 400, 0), new(400, 400, 0)];
        NativeBattleForeignSurface surface = new(new Material(), [Vector2.Zero, Vector2.UnitX, Vector2.UnitY], [Vector4.One, Vector4.One, Vector4.One]);
        return new(Identity, new(default, default, 0, new(new(-400, -400, 0), new(400, 400, 0))), [new(vertices[0], vertices[1], vertices[2], surface) { VertexIndices = [0, 1, 2] }, new(vertices[1], vertices[3], vertices[2], surface) { VertexIndices = [1, 3, 2] }]) { Vertices = vertices };
    }
    private static byte[] Geometry(bool colored)
    {
        byte[] bytes = new byte[colored ? 76 : 72];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 8);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 0);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 4);
        short[] points = [-400, -400, 0, 400, -400, 0, -400, 400, 0, 400, 400, 0];
        for (int index = 0; index < points.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(14 + index * 2), points[index]);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(40 + (colored ? 6 : 2)), 1);
        for (int corner = 0; corner < 4; corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(52 + corner * 2), (ushort)corner);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(60), 0x12340201);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(64), 0x00040603);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(68), 0x08070504);
        if (colored)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(72), 0x2C403020);
        }
        return bytes;
    }
}
