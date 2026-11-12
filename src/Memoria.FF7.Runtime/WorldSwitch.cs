namespace Memoria.FF7.Runtime;

public sealed record WorldSwitchAvailability(bool Allowed, string? Reason = null);

/// <summary>Preparing a destination must succeed before the active session changes.</summary>
public sealed class WorldSwitch
{
    public WorldSwitch(CombinedSessionSave state)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.Validate();
        State = state;
    }

    public CombinedSessionSave State { get; private set; }
    public SessionGame Destination => State.ActiveGame == SessionGame.Ff8 ? SessionGame.Ff7 : SessionGame.Ff8;

    public void ToFf7(WorldSwitchAvailability availability, byte[] outgoingFf8, string codec, Func<Ff7SessionSave> createNewGame, Action<Ff7SessionSave> prepareDestination)
    {
        RequireAvailable(availability, SessionGame.Ff8);
        ArgumentNullException.ThrowIfNull(outgoingFf8);
        ArgumentNullException.ThrowIfNull(createNewGame);
        ArgumentNullException.ThrowIfNull(prepareDestination);
        Ff7SessionSave target = State.Ff7 ?? createNewGame();
        target.Validate();
        CombinedSessionSave prepared = State with { ActiveGame = SessionGame.Ff7, Ff8Payload = (byte[])outgoingFf8.Clone(), Ff8PayloadFormat = codec, Ff7 = target };
        prepared.Validate();
        prepareDestination(target);
        State = prepared with { EnemyIntegration = (prepared.EnemyIntegration ?? new()) with { EnteredFromFf8 = true } };
    }

    public void ToFf8(WorldSwitchAvailability availability, Ff7SessionSave outgoingFf7, Action<byte[], string> prepareDestination)
    {
        RequireAvailable(availability, SessionGame.Ff7);
        ArgumentNullException.ThrowIfNull(outgoingFf7);
        ArgumentNullException.ThrowIfNull(prepareDestination);
        if (State.Ff8Payload is not { Length: > 0 } payload || State.Ff8PayloadFormat is not string codec)
        {
            throw new InvalidOperationException("There is no suspended FFVIII session to resume.");
        }
        outgoingFf7.Validate();
        CombinedSessionSave prepared = State with { ActiveGame = SessionGame.Ff8, Ff7 = outgoingFf7 };
        prepared.Validate();
        prepareDestination((byte[])payload.Clone(), codec);
        State = prepared;
    }

    private void RequireAvailable(WorldSwitchAvailability availability, SessionGame expected)
    {
        ArgumentNullException.ThrowIfNull(availability);
        if (State.ActiveGame != expected)
        {
            throw new InvalidOperationException("The requested switch does not leave the active game.");
        }
        if (!availability.Allowed)
        {
            throw new InvalidOperationException(availability.Reason ?? "World switching is available only where saving is allowed.");
        }
    }
}
