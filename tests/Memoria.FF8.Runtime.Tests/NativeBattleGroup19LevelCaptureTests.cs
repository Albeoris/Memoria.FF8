using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGroup19LevelCaptureTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Loaded_capture_preserves_native_registers_masks_windows_depth_and_both_groups(bool glyph, bool bias)
    {
        var mesh = Mesh(glyph);
        List<int> nativeReads = [];
        List<int> capturedReads = [];
        var native = Run(glyph, bias, mesh, nativeReads, false, null);
        string baseline = NativeBattleTestSnapshots.Json(native);
        var identity = new NativeBattlePilotVisualIdentity(glyph && !bias ? (ushort)271 : (ushort)272, 0x2500000, 2, 123, 44) { ResourceGeneration = mesh.Address };
        var captured = Run(glyph, bias, mesh, capturedReads, false, identity);
        Assert.Equal(baseline, NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(new[] { 0, 8, 16, 24, 0, 8, 16, 24 }, nativeReads);
        Assert.Equal(nativeReads, capturedReads);
        Assert.All(native.Batch.Attempts, attempt => Assert.Null(attempt.Primitive?.PilotGeometry));
        var geometries = captured.Batch.Attempts.Where(attempt => attempt.Primitive?.PilotGeometry is not null).Select(attempt => attempt.Primitive!.PilotGeometry!).ToArray();
        Assert.Equal(2, geometries.Length);
        var textureWindows = captured.Batch.Attempts.Where(attempt => attempt.Primitive is { Words.Length: > 1 } primitive && primitive.Words[1] >> 24 == 0xE2).ToArray();
        Assert.Equal(glyph ? 4 : 0, textureWindows.Length);
        Assert.All(textureWindows, attempt => Assert.Null(attempt.Primitive!.PilotGeometry));
        Assert.NotEqual(geometries[0].Identity, geometries[1].Identity);
        foreach (var geometry in geometries)
        {
            Assert.Equal(identity.Effect, geometry.Identity.Effect);
            Assert.Equal(identity.Generation, geometry.Identity.Generation);
            Assert.Equal(identity.ResourceGeneration, geometry.Identity.ResourceGeneration);
            Assert.Equal(new Vector3(-80, -80, 3000), geometry.A);
            Assert.Equal(4, geometry.CornerCount);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Rejected_first_triple_defers_invalid_fourth_in_native_and_captured_paths(bool glyph)
    {
        var mesh = Mesh(glyph);
        List<int> nativeReads = [];
        List<int> capturedReads = [];
        var native = Run(glyph, true, mesh, nativeReads, true, null);
        string baseline = NativeBattleTestSnapshots.Json(native);
        var captured = Run(glyph, true, mesh, capturedReads, true, new(272, 1, 2, 3, 4));
        Assert.Equal(baseline, NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(new[] { 0, 8, 16, 0, 8, 16 }, nativeReads);
        Assert.Equal(nativeReads, capturedReads);
        Assert.Equal(0, captured.Batch.AllocationBytes);
        Assert.All(captured.Batch.Attempts, attempt => Assert.Null(attempt.Primitive));
    }

    private static NativeBattleLevelGlyphPacketsStep Run(bool glyph, bool bias, NativeBattleStaticMeshResource mesh, List<int> reads, bool reject, NativeBattlePilotVisualIdentity? identity)
    {
        var view = NativeBattleCuragaMatrices.Identity8DD770() with { X = 100, Y = 200, Z = 300 };
        var loaded = NativeBattleCuragaMatrices.Identity8DD770() with { X = 120, Y = 220, Z = 3300 };
        var memory = NativeBattleEffectMemory.FromColdProcess() with
        {
            GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), loaded, true, true, null),
            ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(),
            ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(),
            PrimitiveProcess = NativeBattlePrimitiveProcess.FromColdProcess()
        };
        var projection = new NativeBattleCameraProjection(view, 216, 160, 108);
        var context = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(mesh.Address)).Write(4, BitConverter.GetBytes(0x2000000u)).Write(0x18, BitConverter.GetBytes(-128)).Write(0x28, BitConverter.GetBytes((ushort)256)).Write(0x2A, BitConverter.GetBytes((ushort)256)).Write(0x30, BitConverter.GetBytes((ushort)16)).Write(0x32, BitConverter.GetBytes((ushort)64));
        NativeBattleEffectArena Read(int offset)
        {
            reads.Add(offset);
            if (reject && offset == 24)
            {
                throw new InvalidDataException("Unreached fourth vertex.");
            }
            (short x, short y) = offset switch
            {
                0 => ((short)-100, (short)-100),
                8 => ((short)-100, (short)100),
                16 => ((short)100, (short)-100),
                _ => ((short)100, (short)100)
            };
            byte[] bytes = new byte[8];
            BinaryPrimitives.WriteInt16LittleEndian(bytes, x);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), y);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), reject ? (short)-3300 : (short)0);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(6), 123);
            return NativeBattleEffectArena.Clear().Write(0, bytes);
        }
        if (glyph)
        {
            return NativeBattleLevelGlyphPackets.DrawLoaded(memory, mesh, projection, context, NativeBattleGteColorInterpolation.CodeConstants45F270, 17, 2, Read, bias, identity);
        }
        var result = NativeBattleLevelPlainPackets.DrawLoaded(memory, mesh, projection, context, NativeBattleGteColorInterpolation.CodeConstants45F270, 17, 2, Read, identity);
        return new(result.Memory, result.Context88, result.Batch);
    }

    private static NativeBattleStaticMeshResource Mesh(bool glyph)
    {
        int second = 20 + 24 + (glyph ? 12 : 4);
        int stride = glyph ? 36 : 24;
        byte[] bytes = new byte[second + 4 + stride];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 4);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16), 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(second), 1);
        for (int group = 0; group < 2; group++)
        {
            int face = group == 0 ? 20 : second + 4;
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(face), group == 0 ? 0x2C332211u : glyph ? 0x3C332211u : 0x38332211u);
            for (int corner = 0; corner < 4; corner++)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(face + 4 + corner * 2), (ushort)(corner * 2));
            }
        }
        return new(0, 0x1000000, bytes.ToImmutableArray());
    }
}
