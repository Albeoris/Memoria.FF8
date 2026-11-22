using Memoria.FF7.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private Ff7CondorRuntime? condorOwner;
    private ushort condorHeld;
    private ushort condorPressed;

    private void UpdateCondor(GameTime time)
    {
        Ff7Session session = Session!;
        Ff7CondorRuntime condor = session.Condor!;
        if (!ReferenceEquals(condorOwner, condor))
        {
            condorOwner = condor;
            condorHeld = 0;
            condorPressed = 0;
            tickRemainder = 0;
        }
        ushort input = Ff7ControlButtons.FromShared(sharedInput.HeldFieldButtons);
        input |= semanticInput.Horizontal < 0 ? (ushort)0x8000 : semanticInput.Horizontal > 0 ? (ushort)0x2000 : (ushort)0;
        input |= semanticInput.Vertical < 0 ? (ushort)0x1000 : semanticInput.Vertical > 0 ? (ushort)0x4000 : (ushort)0;
        condorPressed |= (ushort)(input & ~condorHeld);
        condorHeld = input;
        session.AdvanceClock(time.ElapsedGameTime);
        Ff7FieldTiming.Advance(sharedClock, TimeSpan.FromSeconds(Math.Min(time.ElapsedGameTime.TotalSeconds, 0.25)), TimingScene, RequiresRealTimeFieldInput, AdvanceCondor, ref tickRemainder);
        UpdateAudio();

        bool AdvanceCondor()
        {
            if (autoPlay?.Running == true && !autoPlay.Paused)
            {
                AdvanceAutoPlay();
            }
            else
            {
                session.SetCondorInput(condorHeld, condorPressed);
                AdvanceFieldTick();
            }
            condorPressed = 0;
            if (ReferenceEquals(session.Condor, condor))
            {
                return error is null;
            }
            if (session.Battle is { } battle)
            {
                EnsureMixedBattle(battle);
            }
            pendingPressedKeys = 0;
            heldKeys = Ff7ControlButtons.FromShared(sharedInput.HeldFieldButtons);
            tickRemainder = 0;
            autoPlay?.ResetIdleDeadline();
            RememberReportCheckpoint();
            return false;
        }
    }

    private void RenderCondor(Rectangle bounds)
    {
        triangles.Begin(bounds.Width, bounds.Height, new(0.025f, 0.035f, 0.05f, 1));
        triangles.End();
    }

    private void DrawCondor(SpriteBatch batch, Rectangle bounds, Ff7CondorFrame frame)
    {
        // TODO(TD-036): Replace the schematic battlefield with native Condor artwork and camera presentation.
        DrawWindow(batch, new Rectangle(2, 2, 316, 236), bounds);
        DrawPlain(batch, "Fort Condor", 12, 10, bounds);
        DrawPlain(batch, $"Gil: {frame.Funds}", 176, 10, bounds);
        string phase = frame.Phase switch
        {
            Ff7CondorPhase.Introduction => "Preparing the battlefield",
            Ff7CondorPhase.Deployment => "Deployment",
            Ff7CondorPhase.StartConfirmation => "Begin the battle?",
            Ff7CondorPhase.CombatIntroduction => "The enemy is approaching",
            Ff7CondorPhase.Combat => "Defend the shed",
            Ff7CondorPhase.Overrun => "The enemy has reached the shed",
            _ => "Returning to the shed"
        };
        DrawPlain(batch, phase, 12, 30, bounds);
        Rectangle map = Logical(new Rectangle(12, 54, 296, 146), bounds);
        batch.Draw(pixel, map, new Color(18, 30, 27));
        foreach (Ff7CondorUnitState enemy in frame.Enemies)
        {
            Vector2 position = MapPoint(enemy.X, enemy.Y);
            if (enemy.TargetX != 0 || enemy.TargetY != 0)
            {
                Vector2 destination = MapPoint(enemy.TargetX, enemy.TargetY);
                Vector2 delta = destination - position;
                float length = delta.Length();
                if (length > 0)
                {
                    batch.Draw(pixel, position, null, new Color(100, 70, 55), MathF.Atan2(delta.Y, delta.X), Vector2.Zero, new Vector2(length, Math.Max(1, bounds.Width / 320f)), SpriteEffects.None, 0);
                }
            }
            int size = Math.Max(3, bounds.Width * 3 / 320);
            batch.Draw(pixel, new Rectangle((int)position.X - size / 2, (int)position.Y - size / 2, size, size), new Color(255, 130, 90));
        }
        DrawPlain(batch, $"Enemies: {frame.Enemies.Count}", 12, 204, bounds);
        if (frame.Phase == Ff7CondorPhase.StartConfirmation)
        {
            DrawWindow(batch, new Rectangle(74, 78, 172, 62), bounds);
            DrawPlain(batch, frame.MenuSelection == 0 ? "> Start battle" : "  Start battle", 86, 90, bounds);
            DrawPlain(batch, "Select Start battle", 86, 112, bounds, 0);
        }
        else if (frame.Phase == Ff7CondorPhase.Deployment)
        {
            DrawPlain(batch, "Open the battle menu to begin", 12, 222, bounds);
        }
        else if (frame.Phase == Ff7CondorPhase.Overrun && frame.CanAcknowledge)
        {
            DrawPlain(batch, "Confirm to defend the shed", 12, 222, bounds);
        }
        else if (frame.Phase is Ff7CondorPhase.Introduction or Ff7CondorPhase.CombatIntroduction)
        {
            float progress = Math.Clamp(frame.Alpha / 256f + frame.MapY / 656f, 0, 1);
            batch.Draw(pixel, Logical(new Rectangle(12, 226, (int)(296 * progress), 3), bounds), new Color(150, 185, 150));
        }

        Vector2 MapPoint(int x, int y)
        {
            return new(map.X + Math.Clamp(x / 1024f, 0, 1) * map.Width, map.Y + Math.Clamp(y / 1024f, 0, 1) * map.Height);
        }
    }
}
