using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleGuardianFormPresentation(string ActorId, long InvocationId, GuardianFormInvocationMode Mode, byte ActorIndex, byte Phase, NativeBattleGraphicsModel HumanModel, NativeBattleCameraState Camera, NativeBattleStageState Stage, ImmutableArray<ushort> ModelVisibility, uint GraphicsFlags)
{
    public bool SkipRequested { get; init; }
    public ImmutableArray<ushort?> ModelVisibilityFlags2 { get; init; } = [];
    public short HumanFocalLength { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ActorId) || InvocationId <= 0 || !Enum.IsDefined(Mode) || Mode == GuardianFormInvocationMode.FullClassic || ActorIndex >= 3 || Phase > 5 || HumanModel.Index != ActorIndex || HumanFocalLength <= 0 || ModelVisibility.IsDefault || ModelVisibility.Length != 7 || ModelVisibilityFlags2.IsDefault || ModelVisibilityFlags2.Length != 7)
        {
            throw new InvalidDataException("Saved GF presentation has an invalid invocation or restoration owner.");
        }
    }
}

public sealed partial record NativeBattleGraphicsState
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleGuardianFormPresentation? GuardianFormPresentation { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public short? GuardianFormHumanFocalToRestore { get; init; }
}
