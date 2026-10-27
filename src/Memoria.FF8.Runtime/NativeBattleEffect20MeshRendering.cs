using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect20MeshPreparation(NativeBattleActionMatrix Matrix, NativeBattleEffectArena Context104, ImmutableArray<NativeBattleDrawStock15ParticleOperation> Operations);

public static class NativeBattleEffect20MeshRendering
{
    public static NativeBattleEffect20MeshPreparation Prepare(NativeBattleModulePoolTask task, NativeBattleActionMatrix camera, NativeBattleEffectArena retained104, ushort? cameraPadding18 = null)
    {
        if (task.Callback != 0x8A0990 || task.ReadDword(0x4C) != 0x1629E00 || (task.ReadByte(0x26) & 4) != 0)
        {
            throw new InvalidDataException("Effect mesh drawing requires its visible original geometry task.");
        }
        retained104.Validate();
        retained104.ValidateExtent(104);
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770();
        ImmutableArray<NativeBattleDrawStock15ParticleOperation>.Builder operations = ImmutableArray.CreateBuilder<NativeBattleDrawStock15ParticleOperation>();
        Rotate('Y', Signed(task, 0x46));
        Rotate('X', Signed(task, 0x44));
        Rotate('Z', Signed(task, 0x48));
        matrix = matrix with { X = Signed(task, 0x1C), Y = Signed(task, 0x1E), Z = Signed(task, 0x20) };
        matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, unchecked((int)task.ReadDword(0x30)), unchecked((int)task.ReadDword(0x34)), unchecked((int)task.ReadDword(0x38)));
        matrix = NativeBattleGfMatrices.Compose56C2F0(camera, matrix);
        operations.Add(new NativeBattleDrawStock15Matrix(matrix, true, true));
        NativeBattleEffectArena context = Dword(retained104, 0, task.ReadDword(0x4C));
        context = Dword(context, 8, task.ReadDword(0x40));
        context = Dword(context, 12, unchecked((uint)(int)Signed(task, 0x50)));
        context = Dword(context, 16, unchecked((uint)(int)Signed(task, 0x52)));
        context = Dword(context, 20, 0xF0);
        context = Word(context, 0x18, 0);
        foreach (int offset in new[] { 0x1A, 0x1C, 0x1E, 0x24, 0x26 })
        {
            context = Word(context, offset, 0);
        }
        foreach (int offset in new[] { 0x20, 0x22, 0x28, 0x2A })
        {
            context = Word(context, offset, 256);
        }
        return new(matrix, context, operations.ToImmutable());

        void Rotate(char axis, short angle)
        {
            operations.Add(new NativeBattleDrawStock15Rotate(matrix, axis, angle));
            matrix = axis switch
            {
                'Y' => NativeBattleCuragaMatrices.RotateY8DD8A0(matrix, angle),
                'X' => NativeBattleCuragaMatrices.RotateX8DD7E0(matrix, angle),
                _ => NativeBattleCuragaMatrices.RotateZ8DD960(matrix, angle)
            };
        }
    }

    private static short Signed(NativeBattleModulePoolTask task, int offset)
    {
        return unchecked((short)task.ReadWord(offset));
    }

    private static NativeBattleEffectArena Dword(NativeBattleEffectArena context, int offset, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return context.Write(offset, bytes);
    }

    private static NativeBattleEffectArena Word(NativeBattleEffectArena context, int offset, ushort value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return context.Write(offset, bytes);
    }
}
