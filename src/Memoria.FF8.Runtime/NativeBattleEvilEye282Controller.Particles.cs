namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEvilEye282Controller
{
    public static (NativeBattleEvilEye282Frame Frame, int Result) AdvanceProgramme67DB20(NativeBattleEvilEye282Frame frame, uint address, NativeBattleEvilEye282Resources resources, NativeBattleEvilEye282Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleActionMatrix local = NativeBattleCuragaMatrices.Identity8DD770() with { X = Signed(task, 12), Y = Signed(task, 14), Z = Signed(task, 16) };
        NativeBattlePackedMatrix32 caller = NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local), 0);
        (frame, int remaining) = DrawProgramme67D820(frame, address, 0x18, 384, caller, resources, inputs);
        if (remaining == 0)
        {
            return (frame, 2);
        }
        if (remaining < 28 && (remaining & 3) == 0)
        {
            (frame, uint child) = Append(frame, 0x2507EA0, 0x67DC60);
            if (child != 0)
            {
                while (true)
                {
                    uint target = Read(frame, address).ReadDword(0x14);
                    frame = DrawRandom(frame, out ushort random);
                    int count = inputs.ReadBoneCount(frame, target);
                    int selected = unchecked(random * count) >> 15;
                    NativeBattleEvilEye282PointStep anchor = ReadAnchor67E550(frame, target, selected, TaskView(Read(frame, child), 12, 8), inputs);
                    frame = anchor.Frame;
                    if (anchor.Result == 0)
                    {
                        frame = WriteBytes(frame, child + 12, anchor.Point8, 6);
                        break;
                    }
                }
                frame = Write(frame, child, Read(frame, child).WriteWord(0x12, 0));
            }
        }
        return (frame, 0);
    }

    public static (NativeBattleEvilEye282Frame Frame, int Result) AdvanceSprite67DC60(NativeBattleEvilEye282Frame frame, uint address, NativeBattleEvilEye282Resources resources, NativeBattleEvilEye282Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleVector point = new(Signed(task, 12), Signed(task, 14), Signed(task, 16));
        frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), point, Signed(task, 18), 2048, -512) };
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleEffectArena context = Context(frame, temporary, 180).Write(0, BitConverter.GetBytes(0x109EAB8u)).Write(4, BitConverter.GetBytes(task.ReadWord(18))).Write(36, BitConverter.GetBytes((ushort)0));
        frame = PublishContext(frame, temporary, context, 180);
        frame = inputs.Execute(frame, new NativeBattleEvilEye282DrawSprite180(temporary, 0x109EAB8));
        frame = Release(frame, 180);
        task = Read(frame, address);
        short phase = unchecked((short)(task.ReadWord(18) + 1));
        frame = Write(frame, address, task.WriteWord(14, unchecked((ushort)(task.ReadWord(14) - 10))).WriteWord(18, unchecked((ushort)phase)));
        return (frame, phase < 16 ? 0 : 2);
    }
}
