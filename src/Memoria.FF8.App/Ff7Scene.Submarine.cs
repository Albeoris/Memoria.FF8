using Memoria.FF7.Runtime;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private Ff7SubmarineRuntime? submarineOwner;
    private ushort submarineHeld;
    private ushort submarinePressed;

    private void UpdateSubmarine(GameTime time)
    {
        Ff7Session session = Session!;
        Ff7SubmarineRuntime submarine = session.Submarine!;
        if (!ReferenceEquals(submarineOwner, submarine))
        {
            submarineOwner = submarine;
            submarineHeld = 0;
            submarinePressed = 0;
            tickRemainder = 0;
        }
        ushort input = Ff7ControlButtons.FromShared(sharedInput.HeldFieldButtons);
        input |= semanticInput.Horizontal < 0 ? (ushort)0x8000 : semanticInput.Horizontal > 0 ? (ushort)0x2000 : (ushort)0;
        input |= semanticInput.Vertical < 0 ? (ushort)0x1000 : semanticInput.Vertical > 0 ? (ushort)0x4000 : (ushort)0;
        submarinePressed |= (ushort)(input & ~submarineHeld);
        submarineHeld = input;
        session.AdvanceClock(time.ElapsedGameTime);
        Ff7FieldTiming.Advance(sharedClock, TimeSpan.FromSeconds(Math.Min(time.ElapsedGameTime.TotalSeconds, 0.25)), TimingScene, RequiresRealTimeFieldInput, AdvanceSubmarine, ref tickRemainder);
        UpdateAudio();

        bool AdvanceSubmarine()
        {
            if (autoPlay?.Running == true && !autoPlay.Paused)
            {
                AdvanceAutoPlay();
                if (error is not null || autoPlay.Paused)
                {
                    return false;
                }
            }
            else
            {
                session.SetSubmarineInput(submarineHeld, submarinePressed);
                AdvanceFieldTick();
            }
            submarinePressed = 0;
            if (ReferenceEquals(session.Submarine, submarine))
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

    private void RenderSubmarine(Rectangle bounds)
    {
        triangles.Begin(bounds.Width, bounds.Height, new System.Numerics.Vector4(0.015f, 0.035f, 0.07f, 1));
        triangles.End();
    }

    private void DrawSubmarine(SpriteBatch batch, Rectangle bounds, Ff7SubmarineFrame frame)
    {
        // TODO(TD-038): Restore the native submarine artwork, terrain view and cockpit presentation.
        int width = Math.Min(bounds.Width, bounds.Height * 4 / 3);
        int height = Math.Min(bounds.Height, bounds.Width * 3 / 4);
        bounds = new Rectangle(bounds.X + (bounds.Width - width) / 2, bounds.Y + (bounds.Height - height) / 2, width, height);
        DrawWindow(batch, new Rectangle(2, 2, 316, 236), bounds);
        DrawPlain(batch, "Submarine", 12, 9, bounds);
        int seconds = Math.Max(0, frame.RemainingFrames) / 60;
        DrawPlain(batch, $"Time {seconds / 60:00}:{seconds % 60:00}", 204, 9, bounds);
        DrawPlain(batch, $"Hull {Math.Max(0, frame.Player.Health)}", 12, 28, bounds);
        Ff7SubmarineVesselFrame? objective = frame.Ships.FirstOrDefault(ship => ship.Objective);
        if (objective is not null)
        {
            DrawPlain(batch, $"Leader {Math.Max(0, objective.Health)}", 164, 28, bounds);
        }
        Rectangle map = Logical(new Rectangle(12, 49, 296, 151), bounds);
        batch.Draw(pixel, map, new Color(5, 23, 41));
        for (int line = 1; line < 8; line++)
        {
            batch.Draw(pixel, new Rectangle(map.X + map.Width * line / 8, map.Y, 1, map.Height), new Color(15, 47, 62));
            batch.Draw(pixel, new Rectangle(map.X, map.Y + map.Height * line / 8, map.Width, 1), new Color(15, 47, 62));
        }
        foreach (Ff7SubmarineVesselFrame ship in frame.Ships)
        {
            if (ship.Slot == frame.Player.Slot || ship.Health <= 0)
            {
                continue;
            }
            Vector2 point = MapPoint(ship.X4096, ship.Z4096);
            int size = Math.Max(3, bounds.Width * (ship.Objective ? 5 : 3) / 320);
            batch.Draw(pixel, new Rectangle((int)point.X - size / 2, (int)point.Y - size / 2, size, size), ship.Objective ? new Color(255, 105, 80) : new Color(184, 138, 126));
        }
        foreach (Ff7SubmarineTorpedoFrame torpedo in frame.Torpedoes)
        {
            Vector2 point = MapPoint(torpedo.X4096, torpedo.Z4096);
            int size = Math.Max(2, bounds.Width * 2 / 320);
            batch.Draw(pixel, new Rectangle((int)point.X, (int)point.Y, size, size), new Color(255, 220, 125));
        }
        Vector2 player = MapPoint(frame.Player.X4096, frame.Player.Z4096);
        float angle = frame.Player.Heading * (MathF.Tau / 4096f);
        Vector2 forward = new(MathF.Sin(angle), MathF.Cos(angle));
        Vector2 right = new(forward.Y, -forward.X);
        float radius = Math.Max(4, bounds.Width * 5 / 320f);
        DrawSegment(player + forward * radius, player - forward * radius + right * radius * 0.65f, Color.White);
        DrawSegment(player - forward * radius + right * radius * 0.65f, player - forward * radius - right * radius * 0.65f, Color.White);
        DrawSegment(player - forward * radius - right * radius * 0.65f, player + forward * radius, Color.White);
        string status = frame.Phase switch
        {
            Ff7SubmarinePhase.Introduction => "Mission ready",
            Ff7SubmarinePhase.Playing => "Sink the Red Leader",
            Ff7SubmarinePhase.Result when frame.Outcome == 1 => "Mission complete",
            Ff7SubmarinePhase.Result => "Mission ended",
            _ => "Returning to the vessel"
        };
        DrawPlain(batch, status, 12, 205, bounds);
        DrawPlain(batch, frame.CanAcknowledge ? "Continue" : "Steer / Dive / Fire", 12, 223, bounds);

        Vector2 MapPoint(int x, int z)
        {
            return new(map.X + Math.Clamp((x / 4096f + 2048) / 4096, 0, 1) * map.Width, map.Y + Math.Clamp((z / 4096f + 2048) / 4096, 0, 1) * map.Height);
        }

        void DrawSegment(Vector2 start, Vector2 end, Color color)
        {
            Vector2 delta = end - start;
            batch.Draw(pixel, start, null, color, MathF.Atan2(delta.Y, delta.X), Vector2.Zero, new Vector2(delta.Length(), Math.Max(1, bounds.Width / 320f)), SpriteEffects.None, 0);
        }
    }
}
