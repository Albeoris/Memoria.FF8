using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect79Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect79Controller
{
    public static NativeBattleEffect79Frame InitializeIo7B50C0(NativeBattleEffect79Frame frame)
    {
        uint next = frame.State.ReadDword(0x25EB3B8);
        NativeBattleProfiledIoState initialized = NativeBattleEffect79IoProfile.Initialize(frame.State.ProgramData ?? throw new NotSupportedException("Effect79 requires its retained current script bytes."), next);
        frame = WriteIo(frame, initialized);
        return frame with { State = frame.State.WriteDword(0x25EAD84, ControlAddress).WriteDword(0x25EB3B8, checked(next + 0xA000)) };
    }

    public static NativeBattleEffect79Step AdvanceIo7B51C0(NativeBattleEffect79Frame frame, uint address, NativeBattleEffect79Resources resources, NativeBattleEffect79Inputs inputs)
    {
        NativeBattleProfiledIoAdvance advanced = NativeBattleEffect79IoProfile.Advance(Read(frame, address), ReadIo(frame));
        frame = Write(WriteIo(frame, advanced.State), address, advanced.Task);
        if (advanced.Request is { } request)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect79QueueFile48D0A0(request.FileId, request.DestinationAddress, request.CompletionCallback));
        }
        if (advanced.ReadyToComplete)
        {
            frame = frame with { Effect = frame.Effect with { Pools = new NativeBattleEffectHierarchy(frame.Effect.Pools).Complete8DC530(address).Pools } };
        }
        return new(frame, advanced.ReadyToComplete ? 2 : 0);
    }

    private static NativeBattleProfiledIoState ReadIo(NativeBattleEffect79Frame frame)
    {
        if (frame.State.ReadDword(0x25EAD84) != ControlAddress)
        {
            throw new InvalidDataException("Effect79 IO pointer does not identify its actual fixed control owner.");
        }
        return new(frame.State.Read(ControlAddress, 0x16C).ToImmutableArray(), frame.State.ProgramData ?? throw new NotSupportedException("Effect79 requires its current script owner."), frame.State.ReadDword(0x25EA348));
    }

    private static NativeBattleEffect79Frame WriteIo(NativeBattleEffect79Frame frame, NativeBattleProfiledIoState io) => frame with { State = frame.State.Write(ControlAddress, io.Control.AsSpan()).Write(ScriptAddress, io.Script.AsSpan()).WriteDword(0x25EA348, io.CursorAddress) };

    public static NativeBattleEffect79Frame CompleteFileCallback(NativeBattleEffect79Frame frame, uint callback, int result, NativeBattleEffect79Resources resources, NativeBattleEffect79Inputs inputs)
    {
        if (callback == 0x7BE1E0)
        {
            return frame with { State = frame.State.Write(0x25DF340, [1]) };
        }
        uint control = frame.State.ReadDword(0x25EAD84);
        switch (callback)
        {
            case 0x7B53C0:
                frame = inputs.Execute(frame, new NativeBattleEffect79UploadTim(frame.State.ReadDword(frame.State.ReadDword(0x25EA348) + 0xC)));
                break;
            case 0x7B53F0:
                frame = inputs.Execute(frame, new NativeBattleEffect79SoundBank501860(frame.State.ReadDword(frame.State.ReadDword(0x25EA348) + 0xC), control + 0x16A));
                break;
            case 0x7B5420:
            case 0x7B5430:
            case 0x7B5440:
            case 0x7B5450:
                break;
            default:
                throw new NotSupportedException("Effect79 requires its actual selected file completion callback.");
        }
        return frame with { State = frame.State.WriteWord(control + 0x164, 0) };
    }
}
