using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEuler7014B0
{
    public static NativeBattleActionMatrix Rotation(NativeBattleVector angles)
    {
        NativeBattleActionMatrix y = NativeBattlePandemona291Restoration.Rotation701270(angles.Y);
        short sine = unchecked((short)NativeBattleCameraMath.Sine(angles.Z));
        short cosine = unchecked((short)NativeBattleCameraMath.Cosine(angles.Z));
        NativeBattleActionMatrix z = new([cosine, sine, 0, unchecked((short)-sine), cosine, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleActionMatrix intermediate = NativeBattleGfMatrices.Multiply56C090(y, z);
        NativeBattleActionMatrix x = NativeBattlePandemona291Restoration.Rotation701220(angles.X);
        return NativeBattleGfMatrices.Multiply56C090(intermediate, x);
    }

    public static NativeBattlePackedMatrix32 Write(NativeBattleEffectArena angles8, NativeBattlePackedMatrix32 destination)
    {
        destination.Validate();
        NativeBattleVector angles = new(BinaryPrimitives.ReadInt16LittleEndian(angles8.Read(0, 2)), BinaryPrimitives.ReadInt16LittleEndian(angles8.Read(2, 2)), BinaryPrimitives.ReadInt16LittleEndian(angles8.Read(4, 2)));
        NativeBattleActionMatrix rotation = Rotation(angles);
        byte[] bytes = destination.Bytes.ToArray();
        bool[] known = destination.Known.ToArray();
        for (int index = 0; index < 9; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * 2), rotation.Rotation[index]);
            known[index * 2] = true;
            known[index * 2 + 1] = true;
        }
        bytes[18] = 0;
        bytes[19] = 0;
        known[18] = false;
        known[19] = false;
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }
}
