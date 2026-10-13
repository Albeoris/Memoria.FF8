using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public sealed record NativeMusicVolume(int Version, int FieldId, int ObjectId, int ScriptId, int Operation, long Frame, uint RawSlot, int Slot, int? ChannelId, int RequestedVolume, byte StoredVolume, int RequestedDuration, int EffectiveDuration, bool? BothChannels, GlobalSoundBackendStatus BackendStatus, [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int? TransitionDurationOperand = null);

public sealed partial class EventEngine
{
    private string? UnsupportedMusicVolume(Fiber fiber, Jsm.Opcode opcode)
    {
        if (fiber.Stack.Count < (opcode == Jsm.Opcode.MUSICVOLTRANS ? 3 : 2))
        {
            return "Native music volume requires its original slot, volume and optional transition duration operands";
        }
        return FieldId >= 72 ? UnsupportedSoundInitializationAliases() : "Native MUSICVOL requires an ordinary field";
    }

    private int[] ExecuteMusicVolume(Fiber fiber, Jsm.Opcode opcode)
    {
        int[] args = Pop(fiber, opcode == Jsm.Opcode.MUSICVOLTRANS ? 3 : 2);
        int slot = args[0] & 1;
        NativeMusicChannelState? channel = ReadMusicChannel(slot);
        int volume = args[^1];
        int? transition = opcode == Jsm.Opcode.MUSICVOLTRANS ? args[1] : null;
        int duration = transition is int requested ? unchecked((byte)(2 * requested)) : 16;
        byte stored = unchecked((byte)volume);
        // Native51FC70/51FCD0 write B197/198 unconditionally after 46BBC0; unknown muted backend routing does not change this BYTE authority.
        SetSoundVolumeByte(197 + slot, stored);
        Scene.MusicVolumeRequest = new NativeMusicVolume(1, FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, Frame, unchecked((uint)args[0]), slot, channel?.ChannelId, volume, stored, duration, duration < 50 ? 0 : duration, channel is null ? null : unchecked((uint)channel.ChannelId) > 1, GlobalSoundBackendStatus.Muted, transition);
        return args;
    }

    private void ValidateMusicVolumeCheckpoint(Checkpoint saved)
    {
        if (saved.Scene.MusicVolumeRequest is not NativeMusicVolume volume)
        {
            return;
        }
        bool? bothChannels = volume.ChannelId is int channel ? unchecked((uint)channel) > 1 : null;
        int duration = volume.TransitionDurationOperand is int requested ? unchecked((byte)(2 * requested)) : 16;
        if (volume.Version != 1 || volume.FieldId < 72 || volume.ObjectId < 0 || volume.ScriptId < 0 || volume.Operation < 0 || volume.Frame < 0 || volume.Slot != (volume.RawSlot & 1) || volume.StoredVolume != unchecked((byte)volume.RequestedVolume) || volume.RequestedDuration != duration || volume.EffectiveDuration != (duration < 50 ? 0 : duration) || volume.BothChannels != bothChannels || volume.BackendStatus != GlobalSoundBackendStatus.Muted)
        {
            throw new InvalidDataException("Saved music volume request lost its original operand widths, byte write or muted backend routing.");
        }
        if (volume.FieldId == saved.FieldId)
        {
            EventScript? script = program!.Objects.FirstOrDefault(actor => actor.Index == volume.ObjectId)?.Scripts.FirstOrDefault(script => script.Id == volume.ScriptId);
            Jsm.Opcode opcode = volume.TransitionDurationOperand is null ? Jsm.Opcode.MUSICVOL : Jsm.Opcode.MUSICVOLTRANS;
            if (script is null || volume.Operation < script.Start || volume.Operation >= script.End || program.Operations[volume.Operation].Opcode != opcode)
            {
                throw new InvalidDataException("Saved music volume request does not match its original instruction.");
            }
        }
    }
}
