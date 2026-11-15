using Memoria.FF7.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private Ff7ChocoboRuntime? chocoboOwner;
    private Ff7ChocoboPresentation? chocoboPresentation;
    private Ff7ChocoboHud? chocoboHud;
    private readonly CommittedPresentationHistory<(Ff7ChocoboRuntime Owner, long Tick), Ff7ChocoboSnapshot> chocoboHistory = new(
        (first, second) => ReferenceEquals(first.Owner, second.Owner) && first.Tick == second.Tick,
        (first, second) => ReferenceEquals(first.Owner, second.Owner) && second.Tick == first.Tick + 1,
        (first, second) => first.Phase == Ff7ChocoboPhase.Racing && second.Phase == Ff7ChocoboPhase.Racing && first.Countdown > 1 && first.Camera?.TargetRacer == second.Camera?.TargetRacer && first.Camera?.FinishOrbit == second.Camera?.FinishOrbit);

    private void UpdateChocobo(GameTime time)
    {
        Ff7Session session = Session!;
        Ff7ChocoboRuntime race = session.ChocoboRace!;
        if (!ReferenceEquals(chocoboOwner, race))
        {
            chocoboOwner = race;
            tickRemainder = 0;
        }
        ushort input = Ff7ControlButtons.FromShared(sharedInput.HeldFieldButtons);
        input |= semanticInput.Horizontal < 0 ? (ushort)0x8000 : semanticInput.Horizontal > 0 ? (ushort)0x2000 : (ushort)0;
        input |= semanticInput.Vertical < 0 ? (ushort)0x1000 : semanticInput.Vertical > 0 ? (ushort)0x4000 : (ushort)0;
        session.AdvanceClock(time.ElapsedGameTime);
        Ff7FieldTiming.Advance(sharedClock, TimeSpan.FromSeconds(Math.Min(time.ElapsedGameTime.TotalSeconds, 0.25)), TimingScene, RequiresRealTimeFieldInput, AdvanceChocobo, ref tickRemainder);
        UpdateAudio();

        bool AdvanceChocobo()
        {
            if (autoPlay?.Running == true && !autoPlay.Paused)
            {
                AdvanceAutoPlay();
            }
            else
            {
                session.SetChocoboInput(input);
                AdvanceFieldTick();
            }
            if (ReferenceEquals(session.ChocoboRace, race))
            {
                return error is null;
            }
            pendingPressedKeys = 0;
            heldKeys = Ff7ControlButtons.FromShared(sharedInput.HeldFieldButtons);
            tickRemainder = 0;
            autoPlay?.ResetIdleDeadline();
            RememberReportCheckpoint();
            return false;
        }
    }

    private void RenderChocobo(Ff7ChocoboRuntime race, Rectangle bounds)
    {
        chocoboPresentation ??= new(installation);
        chocoboHud ??= new(installation);
        float fraction = PresentationFraction(PresentationPreferences.FieldFps);
        var (previous, _) = chocoboHistory.Sample((race, race.Tick), fraction, race.Capture, frame => frame.Tick == race.Tick);
        chocoboPresentation.Render(triangles, race, bounds.Width, bounds.Height, appearance, chocoboHud, previous, fraction);
    }
}
