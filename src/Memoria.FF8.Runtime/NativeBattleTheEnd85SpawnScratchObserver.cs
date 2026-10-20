using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

internal sealed class NativeBattleTheEnd85SpawnScratchObserver
{
    private const int Temporary = 100;
    internal NativeBattleEffectArena View { get; private set; }

    internal NativeBattleTheEnd85SpawnScratchObserver(NativeBattleEffectArena view)
    {
        view.Validate();
        if (view.Regions.Any(region => region.Offset > 180 - region.Data.Length) || view.UnknownRegions.Any(region => region.Offset > 180 - region.Length))
        {
            throw new InvalidDataException("Native The End spawn observation requires its bounded physical temporary view.");
        }
        View = view;
    }

    internal void Begin(int frame)
    {
        Word(78, frame);
    }

    internal void Placement(NativeBattleVector angles, NativeBattleActionMatrix matrix, short[] radius, NativeBattleVector unit, byte placement)
    {
        if (placement is not (1 or 2) || radius.Length != 3)
        {
            throw new InvalidDataException("Native The End placement observation requires its actual placement operation.");
        }
        Vector(40, angles);
        for (int index = 0; index < 9; index++)
        {
            Word(index * 2, matrix.Rotation[index]);
        }
        Dword(20, 0);
        Dword(24, 0);
        Dword(28, 0);
        for (int axis = 0; axis < 3; axis++)
        {
            Word(72 + axis * 2, radius[axis]);
        }
        Vector(32, unit);
        Dword(56, unchecked(radius[0] * unit.X << 4));
        Dword(60, placement == 1 ? unchecked(radius[1] * unit.Y << 4) : radius[1] << 16);
        Dword(64, unchecked(radius[2] * unit.Z << 4));
    }

    internal void RandomEuler(NativeBattleVector angles)
    {
        Vector(48, angles);
    }

    internal void CopyPlacementEuler()
    {
        View = NativeBattleEffectNodeContextMemory.Copy(View, Temporary + 40, View, Temporary + 48, 8);
    }

    private void Vector(int offset, NativeBattleVector vector)
    {
        Word(offset, vector.X);
        Word(offset + 2, vector.Y);
        Word(offset + 4, vector.Z);
    }
    private void Word(int offset, int value)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, unchecked((ushort)value));
        View = View.Write(Temporary + offset, bytes);
    }
    private void Dword(int offset, int value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        View = View.Write(Temporary + offset, bytes);
    }
}
