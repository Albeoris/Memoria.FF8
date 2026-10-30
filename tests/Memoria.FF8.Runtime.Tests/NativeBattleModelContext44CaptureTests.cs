using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModelContext44CaptureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Optional_body_and_discrete_snapshot_capture_keep_two_object_sources_and_native_writes(bool screen)
    {
        NativeBattleModelContext44Step Run(bool capture)
        {
            byte[] one = Geometry(false);
            byte[] bytes = new byte[one.Length + 4];
            BinaryPrimitives.WriteInt32LittleEndian(bytes, 2);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 12);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 12);
            one.AsSpan(8).CopyTo(bytes.AsSpan(12));
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };
            NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 44).Write(0x14, new byte[4]).Write(0x18, BitConverter.GetBytes((short)320)).Write(0x1A, BitConverter.GetBytes((short)216)).Write(0x1C, BitConverter.GetBytes(0x808080u)).Write(0x20, BitConverter.GetBytes(3u));
            NativeBattleActionMatrix view = NativeBattleCuragaMatrices.Identity8DD770();
            return NativeBattleModelContext44.Draw(memory, context, [NativeBattlePackedMatrix32.FromMatrix(view with { Z = 2000 }, null)], (offset, length) => bytes.AsSpan(offset, length).ToArray(), 0x1939C30, NativeBattleEffectArena.Clear().MarkUnknown(0, 64), 64, new(view, 256, 160, 108), NativeBattleGteColorInterpolation.CodeConstants45F270, false, 17, 4, pilotIdentity: capture ? new(166, 1, 2, 3, 4) { ResourceGeneration = 0x1939C30 } : null, pilotScreenSpace: screen, pilotContinuous: !screen);
        }
        NativeBattleModelContext44Step native = Run(false);
        string baseline = NativeBattleTestSnapshots.Json(native);
        NativeBattleModelContext44Step captured = Run(true);
        Assert.Equal(baseline, NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(2, captured.Batch.Attempts.Length);
        Assert.Equal(2, captured.Batch.Attempts.Select(attempt => attempt.Primitive!.PilotGeometry!.Identity).Distinct().Count());
        foreach (NativeBattleGfGeometryAttempt attempt in captured.Batch.Attempts)
        {
            NativeBattleGeometryPrimitive primitive = attempt.Primitive!;
            NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(primitive.PilotGeometry);
            Assert.Equal(screen, geometry.ScreenSpace);
            Assert.Equal(!screen, geometry.Continuous);
            Assert.Equal(0x1939C30L, geometry.Identity.ResourceGeneration);
            if (screen)
            {
                Assert.Equal(unchecked((short)primitive.Words[2]), geometry.A.X);
                Assert.Equal(unchecked((short)(primitive.Words[2] >> 16)), geometry.A.Y);
            }
            else
            {
                Assert.Equal(new Vector3(-400, -400, 2000), geometry.A);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Captured218_model_and_discrete_stage_keep_two_original_objects_and_all_native_writes(bool screen)
    {
        NativeBattleModelContext44Step Run(bool capture)
        {
            byte[] one = Geometry(false);
            byte[] bytes = new byte[one.Length + 4];
            BinaryPrimitives.WriteInt32LittleEndian(bytes, 2);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 12);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8), 12);
            one.AsSpan(8).CopyTo(bytes.AsSpan(12));
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };
            NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 44).Write(0x14, new byte[4]).Write(0x18, BitConverter.GetBytes((short)320)).Write(0x1A, BitConverter.GetBytes((short)216)).Write(0x1C, BitConverter.GetBytes(0x808080u)).Write(0x20, BitConverter.GetBytes(3u));
            NativeBattleActionMatrix view = NativeBattleCuragaMatrices.Identity8DD770();
            return NativeBattleModelContext44.Draw(memory, context, [NativeBattlePackedMatrix32.FromMatrix(view with { Z = 2000 }, null)], (offset, length) => bytes.AsSpan(offset, length).ToArray(), 0x1939C30, NativeBattleEffectArena.Clear().MarkUnknown(0, 64), 64, new(view, 256, 160, 108), NativeBattleGteColorInterpolation.CodeConstants45F270, false, 17, 4, pilotIdentity: capture ? new(218, 1, 2, 3, 4) { ResourceGeneration = 0x1939C30 } : null, pilotScreenSpace: false, pilotContinuous: !screen);
        }
        NativeBattleModelContext44Step native = Run(false);
        string baseline = NativeBattleTestSnapshots.Json(native);
        NativeBattleModelContext44Step captured = Run(true);
        Assert.Equal(baseline, NativeBattleTestSnapshots.Json(captured));
        Assert.Equal(2, captured.Batch.Attempts.Length);
        Assert.Equal(2, captured.Batch.Attempts.Select(attempt => attempt.Primitive!.PilotGeometry!.Identity).Distinct().Count());
        foreach (NativeBattleGfGeometryAttempt attempt in captured.Batch.Attempts)
        {
            NativeBattleGeometryPrimitive primitive = attempt.Primitive!;
            NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(primitive.PilotGeometry);
            Assert.False(geometry.ScreenSpace);
            Assert.Equal(!screen, geometry.Continuous);
            Assert.Equal(0x1939C30L, geometry.Identity.ResourceGeneration);
            Assert.Equal((ushort)218, geometry.Identity.Effect);
            Assert.Equal(new Vector3(-400, -400, 2000), geometry.A);
        }
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
