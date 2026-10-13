using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public sealed record NativeAllSoundVolume(int Version, int FieldId, int ObjectId, int ScriptId, int Operation, long Frame, int RawVolume, byte Volume, int DurationMilliseconds, int FirstVoice, int VoiceCount, GlobalSoundBackendStatus BackendStatus);

public sealed partial class EventEngine
{
    private string? UnsupportedAllSoundVolume(Fiber fiber, JsmOperation operation)
    {
        return operation.Opcode == Jsm.Opcode.ALLSEVOL && (FieldId < 72 || fiber.Stack.Count < 1) ? "ALLSEVOL requires its original DWORD volume in an ordinary field" : null;
    }

    private int[] ExecuteAllSoundVolume(Fiber fiber)
    {
        int[] args = Pop(fiber, 1);
        // Native51FFA0 consumes one DWORD and46B420 forwards its low seven bits
        // to46A8E0(volume,0), covering voices0..31 rather than transition1..29.
        // Volume is that request input; unknown backend master gain/buffers are
        // not inferred. TODO(TD-003):, muted audio.
        Scene.AllSoundVolume = new NativeAllSoundVolume(1, FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, Frame, args[0], (byte)(args[0] & 127), 0, 0, 32, GlobalSoundBackendStatus.Muted);
        return args;
    }

    private void ValidateAllSoundVolumeCheckpoint(Checkpoint saved)
    {
        if (saved.Scene.AllSoundVolume is not NativeAllSoundVolume request)
        {
            return;
        }
        if (request.Version != 1 || request.FieldId is < 72 or > ushort.MaxValue || request.ObjectId < 0 || request.ScriptId < 0 || request.Operation < 0 || request.Frame < 0 || request.Volume != (request.RawVolume & 127) || request.DurationMilliseconds != 0 || request.FirstVoice != 0 || request.VoiceCount != 32 || request.BackendStatus != GlobalSoundBackendStatus.Muted)
        {
            throw new InvalidDataException("Saved ALLSEVOL lost its original DWORD, seven-bit volume, immediate duration, voice range or muted backend.");
        }
        if (request.FieldId == saved.FieldId)
        {
            EventScript? script = program!.Objects.FirstOrDefault(item => item.Index == request.ObjectId)?.Scripts.FirstOrDefault(item => item.Id == request.ScriptId);
            if (request.Frame > saved.Frame || script is null || request.Operation < script.Start || request.Operation >= script.End || program.Operations[request.Operation].Opcode != Jsm.Opcode.ALLSEVOL)
            {
                throw new InvalidDataException("Saved ALLSEVOL source does not match its original instruction.");
            }
        }
    }
}
