using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed record NativeSoundPosition(int Version, string Opcode, int FieldId, int ObjectId, int ScriptId, int Operation, long Frame, uint RawMask, int? RawDuration, int RawPan, uint Mask, int Voice, byte DurationByte, int DurationMilliseconds, byte Pan, GlobalSoundBackendStatus BackendStatus, [property: System.Text.Json.Serialization.JsonRequired] long LoadEpoch, [property: System.Text.Json.Serialization.JsonRequired] NativeGlobalSoundAllocation Allocation);

public sealed partial class EventEngine
{
    private string? UnsupportedSoundPosition(Fiber fiber, JsmOperation operation)
    {
        if (operation.Opcode is not (Jsm.Opcode.SEPOS or Jsm.Opcode.SEPOSTRANS))
        {
            return null;
        }
        int count = operation.Opcode == Jsm.Opcode.SEPOSTRANS ? 3 : 2;
        if (FieldId < 72 || fiber.Stack.Count < count || fiber.Stack.Capture().Take(count).Any(value => !value.IsExact))
        {
            return "Native sound position requires its complete exact consumed DWORD operands in an ordinary field";
        }
        if (services.GlobalSoundProcess is not NativeGlobalSoundProcess process || !services.CanStoreGlobalSoundProcess)
        {
            return "Native sound position requires the actual persistent global voice allocator";
        }
        process.Validate();
        return null;
    }

    private int[] ExecuteSoundPosition(Fiber fiber, bool transition)
    {
        int count = transition ? 3 : 2;
        int[] prospective = fiber.Stack.Take(count).Reverse().ToArray();
        uint rawMask = unchecked((uint)prospective[0]);
        NativeGlobalSoundProcess next = services.GlobalSoundProcess!.SelectVoice(rawMask, out NativeGlobalSoundAllocation allocation);
        PublishGlobalSoundProcess(fiber, next);
        int[] args = Pop(fiber, count);
        int? rawDuration = transition ? args[1] : null;
        byte duration = transition ? unchecked((byte)(2 * args[1])) : (byte)0;
        int milliseconds = SoundPositionDuration(duration);
        byte pan = (byte)(unchecked((byte)args[^1]) >> 1);
        // Native520110/520150 return2 even when46A9E0 has no backend buffer.
        // The saved bufferless process owns allocation, never an invented native pan ramp.
        Scene.SoundPosition = new NativeSoundPosition(1, transition ? "SEPOSTRANS" : "SEPOS", FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, Frame, rawMask, rawDuration, args[^1], rawMask & 0xFFFFFF, allocation.Voice, duration, milliseconds, pan, GlobalSoundBackendStatus.Muted, Scene.AudioLoadEpoch, allocation);
        AudioCommandExecuted?.Invoke(Scene.SoundPosition);
        return args;
    }

    private static int SoundPositionDuration(byte duration)
    {
        int milliseconds = 10 * duration;
        return milliseconds < 50 ? 0 : milliseconds;
    }

    private void ValidateSoundPositionCheckpoint(Checkpoint saved)
    {
        if (saved.Scene.SoundPosition is not NativeSoundPosition request)
        {
            return;
        }
        bool transition = request.Opcode == "SEPOSTRANS";
        byte duration = transition && request.RawDuration is int raw ? unchecked((byte)(2 * raw)) : (byte)0;
        ValidateProcessMediaLoadOrigin(saved, request.LoadEpoch, request.FieldId, request.Frame);
        if (request.Version != 1 || request.Opcode is not ("SEPOS" or "SEPOSTRANS") || transition != request.RawDuration.HasValue || request.FieldId is < 72 or > ushort.MaxValue || request.ObjectId < 0 || request.ScriptId < 0 || request.Operation < 0 || request.Frame < 0 || request.Mask != (request.RawMask & 0xFFFFFF) || request.Allocation is null || request.Voice != request.Allocation.Voice || request.DurationByte != duration || request.DurationMilliseconds != SoundPositionDuration(duration) || request.Pan != unchecked((byte)request.RawPan) >> 1 || request.BackendStatus != GlobalSoundBackendStatus.Muted)
        {
            throw new InvalidDataException("Saved native sound position lost original DWORDs, low24 allocation, BYTE pan/duration or explicit backend provenance.");
        }
        NativeGlobalSoundProcess.ValidateAllocation(request.RawMask, request.Allocation);
        if (request.FieldId == saved.FieldId)
        {
            EventScript? script = program!.Objects.FirstOrDefault(item => item.Index == request.ObjectId)?.Scripts.FirstOrDefault(item => item.Id == request.ScriptId);
            if (script is null || !script.ContainsOperation(request.Operation) || program.Operations[request.Operation].Opcode.ToString() != request.Opcode)
            {
                throw new InvalidDataException("Saved sound position does not identify its executed original instruction.");
            }
        }
    }
}
