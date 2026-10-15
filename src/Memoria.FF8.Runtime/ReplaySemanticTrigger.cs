namespace Memoria.FF8.Runtime;

public enum ReplayRecreationMode
{
    Semantic,
    Strict
}

public enum ReplaySemanticTriggerKind
{
    ActorInteraction,
    PushContact,
    DoorEntry,
    DoorAction,
    DoorCrossing,
    DoorExit,
    AreaEntry,
    FieldGateway,
    WorldFieldEntry,
    DoorFacing,
    DoorContact,
    AreaExit
}

/// <summary>An actual native admission site, never a position or a recorded state write.</summary>
public sealed record ReplaySemanticTrigger(int Version, ReplaySemanticTriggerKind Kind, int FieldId, string ProgramHash, int NativeObjectId, int ObjectId, int ScriptId, int NativeSlot, int LocalScript, int InfIndex = -1, byte EventBits = 0, bool Contact = false, bool Inferred = false, string? ExecutableHash = null, int Sequence = -1, ReplayActorApproach? Approach = null)
{
    public void Validate()
    {
        Approach?.Validate();
        if (Approach is not null && Kind != ReplaySemanticTriggerKind.ActorInteraction)
        {
            throw new InvalidDataException("Only actor interactions carry an incoming approach direction.");
        }
        if (Version != 1 || !Enum.IsDefined(Kind) || FieldId is < 0 or > ushort.MaxValue || !IsHash(ProgramHash) || ExecutableHash is not null && !IsHash(ExecutableHash) || NativeObjectId < -1 || ObjectId < -1 || ScriptId < -1 || NativeSlot is < -1 or > 7 || LocalScript < -1 || InfIndex < -1 || Sequence < -1)
        {
            throw new InvalidDataException("Invalid semantic native admission provenance.");
        }
    }

    private static bool IsHash(string? value)
    {
        return value is { Length: 64 } && value.All(Uri.IsHexDigit);
    }
}

/// <summary>A recorded script producer to reach through the current VM; it contains no old written value.</summary>
public sealed record ReplayScriptGoal(int FieldId, string ProgramHash, string? ExecutableHash, int ObjectId, int ScriptId, int Operation, int Code, bool Inferred);

public sealed record ReplayVisitExit(SceneKind Scene, int FieldId, ReplaySemanticTrigger? Trigger = null);

public sealed record ReplayCompletedVisit(int Ordinal, SceneKind Scene, int FieldId, ReplayJournalEntry? EntryFieldLoad, ReplayVisitExit Exit, ReplayJournalEntry[] Intents, bool Inferred);

public enum ReplayPersistentEffectKind
{
    SalaryProgress,
    SalaryPayment,
    WorldFuel,
    WorldDrawPointAging,
    WorldBattle,
    WorldCallbackEffects
}

public sealed record ReplayWorldAllocation(int Version, WorldTransition Packet, string ProgramHash, string ExecutableHash)
{
    public void Validate()
    {
        if (Version != 1 || Packet is null || ProgramHash is not { Length: 64 } || !ProgramHash.All(Uri.IsHexDigit) || ExecutableHash is not { Length: 64 } || !ExecutableHash.All(Uri.IsHexDigit))
        {
            throw new InvalidDataException("World allocation requires its actual committed packet and authenticated current resources.");
        }
    }
}

/// <summary>Primitive causes sampled by an actual world effect phase; no resulting saved values or travel positions.</summary>
public sealed record ReplayWorldEffectCause(byte EntityType, uint MovementUnits, int RawMagnitude, byte? FuelGroundType, int UiTicks, bool SampleSalaryAndDrawPoints)
{
    public int[] ToArguments()
    {
        return [EntityType, checked((int)MovementUnits), RawMagnitude, (int?)FuelGroundType ?? -1, UiTicks, SampleSalaryAndDrawPoints ? 1 : 0];
    }

    public static ReplayWorldEffectCause FromArguments(int[] arguments)
    {
        if (arguments.Length != 6 || arguments[0] is < 0 or > byte.MaxValue || arguments[1] is < 0 or > 5792 || arguments[2] is < 0 or > 5792 || arguments[3] is < -1 or > byte.MaxValue || arguments[4] is not (0 or 2) || arguments[5] is not (0 or 1))
        {
            throw new InvalidDataException("Invalid semantic world effect primitive causes.");
        }
        return new ReplayWorldEffectCause((byte)arguments[0], (uint)arguments[1], arguments[2], arguments[3] == -1 ? null : (byte)arguments[3], arguments[4], arguments[5] == 1);
    }
}
