using static Memoria.FF8.Runtime.NativeBattleEffect83Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect83Controller
{
    public static NativeBattleEffect83Step AdvanceDriver(NativeBattleEffect83Frame frame, uint address, NativeBattleEffect83Resources resources, NativeBattleEffect83Inputs inputs)
    {
        frame = frame with { State = frame.State.WriteWord(TargetAddress + 0x46, unchecked((ushort)(frame.State.ReadWord(TargetAddress + 0x46) + 1))) };
        if (frame.State.ReadWord(TargetAddress + 0x40) != frame.State.ReadWord(TargetAddress + 0x42))
        {
            frame = frame with { State = frame.State.WriteWord(TargetAddress + 0x40, frame.State.ReadWord(TargetAddress + 0x42)).WriteWord(TargetAddress + 0x46, 0) };
            if (unchecked((short)frame.State.ReadWord(TargetAddress + 0x40)) == 1)
            {
                frame = SpawnDriverObjects797E00(frame, address, resources, inputs);
            }
        }
        if (frame.State.ReadWord(TargetAddress + 0x42) != frame.State.ReadWord(TargetAddress + 0x44))
        {
            frame = frame with { State = frame.State.WriteWord(TargetAddress + 0x42, frame.State.ReadWord(TargetAddress + 0x44)) };
        }
        switch (Phase(frame, address))
        {
            case 0:
                frame = inputs.Execute(frame, new NativeBattleEffect83UploadTim(EmbeddedTimAddress));
                frame = NextPhase(frame, address);
                break;
            case 1:
                (frame, bool requestedFirst) = RequestDriverState797D00(frame, 1);
                if (requestedFirst)
                {
                    frame = NextPhase(frame, address);
                }
                break;
            case 2:
                if (unchecked((short)frame.State.ReadWord(TargetAddress + 0x40)) >= 1)
                {
                    frame = inputs.Execute(frame, new NativeBattleEffect83Sound501330(resources.Sound, 0, 128));
                    frame = NextPhase(frame, address);
                }
                break;
            case 3:
                if (unchecked((short)frame.State.ReadWord(TargetAddress + 0x46)) >= 0x6E)
                {
                    frame = frame with { State = frame.State.WriteWord(TargetAddress + 0x48, 1) };
                    frame = NextPhase(frame, address);
                }
                break;
            case 4:
                (frame, bool requestedSecond) = RequestDriverState797D00(frame, 2);
                if (requestedSecond)
                {
                    frame = NextPhase(frame, address);
                }
                break;
            case 5:
                frame = NextPhase(MarkComplete(frame, address), address);
                break;
            case 6:
                break;
            default:
                throw new InvalidDataException("Effect83 driver signed phase leaves its seven actual handlers.");
        }
        return Finish(frame, address);
    }

    public static (NativeBattleEffect83Frame Frame, bool Requested) RequestDriverState797D00(NativeBattleEffect83Frame frame, short value)
    {
        ushort current = frame.State.ReadWord(TargetAddress + 0x40);
        if (frame.State.ReadWord(TargetAddress + 0x44) != current || unchecked((short)current) != value - 1)
        {
            return (frame, false);
        }
        return (frame with { State = frame.State.WriteWord(TargetAddress + 0x44, unchecked((ushort)value)) }, true);
    }

    private static NativeBattleEffect83Frame SpawnDriverObjects797E00(NativeBattleEffect83Frame frame, uint address, NativeBattleEffect83Resources resources, NativeBattleEffect83Inputs inputs)
    {
        (frame, uint model) = AppendHierarchy(frame, 7, 0x79E690, 0x140, address, inputs);
        frame = NativeBattleEffect83Models.Initialize(frame, model, resources, inputs);
        (frame, uint node) = AppendHierarchy(frame, 3, 0x797ED0, 0x2A4, address, inputs);
        frame = Write(frame, node, Read(frame, node).WriteDword(0x30, NodeSourceAddress).WriteWord(0x298, 0).WriteWord(0x29C, 0).WriteWord(0x29E, 0xF));
        (frame, _) = AppendHierarchy(frame, 5, 0x79D9B0, 0xC8, address, inputs);
        (frame, _) = AppendHierarchy(frame, 4, 0x79D860, 0xB0, address, inputs);
        return frame;
    }

    public static NativeBattleEffect83Step AdvanceNode(NativeBattleEffect83Frame frame, uint address, NativeBattleEffect83Resources resources, NativeBattleEffect83Inputs inputs)
    {
        switch (Phase(frame, address))
        {
            case 0:
                if (Signed(Read(frame, address), 0x24) >= Signed(Read(frame, address), 0x298))
                {
                    frame = NativeBattleEffect83NodeEngine.Initialize797F60(frame, address, resources, inputs);
                    frame = NativeBattleEffect83NodeEngine.Advance798A40(frame, address, resources, inputs).Frame;
                    frame = NextPhase(frame, address);
                }
                break;
            case 1:
                NativeBattleEffect83NodeStep step = NativeBattleEffect83NodeEngine.Advance798A40(frame, address, resources, inputs);
                frame = step.Frame;
                if (step.Result != 0)
                {
                    frame = NextPhase(MarkComplete(frame, address), address);
                }
                break;
            case 2:
                break;
            default:
                throw new InvalidDataException("Effect83 node signed phase leaves its three actual handlers.");
        }
        return Finish(frame, address);
    }

    public static NativeBattleEffect83Step AdvanceStageTransition(NativeBattleEffect83Frame frame, uint address, NativeBattleEffect83Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (Phase(frame, address))
        {
            case 0:
                frame = Write(frame, address, task.WriteWord(0x1C, 0));
                frame = PublishStageTransition(frame, 0, true, inputs);
                frame = NextPhase(frame, address);
                break;
            case 1:
                short up = unchecked((short)(Signed(task, 0x1C) + 0x200));
                if (up >= 0x800)
                {
                    up = 0x800;
                    frame = NextPhase(frame, address);
                }
                frame = Write(frame, address, Read(frame, address).WriteWord(0x1C, unchecked((ushort)up)));
                frame = PublishStageTransition(frame, unchecked((ushort)up), false, inputs);
                break;
            case 2:
                if (Signed(task, 0x24) >= 0x50)
                {
                    frame = NextPhase(frame, address);
                }
                break;
            case 3:
                short down = unchecked((short)(Signed(task, 0x1C) - 0x100));
                if (down <= 0)
                {
                    down = 0;
                    frame = NextPhase(MarkComplete(frame, address), address);
                }
                frame = Write(frame, address, Read(frame, address).WriteWord(0x1C, unchecked((ushort)down)));
                frame = PublishStageTransition(frame, unchecked((ushort)down), false, inputs);
                break;
            case 4:
                break;
            default:
                throw new InvalidDataException("Effect83 transition signed phase leaves its five actual handlers.");
        }
        return Finish(frame, address);
    }

    private static NativeBattleEffect83Frame PublishStageTransition(NativeBattleEffect83Frame frame, ushort intensity, bool clearRgb, NativeBattleEffect83Inputs inputs)
    {
        for (int index = 0; index < 4; index++)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect83StageTransition(index, intensity, clearRgb));
        }
        return frame;
    }

    public static NativeBattleEffect83Step AdvanceTargetBody(NativeBattleEffect83Frame frame, uint address, NativeBattleEffect83Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        uint target = PhysicalModelAddress(task.ReadByte(0x2D));
        switch (Phase(frame, address))
        {
            case 0:
                if (Signed(task, 0x24) >= 0x37)
                {
                    frame = inputs.Execute(frame, new NativeBattleEffect83TargetCapture79E590(address));
                    frame = inputs.Execute(frame, new NativeBattleEffect83TargetFlags(target, 0xFF, 4, false));
                    task = Read(frame, address).WriteByte(0x26, (byte)(Read(frame, address).ReadByte(0x26) | 8)).WriteWord(0xC0, 0x300).WriteWord(0xC4, 0x5A);
                    frame = NextPhase(Write(frame, address, task), address);
                }
                break;
            case 1:
                short factor = unchecked((short)(Signed(task, 0xC0) - 0x10));
                short countdown = unchecked((short)(Signed(task, 0xC4) - 1));
                task = task.WriteWord(0xC0, unchecked((ushort)Math.Max(0, (int)factor))).WriteWord(0xC4, unchecked((ushort)countdown));
                frame = Write(frame, address, task);
                if (countdown == 0x3C)
                {
                    frame = inputs.Execute(frame, new NativeBattleEffect83TargetFlags(target, 0xFF, 0x20, false));
                }
                else if (countdown <= 0)
                {
                    frame = inputs.Execute(frame, new NativeBattleEffect83TargetFlags(target, 0xFFFB, 0, true));
                    task = Read(frame, address).WriteWord(0x26, (ushort)((Read(frame, address).ReadWord(0x26) & ~8) | 1));
                    frame = NextPhase(Write(frame, address, task), address);
                }
                break;
            case 2:
                break;
            default:
                throw new InvalidDataException("Effect83 target body signed phase leaves its three actual handlers.");
        }
        if ((Read(frame, address).ReadByte(0x26) & 8) != 0)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect83TargetDraw79DA20(address + 0x6C));
        }
        return Finish(frame, address);
    }
}
