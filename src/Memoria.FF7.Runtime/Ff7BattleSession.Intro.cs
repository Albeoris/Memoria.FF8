namespace Memoria.FF7.Runtime;

public sealed record Ff7BattleIntroState(byte Callbacks = 0)
{
    public bool Complete => Callbacks == 63;
}

public sealed partial class Ff7BattleSession
{
    public bool IntroActive => state.Intro is { Complete: false };

    public bool IsActorPresentationReady(int slot)
    {
        return state.Intro is not { } intro || intro.Callbacks >= (slot >= 4 ? 6 : 7);
    }

    private void AdvanceIntroPresentation()
    {
        Ff7BattleIntroState intro = state.Intro!;
        BeginCameraPresentation();
        AdvanceCameraPresentation();
        FinishCameraPresentation();
        state = state with { Intro = intro with { Callbacks = checked((byte)(intro.Callbacks + 1)) } };
    }

    private long IdlePresentationFrame(int slot)
    {
        return state.Intro is null ? PresentationTick : Math.Max(0, PresentationTick - (slot >= 4 ? 6 : 7));
    }
}
