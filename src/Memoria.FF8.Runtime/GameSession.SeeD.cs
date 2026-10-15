using System.Text.Json;

namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public NativeSeeDState? SeeD => State.SeeD;
    public bool CanStoreSeeD => true;

    public void SetSeeD(NativeSeeDState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.Validate();
        ValidateLiveEventMemoryOwners(State with { SeeD = state }, State.EventMemory);
        State = State with { SeeD = state };
    }

    public static NativeSalaryResult? SampleWorldSalary(GameState source, NativeSeeDState? current, NativeEconomy? economy, uint units)
    {
        if (current is null)
        {
            return null;
        }
        if (source.WorldControls is not NativeWorldControlState controls)
        {
            throw new NotSupportedException("World salary requires the executed event DWORD104 owner.");
        }
        return NativeSalaryCalculator.Sample(current, economy, source.CharacterVitals, units, controls.EventFlags104);
    }

    private static void ValidateSeeDCheckpoint(GameState state, JsonElement? vm)
    {
        state.SeeD?.Validate();
        if (state.SeeD is not null)
        {
            ValidateSeeDAliases(state.EventMemory);
        }
        if (vm is not JsonElement saved)
        {
            return;
        }
        NativeSeeDState? captured = saved.TryGetProperty("SeeD", out JsonElement value) && value.ValueKind != JsonValueKind.Null ? value.Deserialize<NativeSeeDState>(JsonOptions) : null;
        captured?.Validate();
        if (captured is not null && saved.TryGetProperty("EventMemory", out JsonElement memory) && memory.ValueKind != JsonValueKind.Null)
        {
            NativeEventMemory aliases = memory.Deserialize<NativeEventMemory>(JsonOptions) ?? throw new InvalidDataException("Empty typed event-memory metadata.");
            if (aliases.Cells.Any(pair => OverlapsSeeDMemory(pair.Key, pair.Value.Width)))
            {
                throw new InvalidDataException("Canonical SeeD words overlap a typed VM alias; migrate this save explicitly.");
            }
        }
        if (captured != state.SeeD)
        {
            throw new InvalidDataException("Root and event SeeD rank/salary checkpoints disagree; migrate this save explicitly.");
        }
    }

    private static void ValidateSeeDAliases(NativeEventMemory memory)
    {
        if (memory.Cells.Any(pair => OverlapsSeeDMemory(pair.Key, pair.Value.Width)))
        {
            throw new InvalidDataException("Canonical SeeD event words overlap a typed root variable alias; migrate this save explicitly.");
        }
    }

    private static bool OverlapsSeeDMemory(int address, int width)
    {
        long end = (long)address + width;
        return address < 12 && end > 8 || address < 20 && end > 16 || address < 188 && end > 184;
    }
}
