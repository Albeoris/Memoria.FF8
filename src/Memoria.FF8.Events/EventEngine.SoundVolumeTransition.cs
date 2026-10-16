using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed record NativeSoundVolumeTransition(int Version, int FieldId, int ObjectId, int ScriptId, int Operation, long Frame, uint RawMask, int RawDuration, int RawVolume, uint Mask, int Voice, byte DurationByte, int DurationMilliseconds, byte Volume, GlobalSoundBackendStatus BackendStatus, [property: System.Text.Json.Serialization.JsonRequired] long LoadEpoch, [property: System.Text.Json.Serialization.JsonRequired] NativeGlobalSoundAllocation? Allocation = null);

public sealed partial class EventEngine
{
    private string? UnsupportedSoundVolumeTransition(Fiber fiber, JsmOperation operation)
    {
        if (operation.Opcode != Jsm.Opcode.SEVOLTRANS)
        {
            return null;
        }
        if (fiber.Stack.Count < 3 || services.GlobalSoundProcess is null || !services.CanStoreGlobalSoundProcess)
        {
            return "SEVOLTRANS requires its three original DWORD operands and current persistent global allocator";
        }
        return null;
    }

    private int[] ExecuteSoundVolumeTransition(Fiber fiber)
    {
        int[] prospective = fiber.Stack.Take(3).Reverse().ToArray();
        uint raw = unchecked((uint)prospective[0]);
        NativeGlobalSoundProcess process = services.GlobalSoundProcess!.SelectVoice(raw, out NativeGlobalSoundAllocation allocation);
        PublishGlobalSoundProcess(fiber, process);
        int[] args = Pop(fiber, 3);
        uint mask = raw & 0xFFFFFF;
        int voice = allocation.Voice;
        byte duration = unchecked((byte)(2 * args[1]));
        int milliseconds = 10 * duration;
        if (milliseconds < 50)
        {
            milliseconds = 0;
        }
        Scene.SoundVolumeTransition = new NativeSoundVolumeTransition(1, FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, Frame, raw, args[1], args[2], mask, voice, duration, milliseconds, (byte)(args[2] & 127), GlobalSoundBackendStatus.Muted, Scene.AudioLoadEpoch, allocation);
        AudioCommandExecuted?.Invoke(Scene.SoundVolumeTransition);
        return args;
    }

    private void ValidateSoundVolumeTransitionCheckpoint(Checkpoint saved)
    {
        if (saved.Scene.SoundVolumeTransition is not NativeSoundVolumeTransition request)
        {
            return;
        }
        uint mask = request.RawMask & 0xFFFFFF;
        ValidateProcessMediaLoadOrigin(saved, request.LoadEpoch, request.FieldId, request.Frame);
        byte duration = unchecked((byte)(2 * request.RawDuration));
        int milliseconds = 10 * duration;
        milliseconds = milliseconds < 50 ? 0 : milliseconds;
        if (request.Version != 1 || request.FieldId is < 72 or > ushort.MaxValue || request.ObjectId < 0 || request.ScriptId < 0 || request.Operation < 0 || request.Frame < 1 || request.Allocation is null || request.Mask != mask || request.Voice != request.Allocation.Voice || request.DurationByte != duration || request.DurationMilliseconds != milliseconds || request.Volume != (request.RawVolume & 127) || request.BackendStatus != GlobalSoundBackendStatus.Muted)
        {
            throw new InvalidDataException("Saved SEVOLTRANS request lost its original voice mask, BYTE narrowing, volume or muted backend.");
        }
        NativeGlobalSoundProcess.ValidateAllocation(request.RawMask, request.Allocation!);
        if (request.FieldId == saved.FieldId)
        {
            EventScript? script = program!.Objects.FirstOrDefault(item => item.Index == request.ObjectId)?.Scripts.FirstOrDefault(item => item.Id == request.ScriptId);
            if (script is null || !script.ContainsOperation(request.Operation) || program.Operations[request.Operation].Opcode != Jsm.Opcode.SEVOLTRANS)
            {
                throw new InvalidDataException("Saved SEVOLTRANS request does not identify its executed original instruction.");
            }
        }
    }
}
