using static Memoria.FF8.Runtime.NativeBattleEffect68Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect68Controller
{
    public static NativeBattleEffect68Step AdvanceRoot(NativeBattleEffect68Frame frame, uint address, NativeBattleEffect68Resources resources, NativeBattleEffect68Inputs inputs)
    {
        uint selected = frame.State.ReadDword((Read(frame, address).ReadByte(0x5C) & 1) != 0 ? FirstPacketGlobal : SecondPacketGlobal);
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, selected) };
        frame = inputs.Execute(frame, new NativeBattleEffect68UpdateCaster8DC740(address));
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (Phase(frame, address))
        {
            case 0:
                frame = NextPhase(frame, address);
                break;
            case 1:
                frame = Write(frame, address, task.WriteByte(0x63, 1));
                (frame, _) = AppendHierarchy(frame, 1, 0x7DC3A0, 0x58, address, inputs);
                frame = inputs.Execute(frame, new NativeBattleEffect68ForceCamera50A730());
                frame = inputs.Execute(frame, new NativeBattleEffect68SetMode4A8480(0));
                frame = NextPhase(frame, address);
                break;
            case 2:
                if (task.ReadByte(0x63) == 0)
                {
                    if (unchecked((sbyte)task.ReadByte(0x2A)) < unchecked((short)task.ReadWord(0x58)))
                    {
                        frame = Write(frame, address, task.WriteByte(0x2A, unchecked((byte)(task.ReadByte(0x2A) + 1))).WriteByte(0x29, 1));
                    }
                    else
                    {
                        frame = NextPhase(frame, address);
                    }
                }
                break;
            case 3:
                if (task.ReadWord(0x5E) == 0)
                {
                    frame = inputs.Execute(frame, new NativeBattleEffect68SetMode4A8480(1));
                    frame = NextPhase(MarkComplete(frame, address), address);
                }
                break;
            case 4:
                break;
            default:
                throw new InvalidDataException("Effect68 root signed phase exceeds its native handlers.");
        }
        ushort count = 0;
        for (int index = 1; index < 5; index++)
        {
            (frame, int survivors) = Scan(frame, index, resources, inputs);
            count = unchecked((ushort)(count + survivors));
            frame = Write(frame, address, Read(frame, address).WriteWord(0x5E, count));
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(0x5C, unchecked((ushort)(Read(frame, address).ReadWord(0x5C) + 1))));
        return Finish(frame, address);
    }

    public static NativeBattleEffect68Step AdvanceDriver(NativeBattleEffect68Frame frame, uint address, NativeBattleEffect68Resources resources, NativeBattleEffect68Inputs inputs)
    {
        if (Read(frame, address).ReadWord(0x24) == 0)
        {
            _ = Read(frame, address).ReadDword(0x1C);
            _ = Read(frame, address).ReadDword(0x20);
            frame = inputs.Execute(frame, new NativeBattleEffect68Sound501330(0x1594994, 0, 128));
        }
        frame = inputs.Execute(frame, new NativeBattleEffect68UpdateTarget8DC610(address));
        frame = inputs.Execute(frame, new NativeBattleEffect68UpdateBounds8DC870(address));
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (Phase(frame, address))
        {
            case 0:
                frame = frame with { State = frame.State.WriteWord(ControlFlagGlobal, 0).WriteWord(ContactFlagGlobal, 0) };
                frame = NativeBattleEffect68Lower.Select7DC470(frame, task.ReadByte(0x2D), resources, inputs);
                (frame, _) = AppendHierarchy(frame, 2, 0x7DC600, 0x78, address, inputs);
                frame = NextPhase(frame, address);
                break;
            case 1:
                if (task.ReadByte(0x28) == 0)
                {
                    uint root = task.ReadDword(0x10);
                    frame = Write(frame, root, Read(frame, root).WriteByte(0x63, 0));
                    int group = checked(Parameters(frame).HeaderIndex7C + unchecked((sbyte)task.ReadByte(0x2A)));
                    NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
                    int contact = packets.Headers[group].FirstContactIndex ?? throw new NotSupportedException("Effect68 requires its current header contact pointer.");
                    frame = inputs.Execute(frame, new NativeBattleEffect68Contact506690(contact));
                    frame = NextPhase(MarkComplete(frame, address), address);
                }
                break;
            case 2:
                break;
            default:
                throw new InvalidDataException("Effect68 driver signed phase exceeds its native handlers.");
        }
        return Finish(frame, address);
    }

    public static NativeBattleEffect68Step AdvanceMain(NativeBattleEffect68Frame frame, uint address, NativeBattleEffect68Resources resources, NativeBattleEffect68Inputs inputs)
    {
        switch (Phase(frame, address))
        {
            case 0:
                int actor = Read(frame, address).ReadByte(0x2D);
                NativeBattleEffectArena row = inputs.ReadModels(frame)[actor];
                byte flags = row.Read(1, 1)[0];
                frame = inputs.Execute(frame, new NativeBattleEffect68WriteModelBytes(actor, 1, NativeBattleEffectArena.Clear().Write(0, [(byte)(flags | 0x78)]), 1));
                (frame, uint first) = AppendHierarchy(frame, 2, 0x7DD950, 0x78, address, inputs);
                frame = Write(frame, first, Read(frame, first).WriteWord(0x68, 2));
                frame = frame with { State = frame.State.WriteWord(MainFlagGlobal, 1) };
                (frame, _) = AppendHierarchy(frame, 2, 0x7DC6F0, 0x78, address, inputs);
                frame = Write(frame, address, Read(frame, address).WriteWord(0x66, 45));
                frame = NextPhase(frame, address);
                break;
            case 1:
            case 2:
            case 3:
            case 4:
            case 5:
            case 6:
                uint[] functions = [0x7DC680, 0x7DDE90, 0x7DE0C0, 0x7DE710, 0x7DE7A0, 0x7DE910, 0x7DE930];
                frame = NativeBattleEffect68Lower.Invoke(frame, functions[Phase(frame, address)], address, resources, inputs);
                break;
            default:
                throw new InvalidDataException("Effect68 main signed phase exceeds its native handlers.");
        }
        return Finish(frame, address);
    }
}
