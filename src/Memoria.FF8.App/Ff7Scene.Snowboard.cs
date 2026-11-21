using Memoria.FF7.Runtime;
using Microsoft.Xna.Framework;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private Ff7SnowboardRuntime? snowboardOwner;
    private Ff7SnowboardPresentation? snowboardPresentation;
    private ushort snowboardHeld;
    private ushort snowboardPressed;

    internal object? SnowboardStatus => Session?.Snowboard is { } snowboard ? new { snowboard.Frame.Tick, Phase = snowboard.Frame.Phase.ToString(), snowboard.Frame.Course, snowboard.Frame.Segment, snowboard.Frame.Speed } : null;

    private void UpdateSnowboard(GameTime time)
    {
        Ff7Session session = Session!;
        Ff7SnowboardRuntime snowboard = session.Snowboard!;
        if (!ReferenceEquals(snowboardOwner, snowboard))
        {
            snowboardOwner = snowboard;
            snowboardHeld = 0;
            snowboardPressed = 0;
            tickRemainder = 0;
        }
        ushort input = Ff7ControlButtons.FromShared(sharedInput.HeldFieldButtons);
        input |= semanticInput.Horizontal < 0 ? (ushort)0x8000 : semanticInput.Horizontal > 0 ? (ushort)0x2000 : (ushort)0;
        input |= semanticInput.Vertical < 0 ? (ushort)0x1000 : semanticInput.Vertical > 0 ? (ushort)0x4000 : (ushort)0;
        snowboardPressed |= (ushort)(input & ~snowboardHeld);
        snowboardHeld = input;
        session.AdvanceClock(time.ElapsedGameTime);
        Ff7FieldTiming.Advance(sharedClock, TimeSpan.FromSeconds(Math.Min(time.ElapsedGameTime.TotalSeconds, 0.25)), TimingScene, RequiresRealTimeFieldInput, AdvanceSnowboard, ref tickRemainder);
        UpdateAudio();

        bool AdvanceSnowboard()
        {
            if (autoPlay?.Running == true && !autoPlay.Paused)
            {
                AdvanceAutoPlay();
            }
            else
            {
                session.SetSnowboardInput(snowboardHeld, snowboardPressed);
                AdvanceFieldTick();
            }
            snowboardPressed = 0;
            if (ReferenceEquals(session.Snowboard, snowboard))
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

    private void RenderSnowboard(Ff7SnowboardRuntime snowboard, Rectangle bounds)
    {
        snowboardPresentation ??= new(installation);
        Memoria.FF8.Runtime.PixelBounds fit = Memoria.FF8.Runtime.ViewportLayout.Fit(4, 3, bounds.Width, bounds.Height);
        snowboardPresentation.Render(triangles, snowboard.Frame, fit.Width, fit.Height);
    }
}
