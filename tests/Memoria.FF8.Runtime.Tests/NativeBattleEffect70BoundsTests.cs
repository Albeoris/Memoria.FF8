using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect70BoundsTests
{
    [Fact]
    public void Bounds_keep_world_translation_and_publish_actual_RTPS_fifos_and_saturated_IR_y()
    {
        byte[] geometry = Geometry();
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770() with { Y = 40000, Z = 5000 };
        NativeBattleEffectMemory memory = Memory();
        NativeBattleEffect70BoundsStep step = NativeBattleEffect70BoundsCapture.Capture6DFD90(memory, Temporary(), [NativeBattlePackedMatrix32.FromMatrix(matrix, null)], (offset, length) => geometry.AsSpan(offset, length).ToArray(), 1, new(null, 256, 160, 108));
        Assert.Equal((short)32767, step.Maximum);
        Assert.Equal((short)7232, step.Minimum);
        Assert.Equal(7232, BinaryPrimitives.ReadInt32LittleEndian(step.Temporary24.Read(0, 4)));
        Assert.Equal(32767, BinaryPrimitives.ReadInt32LittleEndian(step.Temporary24.Read(4, 4)));
        Assert.Equal(7232, step.Memory.ScriptedColorRegisters!.Ir[1]);
        Assert.Equal(7232, step.Memory.ScriptedColorRegisters.Mac[1]);
        Assert.Equal((uint?)5000, step.Memory.GeometryRegisters!.Sz[3]);
        Assert.NotEqual(memory.GeometryRegisters!.Sxy, step.Memory.GeometryRegisters.Sxy);
        Assert.Throws<NotSupportedException>(() => step.Temporary24.Read(14, 2));
        Assert.Equal(new byte[] { 0xAA, 0xBB }, step.Temporary24.Read(18, 2));
    }

    [Fact]
    public void Masked_object_does_not_read_vertices_or_change_projection_registers()
    {
        byte[] geometry = Geometry();
        NativeBattleEffectMemory memory = Memory();
        NativeBattleEffect70BoundsStep step = NativeBattleEffect70BoundsCapture.Capture6DFD90(memory, Temporary(), [], (offset, length) =>
        {
            Assert.True(offset < 8);
            return geometry.AsSpan(offset, length).ToArray();
        }, 0, new(null, 256, 160, 108));
        Assert.Equal((short)0, step.Maximum);
        Assert.Equal((short)0, step.Minimum);
        Assert.Same(memory.GeometryRegisters, step.Memory.GeometryRegisters);
        Assert.Same(memory.ScriptedColorRegisters, step.Memory.ScriptedColorRegisters);
    }

    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };

    private static NativeBattleEffectArena Temporary() => NativeBattleEffectArena.Clear().MarkUnknown(0, 24).Write(18, [0xAA, 0xBB]);

    private static byte[] Geometry()
    {
        byte[] bytes = new byte[26];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 8);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10), 0);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(12), 2);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(16), 32767);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(22), -32768);
        return bytes;
    }
}
