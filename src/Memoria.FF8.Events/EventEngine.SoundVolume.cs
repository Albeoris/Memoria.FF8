using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed record NativeSoundVolume(int Version, int FieldId, int ObjectId, int ScriptId, int Operation, long Frame, uint RawMask, int RawVolume, uint Mask, int Voice, byte Volume, int DurationMilliseconds, GlobalSoundBackendStatus BackendStatus, [property: System.Text.Json.Serialization.JsonRequired] long LoadEpoch, [property: System.Text.Json.Serialization.JsonRequired] NativeGlobalSoundAllocation? Allocation = null);

public sealed partial class EventEngine
{
    private string? UnsupportedSoundVolume(Fiber fiber, JsmOperation operation)
    {
        if (operation.Opcode != Jsm.Opcode.SEVOL)
        {
            return null;
        }
        if (FieldId < 72 || fiber.Stack.Count < 2)
        {
            return "SEVOL requires its two original DWORD operands in an ordinary field";
        }
        if (services.GlobalSoundProcess is null || !services.CanStoreGlobalSoundProcess)
        {
            return "SEVOL requires its current persistent global voice allocator";
        }
        return null;
    }

    private int[] ExecuteSoundVolume(Fiber fiber)
    {
        int[] prospective = fiber.Stack.Take(2).Reverse().ToArray();
        uint rawMask = unchecked((uint)prospective[0]);
        NativeGlobalSoundProcess process = services.GlobalSoundProcess!.SelectVoice(rawMask, out NativeGlobalSoundAllocation allocation);
        PublishGlobalSoundProcess(fiber, process);
        int[] args = Pop(fiber, 2);
        uint mask = rawMask & 0xFFFFFF;
        int voice = allocation.Voice;
        // Native520080 passes DWORD mask/volume to46BD70, which selects the
        // least low24 mask bit via46B0F0 and calls46A480(voice,volume&127,0).
        // TODO(TD-003):, muted audio records the
        // requested voice/volume without inventing backend buffers or gain.
        Scene.SoundVolume = new NativeSoundVolume(1, FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, Frame, rawMask, args[1], mask, voice, (byte)(args[1] & 127), 0, GlobalSoundBackendStatus.Muted, Scene.AudioLoadEpoch, allocation);
        AudioCommandExecuted?.Invoke(Scene.SoundVolume);
        return args;
    }

    private void ValidateSoundVolumeCheckpoint(Checkpoint saved)
    {
        if (saved.Scene.SoundVolume is not NativeSoundVolume request)
        {
            return;
        }
        uint mask = request.RawMask & 0xFFFFFF;
        ValidateProcessMediaLoadOrigin(saved, request.LoadEpoch, request.FieldId, request.Frame);
        if (request.Version != 1 || request.FieldId is < 72 or > ushort.MaxValue || request.ObjectId < 0 || request.ScriptId < 0 || request.Operation < 0 || request.Frame < 0 || request.Allocation is null || request.Mask != mask || request.Voice != request.Allocation.Voice || request.Volume != (request.RawVolume & 127) || request.DurationMilliseconds != 0 || request.BackendStatus != GlobalSoundBackendStatus.Muted)
        {
            throw new InvalidDataException("Saved SEVOL lost its original DWORDs, low24 mask, selected voice, seven-bit volume, immediate duration or muted backend.");
        }
        NativeGlobalSoundProcess.ValidateAllocation(request.RawMask, request.Allocation!);
        if (request.FieldId == saved.FieldId)
        {
            EventScript? script = program!.Objects.FirstOrDefault(item => item.Index == request.ObjectId)?.Scripts.FirstOrDefault(item => item.Id == request.ScriptId);
            if (script is null || request.Operation < script.Start || request.Operation >= script.End || program.Operations[request.Operation].Opcode != Jsm.Opcode.SEVOL)
            {
                throw new InvalidDataException("Saved SEVOL source does not match its original instruction.");
            }
        }
    }

    private static void ValidateProcessMediaLoadOrigin(Checkpoint saved, long sourceEpoch, int sourceField, long sourceFrame)
    {
        // Process audio survives field allocation. Its local frame belongs to
        // the executed load that issued it, even when the same field is revisited.
        if (saved.Scene.AudioLoadEpoch <= 0 || sourceEpoch <= 0 || sourceEpoch > saved.Scene.AudioLoadEpoch || sourceEpoch == saved.Scene.AudioLoadEpoch && (sourceField != saved.FieldId || sourceFrame > saved.Frame))
        {
            throw new InvalidDataException("Saved process media lost its executed load identity or belongs to a future field callback.");
        }
    }
}
