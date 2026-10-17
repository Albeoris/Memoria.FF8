namespace Memoria.FF8.Runtime;

public abstract record NativeBattleCuragaOperation;
public sealed record NativeBattleCuragaStageFadeOperation(short Fade, bool ClearRgb) : NativeBattleCuragaOperation;
public sealed record NativeBattleCuragaPacketOperation(NativeBattleModulePacketBatch Batch) : NativeBattleCuragaOperation;

/// <summary>87DE50/882FC0's stage fades and87E020's original fullscreen flash program/OT packets.</summary>
public static partial class NativeBattleCuragaParticles
{
    public static NativeBattleCuragaLeafStep AdvanceSimple(NativeBattleCuragaFrame frame, uint address, NativeBattleCuragaResources resources, Func<NativeBattleCuragaFrame, NativeBattleCuragaOperation, NativeBattleCuragaFrame> execute)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        switch (task.Callback)
        {
            case 0x87DE50:
                FadeIn();
                break;
            case 0x882FC0:
                FadeOut();
                break;
            case 0x87E020:
                Flash();
                break;
            default:
                throw new NotSupportedException($"Native Curaga simple callback{task.Callback:X} requires its original owner.");
        }
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        hierarchy = hierarchy.Write(address, task);
        bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
        if (complete)
        {
            hierarchy = hierarchy.Complete8DC530(address);
        }
        return new(frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } }, complete ? 2 : 0);

        int Phase() => unchecked((sbyte)task.ReadByte(0x29));
        void Next() => task = task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
        void Complete()
        {
            task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1));
            Next();
        }
        void Execute(NativeBattleCuragaOperation operation)
        {
            hierarchy = hierarchy.Write(address, task);
            frame = execute(frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } }, operation);
            hierarchy = new(frame.Effect.Pools);
            task = hierarchy.Read(address);
        }
        void FadeIn()
        {
            switch (Phase())
            {
                case 0:
                    task = task.WriteWord(0x1C, 0);
                    Execute(new NativeBattleCuragaStageFadeOperation(0, true));
                    Next();
                    break;
                case 1:
                    task = task.WriteWord(0x1C, unchecked((ushort)(task.ReadWord(0x1C) + 256)));
                    if (Word(task, 0x1C) >= 1024)
                    {
                        task = task.WriteWord(0x1C, 1024);
                        Complete();
                    }
                    Execute(new NativeBattleCuragaStageFadeOperation(Word(task, 0x1C), false));
                    break;
                case 2:
                    break;
                default:
                    InvalidPhase();
                    break;
            }
        }
        void FadeOut()
        {
            switch (Phase())
            {
                case 0:
                    task = task.WriteWord(0x1C, 1024);
                    Next();
                    break;
                case 1:
                    task = task.WriteWord(0x1C, unchecked((ushort)(task.ReadWord(0x1C) - 256)));
                    if (Word(task, 0x1C) <= 0)
                    {
                        task = task.WriteWord(0x1C, 0);
                        Complete();
                    }
                    Execute(new NativeBattleCuragaStageFadeOperation(Word(task, 0x1C), false));
                    break;
                case 2:
                    break;
                default:
                    InvalidPhase();
                    break;
            }
        }
        void Flash()
        {
            if (task.ReadDword(0x30) != 0x160E0A0 || resources.FlashProgram.IsDefault)
            {
                throw new NotSupportedException("Native87E020 requires its selected original160E0A0 color program.");
            }
            int index = Word(task, 0x38);
            uint entry = ReadProgram(index);
            byte command = (byte)(entry >> 24);
            if (command == 0xFF)
            {
                task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1));
                return;
            }
            if (command == 0xFE)
            {
                int delay = (byte)(entry >> 16);
                if (Word(task, 0x24) >= delay)
                {
                    index++;
                    task = task.WriteWord(0x38, unchecked((ushort)index)).WriteDword(0x34, ReadProgram(index));
                }
            }
            else
            {
                task = task.WriteDword(0x34, entry).WriteWord(0x38, unchecked((ushort)(index + 1)));
            }
            uint color = (task.ReadDword(0x34) & 0xFFFFFF) | 0x2A000000;
            NativeBattleGeometryPrimitive quad = new(7, [0x05000000, color, 0, 320, 216u << 16, (216u << 16) | 320], [0, 0, 0, 0]);
            //45BFC0(packet,0,0,45C690(0,1,640,0),NULL) emits E1 and an explicit trailing zero DWORD.
            NativeBattleGeometryPrimitive mode = new(7, [0x02000000, 0xE100002A, 0], [0, 0, 0, 0]);
            Execute(new NativeBattleCuragaPacketOperation(new([new(quad, 0, 1), new(mode, 24, 0)], 36)));

            uint ReadProgram(int offset)
            {
                if (offset < 0 || offset >= resources.FlashProgram.Length)
                {
                    throw new InvalidDataException("Native87E020 signed program index leaves its authenticated DWORD stream.");
                }
                return resources.FlashProgram[offset];
            }
        }
        void InvalidPhase() => throw new InvalidDataException("Native Curaga signed fade phase exceeds its three original callbacks.");
    }

    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
}
