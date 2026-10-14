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
        State = State with { SeeD = state };
    }

    private static NativeSeeDState? AdvanceWorldSeeD(GameState departure, WorldState finalWorld, uint salaryMovementUnits)
    {
        long callbacks = finalWorld.Frame - departure.World!.Frame;
        if (callbacks < 0 || salaryMovementUnits > (ulong)callbacks * 5792 || finalWorld.NativeBoatRuntime is not null && salaryMovementUnits != 0)
        {
            throw new InvalidDataException("World salary units exceed the executed native callbacks or scripted boat caller contract.");
        }
        if (departure.SeeD is null || callbacks == 0 || finalWorld.NativeBoatRuntime is not null)
        {
            return departure.SeeD;
        }
        if (finalWorld.NativeFootRuntime is null && finalWorld.NativeCarRuntime is null)
        {
            throw new NotSupportedException("World salary requires its current eligible native body producer.");
        }
        return departure.SeeD?.WithMovementUnits(salaryMovementUnits);
    }

    private static void ValidateSeeDCheckpoint(GameState state, JsonElement? vm)
    {
        state.SeeD?.Validate();
        if (vm is not JsonElement saved)
        {
            return;
        }
        NativeSeeDState? captured = saved.TryGetProperty("SeeD", out JsonElement value) && value.ValueKind != JsonValueKind.Null ? value.Deserialize<NativeSeeDState>(JsonOptions) : null;
        captured?.Validate();
        if (captured != state.SeeD)
        {
            throw new InvalidDataException("Root and event SeeD rank/salary checkpoints disagree; recreate the authentic journal.");
        }
    }
}
