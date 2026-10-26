namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect326Controller
{
    public static NativeBattleEffect326Frame InitializeBoneParticles62F710(NativeBattleEffect326Frame frame)
    {
        (frame, uint address) = AppendRequired326(frame, 0x62F740);
        frame = Write(frame, address, Read(frame, address).WriteWord(12, 0));
        return ClearRecordHeads(frame, 0x24F0C88, 3);
    }

    public static NativeBattleEffect326Frame InitializePairedBoneParticles62FC20(NativeBattleEffect326Frame frame)
    {
        (frame, uint first) = AppendRequired326(frame, 0x62FC90);
        frame = Write(frame, first, Read(frame, first).WriteWord(12, 0).WriteWord(14, 30).WriteWord(0x18, 1));
        (frame, uint second) = AppendRequired326(frame, 0x62FC90);
        frame = Write(frame, second, Read(frame, second).WriteWord(12, 0).WriteWord(14, 52).WriteWord(0x18, 2));
        frame = ClearRecordHeads(frame, 0x24F2180, 10);
        frame = ClearRecordHeads(frame, 0x24F0C88, 3);
        return ClearRecordHeads(frame, 0x24F0DF0, 3);
    }

    private static NativeBattleEffect326Frame ClearRecordHeads(NativeBattleEffect326Frame frame, uint address, int count)
    {
        for (int index = 0; index < count; index++)
        {
            frame = WriteBytes(frame, checked(address + (uint)(index * 20)), NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(0u)), 4);
        }
        return frame;
    }
}
