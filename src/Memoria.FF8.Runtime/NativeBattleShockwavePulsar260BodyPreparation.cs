using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleShockwavePulsar260BodyPreparationStep(NativeBattleEffectArena Temporary92, NativeBattleActionMatrix Matrix);

public static class NativeBattleShockwavePulsar260BodyPreparation
{
    public static NativeBattleShockwavePulsar260BodyPreparationStep Prepare693170(NativeBattleEffectArena retained92, NativeBattleActionMatrix modelWorld, NativeBattlePackedMatrix32 camera, NativeBattleEffectArena rotation8, NativeBattleEffectArena offset8, short scale, uint projectedScratch, NativeBattleEffectArena primitiveFlags4, uint modelColor, uint objectMask)
    {
        retained92.Validate();
        retained92.ValidateExtent(92);
        NativeBattleVector rotation = Vector(rotation8);
        NativeBattleVector offset = Vector(offset8);
        NativeBattleActionMatrix local = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CE30(rotation), scale, scale, scale) with { X = offset.X, Y = offset.Y, Z = offset.Z };
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(camera.ReadMatrix(), NativeBattleGfMatrices.Compose56C2F0(modelWorld, local));
        (int x, int y, int z) = NativeBattleGfMatrices.Rotate56C3D0(matrix, offset);
        matrix = matrix with { X = unchecked(matrix.X - x), Y = unchecked(matrix.Y - y), Z = unchecked(matrix.Z - z) };
        NativeBattleEffectArena temporary = retained92;
        for (int index = 0; index < 9; index++)
        {
            temporary = temporary.Write(index * 2, BitConverter.GetBytes(matrix.Rotation[index]));
        }
        temporary = temporary.MarkUnknown(18, 2);
        temporary = Dword(Dword(Dword(temporary, 20, unchecked((uint)matrix.X)), 24, unchecked((uint)matrix.Y)), 28, unchecked((uint)matrix.Z));
        temporary = Dword(Dword(Dword(temporary, 32, unchecked((uint)x)), 36, unchecked((uint)y)), 40, unchecked((uint)z));
        temporary = Dword(temporary, 0x34, projectedScratch);
        temporary = NativeBattleEffectNodeContextMemory.Copy(primitiveFlags4, 0, temporary, 0x40, 4);
        temporary = Word(Word(Word(Word(temporary, 0x44, 0), 0x46, 0), 0x48, 320), 0x4A, 216);
        temporary = Dword(Dword(Dword(temporary, 0x4C, modelColor), 0x50, objectMask), 0x58, 0);
        return new(temporary, matrix);
    }

    private static NativeBattleVector Vector(NativeBattleEffectArena source)
    {
        byte[] xyz = source.Read(0, 6);
        return new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4)));
    }
    private static NativeBattleEffectArena Word(NativeBattleEffectArena target, int offset, ushort value) => target.Write(offset, BitConverter.GetBytes(value));
    private static NativeBattleEffectArena Dword(NativeBattleEffectArena target, int offset, uint value) => target.Write(offset, BitConverter.GetBytes(value));
}
