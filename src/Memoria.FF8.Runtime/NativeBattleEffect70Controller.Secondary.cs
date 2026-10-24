using static Memoria.FF8.Runtime.NativeBattleEffect70Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect70Controller
{
    public static NativeBattleEffect70Step AdvanceLaunch(NativeBattleEffect70Frame frame, uint address, NativeBattleEffect70Resources resources, NativeBattleEffect70Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        uint record = task.ReadDword(12);
        int actor = Actor(ReadDword(frame, record));
        NativeBattleEffectArena caller = TargetCaller(frame, record, inputs);
        frame = NativeBattleEffect70Programme.Advance(frame, address + 20, 0x100, 0x1325A88, 0x100, false, caller, resources, inputs).Frame;
        int clock = Signed(Read(frame, address), 16);
        int duration = Signed(Read(frame, address), 18);
        int phase = clock - 13;
        if (unchecked((uint)phase) < unchecked((uint)duration))
        {
            int ratio = unchecked(phase << 12) / duration;
            if (phase == 0)
            {
                NativeBattleVector position = Position(frame, actor, inputs);
                (frame, uint child) = Append(frame, 2, 0x6E02E0);
                frame = PutWord(PutWord(PutWord(frame, child + 12, position.X), child + 14, 0), child + 16, position.Z);
                frame = InitializeWorkspace(frame, child + 20, 0x100, resources.Act(0x1324FF0), 0xD8);
            }
            frame = AdjustRotation(frame, record, actor, ratio / 8);
            int square = unchecked(ratio * ratio) >> 12;
            short savedY = ReadShort(frame, record + 6);
            frame = inputs.Execute(frame, new NativeBattleEffect70ModelPositionWord(actor, 0x1E, unchecked((short)(savedY - square * 2))));
            short middle = unchecked((short)((ReadShort(frame, record + 20) + ReadShort(frame, record + 22)) >> 1));
            frame = inputs.Execute(frame, new NativeBattleEffect70DrawActor693170(actor, ReadBytes(frame, record + 12, 8), Point(0, middle, 0), 4096));
            frame = inputs.Execute(frame, new NativeBattleEffect70ModelFlags(actor, 0xFFFF, 4, false));
        }
        else if (unchecked((uint)phase) < 200)
        {
            if (phase == duration)
            {
                frame = inputs.Execute(frame, new NativeBattleEffect70ModelPositionWord(actor, 0x1E, -12288));
            }
            short y = unchecked((short)(Position(frame, actor, inputs).Y - 256));
            frame = inputs.Execute(frame, new NativeBattleEffect70ModelPositionWord(actor, 0x1E, y));
        }
        task = Read(frame, address);
        return new(Write(frame, address, task.WriteWord(16, unchecked((ushort)(task.ReadWord(16) + 1)))), 0);
    }

    public static NativeBattleEffect70Step AdvanceReturn(NativeBattleEffect70Frame frame, uint address, NativeBattleEffect70Resources resources, NativeBattleEffect70Inputs inputs)
    {
        uint record = Read(frame, address).ReadDword(12);
        int actor = Actor(ReadDword(frame, record));
        NativeBattleEffectArena caller = TargetCaller(frame, record, inputs);
        frame = NativeBattleEffect70Programme.Advance(frame, address + 20, 0x100, 0x1326C68, 0xBC, false, caller, resources, inputs).Frame;
        int clock = Signed(Read(frame, address), 16);
        int ratio = unchecked(clock << 12) / 29;
        if (clock == 5)
        {
            frame = BirthReturnSparks(frame, actor, inputs);
        }
        int inverse = 4096 - ratio;
        frame = AdjustRotation(frame, record, actor, inverse / 8);
        short y = unchecked((short)(unchecked(inverse * inverse) >> 12));
        frame = inputs.Execute(frame, new NativeBattleEffect70ModelPositionWord(actor, 0x1E, y));
        NativeBattleModulePoolTask task = Read(frame, address);
        ushort next = unchecked((ushort)(task.ReadWord(16) + 1));
        frame = Write(frame, address, task.WriteWord(16, next));
        if (unchecked((short)next) >= 30)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect70ModelFlags(actor, 0xFFF7, 0, true));
            return new(frame, 2);
        }
        return new(frame, 0);
    }

    public static NativeBattleEffect70Step AdvanceGround(NativeBattleEffect70Frame frame, uint address, NativeBattleEffect70Resources resources, NativeBattleEffect70Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleEffectArena caller = Point(Signed(task, 12), Signed(task, 14), Signed(task, 16)).Write(20, BitConverter.GetBytes(0)).Write(24, BitConverter.GetBytes(0x1334D5Cu)).Write(28, BitConverter.GetBytes((short)-128)).Write(32, BitConverter.GetBytes(frame.State.ReadDword(ArenaGlobal) + 0x241C));
        NativeBattleEffect70ProgrammeStep step = NativeBattleEffect70Programme.Advance(frame, address + 20, 0x100, 0x1324FF0, 0xD8, false, caller, resources, inputs);
        return new(step.Frame, step.Remaining == 0 ? 2 : 0);
    }

    private static NativeBattleEffectArena TargetCaller(NativeBattleEffect70Frame frame, uint record, NativeBattleEffect70Inputs inputs)
    {
        int actor = Actor(ReadDword(frame, record));
        NativeBattleVector position = Position(frame, actor, inputs);
        int maximum = ReadShort(frame, record + 20);
        int minimum = ReadShort(frame, record + 22);
        short y = unchecked((short)(position.Y + (maximum + minimum) / 2));
        int extent = minimum - maximum;
        return Point(position.X, y, position.Z).Write(8, BitConverter.GetBytes(extent)).Write(12, BitConverter.GetBytes(extent)).Write(16, BitConverter.GetBytes(extent)).Write(20, BitConverter.GetBytes(1)).Write(24, BitConverter.GetBytes(0x1334D5Cu)).Write(28, BitConverter.GetBytes((short)-256)).Write(32, BitConverter.GetBytes(frame.State.ReadDword(ArenaGlobal) + 0x241C));
    }

    private static NativeBattleEffect70Frame AdjustRotation(NativeBattleEffect70Frame frame, uint record, int actor, int increment)
    {
        int delta = (actor & 1) != 0 ? increment : -increment;
        frame = PutWord(frame, record + 12, unchecked((short)(ReadShort(frame, record + 12) + delta)));
        return PutWord(frame, record + 14, unchecked((short)(ReadShort(frame, record + 14) + delta)));
    }

    private static NativeBattleVector Position(NativeBattleEffect70Frame frame, int actor, NativeBattleEffect70Inputs inputs) => inputs.ReadModel(frame, actor).Transform?.Position28 ?? throw new NotSupportedException("Effect70 requires its actual current actor position.");
}
