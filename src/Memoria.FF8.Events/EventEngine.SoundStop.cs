using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public sealed record NativeSoundStop(int Version, int FieldId, int ObjectId, int ScriptId, int Operation, long Frame, uint RawChannelMask, uint ChannelMask, int[] Voices, int StopMode, uint PollResult, GlobalSoundBackendStatus BackendStatus);

public sealed partial class EventEngine
{
    private string? UnsupportedSoundStop(Fiber fiber, JsmOperation operation)
    {
        if (operation.Opcode != Jsm.Opcode.SESTOP)
        {
            return null;
        }
        if (FieldId < 72 || fiber.Stack.Count == 0)
        {
            return "Native SESTOP requires its original DWORD mask in an ordinary field";
        }
        uint mask = unchecked((uint)fiber.Stack.Peek()) & 0xFFFFFF;
        if (mask == 0)
        {
            return "Native zero-mask SESTOP requires the original effect0 voice lookup; PC and operand preserved";
        }
        if (mask == 0xFFFFFF)
        {
            return "Native full-mask SESTOP requires original effect-ID authority for voices1..28; PC and operand preserved";
        }
        return null;
    }

    private static int[] SoundStopVoices(uint mask)
    {
        List<int> voices = [];
        for (int voice = 1; voice <= 24; voice++)
        {
            if ((mask & (1u << (voice - 1))) != 0)
            {
                voices.Add(voice);
            }
        }
        return voices.ToArray();
    }

    private int[] ExecuteSoundStop(Fiber fiber)
    {
        int[] args = Pop(fiber, 1);
        uint rawMask = unchecked((uint)args[0]);
        uint mask = rawMask & 0xFFFFFF;
        // Native5201A0 calls46B450(0, mask), then46C040 literally returns0.
        // Native46A0A0 stops buffers without clearing the original-effect table.
        Scene.SoundStop = new NativeSoundStop(1, FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, Frame, rawMask, mask, SoundStopVoices(mask), 0, 0, GlobalSoundBackendStatus.Muted);
        AudioCommandExecuted?.Invoke(Scene.SoundStop);
        return args;
    }

    private void ValidateSoundStopCheckpoint(Checkpoint saved)
    {
        if (saved.Scene.SoundStop is not NativeSoundStop stopped)
        {
            return;
        }
        if (stopped.Version != 1 || stopped.FieldId is < 72 or > ushort.MaxValue || stopped.ObjectId < 0 || stopped.ScriptId < 0 || stopped.Operation < 0 || stopped.Frame < 0 || stopped.ChannelMask != (stopped.RawChannelMask & 0xFFFFFF) || stopped.ChannelMask is 0 or 0xFFFFFF || stopped.Voices is null || !stopped.Voices.SequenceEqual(SoundStopVoices(stopped.ChannelMask)) || stopped.StopMode != 0 || stopped.PollResult != 0 || stopped.BackendStatus != GlobalSoundBackendStatus.Muted)
        {
            throw new InvalidDataException("Saved sound stop lost its original mask, selected voices, immediate poll or muted request provenance.");
        }
        if (stopped.FieldId == saved.FieldId)
        {
            EventScript? script = program!.Objects.FirstOrDefault(actor => actor.Index == stopped.ObjectId)?.Scripts.FirstOrDefault(script => script.Id == stopped.ScriptId);
            if (script is null || !script.ContainsOperation(stopped.Operation) || program.Operations[stopped.Operation].Opcode != Jsm.Opcode.SESTOP)
            {
                throw new InvalidDataException("Saved sound stop does not match its original field instruction.");
            }
        }
    }
}
