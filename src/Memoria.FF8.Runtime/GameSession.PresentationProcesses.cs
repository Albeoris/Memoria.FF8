using System.Text.Json;

namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public NativeGlobalSoundProcess? GlobalSoundProcess => State.GlobalSoundProcess;
    public bool CanStoreGlobalSoundProcess => true;
    public NativeSalaryPresentation? SalaryPresentation => State.SalaryPresentation;
    public bool CanStoreSalaryPresentation => true;

    public void SetGlobalSoundProcess(NativeGlobalSoundProcess state)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.Validate();
        State = State with { GlobalSoundProcess = state };
    }

    public void SetSalaryPresentation(NativeSalaryPresentation state)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.Validate();
        State = State with { SalaryPresentation = state };
    }

    private static void ValidatePresentationProcessCheckpoint(GameState state, JsonElement? currentVm)
    {
        state.GlobalSoundProcess?.Validate();
        state.SalaryPresentation?.Validate();
        if (currentVm is not JsonElement vm)
        {
            return;
        }
        if (state.Scene == SceneKind.WorldMap)
        {
            // The departing VM is frozen; current world callbacks own these persistent processes.
            // Validate its captured shapes without replacing or equating their earlier values to current globals.
            foreach (string name in new[] { "GlobalSoundProcess", "SalaryPresentation" })
            {
                if (!vm.TryGetProperty(name, out JsonElement captured))
                {
                    throw new InvalidDataException($"Departing VM lacks the current {name} shape; migrate this save explicitly.");
                }
                if (captured.ValueKind != JsonValueKind.Null)
                {
                    if (name == "GlobalSoundProcess")
                    {
                        captured.Deserialize<NativeGlobalSoundProcess>(JsonOptions)!.Validate();
                    }
                    else
                    {
                        captured.Deserialize<NativeSalaryPresentation>(JsonOptions)!.Validate();
                    }
                }
            }
            return;
        }
        foreach ((string name, JsonElement expected) in new[] { ("GlobalSoundProcess", JsonSerializer.SerializeToElement(state.GlobalSoundProcess, JsonOptions)), ("SalaryPresentation", JsonSerializer.SerializeToElement(state.SalaryPresentation, JsonOptions)) })
        {
            if (!vm.TryGetProperty(name, out JsonElement value) || !JsonElement.DeepEquals(expected, value))
            {
                throw new InvalidDataException($"Root and event {name} owners disagree or lack the current required shape; migrate this save explicitly.");
            }
        }
    }
}
