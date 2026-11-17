using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public enum BattleTimedInputUiPhase
{
    Opening,
    Active,
    Closing,
    Closed
}

/// <summary>Small presentation state for a timed command, independent of model resources and effect completion.</summary>
public sealed record BattleTimedInputUi(BattleTimedInputUiPhase Phase, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int Progress = 0)
{
    [JsonIgnore]
    public bool OwnsInput => Phase != BattleTimedInputUiPhase.Closed;

}
