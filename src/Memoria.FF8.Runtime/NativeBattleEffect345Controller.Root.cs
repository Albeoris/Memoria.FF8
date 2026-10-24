using static Memoria.FF8.Runtime.NativeBattleEffect345Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect345Controller
{
    private static NativeBattleEffect345Step AdvanceRoot(NativeBattleEffect345Frame frame, uint address, NativeBattleEffect345Resources resources, NativeBattleEffect345Inputs inputs)
    {
        frame = WriteBytes(frame, 0x2793E58, PackedArena(inputs.ReadView(frame)), 32);
        frame = frame with { State = frame.State.WriteDword(0x255EA84, 0x2793E58).WriteDword(0x2557208, 0x2793E58) };
        bool odd = (Read(frame, address).ReadWord(0x5C) & 1) != 0;
        frame = frame with { State = frame.State.WriteDword(PrimaryCursorGlobal, frame.State.ReadDword(odd ? 0x255DC0Cu : 0x255DC08u)).WriteDword(SecondaryCursorGlobal, frame.State.ReadDword(odd ? 0x255CD64u : 0x255CD60u)) };
        frame = inputs.Execute(frame, new NativeBattleEffect345UpdateCaster8DC740(address));
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (NativePhase(task, 10))
        {
            case 0:
            case 1:
            case 5:
                frame = IncrementPhase(frame, address);
                break;
            case 2:
                if (task.ReadByte(0x28) == 0)
                {
                    uint emitter = frame.State.ReadDword(AllocationCursorGlobal);
                    uint draws = checked(emitter + 0x870);
                    frame = frame with { State = frame.State.WriteDword(0x255EA44, emitter).WriteDword(0x255EA88, draws).WriteDword(AllocationCursorGlobal, checked(draws + 0x9D80)) };
                    frame = ClearBytes(ClearBytes(frame, emitter, 0x870), draws, 0x9D80);
                    frame = frame with { State = frame.State.WriteWord(0x2557758, 0).WriteWord(0x255EA50, 0).WriteWord(0x255E610, 0).WriteWord(0x25570A8, 0) };
                    frame = IncrementPhase(frame, address);
                }
                break;
            case 3:
                frame = Write(frame, address, task.WriteByte(0x63, 1));
                (frame, _) = AppendHierarchy(frame, 1, 0x705E50, 88, address, inputs);
                frame = IncrementPhase(frame, address);
                break;
            case 4:
                if (task.ReadByte(0x63) == 0)
                {
                    sbyte target = unchecked((sbyte)task.ReadByte(0x2B));
                    frame = target < Signed(task, 0x5A) - 1 ? Write(frame, address, task.WriteByte(0x2B, unchecked((byte)(target + 1))).WriteByte(0x2E, unchecked((byte)(task.ReadByte(0x2E) + 1))).WriteByte(0x29, 3)) : IncrementPhase(frame, address);
                }
                break;
            case 6:
                if (task.ReadWord(0x5E) == 0)
                {
                    frame = IncrementPhase(frame, address);
                }
                break;
            case 7:
                frame = inputs.Execute(frame, new NativeBattleEffect345ReleaseSource508630(frame.State.ReadDword(InitializerGlobal), checked(address + 0x62)));
                frame = IncrementPhase(frame, address);
                break;
            case 8:
                if (task.ReadByte(0x62) != 0)
                {
                    frame = IncrementPhase(frame, address);
                }
                break;
            case 9:
                frame = IncrementPhase(MarkComplete(frame, address), address);
                break;
            case 10:
                break;
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(0x5E, 0));
        frame = frame with { State = frame.State.WriteWord(0x255EA7C, 0).WriteWord(0x255EA40, 0) };
        foreach (int index in new[] { 1, 2, 7, 3, 4, 5, 6 })
        {
            (frame, int survivors) = Scan(frame, index, resources, inputs);
            frame = Write(frame, address, Read(frame, address).WriteWord(0x5E, unchecked((ushort)(Read(frame, address).ReadWord(0x5E) + survivors))));
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(0x5C, unchecked((ushort)(Read(frame, address).ReadWord(0x5C) + 1))));
        return Finish(frame, address);
    }

    private static NativeBattleEffect345Step AdvanceFade(NativeBattleEffect345Frame frame, uint address, bool outgoing, NativeBattleEffect345Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (NativePhase(task, 2))
        {
            case 0:
                if (Signed(task, 0x24) >= (outgoing ? 0 : 28))
                {
                    frame = Write(frame, address, task.WriteWord(0x1C, outgoing ? (ushort)0x600 : (ushort)0).WriteByte(0x29, 1));
                    if (!outgoing)
                    {
                        for (int group = 0; group < 4; group++)
                        {
                            frame = inputs.Execute(frame, new NativeBattleEffect345StageTransition(group, 0, true));
                        }
                    }
                }
                break;
            case 1:
                short intensity = unchecked((short)(task.ReadWord(0x1C) + (outgoing ? -0x100 : 0x200)));
                bool done = outgoing ? intensity <= 0 : intensity >= 0x600;
                if (done)
                {
                    intensity = outgoing ? (short)0 : (short)0x600;
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)).WriteByte(0x29, 2);
                }
                frame = Write(frame, address, task.WriteWord(0x1C, unchecked((ushort)intensity)));
                for (int group = 0; group < 4; group++)
                {
                    frame = inputs.Execute(frame, new NativeBattleEffect345StageTransition(group, unchecked((ushort)intensity), false));
                }
                break;
            case 2:
                break;
        }
        return Finish(frame, address);
    }

    public static NativeBattleEffect345Frame InitializeAppearance70C840(NativeBattleEffect345Frame frame, uint context, uint projectedRows, uint descriptor, uint model)
    {
        frame = ClearBytes(frame, context, 0x54);
        NativeBattleEffectArena view = ReadBytes(frame, context, 0x54).Write(0x40, BitConverter.GetBytes(projectedRows)).Write(0x44, BitConverter.GetBytes(descriptor)).Write(0x48, BitConverter.GetBytes(model)).Write(0x4C, BitConverter.GetBytes(PrimaryCursorGlobal)).Write(0x50, BitConverter.GetBytes(PrimaryCursorGlobal));
        frame = WriteBytes(frame, context, view, 0x54);
        frame = WriteBytes(frame, descriptor + 4, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(projectedRows)), 4);
        NativeBattleEffectArena fields = NativeBattleEffectArena.Clear().Write(0, new byte[11]).Write(4, BitConverter.GetBytes((ushort)0x140)).Write(8, [0x80, 0x80, 0x80]);
        frame = WriteBytes(frame, descriptor + 0x14, fields, 11);
        return WriteBytes(frame, descriptor + 0x20, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(uint.MaxValue)).Write(4, BitConverter.GetBytes((ushort)0)), 6);
    }
}
