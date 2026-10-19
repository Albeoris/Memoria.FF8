using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuel274State([property: JsonRequired] bool ColdGlobalsPublished)
{
    public static NativeBattleDuel274State FromColdProcess() => new(false);
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleDuel274State? Duel274 { get; init; }
}

public sealed record NativeBattleDuel274Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleFileStaging Staging, NativeCrtRandomState Random);
public sealed record NativeBattleDuel274TaskStep(NativeBattleDuel274Frame Frame, NativeBattleEffectTaskPool Pool, int Result);

public sealed record NativeBattleDuel274ActorServices(Func<int, ushort> ReadFlags, Action<int, ushort> WriteFlags, Func<int, uint> ReadLinkedModel140, Func<int, int> ResolveMinimum501FF0, Func<int, (short Maximum, short Minimum)> ReadBounds6DFD90);
