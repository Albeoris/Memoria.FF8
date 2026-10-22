using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffectOwnedModelCameraStep(NativeBattleEffectMemory Memory, NativeBattleEffectArena Scratch);

public static partial class NativeBattleEffectOwnedModelRendering
{
    public static NativeBattleEffectOwnedModelCameraStep PrepareCamera84(NativeBattleEffectMemory memory, NativeBattleEffectArena scratch, NativeBattlePackedMatrix32 world, NativeBattlePackedMatrix32 camera)
    {
        world.Validate();
        camera.Validate();
        NativeBattleActionMatrix worldMatrix = world.ReadMatrix();
        NativeBattleActionMatrix view = camera.ReadMatrix();
        scratch = StoreMatrix(scratch, 0, world);
        NativeBattleGeometryRegisterStep registers = Registers(memory);
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, view, true, false, Padding(camera)) };
        for (int column = 0; column < 3; column++)
        {
            registers = NativeBattleGeometry.LoadIrColumn(registers, unchecked((ushort)worldMatrix.Rotation[column]), unchecked((ushort)worldMatrix.Rotation[column + 3]), unchecked((ushort)worldMatrix.Rotation[column + 6]));
            registers = NativeBattleGeometry.Transform(registers, translation: false, irInput: true);
            for (int row = 0; row < 3; row++)
            {
                scratch = Word(scratch, 0x20 + row * 6 + column * 2, unchecked((ushort)RequireCameraOutput(registers.Colors.Ir[row])));
            }
        }
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, view, false, true, Padding(camera)) };
        short z = unchecked((short)worldMatrix.Z);
        registers = NativeBattleGeometry.LoadVector(registers, 0, new(unchecked((short)worldMatrix.X), unchecked((short)worldMatrix.Y), z), z < 0 ? (short)-1 : (short)0);
        registers = NativeBattleGeometry.Transform(registers);
        for (int row = 0; row < 3; row++)
        {
            scratch = Dword(scratch, 0x34 + row * 4, unchecked((uint)RequireCameraOutput(registers.Colors.Mac[row])));
        }
        return new(memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors }, scratch);
    }

    private static short? Padding(NativeBattlePackedMatrix32 matrix)
    {
        return matrix.Known[18] && matrix.Known[19] ? BinaryPrimitives.ReadInt16LittleEndian(matrix.Bytes.AsSpan().Slice(18, 2)) : null;
    }

    private static int RequireCameraOutput(int? value)
    {
        return value ?? throw new NotSupportedException("Effect-owned model camera preparation requires its reached arithmetic output.");
    }
}
