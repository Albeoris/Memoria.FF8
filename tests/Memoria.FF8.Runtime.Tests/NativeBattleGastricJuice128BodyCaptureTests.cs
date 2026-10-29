using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGastricJuice128BodyCaptureTests
{
    [Theory]
    [InlineData((short)0)]
    [InlineData((short)1024)]
    public void Capture_retains_post_deformation_WORD_vertices_before_native_scratch_overwrite(short fraction)
    {
        NativeBattleDeformedBodyStep native = Draw(Geometry(true), Context(fraction), Memory(255), 2000);
        string baseline = NativeBattleTestSnapshots.Json(native);
        NativeBattlePilotVisualIdentity identity = new(128, 0x2521250, 0, 11, 0) { ResourceGeneration = GeometryAddress };
        NativeBattleDeformedBodyStep captured = Draw(Geometry(true), Context(fraction), Memory(255), 2000, pilotIdentity: identity);
        Assert.Equal(baseline, NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(2, captured.Batch.Attempts.Length);
        foreach (NativeBattleGfGeometryAttempt attempt in captured.Batch.Attempts)
        {
            NativeBattlePilotVisualGeometry geometry = attempt.Primitive!.PilotGeometry!;
            Assert.Equal(new Vector3(-400, -400 + (255 * fraction >> 10), 2000), geometry.A);
            Assert.Equal(identity.Generation, geometry.Identity.Generation);
            Assert.Equal(identity.ResourceGeneration, geometry.Identity.ResourceGeneration);
            Assert.True(geometry.UseSourceDepths);
            Assert.Equal(500f, geometry.SourceDepths!.Value.X);
        }
        Assert.Equal(2, captured.Batch.Attempts.Select(a => a.Primitive!.PilotGeometry!.Identity).Distinct().Count());
        Assert.NotEqual(-400, unchecked((short)U32(captured.VertexScratch, 0)));
    }

    [Fact]
    public void Two_actual_objects_keep_their_own_receipts_when_native_projection_scratch_is_reused()
    {
        byte[] original = Geometry(true);
        byte[] source = new byte[172];
        BinaryPrimitives.WriteInt32LittleEndian(source, 2);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(4), 12);
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(8), 92);
        original.AsSpan(8).CopyTo(source.AsSpan(12));
        original.AsSpan(8).CopyTo(source.AsSpan(92));
        NativeBattlePilotVisualIdentity identity = new(128, 1, 0, 2, 0);
        NativeBattleDeformedBodyStep captured = Draw(source, Context(1024).Write(0x4C, BitConverter.GetBytes(3u)), Memory(255), 2000, pilotIdentity: identity);
        Assert.Equal(4, captured.Batch.Attempts.Length);
        Assert.Equal(4, captured.Batch.Attempts.Select(a => a.Primitive!.PilotGeometry!.Identity).Distinct().Count());
        Assert.All(captured.Batch.Attempts, a => Assert.Equal(new Vector3(-400, -145, 2000), a.Primitive!.PilotGeometry!.A));
    }

    private const uint GeometryAddress = 0x1000000;
    private const uint ScratchAddress = 0x196B000;
    private const int CacheOffset = 0x3F08;
    private const uint CacheAddress = NativeBattleEffectNodeMemory.ArenaAddress + CacheOffset;
    private static NativeBattleCameraProjection Projection => new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108);
    private static NativeBattlePackedMatrix32 Camera() => NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), null);
    private static NativeBattleEffectMemory Memory(byte coefficient) => NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().Write(CacheOffset, Enumerable.Repeat(coefficient, 8).ToArray()), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
    private static NativeBattleDeformedBodyStep Draw(byte[] geometry, NativeBattleEffectArena temporary, NativeBattleEffectMemory memory, int boneZ, int length = 32, NativeBattleEffectArena? scratch = null, NativeBattlePilotVisualIdentity? pilotIdentity = null) => NativeBattleDeformedBodyDrawing.DrawBody(memory, temporary, scratch ?? NativeBattleEffectArena.Clear().MarkUnknown(0, 40), (offset, count) => geometry.AsSpan(offset, count).ToArray(), GeometryAddress, [NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770() with { Z = boneZ }, null)], Camera(), Projection, memory.ScriptedGraphics!, ScratchAddress, length, CacheAddress, 2048, pilotIdentity: pilotIdentity);
    private static uint U32(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static short[] Words(byte[] bytes) => Enumerable.Range(0, bytes.Length / 2).Select(index => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(index * 2))).ToArray();
    private static NativeBattleEffectArena Context(short blend)
    {
        return NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 132).ToArray()).Write(0x24, BitConverter.GetBytes(ScratchAddress)).Write(0x2C, BitConverter.GetBytes(CacheAddress)).Write(0x40, new byte[4]).Write(0x44, BitConverter.GetBytes((short)320)).Write(0x46, BitConverter.GetBytes((short)216)).Write(0x48, BitConverter.GetBytes(0x00112233u)).Write(0x4C, BitConverter.GetBytes(1u)).Write(0x80, BitConverter.GetBytes(blend));
    }
    private static byte[] Geometry(bool quad)
    {
        byte[] data = new byte[quad ? 88 : 68];
        BinaryPrimitives.WriteInt32LittleEndian(data, 1);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4), 8);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(8), 1);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(12), 4);
        short[] xy = [-400, -400, 400, -400, -400, 400, 400, 400];
        for (int index = 0; index < 4; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(14 + index * 6), xy[index * 2]);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(16 + index * 6), xy[index * 2 + 1]);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(40), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(42), quad ? (ushort)1 : (ushort)0);
        Face(52, false);
        if (quad)
        {
            Face(68, true);
        }
        return data;

        void Face(int offset, bool four)
        {
            for (int corner = 0; corner < (four ? 4 : 3); corner++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset + corner * 2), (ushort)(0xF000 | corner));
            }
            if (!four)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset + 6), 0x8123);
            }
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset + 8), 0x12345678);
            BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset + 12), 0xFEDCBA98);
            if (four)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset + 16), 0xF3456789);
            }
        }
    }
}
