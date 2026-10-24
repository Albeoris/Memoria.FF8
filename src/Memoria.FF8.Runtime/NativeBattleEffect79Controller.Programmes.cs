using static Memoria.FF8.Runtime.NativeBattleEffect79Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect79Controller
{
    public static NativeBattleEffect79Frame SpawnNode(NativeBattleEffect79Frame frame, uint parent, uint source, ushort delay, ushort selector, ushort mode, NativeBattleEffect79Resources resources, NativeBattleEffect79Inputs inputs)
    {
        frame = frame with { State = frame.State.BindNodeSource(source, resources) };
        (frame, uint child) = AppendHierarchy(frame, 3, 0x7B82C0, 0x2A4, parent, inputs);
        return Write(frame, child, Read(frame, child).WriteDword(0x30, source).WriteWord(0x298, delay).WriteWord(0x29E, selector).WriteWord(0x29C, mode));
    }

    public static NativeBattleEffect79Frame SpawnProgramme(NativeBattleEffect79Frame frame, uint parent, uint callback, uint source, int sizeBytes, ushort selector, NativeBattleEffect79Inputs inputs)
    {
        (frame, uint child) = AppendHierarchy(frame, 4, callback, 0x2C0, parent, inputs);
        return Write(frame, child, Read(frame, child).WriteDword(0x74, source).WriteDword(0x78, unchecked((uint)(short)sizeBytes)).WriteWord(0x80, selector).WriteWord(0x82, 0));
    }

    public static NativeBattleEffect79Step AdvanceNode(NativeBattleEffect79Frame frame, uint address, NativeBattleEffect79Resources resources, NativeBattleEffect79Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (Phase(frame, address))
        {
            case 0:
                if (Signed(task, 0x24) >= Signed(task, 0x298))
                {
                    frame = NativeBattleEffect79NodeEngine.Initialize7B8350(frame, address, resources, inputs);
                    frame = NativeBattleEffect79NodeEngine.Advance7B8E30(frame, address, resources, inputs).Frame;
                    frame = NextPhase(frame, address);
                }
                break;
            case 1:
                NativeBattleEffect79NodeStep advanced = NativeBattleEffect79NodeEngine.Advance7B8E30(frame, address, resources, inputs);
                frame = advanced.Frame;
                if (advanced.Result == 1)
                {
                    frame = NextPhase(MarkComplete(frame, address), address);
                }
                break;
            case 2:
                break;
            default:
                throw new InvalidDataException("Effect79 node phase leaves its three native handlers.");
        }
        return Finish(frame, address);
    }

    public static NativeBattleEffect79Step AdvanceProgramme(NativeBattleEffect79Frame frame, uint address, NativeBattleEffect79Resources resources, NativeBattleEffect79Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        bool followsBody = task.Callback == 0x7B80B0;
        switch (Phase(frame, address))
        {
            case 0:
                if (followsBody && Signed(task, 0x24) < 4)
                {
                    break;
                }
                frame = NativeBattleEffect79Programme.Initialize7016B0(frame, address, task.ReadDword(0x74), unchecked((int)task.ReadDword(0x78)), resources);
                task = Read(frame, address).WriteDword(0x50, 4096).WriteDword(0x54, 4096).WriteDword(0x58, 4096).WriteWord(0x88, 0xFE00);
                if (followsBody)
                {
                    task = task.WriteDword(0x1C, frame.State.ReadDword(BodyContextAddress + 0x78)).WriteDword(0x20, frame.State.ReadDword(BodyContextAddress + 0x7C));
                }
                else
                {
                    NativeBattleModelTransform transform = inputs.ReadModel(frame, task.ReadByte(0x2C)).Transform ?? throw new NotSupportedException("Effect79 requires its current caster transform.");
                    task = task.WriteWord(0x1C, unchecked((ushort)transform.Position28.X)).WriteWord(0x1E, unchecked((ushort)(transform.Position28.Y - 2048))).WriteWord(0x20, unchecked((ushort)transform.Position28.Z)).WriteWord(0x22, unchecked((ushort)transform.Vertical34));
                }
                frame = Write(frame, address, task);
                frame = NativeBattleEffect79Programme.Draw7B5C50(frame, address, resources, inputs).Frame;
                frame = NextPhase(frame, address);
                break;
            case 1:
                if (followsBody)
                {
                    frame = Write(frame, address, task.WriteDword(0x1C, frame.State.ReadDword(BodyContextAddress + 0x78)).WriteDword(0x20, frame.State.ReadDword(BodyContextAddress + 0x7C)));
                }
                NativeBattleEffect79ProgrammeStep step = NativeBattleEffect79Programme.Draw7B5C50(frame, address, resources, inputs);
                frame = step.Frame;
                if (step.Remaining == 0)
                {
                    frame = NextPhase(MarkComplete(frame, address), address);
                }
                break;
            case 2:
                break;
            default:
                throw new InvalidDataException("Effect79 programme phase leaves its three native handlers.");
        }
        return Finish(frame, address);
    }
}
