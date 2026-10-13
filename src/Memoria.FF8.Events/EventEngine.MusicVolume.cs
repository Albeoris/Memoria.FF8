using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public sealed record NativeMusicVolume(int Version, int FieldId, int ObjectId, int ScriptId, int Operation, long Frame, uint RawSlot, int Slot, int ChannelId, int RequestedVolume, byte StoredVolume, int RequestedDuration, int EffectiveDuration, bool BothChannels, GlobalSoundBackendStatus BackendStatus);

public sealed partial class EventEngine
{
    private string? UnsupportedMusicVolume(Fiber fiber)
    {
        if (fiber.Stack.Count < 2)
        {
            return "Native MUSICVOL requires its original slot and volume operands";
        }
        int slot = fiber.Stack.ElementAt(1) & 1;
        if (ReadMusicChannel(slot) is null)
        {
            return "Native MUSICVOL needs known selected channel authority before replacing B197/198; original PC and operands preserved";
        }
        return FieldId >= 72 ? UnsupportedSoundInitializationAliases() : "Native MUSICVOL requires an ordinary field";
    }

    private int[] ExecuteMusicVolume(Fiber fiber)
    {
        int[] args = Pop(fiber, 2);
        int slot = args[0] & 1;
        if (ReadMusicChannel(slot) is not NativeMusicChannelState channel)
        {
            // Preserve the historical logical-only contract when no native
            // current-byte/channel authority has ever been established.
            Scene.MusicVolume = args[1];
            return args;
        }
        byte stored = unchecked((byte)args[1]);
        SetSoundVolumeByte(197 + slot, stored);
        Scene.MusicVolumeRequest = new NativeMusicVolume(1, FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, Frame, unchecked((uint)args[0]), slot, channel.ChannelId, args[1], stored, 16, 0, unchecked((uint)channel.ChannelId) > 1, GlobalSoundBackendStatus.Muted);
        return args;
    }

    private void ValidateMusicVolumeCheckpoint(Checkpoint saved)
    {
        if (saved.Scene.MusicVolumeRequest is not NativeMusicVolume volume)
        {
            return;
        }
        if (volume.Version != 1 || volume.FieldId < 72 || volume.ObjectId < 0 || volume.ScriptId < 0 || volume.Operation < 0 || volume.Frame < 0 || volume.Slot != (volume.RawSlot & 1) || volume.StoredVolume != unchecked((byte)volume.RequestedVolume) || volume.RequestedDuration != 16 || volume.EffectiveDuration != 0 || volume.BothChannels != (unchecked((uint)volume.ChannelId) > 1) || volume.BackendStatus != GlobalSoundBackendStatus.Muted)
        {
            throw new InvalidDataException("Saved music volume request lost its original operand widths, byte write or muted backend routing.");
        }
        if (volume.FieldId == saved.FieldId)
        {
            EventScript? script = program!.Objects.FirstOrDefault(actor => actor.Index == volume.ObjectId)?.Scripts.FirstOrDefault(script => script.Id == volume.ScriptId);
            if (script is null || volume.Operation < script.Start || volume.Operation >= script.End || program.Operations[volume.Operation].Opcode != Jsm.Opcode.MUSICVOL)
            {
                throw new InvalidDataException("Saved music volume request does not match its original instruction.");
            }
        }
    }
}
