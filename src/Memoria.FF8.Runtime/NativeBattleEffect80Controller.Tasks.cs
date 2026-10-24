using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect80Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect80Controller
{
    public static NativeBattleEffect80Step AdvanceNode(NativeBattleEffect80Frame frame, uint address, NativeBattleEffect80Resources resources, NativeBattleEffect80Inputs inputs)
    {
        switch (Phase(frame, address))
        {
            case 0:
                if (Signed(Read(frame, address), 0x24) >= Signed(Read(frame, address), 0x298))
                {
                    frame = NativeBattleEffect80NodeEngine.Initialize7B0B70(frame, address, resources, inputs);
                    frame = NativeBattleEffect80NodeEngine.Advance7B1650(frame, address, resources, inputs).Frame;
                    frame = NextPhase(frame, address);
                }
                break;
            case 1:
                NativeBattleEffect80NodeStep step = NativeBattleEffect80NodeEngine.Advance7B1650(frame, address, resources, inputs);
                frame = step.Frame;
                if (step.Result != 0)
                {
                    frame = NextPhase(MarkComplete(frame, address), address);
                }
                break;
            case 2:
                break;
            default:
                throw new InvalidDataException("Effect80 node signed phase leaves its three actual handlers.");
        }
        return Finish(frame, address);
    }

    public static NativeBattleEffect80Step AdvanceAct(NativeBattleEffect80Frame frame, uint address, NativeBattleEffect80Resources resources, NativeBattleEffect80Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        bool second = task.Callback == 0x7B4540;
        switch (Phase(frame, address))
        {
            case 0:
                frame = NativeBattleEffect80Programme.Initialize7016B0(frame, address, task.ReadDword(0x74), unchecked((int)task.ReadDword(0x78)), resources);
                task = Read(frame, address).WriteDword(0x50, 4096).WriteDword(0x54, 4096).WriteDword(0x58, 4096).WriteWord(0x88, second ? (ushort)0xFC00 : (ushort)0);
                frame = Write(frame, address, task);
                frame = second ? WriteBytes(frame, address + 0x1C, inputs.ReadPosition8(frame, task.ReadByte(0x2C)), 8) : PositionAct7AC060(frame, address, Signed(task, 0x84), resources, inputs);
                frame = NativeBattleEffect80Programme.Draw7AC0B0(frame, address, resources, inputs).Frame;
                frame = NextPhase(frame, address);
                break;
            case 1:
                if (!second)
                {
                    ushort age = unchecked((ushort)(frame.State.ReadWord(TargetAddress + 0x50) + 1));
                    frame = frame with { State = frame.State.WriteWord(TargetAddress + 0x50, age) };
                    frame = PositionAct7AC060(frame, address, unchecked((short)age), resources, inputs);
                }
                NativeBattleEffect80ProgrammeStep step = NativeBattleEffect80Programme.Draw7AC0B0(frame, address, resources, inputs);
                frame = step.Frame;
                if (step.Remaining == 0)
                {
                    frame = NextPhase(MarkComplete(frame, address), address);
                }
                break;
            case 2:
                break;
            default:
                throw new InvalidDataException("Effect80 ACT signed phase leaves its three actual handlers.");
        }
        frame = WriteBytes(frame, TargetAddress + 0x10, TaskView(Read(frame, address), 0x1C, 8), 8);
        return Finish(frame, address);
    }

    public static NativeBattleEffect80Frame PositionAct7AC060(NativeBattleEffect80Frame frame, uint address, short age, NativeBattleEffect80Resources resources, NativeBattleEffect80Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        frame = WriteBytes(frame, address + 0x1C, inputs.ReadPosition8(frame, task.ReadByte(0x2C)), 8);
        int index = age >= 0x78 ? 0x77 : age;
        short offset = BinaryPrimitives.ReadInt16LittleEndian(resources.ReadStatic(unchecked((uint)((long)AgeTableAddress + 2 * index)), 2).AsSpan());
        task = Read(frame, address);
        return Write(frame, address, task.WriteWord(0x1E, unchecked((ushort)(task.ReadWord(0x1E) + offset))));
    }

    public static NativeBattleEffect80Step AdvancePointEmitter(NativeBattleEffect80Frame frame, uint address, NativeBattleEffect80Inputs inputs)
    {
        switch (Phase(frame, address))
        {
            case 0:
                frame = NextPhase(Write(frame, address, Read(frame, address).WriteWord(0x9C, 0x26)), address);
                break;
            case 1:
                frame = BirthPointParticle7AE740(frame, address, inputs);
                NativeBattleModulePoolTask task = Read(frame, address);
                short next = unchecked((short)(task.ReadWord(0x9C) - 1));
                frame = Write(frame, address, task.WriteWord(0x9C, unchecked((ushort)next)));
                if (next <= 0)
                {
                    frame = NextPhase(MarkComplete(frame, address), address);
                }
                break;
            case 2:
                break;
            default:
                throw new InvalidDataException("Effect80 point emitter has an unsupported signed phase.");
        }
        return Finish(frame, address);
    }

    private static NativeBattleEffect80Frame BirthPointParticle7AE740(NativeBattleEffect80Frame frame, uint address, NativeBattleEffect80Inputs inputs)
    {
        frame = inputs.Execute(frame, new NativeBattleEffect80Point502170(PhysicalModelAddress(Read(frame, address).ReadByte(0x2D)), 0xF1, address + 0x1C));
        (frame, uint child) = AppendHierarchy(frame, 5, 0x7AE7D0, 0xA0, address, inputs);
        frame = WriteBytes(frame, child + 0x5C, TaskView(Read(frame, address), 0x1C, 8), 8);
        NativeBattleModulePoolTask task = Read(frame, child);
        NativeCrtRandomState random = frame.Random.Draw(out ushort x);
        task = task.WriteWord(0x5C, unchecked((ushort)(task.ReadWord(0x5C) + (x & 0x7FF) - 0x400)));
        random = random.Draw(out ushort y);
        task = task.WriteWord(0x5E, unchecked((ushort)(task.ReadWord(0x5E) + (y & 0x7FF) - 0x400)));
        random = random.Draw(out ushort z);
        task = task.WriteWord(0x60, unchecked((ushort)(task.ReadWord(0x60) + (z & 0x1FF) - 0x100)));
        return Write(frame with { Random = random }, child, task);
    }

    public static NativeBattleEffect80Step AdvancePuffEmitter(NativeBattleEffect80Frame frame, uint address, NativeBattleEffect80Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (Phase(frame, address))
        {
            case 0:
                if (Signed(task, 0x24) >= 0x78)
                {
                    frame = NextPhase(MarkComplete(frame, address), address);
                }
                else if ((task.ReadByte(0x24) & 1) == 0)
                {
                    (frame, _) = AppendHierarchy(frame, 5, 0x7AF030, 0xA0, address, inputs);
                }
                break;
            case 1:
                break;
            default:
                throw new InvalidDataException("Effect80 puff emitter has an unsupported signed phase.");
        }
        return Finish(frame, address);
    }

    public static NativeBattleEffect80Step AdvanceStageRise(NativeBattleEffect80Frame frame, uint address, NativeBattleEffect80Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (Phase(frame, address))
        {
            case 0:
                frame = NextPhase(Write(frame, address, task.WriteWord(0x1C, 0)), address);
                frame = PublishStageTransition(frame, 0, true, inputs);
                break;
            case 1:
                short up = unchecked((short)(task.ReadWord(0x1C) + 0xA0));
                if (up >= 0xA00)
                {
                    up = 0xA00;
                    frame = NextPhase(MarkComplete(frame, address), address);
                }
                frame = Write(frame, address, Read(frame, address).WriteWord(0x1C, unchecked((ushort)up)));
                frame = PublishStageTransition(frame, unchecked((ushort)up), false, inputs);
                break;
            case 2:
                break;
            default:
                throw new InvalidDataException("Effect80 rising stage transition has an unsupported signed phase.");
        }
        return Finish(frame, address);
    }

    public static NativeBattleEffect80Step AdvanceStageFall(NativeBattleEffect80Frame frame, uint address, NativeBattleEffect80Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (Phase(frame, address))
        {
            case 0:
                if (Signed(task, 0x24) >= 0x96)
                {
                    frame = NextPhase(Write(frame, address, task.WriteWord(0x1C, 0xA00)), address);
                }
                break;
            case 1:
                short down = unchecked((short)(task.ReadWord(0x1C) - 0x100));
                if (down <= 0)
                {
                    down = 0;
                    frame = NextPhase(MarkComplete(frame, address), address);
                }
                frame = Write(frame, address, Read(frame, address).WriteWord(0x1C, unchecked((ushort)down)));
                frame = PublishStageTransition(frame, unchecked((ushort)down), false, inputs);
                break;
            case 2:
                break;
            default:
                throw new InvalidDataException("Effect80 falling stage transition has an unsupported signed phase.");
        }
        return Finish(frame, address);
    }

    private static NativeBattleEffect80Frame PublishStageTransition(NativeBattleEffect80Frame frame, ushort intensity, bool clearRgb, NativeBattleEffect80Inputs inputs)
    {
        for (int index = 0; index < 4; index++)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect80StageTransition(index, intensity, clearRgb));
        }
        return frame;
    }
}

