using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuel274State([property: JsonRequired] bool ColdGlobalsPublished, [property: JsonRequired] bool MutableImagePublished = false)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleDuel274Resources? Resources { get; init; }

    public static NativeBattleDuel274State FromColdProcess() => new(false, false);
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleDuel274State? Duel274
    {
        get => effectState.Duel274;
        init => effectState = effectState with { Duel274 = value };
    }
}

public sealed record NativeBattleDuel274Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleFileStaging Staging, NativeCrtRandomState Random)
{
    [JsonIgnore]
    public bool CapturePresentation { get; init; }

    [JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleDuel274TaskStep(NativeBattleDuel274Frame Frame, NativeBattleEffectTaskPool Pool, int Result);

public sealed record NativeBattleDuel274ActorServices(NativeBattleModelAliases Aliases, Func<BattleModelOwner, ushort> ReadFlags, Action<BattleModelOwner, ushort> WriteFlags, Func<BattleModelOwner, BattleModelOwner?> ReadLinkedModel140, Func<BattleModelOwner, BattleModelOwner> ResolveMinimum501FF0, Func<BattleModelOwner, (short Maximum, short Minimum)> ReadBounds6DFD90)
{
    public Func<BattleModelOwner, byte, bool>? IsNativeModel { get; init; }
}

public sealed partial record NativeBattleOriginalEffectState
{
    [JsonIgnore]
    internal ImmutableDictionary<BattleModelOwner, ushort> Duel274SavedFlags { get; init; } = ImmutableDictionary<BattleModelOwner, ushort>.Empty;

    [JsonIgnore]
    internal ImmutableDictionary<BattleModelOwner, short> Duel274SavedPartyZ { get; init; } = ImmutableDictionary<BattleModelOwner, short>.Empty;
}
