using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public readonly record struct NativeBattleGuardianModelVisibility(ushort Flags0, ushort? Flags2);

public sealed record NativeBattleGuardianFormPresentation(string ActorId, long InvocationId, GuardianFormInvocationMode Mode, BattlePartyActorModel Actor, byte Phase, NativeBattleGraphicsModel HumanModel, NativeBattleCameraState Camera, NativeBattleStageState Stage, ImmutableDictionary<BattleModelOwner, NativeBattleGuardianModelVisibility> ModelVisibility, uint GraphicsFlags)
{
    public bool SkipRequested { get; init; }
    internal ImmutableDictionary<NativeBattleModelSlot, NativeBattleGuardianModelVisibility> UnboundVisibility { get; init; } = ImmutableDictionary<NativeBattleModelSlot, NativeBattleGuardianModelVisibility>.Empty;
    public short HumanFocalLength { get; init; }
    public bool IdleCameraRestorePending { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ActorId) || InvocationId <= 0 || !Enum.IsDefined(Mode) || Mode == GuardianFormInvocationMode.FullClassic || Actor is null || Actor.Actor.ActorId != ActorId || Phase > 5 || HumanModel is null || HumanFocalLength <= 0 || ModelVisibility is null || !ModelVisibility.ContainsKey(Actor))
        {
            throw new InvalidDataException("Saved GF presentation has an invalid invocation or restoration owner.");
        }
    }
}

public sealed partial record NativeBattleGraphicsState
{
    [JsonIgnore]
    public NativeBattleGuardianFormPresentation? GuardianFormPresentation { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public short? GuardianFormHumanFocalToRestore { get; init; }
}
