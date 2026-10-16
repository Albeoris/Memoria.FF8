using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed record NativeMusicReplayRequest(int Version, int FieldId, int ObjectId, int ScriptId, int Operation, long Frame, byte Bank, byte Music, byte Volume, bool PriorKeepBit400, string ResourceOwner);

public sealed partial class EventEngine
{
    private (byte Music, string Owner) ReadNativeReplayResource(byte bank)
    {
        if (Scene.WorldMusicLoad is { Operation: NativeWorldMusicOperation.Play, Music: byte worldMusic } world && world.Bank == bank)
        {
            return (worldMusic, "WorldMusicLoad");
        }
        if (Scene.MusicPlayRequest is { ConsumedPending: true, Music: byte music } play && play.Bank == bank && play.Opcode != nameof(Jsm.Opcode.DUALMUSIC))
        {
            return (music, "MusicPlayRequest");
        }
        throw new NotSupportedException("MUSICREPLAY requires the selected native buffer's executed resource owner; a pending request or channel music byte cannot identify its contents.");
    }

    private string? UnsupportedNativeMusicReplay(JsmOperation operation)
    {
        if (operation.Opcode != Jsm.Opcode.MUSICREPLAY)
        {
            return null;
        }
        try
        {
            if (NativeRuntimeProfile is not { IsVerified: true } || Scene.MusicProcess?.Bank201 is not byte bank || bank > 1 || Scene.SoundMusicVolume197 is null || Scene.MusicChannel108 is null || services.WorldControls is not { } controls)
            {
                return "MUSICREPLAY requires authenticated current buffer, volume BYTE197, primary channel and keep-flag ownership";
            }
            controls.Validate();
            if (UnsupportedWorldControlAliases() is string aliases)
            {
                return aliases;
            }
            if (UnsupportedNativeMusicAliases(EventMemory) is string musicAliases)
            {
                return musicAliases;
            }
            if (EventMemory.Cells.Any(pair => pair.Key < 116 && (long)pair.Key + pair.Value.Width > 108 || pair.Key < 203 && (long)pair.Key + pair.Value.Width > 197))
            {
                return "MUSICREPLAY intersects a separate channel, volume or bank memory alias";
            }
            _ = ReadNativeReplayResource(bank);
            return null;
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidDataException)
        {
            return exception.Message;
        }
    }

    private void ExecuteNativeMusicReplay(Fiber fiber)
    {
        byte bank = Scene.MusicProcess!.Bank201!.Value;
        (byte music, string owner) = ReadNativeReplayResource(bank);
        NativeWorldControlState controls = services.WorldControls!;
        bool priorKeep = (controls.EventFlags104 & 0x400) != 0;
        SetNativeWorldControls(fiber, controls with { MusicKeep207 = priorKeep ? (byte)0 : (byte)1, EventFlags104 = controls.EventFlags104 & ~0x400u });
        //51F880 replaces DWORD108 but does not write the retained music BYTE199.
        Scene.MusicChannel108 = Scene.MusicChannel108! with { ChannelId = 0 };
        Scene.MusicPlaying = true;
        Scene.MusicReplayRequest = new(1, FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, Frame, bank, music, Scene.SoundMusicVolume197!.Value, priorKeep, owner);
        AudioCommandExecuted?.Invoke(Scene.MusicReplayRequest);
        WaitFrames(fiber, 1);
    }

    private void ValidateNativeMusicReplayCheckpoint(Checkpoint saved)
    {
        if (saved.Scene.MusicReplayRequest is not { } replay)
        {
            return;
        }
        if (replay.Version != 1 || replay.Bank > 1 || replay.ResourceOwner is not ("MusicPlayRequest" or "WorldMusicLoad"))
        {
            throw new InvalidDataException("Saved MUSICREPLAY lost its selected native buffer/resource provenance.");
        }
        // Frame is local to each field. A later field's play replaces this receipt's
        // original resource owner and cannot be ordered against its old frame.
        if (replay.ResourceOwner == "MusicPlayRequest" && saved.Scene.MusicPlayRequest is { } play && play.FieldId == replay.FieldId && play.Frame <= replay.Frame && (play.Bank != replay.Bank || play.Music != replay.Music || !play.ConsumedPending || play.Opcode == nameof(Jsm.Opcode.DUALMUSIC)))
        {
            throw new InvalidDataException("Saved MUSICREPLAY disagrees with its retained executed buffer resource.");
        }
        ValidateMusicRequestSite(saved, replay.FieldId, replay.ObjectId, replay.ScriptId, replay.Operation, replay.Frame, Jsm.Opcode.MUSICREPLAY);
    }
}
