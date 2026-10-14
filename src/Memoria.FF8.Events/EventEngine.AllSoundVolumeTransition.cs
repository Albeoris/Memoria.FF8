using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public sealed record NativeAllSoundVolumeTransition(int Version, int FieldId, int ObjectId, int ScriptId, int Operation, long Frame, int RawDuration, int RawVolume, byte DurationByte, int DurationMilliseconds, int DoubledVolume, byte Volume, int FirstVoice, int VoiceCount, GlobalSoundBackendStatus BackendStatus, [property: System.Text.Json.Serialization.JsonRequired] long LoadEpoch);

public sealed partial class EventEngine
{
    private string? UnsupportedAllSoundVolumeTransition(Fiber fiber, JsmOperation operation)
    {
        return operation.Opcode == Jsm.Opcode.ALLSEVOLTRANS && (FieldId < 72 || fiber.Stack.Count < 2) ? "ALLSEVOLTRANS requires its two original DWORD operands in an ordinary field" : null;
    }

    private int[] ExecuteAllSoundVolumeTransition(Fiber fiber)
    {
        int[] args = Pop(fiber, 2);
        byte duration = unchecked((byte)args[0]);
        int milliseconds = 10 * duration;
        int doubled = unchecked(2 * args[1]);
        // 51FFD0 calls46BEC0(first,2*second). That backend independently narrows
        // first to an unsigned time BYTE and masks doubled second with127.
        Scene.AllSoundVolumeTransition = new NativeAllSoundVolumeTransition(1, FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, Frame, args[0], args[1], duration, milliseconds < 50 ? 0 : milliseconds, doubled, (byte)(doubled & 127), 1, 29, GlobalSoundBackendStatus.Muted, Scene.AudioLoadEpoch);
        AudioCommandExecuted?.Invoke(Scene.AllSoundVolumeTransition);
        return args;
    }

    private void ValidateAllSoundVolumeTransitionCheckpoint(Checkpoint saved)
    {
        if (saved.Scene.AllSoundVolumeTransition is not NativeAllSoundVolumeTransition request)
        {
            return;
        }
        byte duration = unchecked((byte)request.RawDuration);
        ValidateProcessMediaLoadOrigin(saved, request.LoadEpoch, request.FieldId, request.Frame);
        int milliseconds = 10 * duration;
        int doubled = unchecked(2 * request.RawVolume);
        if (request.Version != 1 || request.FieldId < 72 || request.ObjectId < 0 || request.ScriptId < 0 || request.Operation < 0 || request.Frame < 0 || request.DurationByte != duration || request.DurationMilliseconds != (milliseconds < 50 ? 0 : milliseconds) || request.DoubledVolume != doubled || request.Volume != (doubled & 127) || request.FirstVoice != 1 || request.VoiceCount != 29 || request.BackendStatus != GlobalSoundBackendStatus.Muted)
        {
            throw new InvalidDataException("Saved ALLSEVOLTRANS lost its native operand positions, byte widths, voice range or muted backend.");
        }
        if (request.FieldId == saved.FieldId)
        {
            EventScript? script = program!.Objects.FirstOrDefault(item => item.Index == request.ObjectId)?.Scripts.FirstOrDefault(item => item.Id == request.ScriptId);
            if (script is null || request.Operation < script.Start || request.Operation >= script.End || program.Operations[request.Operation].Opcode != Jsm.Opcode.ALLSEVOLTRANS)
            {
                throw new InvalidDataException("Saved ALLSEVOLTRANS source does not match its original instruction.");
            }
        }
    }
}
