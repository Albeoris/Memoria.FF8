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
        if ((owners.CrtRandomBefore is null) != (owners.CrtRandomAfter is null) || owners.CrtRandomBefore is not null && State.CrtRandom != owners.CrtRandomBefore)
        {
            throw new InvalidDataException("Native field registration CRT receipt differs from its actual process owner.");
        }
        NativeBattleRetainedMemory memory = State.NativeBattleMemory ?? throw new NotSupportedException("Native field return requires its actual retained battle process memory.");
        NativeBattleRetainedMemory? acceptedFieldMemory = null;
        if (owners.ReloadedMemory is { } reloaded)
        {
            reloaded.Validate();
            NativeBattleRetainedMemory allowed = memory with { FileStaging = reloaded.FileStaging, EffectMemory = reloaded.EffectMemory, ModelCpuResources = reloaded.ModelCpuResources };
            // These lifecycle receipts need independent ownership metadata as well as detached bytes.
            NativeBattleRetainedMemory detachedAllowed = NativeBattleStorageSnapshots.Capture(allowed);
            NativeBattleRetainedMemory detachedReloaded = NativeBattleStorageSnapshots.Capture(reloaded);
            if (!System.Text.Json.JsonElement.DeepEquals(System.Text.Json.JsonSerializer.SerializeToElement(detachedAllowed), System.Text.Json.JsonSerializer.SerializeToElement(detachedReloaded)))
            {
                throw new InvalidDataException("Native field resource reload changed an unrelated retained battle owner.");
            }
            acceptedFieldMemory = NativeFieldEyeProcess.EnsureOwnedMemory(reloaded);
            memory = detachedReloaded;
        }
        memory = memory with { MusicProcess = owners.Music.MusicProcess, VibrationProcess = owners.Vibration };
        memory.Validate();
        GameState candidate = State with { Battle = null, NativeBattleMemory = memory, CrtRandom = owners.CrtRandomAfter ?? State.CrtRandom, WorldControls = owners.Controls, SeeD = owners.SeeD, ExamTimer = owners.Timer, GlobalSoundProcess = owners.Music.SoundProcess, SalaryPresentation = owners.Salary, VibrationProcess = owners.Vibration, FieldFade = owners.Fade, EventMemory = owners.Memory };
        candidate = AdvanceNativePlaytime(candidate);
        ValidateLiveEventMemoryOwners(candidate, owners.Memory);
        ValidateBattleFactsCheckpoint(candidate);
        State = candidate;
        liveFieldMemory = acceptedFieldMemory is null ? null : acceptedFieldMemory with { MusicProcess = owners.Music.MusicProcess, VibrationProcess = owners.Vibration };
    }
}
