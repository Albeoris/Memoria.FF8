using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleBoneMatrixPadding
{
    public static short Euler56CE30(NativeBattleVector angles)
    {
        //The final Y multiply's aligned local padding aliases the preceding X product's rotation WORD5.
        return unchecked((short)-NativeBattleCameraMath.Sine(angles.X));
    }

    public static short UnscaledChild508C90(NativeBattleVector angles)
    {
        //508D6F's56C2F0 copies the high WORD of the final Y helper's sign-extended angle argument.
        return unchecked((short)(angles.Y >> 16));
    }

    public static ImmutableArray<short?> Compose5095B0After62C83D(NativeBattleActionMatrix local, NativeBattleActionMatrix basis, ImmutableArray<NativeBattleActionMatrix> bones, int? drawingEntryModulo8)
    {
        if (drawingEntryModulo8 is not (null or 0 or 4))
        {
            throw new InvalidDataException("Native5095B0 padding requires its actual DWORD-aligned caller stack parity.");
        }
        ImmutableArray<short?>.Builder result = ImmutableArray.CreateBuilder<short?>(bones.Length);
        short first = unchecked((short)(BitConverter.SingleToInt32Bits(local.Rotation[1]) >> 16));
        for (int bone = 0; bone < bones.Length; bone++)
        {
            if (bone == 0)
            {
                result.Add(first);
            }
            else if (drawingEntryModulo8 is null)
            {
                result.Add(null);
            }
            else if (drawingEntryModulo8 == 0)
            {
                //62C83D's56C090 input WORD1 was stored as a float at its physical ESP+10h.
                result.Add(first);
            }
            else
            {
                NativeBattleActionMatrix previous = bones[bone - 1];
                //The preceding bone's56C600 final stored Z double overlaps this aligned local's padding.
                ulong stored = NativeBattleGfMatrices.StoredZ56C600(basis, previous.X, previous.Y, previous.Z);
                result.Add(unchecked((short)(stored >> 16)));
            }
        }
        return result.MoveToImmutable();
    }
}
