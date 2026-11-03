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

    public BattleTimedInputUi Advance(int ticksPerSecond)
    {
        if (Phase is BattleTimedInputUiPhase.Active or BattleTimedInputUiPhase.Closed)
        {
            return this;
        }
        int duration = Math.Max(1, ticksPerSecond / (Phase == BattleTimedInputUiPhase.Opening ? 10 : 5));
        int progress = Progress + 1;
        return progress >= duration
            ? new(Phase == BattleTimedInputUiPhase.Opening ? BattleTimedInputUiPhase.Active : BattleTimedInputUiPhase.Closed)
            : this with { Progress = progress };
    }

    public float Reveal(int ticksPerSecond)
    {
        return Phase switch
        {
            BattleTimedInputUiPhase.Opening => Math.Clamp(Progress / (float)Math.Max(1, ticksPerSecond / 10), 0, 1),
            BattleTimedInputUiPhase.Closing => 1 - Math.Clamp(Progress / (float)Math.Max(1, ticksPerSecond / 5), 0, 1),
            BattleTimedInputUiPhase.Closed => 0,
            _ => 1
        };
    }
}
