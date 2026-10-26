using static Memoria.FF8.Runtime.NativeBattleEffect218Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect218Controller
{
    public static (NativeBattleEffect218Frame Frame, int Result) AdvanceMain6328A0(NativeBattleEffect218Frame frame, uint address, uint graphicsFlags, NativeBattleEffect218Inputs inputs)
    {
        NativeBattleActionMatrix basis = NativeBattleArenaPose.ReadMatrix(frame.State.ReadView(CameraBasis, 32), 0);
        NativeBattleActionMatrix view = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), basis);
        frame = WriteMatrixFields218(frame, CurrentView, view);
        if ((graphicsFlags & 1) != 0)
        {
            return (frame, 0);
        }
        if ((graphicsFlags & 0x200) != 0)
        {
            NativeBattleEffect218IoPoll pausedPoll = inputs.PollIo508500(frame);
            frame = pausedPoll.Frame;
            if (pausedPoll.Busy)
            {
                return (frame, 0);
            }
        }
        short age = Signed(Read(frame, address), 12);
        if (age is 16 or 52 or 115 or 126 or 162)
        {
            NativeBattleEffect218IoPoll poll = inputs.PollIo508500(frame);
            frame = poll.Frame;
            if (poll.Busy)
            {
                return (frame, 0);
            }
        }
        int target = checked((int)frame.State.ReadDword(TargetGlobal));
        uint model = ModelAddress(target);
        uint ioBuffer = checked(frame.State.ReadDword(ArenaGlobal) + StreamedIoOffset);
        switch (age)
        {
            case 0:
                frame = inputs.Execute(frame, new NativeBattleEffect218Camera63E960());
                frame = inputs.Execute(frame, new NativeBattleEffect218LoadStream501860(0xF1DD50, 0x24F2344));
                NativeBattleEffect218VibrationStep vibration = inputs.StartVibration(frame);
                frame = Dword(vibration.Frame, 0x24F2340, unchecked((uint)vibration.Handle));
                (frame, _) = AppendMain632E30(frame, 0x632EB0);
                break;
            case 2:
                frame = inputs.Execute(frame, new NativeBattleEffect218Sound501330(0xE257D0));
                frame = inputs.Execute(frame, new NativeBattleEffect218QueueFile508480(705, ioBuffer));
                break;
            case 11:
                frame = SpawnPair632FF0(frame);
                frame = inputs.Execute(frame, new NativeBattleEffect218StageFlags(0xFE, 0));
                break;
            case 16:
                frame = inputs.Execute(frame, new NativeBattleEffect218UploadTim505E30(ioBuffer));
                frame = inputs.Execute(frame, new NativeBattleEffect218QueueFile508480(706, frame.State.ReadDword(ArenaGlobal)));
                break;
            case 24:
                frame = SpawnRay6331A0(frame);
                break;
            case 32:
                frame = SpawnSpin633500(frame);
                break;
            case 52:
                (frame, _) = AppendSecondary632E50(frame, 0x637350);
                (frame, _) = AppendMain632E30(frame, 0x633C30);
                (frame, _) = AppendMain632E30(frame, 0x633AC0);
                break;
            case 72:
                (frame, _) = AppendMain632E30(frame, 0x633680);
                break;
            case 92:
                frame = inputs.Execute(frame, new NativeBattleEffect218StageFlags(0xFF, 1));
                break;
            case 96:
            case 376:
                frame = inputs.Execute(frame, new NativeBattleEffect218PlayStream5018C0());
                break;
            case 101:
                frame = inputs.Execute(frame, new NativeBattleEffect218QueueFile508480(707, ioBuffer));
                break;
            case 115:
            case 126:
                frame = inputs.Execute(frame, new NativeBattleEffect218UploadTim505E30(ioBuffer));
                break;
            case 116:
                frame = inputs.Execute(frame, new NativeBattleEffect218QueueFile508480(708, ioBuffer));
                break;
            case 152:
                frame = inputs.Execute(frame, new NativeBattleEffect218QueueFile508480(709, 0x209FAB8));
                frame = inputs.Execute(frame, new NativeBattleEffect218TargetSequence4876F0(target, 0, 45));
                break;
            case 162:
                frame = inputs.Execute(frame, new NativeBattleEffect218LoadStream501860(0x209FAB8, 0x24F2344));
                break;
            case 217:
            case 282:
                frame = inputs.Execute(frame, new NativeBattleEffect218TargetSequence4876F0(target, age == 217 ? (ushort)1 : (ushort)2, 45));
                frame = frame with { State = frame.State.WriteByte(StreamedModelAddress, (byte)(frame.State.ReadByte(StreamedModelAddress) | 8)) };
                break;
            case 262:
            case 327:
            case 362:
                frame = Word(frame, StreamedModelAddress, frame.State.ReadWord(StreamedModelAddress) & 0xFFF7);
                break;
            case 347:
                frame = inputs.Execute(frame, new NativeBattleEffect218TargetSequence4876F0(target, 3, 15));
                break;
            case 366:
                frame = inputs.Execute(frame, new NativeBattleEffect218Sound501330(0xE257D4));
                break;
            case 383:
                frame = inputs.Execute(frame, new NativeBattleEffect218SelectSequence505C00(model, 55));
                frame = inputs.Execute(frame, new NativeBattleEffect218ModelFlags(model, 0xFF, 0x20));
                frame = SpawnTarget6336D0(frame, model, inputs);
                break;
            case 401:
                (frame, _) = AppendMain632E30(frame, 0x6338B0);
                frame = inputs.Execute(frame, new NativeBattleEffect218TargetSequence4876F0(target, 5, 20));
                break;
            case 504:
                frame = inputs.Execute(frame, new NativeBattleEffect218TargetSequence4876F0(target, 6, 45));
                break;
            case 559:
                frame = inputs.Execute(frame, new NativeBattleEffect218Sound501330(0xE257D8));
                break;
            case 576:
                int handle = unchecked((int)frame.State.ReadDword(0x24F2340));
                if (inputs.IsVibrationActive(frame, handle))
                {
                    frame = inputs.Execute(frame, new NativeBattleEffect218StopVibration4A2940(handle));
                }
                break;
        }
        if (age >= 362 && age < 383)
        {
            uint flags = frame.State.ReadDword(0x24F3544);
            frame = Dword(frame, 0x24F3544, age >= 370 && age <= 373 ? flags | 4 : flags & 0xFFFFFFFB);
        }
        if (Read(frame, address).ReadWord(12) == 383)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect218Contact506690());
        }
        NativeBattleModulePoolTask task = Read(frame, address);
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        frame = Write(frame, address, task);
        if (Signed(task, 12) <= 579)
        {
            return (frame, 0);
        }
        (frame, _) = AppendMain632E30(frame, 0x633BF0);
        frame = inputs.Execute(frame, new NativeBattleEffect218SelectSequence505C00(model, 1));
        frame = inputs.Execute(frame, new NativeBattleEffect218ModelFlags(model, 0xFF, 4));
        return (frame, 2);
    }

    public static NativeBattleEffect218Frame SpawnTarget6336D0(NativeBattleEffect218Frame frame, uint model, NativeBattleEffect218Inputs inputs)
    {
        (frame, uint address) = AppendRequired(frame, MainDescriptor, 0x633720);
        frame = Write(frame, address, Read(frame, address).WriteWord(12, 0));
        frame = inputs.Execute(frame, new NativeBattleEffect218CapturePoint571400(model, checked(address + 16)));
        NativeBattleModulePoolTask task = Read(frame, address);
        return Write(frame, address, task.WriteWord(20, unchecked((ushort)(task.ReadWord(20) + 400))).WriteWord(18, 0));
    }

    private static NativeBattleEffect218Frame WriteMatrixFields218(NativeBattleEffect218Frame frame, uint destination, NativeBattleActionMatrix matrix)
    {
        NativeBattleEffectArena source = PackedArena(NativeBattlePackedMatrix32.FromMatrix(matrix, null));
        frame = WriteBytes(frame, destination, source, 18);
        return WriteBytes(frame, checked(destination + 20), NativeBattleEffectNodeContextMemory.Copy(source, 20, NativeBattleEffectArena.Clear(), 0, 12), 12);
    }
}
