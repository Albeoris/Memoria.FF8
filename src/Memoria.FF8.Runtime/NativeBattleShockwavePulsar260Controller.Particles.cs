using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleShockwavePulsar260Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleShockwavePulsar260Controller
{
    public static NativeBattleShockwavePulsar260Frame UpdatePuffs(NativeBattleShockwavePulsar260Frame frame)
    {
        uint start = frame.State.ReadDword(WorkingGlobal) + PuffsOffset;
        for (int index = 0; index < PuffsCount; index++)
        {
            uint address = start + (uint)(index * 16);
            byte life = ReadView(frame, address + 15, 1).Read(0, 1)[0];
            if (life == 0)
            {
                continue;
            }
            frame = WriteView(frame, address + 15, NativeBattleEffectArena.Clear().Write(0, new byte[] { unchecked((byte)(life - 1)) }), 1);
            for (int axis = 0; axis < 3; axis++)
            {
                int velocity = unchecked((sbyte)ReadView(frame, address + (uint)(12 + axis), 1).Read(0, 1)[0]);
                uint position = address + (uint)(4 + axis * 2);
                frame = WriteWord(frame, position, unchecked((ushort)(ReadWord(frame, position) + (velocity << 2))));
            }
        }
        return frame;
    }

    public static NativeBattleShockwavePulsar260Frame UpdateSecondaryParticles(NativeBattleShockwavePulsar260Frame frame)
    {
        uint start = frame.State.ReadDword(WorkingGlobal) + SecondaryParticlesOffset;
        for (int index = 0; index < SecondaryParticlesCount; index++)
        {
            uint address = start + (uint)(index * 16);
            ushort life = ReadWord(frame, address);
            if (life == 0)
            {
                continue;
            }
            frame = WriteWord(frame, address, unchecked((ushort)(life - 1)));
            frame = WriteWord(frame, address + 4, unchecked((ushort)(ReadWord(frame, address + 4) + ReadWord(frame, address + 8))));
            frame = WriteWord(frame, address + 6, unchecked((ushort)(ReadWord(frame, address + 6) + ReadWord(frame, address + 10))));
        }
        return frame;
    }

    private static NativeBattleShockwavePulsar260Frame SpawnMainPuffs(NativeBattleShockwavePulsar260Frame frame)
    {
        for (int remaining = 4; remaining > 0; remaining--)
        {
            (NativeBattleShockwavePulsar260Frame allocated, uint address) = AllocatePuff(frame);
            frame = allocated;
            if (address == 0)
            {
                break;
            }
            frame = DrawRandom(frame, out _);
            frame = DrawRandom(frame, out ushort angle);
            NativeBattleEffectArena row = ReadView(frame, address, 16);
            row = row.Write(0, BitConverter.GetBytes(0x111D0F8u));
            row = row.Write(4, BitConverter.GetBytes(unchecked((short)((NativeBattleCameraMath.Sine(angle) >> 3) + (int)frame.State.ReadDword(0x25086A0)))));
            row = row.Write(6, BitConverter.GetBytes(unchecked((short)((remaining & 1) != 0 ? 4096 : -4096))));
            row = row.Write(8, BitConverter.GetBytes(unchecked((short)((NativeBattleCameraMath.Cosine(angle) >> 3) + (int)frame.State.ReadDword(0x25086A4) + 2048))));
            row = row.Write(10, BitConverter.GetBytes((ushort)512));
            frame = DrawRandom(frame, out _);
            row = row.Write(12, new byte[] { 0, unchecked((byte)((remaining & 1) != 0 ? -12 : 12)), 0, 80 });
            frame = WriteView(frame, address, row, 16);
        }
        return frame;
    }

    private static NativeBattleShockwavePulsar260Frame SpawnSecondaryParticles(NativeBattleShockwavePulsar260Frame frame, uint mainAddress, int targets)
    {
        for (int target = 0; target < targets; target++)
        {
            uint record = mainAddress + 0x10 + (uint)(target * 0x24);
            for (int index = 0; index < 2; index++)
            {
                (NativeBattleShockwavePulsar260Frame allocated, uint address) = AllocateSecondary(frame);
                frame = allocated;
                if (address == 0)
                {
                    break;
                }
                frame = WriteWord(frame, address, 6);
                frame = DrawRandom(frame, out ushort y);
                frame = WriteWord(frame, address + 2, unchecked((ushort)(y / 32 - 512)));
                frame = DrawRandom(frame, out ushort scaleDraw);
                short scale = unchecked((short)(((scaleDraw << 11) / 65536) + 2048));
                frame = WriteDword(frame, address + 12, record);
                frame = WriteWord(frame, address + 6, unchecked((ushort)scale));
                short half = unchecked((short)(scale >> 1));
                frame = WriteWord(frame, address + 4, unchecked((ushort)half));
                frame = WriteWord(frame, address + 8, unchecked((ushort)(half >> 4)));
                frame = WriteWord(frame, address + 10, unchecked((ushort)(half >> 2)));
            }
        }
        return frame;
    }
}
