namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public bool CanStoreNativeBattleFieldReturn => true;

    public void SetNativeBattleFieldReturn(NativeBattleFieldReturnOwners owners)
    {
        ArgumentNullException.ThrowIfNull(owners);
        owners.Music.MusicProcess.Validate();
        owners.Music.SoundProcess.Validate();
        owners.Music.Receipt.Validate();
        owners.Controls.Validate();
        owners.SeeD.Validate();
        owners.Timer.Validate();
        owners.Salary.Validate();
        owners.Vibration.Validate();
        owners.Fade.Validate();
        owners.Memory.Validate();
        NativeBattleRetainedMemory memory = State.NativeBattleMemory ?? throw new NotSupportedException("Native field return requires its actual retained battle process memory.");
        if (owners.ReloadedMemory is { } reloaded)
        {
            reloaded.Validate();
            NativeBattleRetainedMemory allowed = memory with { FileStaging = reloaded.FileStaging, EffectMemory = reloaded.EffectMemory };
            if (!System.Text.Json.JsonElement.DeepEquals(System.Text.Json.JsonSerializer.SerializeToElement(allowed), System.Text.Json.JsonSerializer.SerializeToElement(reloaded)))
            {
                throw new InvalidDataException("Native field resource reload changed an unrelated retained battle owner.");
            }
            memory = reloaded;
        }
        memory = memory with { MusicProcess = owners.Music.MusicProcess, VibrationProcess = owners.Vibration };
        memory.Validate();
        GameState candidate = State with { Battle = null, NativeBattleMemory = memory, WorldControls = owners.Controls, SeeD = owners.SeeD, ExamTimer = owners.Timer, GlobalSoundProcess = owners.Music.SoundProcess, SalaryPresentation = owners.Salary, VibrationProcess = owners.Vibration, FieldFade = owners.Fade, EventMemory = owners.Memory };
        candidate = AdvanceNativePlaytime(candidate);
        ValidateLiveEventMemoryOwners(candidate, owners.Memory);
        ValidateBattleFactsCheckpoint(candidate);
        State = candidate;
    }
}
