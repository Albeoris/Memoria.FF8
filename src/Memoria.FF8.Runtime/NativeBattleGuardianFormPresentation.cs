using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGuardianFormPresentation(string ActorId, long InvocationId, GuardianFormInvocationMode Mode, byte ActorIndex, byte Phase, NativeBattleGraphicsModel HumanModel, NativeBattleCameraState Camera, NativeBattleStageState Stage, ImmutableArray<ushort> ModelVisibility, uint GraphicsFlags)
{
    public bool SkipRequested { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ActorId) || InvocationId <= 0 || !Enum.IsDefined(Mode) || Mode == GuardianFormInvocationMode.FullClassic || ActorIndex >= 3 || Phase > 5 || HumanModel.Index != ActorIndex || ModelVisibility.IsDefault || ModelVisibility.Length != 7)
        {
            throw new InvalidDataException("Saved GF presentation has an invalid invocation or restoration owner.");
        }
    }
}

public sealed partial record NativeBattleGraphicsState
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleGuardianFormPresentation? GuardianFormPresentation { get; init; }
}
