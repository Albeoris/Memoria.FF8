using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleActContext92PreparationStep(NativeBattleEffectMemory Memory, NativeBattleFileStaging? Staging, NativeBattleEffectArena Context92);

public static class NativeBattleActContext92Preparation
{
    public static NativeBattleActContext92PreparationStep Prepare(NativeBattleEffectMemory memory, NativeBattleFileStaging? staging, NativeBattleModulePoolTask task, NativeBattlePackedMatrix32 camera, NativeBattleEffectArena retained92, NativeBattleEffectArena compositionTemporary32, uint morphAddress)
    {
        NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleEffectArena context = WriteMatrix(retained92, identity);
        foreach ((char axis, int offset) in new[] { ('Y', 0x62), ('X', 0x60), ('Z', 0x64) })
        {
            short angle = unchecked((short)task.ReadWord(offset));
            if (angle == 0)
            {
                continue;
            }
            (byte[] bytes, bool[] known) = context.ReadSnapshot(0, 32);
            NativeBattleStatefulMatrixRotationStep rotated = NativeBattleStatefulMatrixRotation.Rotate(memory, staging ?? throw new NotSupportedException("ACT rotation requires its physical temporary memory."), new(bytes.ToImmutableArray(), known.ToImmutableArray()), axis, angle);
            memory = rotated.Memory;
            staging = rotated.Staging;
            context = WriteMatrix(context, rotated.Matrix.ReadMatrix());
        }
        (byte[] localBytes, bool[] localKnown) = context.ReadSnapshot(0, 32);
        NativeBattleActionMatrix local = new NativeBattlePackedMatrix32(localBytes.ToImmutableArray(), localKnown.ToImmutableArray()).ReadMatrix();
        local = NativeBattlePoseMatrices.Scale56BEF0(local, unchecked((int)task.ReadDword(0x50)), unchecked((int)task.ReadDword(0x54)), unchecked((int)task.ReadDword(0x58))) with { X = unchecked((short)task.ReadWord(0x1C)), Y = unchecked((short)task.ReadWord(0x1E)), Z = unchecked((short)task.ReadWord(0x20)) };
        NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(camera.ReadMatrix(), local);
        compositionTemporary32 = WriteMatrix(compositionTemporary32, composed);
        context = NativeBattleEffectNodeContextMemory.Copy(compositionTemporary32, 0, context, 0, 32);
        context = Dword(context, 0x38, task.ReadDword(0x70));
        context = Dword(context, 0x44, task.ReadDword(0x7C));
        context = Dword(context, 0x48, morphAddress);
        foreach ((int destination, int source) in new[] { (0x4C, 0x8C), (0x4E, 0x8E), (0x50, 0x90), (0x52, 0x92), (0x54, 0x86), (0x56, 0x80), (0x58, 0x88) })
        {
            context = Word(context, destination, task.ReadWord(source));
        }
        return new(memory, staging, context);
    }

    private static NativeBattleEffectArena WriteMatrix(NativeBattleEffectArena arena, NativeBattleActionMatrix matrix)
    {
        for (int lane = 0; lane < 9; lane++)
        {
            arena = Word(arena, lane * 2, unchecked((ushort)matrix.Rotation[lane]));
        }
        return Dword(Dword(Dword(arena, 20, unchecked((uint)matrix.X)), 24, unchecked((uint)matrix.Y)), 28, unchecked((uint)matrix.Z));
    }

    private static NativeBattleEffectArena Word(NativeBattleEffectArena arena, int offset, ushort value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return arena.Write(offset, bytes);
    }

    private static NativeBattleEffectArena Dword(NativeBattleEffectArena arena, int offset, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return arena.Write(offset, bytes);
    }

}
