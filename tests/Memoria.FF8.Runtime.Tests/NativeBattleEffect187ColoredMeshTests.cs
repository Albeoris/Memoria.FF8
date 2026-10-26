using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect187ColoredMeshTests
{
    [Fact]
    public void UniformContextColorOverridesOriginalMaterialAndKeepsNativeScratchCoordinates()
    {
        NativeBattleEffect187ColoredMesh108Step step = Draw(false, 2000);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(step.Batch.Attempts);
        Assert.NotNull(attempt.Primitive);
        Assert.Equal(40, step.Batch.AllocationBytes);
        Assert.Equal(0x2E2020A0u, BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan(4, 4)));
        Assert.Equal(0x2020A0u, Dword(step.Context108, 0x20));
        Assert.Equal(500u, Dword(step.Context108, 0x30));
        Assert.Equal(17 + (500 >> 3), attempt.Primitive!.Bucket);
        Assert.Throws<NotSupportedException>(() => step.Context108.Read(0x2C, 4));
        Assert.Equal(0x2E2020A0u, step.Memory.ScriptedColorRegisters!.Material);
    }

    [Fact]
    public void ZeroAreaRejectsBeforeFourthProjectionAndDepthConsumption()
    {
        NativeBattleEffect187ColoredMesh108Step step = Draw(true, 2000);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(step.Batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0, step.Batch.AllocationBytes);
        Assert.Equal(0u, Dword(step.Context108, 0x28));
        Assert.Null(step.Memory.GeometryRegisters!.Otz);
        Assert.Throws<NotSupportedException>(() => step.Context108.Read(0x30, 4));
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[4]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[8]);
    }

    [Fact]
    public void NearRejectionStillPublishesUniformPacketPrefixAndFlags()
    {
        NativeBattleEffect187ColoredMesh108Step step = Draw(false, 0);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(step.Batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0, step.Batch.AllocationBytes);
        Assert.NotEqual(0u, Dword(step.Context108, 0x34) & 0x60000);
        Assert.Throws<NotSupportedException>(() => step.Context108.Read(0x28, 4));
        Assert.Equal(0x2E2020A0u, BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan(4, 4)));
    }

    private static NativeBattleEffect187ColoredMesh108Step Draw(bool degenerate, int z)
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess();
        memory = memory with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), NativeBattleCuragaMatrices.Identity8DD770() with { Z = z }, true, true, null), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 108).Write(0, BitConverter.GetBytes(0xE41788u)).Write(8, new byte[4]).Write(12, BitConverter.GetBytes(0u)).Write(0x1C, BitConverter.GetBytes(0xF3u)).Write(0x20, BitConverter.GetBytes(0x2020A0u));
        return NativeBattleEffect187ColoredMesh108.Draw64C9A0(memory, Mesh(degenerate), new(null, 100, 160, 108), context, NativeBattleGteColorInterpolation.CodeConstants45F270);
    }

    private static NativeBattleStaticMeshResource Mesh(bool degenerate)
    {
        byte[] source = new byte[96];
        BinaryPrimitives.WriteInt32LittleEndian(source, 40);
        NativeBattleVector[] vertices = [new(-400, -400, 0), new(400, -400, 0), new(-400, 400, 0), new(400, 400, 0)];
        for (int index = 0; index < vertices.Length; index++)
        {
            NativeBattleVector point = degenerate && index < 3 ? vertices[0] : vertices[index];
            BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(8 + index * 8), point.X);
            BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(10 + index * 8), point.Y);
            BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(12 + index * 8), point.Z);
        }
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(52), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(source.AsSpan(56), 0x2C112233);
        for (int corner = 0; corner < 4; corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(60 + corner * 2), checked((ushort)(corner * 2)));
        }
        return new(0, 0xE41788, source.ToImmutableArray());
    }

    private static uint Dword(NativeBattleEffectArena source, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(source.Read(offset, 4));
}
