using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static class NativeBattleSandStorm155Curve
{
    public static NativeBattleSandStorm155Frame PrepareSamples60752A(NativeBattleSandStorm155Frame frame)
    {
        short x = Word(frame, 0x23F2948);
        short z = Word(frame, 0x23F294C);
        frame = Point(frame, 0x23F2908, x, z);
        frame = frame with { State = frame.State.WriteView(0x23F2930, frame.State.ReadView(0x23F2908, 8), 8) };
        short firstX = Word(frame, 0x23F07F0);
        short firstZ = Word(frame, 0x23F07F4);
        short secondX = Word(frame, 0x23F07F8);
        short secondZ = Word(frame, 0x23F07FC);
        int firstDx = firstX - x;
        int firstDz = firstZ - z;
        int secondDx = secondX - x;
        int secondDz = secondZ - z;
        int firstDistance = unchecked(firstDx * firstDx + firstDz * firstDz);
        int secondDistance = unchecked(secondDx * secondDx + secondDz * secondDz);
        short nearerX = firstDistance > secondDistance ? secondX : firstX;
        short nearerZ = firstDistance > secondDistance ? secondZ : firstZ;
        short fartherX = firstDistance > secondDistance ? firstX : secondX;
        short fartherZ = firstDistance > secondDistance ? firstZ : secondZ;
        frame = Point(frame, 0x23F2918, nearerX, nearerZ);
        frame = Point(frame, 0x23F2928, fartherX, fartherZ);
        frame = Point(frame, 0x23F2910, unchecked((short)((fartherX + x) >> 1)), unchecked((short)((fartherZ + z) >> 1)));
        NativeBattleFileStaging staging = frame.Staging ?? throw new NotSupportedException("Sand Storm requires its actual physical curve workspace.");
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(staging, 400);
        NativeBattleEffectArena work = NativeBattleTemporaryMemory.ReadView(allocation.Staging, allocation.Address, 400);
        work = NativeBattleGilgameshSwordTrailCurve.Prepare571620(6, frame.State.ReadView(0x23F2908, 48), 0, work);
        frame = frame with { Staging = NativeBattleTemporaryMemory.PublishView(allocation.Staging, allocation.Address, work, 400) };
        NativeBattleEffectArena samples = frame.State.ReadView(0x23F2960, 34 * 8);
        for (int index = 0; index < 34; index++)
        {
            samples = NativeBattleGilgameshSwordTrailCurve.Evaluate571690(6, work, (index << 12) / 33, samples, index * 8);
        }
        return frame with { State = frame.State.WriteView(0x23F2960, samples, 34 * 8), Staging = NativeBattleTemporaryMemory.Release5082D0(frame.Staging!, 400) };
    }

    private static short Word(NativeBattleSandStorm155Frame frame, uint address) => BinaryPrimitives.ReadInt16LittleEndian(frame.State.ReadView(address, 2).Read(0, 2));
    private static NativeBattleSandStorm155Frame Point(NativeBattleSandStorm155Frame frame, uint address, short x, short z)
    {
        byte[] bytes = new byte[6];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, x);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), z);
        return frame with { State = frame.State.Write(address, bytes) };
    }
}
