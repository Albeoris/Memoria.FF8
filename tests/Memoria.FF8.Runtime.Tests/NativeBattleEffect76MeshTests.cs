using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect76Lower;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect76MeshTests
{
    [Theory]
    [InlineData(0x33)]
    [InlineData(0xF3)]
    public void Captured_custom_quads_preserve_native_materials_knownness_registers_and_all_eight_sources(int flags)
    {
        NativeBattleActionMatrix view = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleCameraProjection projection = new(view, 100, 160, 108);
        NativeBattleEffect76MeshStep native = NativeBattleEffect76Meshes.DrawLoaded(Memory(2000), Mesh(false), projection, Context(flags), NativeBattleGteColorInterpolation.CodeConstants45F270);
        string baseline = NativeBattleTestSnapshots.Json(native);
        NativeBattleEffect76MeshStep captured = NativeBattleEffect76Meshes.DrawLoaded(Memory(2000), Mesh(false), projection, Context(flags), NativeBattleGteColorInterpolation.CodeConstants45F270, new(76, 0x24C3F20, 3, 42, 0));
        Assert.Equal(baseline, NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(8, captured.Batch.Attempts.Select(attempt => attempt.Primitive!.PilotGeometry!.Identity).Distinct().Count());
        Assert.All(captured.Batch.Attempts, attempt =>
        {
            NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(attempt.Primitive!.PilotGeometry);
            Assert.Equal(4, geometry.CornerCount);
            Assert.Equal(new System.Numerics.Vector4(2000, 2000, 2000, 2000), geometry.SourceDepths);
            Assert.True(geometry.UseSourceDepths);
            Assert.Equal(42, geometry.Identity.Generation);
        });
        NativeBattleEffect76MeshStep rejected = NativeBattleEffect76Meshes.DrawLoaded(Memory(2000), Mesh(true), projection, Context(flags), NativeBattleGteColorInterpolation.CodeConstants45F270, new(76, 1, 2, 3, 4));
        Assert.All(rejected.Batch.Attempts, attempt => Assert.Null(attempt.Primitive));
    }

    [Theory]
    [InlineData(0x33)]
    [InlineData(0xF3)]
    public void Accepted_quads_publish_inline_draw_mode_current_AVSZ_and_fade_only_when_selected(int flags)
    {
        NativeBattleStaticMeshResource mesh = Mesh(false);
        NativeBattleEffectArena context = Context(flags);
        NativeBattleEffect76MeshStep result = NativeBattleEffect76Meshes.DrawLoaded(Memory(2000), mesh, new(null, 100, 160, 108), context, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(352, result.Batch.AllocationBytes);
        Assert.Equal(8, result.Batch.Attempts.Length);
        for (int index = 0; index < 8; index++)
        {
            NativeBattleGfGeometryAttempt attempt = result.Batch.Attempts[index];
            Assert.Equal(index * 44, attempt.ByteOffsetRelative);
            Assert.NotNull(attempt.Primitive);
            Assert.Equal(0xE1000220u, attempt.Primitive!.Words[^1]);
            Assert.Equal(new int[] { 2000, 2000, 2000, 2000 }, attempt.Primitive.VertexPayload4);
            Assert.Equal(17 + ((int)result.Memory.GeometryRegisters!.Otz!.Value >> 2), attempt.Primitive.Bucket);
        }
        Assert.Equal(result.Memory.GeometryRegisters!.Otz!.Value, Dword(result.Context92, 0x2C));
        Assert.Equal(0xE1000220u, Dword(result.Context92, 0x58));
        if (flags == 0x33)
        {
            Assert.DoesNotContain(true, result.Context92.ReadSnapshot(12, 4).Known);
            Assert.Equal(0x2E808080u, result.Batch.Attempts[0].Primitive!.Words[1]);
        }
        else
        {
            Assert.Equal(2048u, result.Memory.ScriptedColorRegisters!.Ir0);
            Assert.Equal(result.Memory.ScriptedColorRegisters.Rgb[2], result.Batch.Attempts[^1].Primitive!.Words[1]);
        }
    }

    [Fact]
    public void Zero_area_rejects_before_invalid_fourth_vertex_and_preserves_retained_depth()
    {
        NativeBattleEffect76MeshStep result = NativeBattleEffect76Meshes.DrawLoaded(Memory(2000), Mesh(true), new(null, 100, 160, 108), Context(0x33), NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.All(result.Batch.Attempts, attempt =>
        {
            Assert.Null(attempt.Primitive);
            Assert.Equal(40, attempt.Data.Length);
            Assert.Equal(0x0A000000u, BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan()));
            Assert.Contains(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask);
        });
        Assert.Equal(0u, Dword(result.Context92, 0x24));
        Assert.Equal(0xA55AA55Au, Dword(result.Context92, 0x2C));
    }

    [Fact]
    public void Near_rejection_retains_unreached_area_and_depth_and_current_projection_flags()
    {
        NativeBattleEffect76MeshStep result = NativeBattleEffect76Meshes.DrawLoaded(Memory(1), Mesh(false), new(null, 100, 160, 108), Context(0x33), NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(0, result.Batch.AllocationBytes);
        Assert.All(result.Batch.Attempts, attempt => Assert.Null(attempt.Primitive));
        Assert.Equal(0xDEADBEEFu, Dword(result.Context92, 0x24));
        Assert.Equal(0xA55AA55Au, Dword(result.Context92, 0x2C));
        Assert.NotEqual(0u, Dword(result.Context92, 0x30) & 0x60000);
    }

    private static NativeBattleEffectArena Context(int flags)
    {
        NativeBattleEffectArena context = Long(Long(Long(Long(Long(NativeBattleEffectArena.Clear(), 0, 0xDE2470), 8, 0), 0x1C, flags), 0x24, unchecked((int)0xDEADBEEF)), 0x2C, unchecked((int)0xA55AA55A));
        return flags == 0x33 ? context.MarkUnknown(12, 4) : Long(context, 12, 2048);
    }

    private static NativeBattleEffectMemory Memory(int z)
    {
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, z, 4096);
        return NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), matrix, true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
    }

    private static NativeBattleStaticMeshResource Mesh(bool degenerate)
    {
        byte[] bytes = new byte[376];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 152);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 18);
        short[] coordinates = [-100, -100, 0, 17, 100, -100, 0, 18, -100, 100, 0, 19, 100, 100, 0, 20];
        for (int index = 0; index < coordinates.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + index * 2), coordinates[index]);
        }
        int cursor = 152;
        for (int group = 0; group < 8; group++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(cursor), group == 3 ? 8 : 0);
            cursor += 4;
            if (group != 3)
            {
                continue;
            }
            for (int face = 0; face < 8; face++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor), 0x2C808080);
                for (int corner = 0; corner < 4; corner++)
                {
                    ushort vertex = degenerate ? corner == 3 ? ushort.MaxValue : (ushort)0 : (ushort)(corner * 2);
                    BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(cursor + 4 + corner * 2), vertex);
                }
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor + 12), 0x12340201);
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor + 16), 0x00400603);
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(cursor + 20), 0x08070A09);
                cursor += 24;
            }
        }
        return new(0, 0xDE2470, bytes.ToImmutableArray());
    }
}
