namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private sealed partial class NativeTornado146Runner
    {
        private int Piece(ref NativeBattleModulePoolTask task)
        {
            int index = Word(task, 14);
            if ((uint)index >= 15)
            {
                throw new InvalidDataException("Native Tornado piece requires its original fifteen models.");
            }
            NativeBattleTemporaryAllocation temporary = NativeBattleTemporaryMemory.Allocate5082B0(owner.NativeFileStaging!, 312);
            owner.NativeFileStaging = temporary.Staging.Write(temporary.Address, BitConverter.GetBytes(resources.Pieces[index].Address)).Write(temporary.Address + 24, BitConverter.GetBytes(0u));
            NativeBattleActionMatrix orbit = NativeBattleGfMatrices.Euler56CD50(new(0, Word(task, 24), 0)) with { X = Word(task, 16), Y = Word(task, 18), Z = Word(task, 20) };
            bool air = Word(task, 18) < resources.LiftHeights[index];
            if (air)
            {
                Stamp(0xD0, BitConverter.GetBytes(unchecked((short)resources.PieceX[index])));
                Stamp(0xD2, BitConverter.GetBytes((short)0));
                Stamp(0xD4, BitConverter.GetBytes(unchecked((short)resources.PieceZ[index])));
                Stamp(0xE4, BitConverter.GetBytes(Word(task, 28)));
                Stamp(0xE6, BitConverter.GetBytes((short)0));
                Stamp(0xE8, BitConverter.GetBytes(Word(task, 32)));
                NativeBattleActionMatrix initial = NativeBattleGfMatrices.Compose56C2F0(View, orbit);
                StampMatrix(0xF8, initial);
                NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CD50(new(Word(task, 28), 0, Word(task, 32))) with { X = unchecked((short)resources.PieceX[index]), Z = unchecked((short)resources.PieceZ[index]) };
                StampMatrix(0x118, NativeBattleGfMatrices.Compose56C2F0(initial, local));
            }
            else
            {
                Stamp(0xD0, BitConverter.GetBytes((short)0));
                Stamp(0xD2, BitConverter.GetBytes((short)-4096));
                Stamp(0xD4, BitConverter.GetBytes((short)0));
                Stamp(0xD6, BitConverter.GetBytes((short)150));
                Stamp(0xE6, BitConverter.GetBytes(Word(task, 18)));
                Stamp(0x40, BitConverter.GetBytes(6));
                owner.NativeFileStaging = owner.NativeFileStaging!.MarkUnknown(temporary.Address + 0x38, 4);
            }
            Append(NativeBattleTornado146Pieces.Draw(resources.Pieces[index], task, resources.LiftHeights[index], unchecked((short)resources.PieceX[index]), unchecked((short)resources.PieceZ[index]), View, Projection, Stamp));
            owner.NativeFileStaging = NativeBattleTemporaryMemory.Release5082D0(owner.NativeFileStaging!, 312);
            if (DrawOnly)
            {
                return 0;
            }
            task = NativeBattleTornado146Pieces.Move(task, resources.LiftHeights[index], out bool complete);
            return complete ? 2 : 0;

            void Stamp(int offset, byte[] bytes) => owner.NativeFileStaging = owner.NativeFileStaging!.Write(temporary.Address + (uint)offset, bytes);
            void StampMatrix(int offset, NativeBattleActionMatrix matrix)
            {
                for (int component = 0; component < 9; component++)
                {
                    Stamp(offset + component * 2, BitConverter.GetBytes(matrix.Rotation[component]));
                }
                Stamp(offset + 20, BitConverter.GetBytes(matrix.X));
                Stamp(offset + 24, BitConverter.GetBytes(matrix.Y));
                Stamp(offset + 28, BitConverter.GetBytes(matrix.Z));
            }
        }
    }
}
