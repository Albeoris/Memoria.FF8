using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect166CapturePreparation(NativeBattleEffectArena Point8, NativeBattlePackedMatrix32 Matrix32, NativeBattleEffectArena Rectangle8);
public sealed record NativeBattleEffect166Capture5700(NativeBattleEffect166CapturePreparation Preparation) : NativeBattleEffect166Operation;

public static class NativeBattleEffect166Capture
{
    public static NativeBattleEffect166CapturePreparation Prepare(NativeBattleEffectArena point8)
    {
        short x = BinaryPrimitives.ReadInt16LittleEndian(point8.Read(0, 2));
        short z = BinaryPrimitives.ReadInt16LittleEndian(point8.Read(4, 2));
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.Scale56BEF0(NativeBattleGfMatrices.Euler56CD50(new(1024, 0, 0)), 16777216 / (37191680 / 256), 4096, 16777216 / (24788992 / 256)) with { X = x / 18, Y = z / 24, Z = 32767 };
        NativeBattleEffectArena rect = NativeBattleEffectArena.Clear().MarkUnknown(0, 8).Write(0, BitConverter.GetBytes((short)384)).Write(2, BitConverter.GetBytes((short)256)).Write(4, BitConverter.GetBytes((short)256)).Write(6, BitConverter.GetBytes((short)256));
        return new(point8, NativeBattlePackedMatrix32.FromMatrix(matrix, null), rect);
    }
}
