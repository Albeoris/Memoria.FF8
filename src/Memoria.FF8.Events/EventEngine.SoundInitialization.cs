using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public sealed record NativeSoundInitialization(int Version, int FieldId, int ObjectId, int ScriptId, int Operation, long Frame, byte MusicVolume197, byte MusicVolume198, byte SoundVolume, int StopFirstVoice, int StopVoiceCount, int PanFirstVoice, int PanVoiceCount, byte Pan, GlobalSoundBackendStatus BackendStatus);

public sealed partial class EventEngine
{
    private string? UnsupportedSoundInitializationAliases()
    {
        foreach ((int address, int value) in variables)
        {
            if (address is < 194 or > 198)
            {
                continue;
            }
            if (address <= 196 && Scene.NativeActorLayoutVersion == 1 && Scene.FieldPartyNativeActorIds.Count == 3 && value == Scene.FieldPartyNativeActorIds[address - 194])
            {
                // These canonical aliases are proved BYTE party indices.
                continue;
            }
            if (TryReadSoundVolumeByte(address, out int known) && value == known)
            {
                continue;
            }
            return "Native sound initialization intersects a bare mixed-width alias near B197/198; original PC and state preserved";
        }
        return null;
    }

    private string? UnsupportedSoundInitializationAccess(Fiber fiber, JsmOperation operation)
    {
        if (operation.Opcode == Jsm.Opcode.INITSOUND)
        {
            return FieldId >= 72 ? UnsupportedSoundInitializationAliases() : "Native INITSOUND requires an ordinary field";
        }
        if (operation.Opcode == Jsm.Opcode.MUSICVOL && (Scene.SoundMusicVolume197 is not null || Scene.SoundMusicVolume198 is not null || Scene.MusicChannel108 is not null || Scene.MusicChannel112 is not null))
        {
            return UnsupportedMusicVolume(fiber);
        }
        if (operation.Opcode == Jsm.Opcode.MUSICVOLTRANS && (Scene.SoundMusicVolume197 is not null || Scene.SoundMusicVolume198 is not null))
        {
            return "Music volume command requires its original channel-to-B197/198 write contract before replacing known sound bytes";
        }
        int width = operation.Opcode switch
        {
            Jsm.Opcode.PSHM_B or Jsm.Opcode.POPM_B => 1,
            Jsm.Opcode.PSHM_W or Jsm.Opcode.PSHSM_W or Jsm.Opcode.POPM_W => 2,
            Jsm.Opcode.PSHM_L or Jsm.Opcode.POPM_L => 4,
            _ => 0
        };
        if (width == 0 || operation.Parameter > 198 || (long)operation.Parameter + width - 1 < 197)
        {
            return null;
        }
        if (width != 1 || operation.Opcode == Jsm.Opcode.PSHM_B && !TryReadSoundVolumeByte(operation.Parameter, out _))
        {
            return "Native sound volume access requires a known exact B197/198; overlapping or unknown widths preserve PC and operands";
        }
        return UnsupportedSoundInitializationAliases();
    }

    private bool TryReadSoundVolumeByte(int address, out int value)
    {
        byte? known = address == 197 ? Scene.SoundMusicVolume197 : address == 198 ? Scene.SoundMusicVolume198 : null;
        value = known.GetValueOrDefault();
        return known is not null;
    }

    private void SetSoundVolumeByte(int address, byte value)
    {
        if (address == 197)
        {
            Scene.SoundMusicVolume197 = value;
            Scene.MusicVolume = value;
        }
        else
        {
            Scene.SoundMusicVolume198 = value;
        }
        variables[address] = value;
    }

    private void ExecuteSoundInitialization(Fiber fiber)
    {
        SetSoundVolumeByte(197, 127);
        SetSoundVolumeByte(198, 127);
        Scene.SoundInitialization = new NativeSoundInitialization(1, FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, Frame, 127, 127, 127, 0, 32, 1, 29, 64, GlobalSoundBackendStatus.Muted);
        // This records the installed calls on an explicit muted backend. It
        // neither invents active music channel IDs nor erases effect history.
    }

    private void ValidateSoundInitializationCheckpoint(Checkpoint saved)
    {
        SceneEventState scene = saved.Scene;
        foreach (int address in new[] { 197, 198 })
        {
            byte? known = address == 197 ? scene.SoundMusicVolume197 : scene.SoundMusicVolume198;
            if (known is byte volume && (saved.Variables.TryGetValue(address, out int alias) && alias != volume || address == 197 && scene.MusicVolume != volume))
            {
                throw new InvalidDataException("Saved sound volume bytes disagree with their canonical byte aliases or logical music volume.");
            }
        }
        if (scene.SoundInitialization is not NativeSoundInitialization reset)
        {
            return;
        }
        if (reset.Version != 1 || reset.FieldId < 72 || reset.ObjectId < 0 || reset.ScriptId < 0 || reset.Operation < 0 || reset.Frame < 0 || reset.MusicVolume197 != 127 || reset.MusicVolume198 != 127 || reset.SoundVolume != 127 || reset.StopFirstVoice != 0 || reset.StopVoiceCount != 32 || reset.PanFirstVoice != 1 || reset.PanVoiceCount != 29 || reset.Pan != 64 || reset.BackendStatus != GlobalSoundBackendStatus.Muted || scene.SoundMusicVolume197 is null || scene.SoundMusicVolume198 is null)
        {
            throw new InvalidDataException("Saved native sound initialization lost its original reset ranges, bytes or muted request provenance.");
        }
        if (reset.FieldId == saved.FieldId)
        {
            EventScript? script = program!.Objects.FirstOrDefault(actor => actor.Index == reset.ObjectId)?.Scripts.FirstOrDefault(script => script.Id == reset.ScriptId);
            // Last reset survives field reloads, whose local frame starts at0.
            if (script is null || reset.Operation < script.Start || reset.Operation >= script.End || program.Operations[reset.Operation].Opcode != Jsm.Opcode.INITSOUND)
            {
                throw new InvalidDataException("Saved sound initialization does not match its original field instruction.");
            }
        }
    }
}
