using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class GameSession
{
    public NativeLimitProgress? LimitProgress => State.LimitProgress;

    public void SetLimitProgress(NativeLimitProgress progress)
    {
        progress.Validate();
        if (State.CharacterVitals is { } vitals && vitals.InitOutSha256 != progress.InitOutSha256)
        {
            throw new InvalidDataException("Native Limit progression and saved characters have different initialization provenance.");
        }
        State = State with { LimitProgress = progress with { TrainingCounters = progress.TrainingCounters.ToImmutableArray() } };
    }
    public void SelectAngeloLearning(int index)
    {
        if ((uint)index >= 8)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        NativeLimitProgress progress = State.LimitProgress ?? throw new NotSupportedException("Angelo learning requires saved Limit progression.");
        byte available = progress.PetPalsFlags ?? throw new NotSupportedException("Angelo learning requires saved Pet Pals availability.");
        if (((available | progress.AngeloFlags) & (1 << index)) == 0)
        {
            throw new InvalidOperationException("Choose an available Angelo ability.");
        }
        State = State with { LimitProgress = progress with { TrainingSelection = (byte)index } };
    }

    public void InitializeNewGameLimitProgress(NativeLimitProgress progress)
    {
        if (State.LimitProgress is not null || State.Scene != SceneKind.Field || State.FieldId != 74 || State.EventState is not null || State.SavedWorld?.Origin.Kind != NativeSavedWorldOriginKind.NativeNewGame)
        {
            throw new NotSupportedException("Limit progression initialization requires the untouched authentic New Game template load.");
        }
        progress.Validate();
        State = State with { LimitProgress = progress };
    }
}
