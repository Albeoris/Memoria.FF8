using Memoria.FF7.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private Ff7ChocoboRuntime? chocoboOwner;
    private Ff7ChocoboPresentation? chocoboPresentation;

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
        chocoboPresentation.Render(triangles, race, bounds.Width, bounds.Height);
    }

    private void DrawChocoboHud(SpriteBatch batch, Rectangle bounds, Ff7ChocoboRuntime race)
    {
        // TODO(TD-032): Complete the original race HUD and its native fade material.
        if (race.DisplayedFadeAlpha > 0)
        {
            batch.Draw(pixel, bounds, Color.Black * (Math.Min(255, race.DisplayedFadeAlpha) / 255f));
        }
    }
}
