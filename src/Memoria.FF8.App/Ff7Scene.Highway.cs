using Memoria.FF7.Runtime;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private Ff7HighwayRuntime? highwayOwner;
    private Ff7HighwayPresentation? highwayPresentation;
    private Texture2D? highwayGaugeTexture;
    private Texture2D? highwayPortraitTexture;
    private static readonly Rectangle[] HighwayGaugeBounds = [new(116, 200, 92, 24), new(20, 40, 84, 16), new(217, 20, 84, 16), new(20, 20, 84, 16), new(217, 40, 84, 16)];
    private static readonly Point[] HighwayGaugeBars = [new(141, 217), new(37, 49), new(219, 29), new(37, 29), new(219, 49)];
    private static readonly Rectangle[] HighwayPortraitBounds = [new(117, 201, 22, 22), new(21, 41, 14, 14), new(286, 21, 14, 14), new(21, 21, 14, 14), new(286, 41, 14, 14)];
    private static readonly int[] HighwayGaugeRows = [68, 34, 0, 34, 0];
    private static readonly int[] HighwayPortraitRows = [112, 0, 28, 56, 84];
    private ushort highwayHeld;
    private ushort highwayPressed;

    private void UpdateHighway(GameTime time)
    {
        Ff7Session session = Session!;
        Ff7HighwayRuntime highway = session.Highway!;
        ushort held = (ushort)(Ff7ControlButtons.FromShared(sharedInput.HeldFieldButtons) & 0xF0A0);
        held |= semanticInput.Horizontal < 0 ? (ushort)0x8000 : semanticInput.Horizontal > 0 ? (ushort)0x2000 : (ushort)0;
        held |= semanticInput.Vertical < 0 ? (ushort)0x1000 : semanticInput.Vertical > 0 ? (ushort)0x4000 : (ushort)0;
        if (!ReferenceEquals(highwayOwner, highway))
        {
            highwayOwner = highway;
            highwayHeld = held;
            highwayPressed = 0;
            tickRemainder = 0;
        }
        highwayPressed |= (ushort)(held & ~highwayHeld & 0xA0);
        highwayHeld = held;
        session.AdvanceClock(time.ElapsedGameTime);
        Ff7FieldTiming.Advance(sharedClock, TimeSpan.FromSeconds(Math.Min(time.ElapsedGameTime.TotalSeconds, 0.25)), TimingScene, RequiresRealTimeFieldInput, AdvanceHighway, ref tickRemainder);
        UpdateAudio();

        bool AdvanceHighway()
        {
            session.SetHighwayInput(highwayHeld, highwayPressed);
            highwayPressed = 0;
            AdvanceFieldTick();
            if (ReferenceEquals(session.Highway, highway))
            {
                return true;
            }
            pendingPressedKeys = 0;
            heldKeys = Ff7ControlButtons.FromShared(sharedInput.HeldFieldButtons);
            tickRemainder = 0;
            autoPlay?.ResetIdleDeadline();
            RememberReportCheckpoint();
            return false;
        }
    }

    private void RenderHighway(Ff7HighwayRuntime highway, Rectangle bounds)
    {
        highwayPresentation ??= new(installation);
        highwayPresentation.Render(triangles, highway, bounds.Width, bounds.Height);
    }

    private void DrawHighwayHud(SpriteBatch batch, Rectangle bounds, Ff7HighwayRuntime highway)
    {
        if (highwayGaugeTexture is null)
        {
            using LgpArchive archive = Ff7HighwayResources.Open(installation);
            highwayGaugeTexture = CreateTexture(PcTexture.Read(archive.ReadEntry(archive.Find("muaa.tex").Single())));
            highwayPortraitTexture = CreateTexture(PcTexture.Read(archive.ReadEntry(archive.Find("nuaa.tex").Single())));
        }
        for (int index = 0; index < highway.Gauges.Count; index++)
        {
            Ff7HighwayGauge gauge = highway.Gauges[index];
            Rectangle plate = HighwayGaugeBounds[index];
            Point bar = HighwayGaugeBars[index];
            int plateRow = HighwayGaugeRows[index];
            Color portraitColor = gauge.Value > 0 ? Color.White : Color.DimGray;
            Rectangle portrait = HighwayPortraitBounds[index];
            int portraitWidth = portrait.Width * 2;
            int portraitX = Math.Max(0, gauge.PortraitState - 1) * portraitWidth;
            int portraitY = gauge.PortraitState == 0 ? plateRow : HighwayPortraitRows[index];
            batch.Draw(highwayPortraitTexture, Logical(portrait, bounds), new Rectangle(portraitX, portraitY, portraitWidth, portrait.Height * 2), portraitColor);
            batch.Draw(highwayGaugeTexture, Logical(plate, bounds), new Rectangle(0, plateRow, plate.Width * 2, plate.Height * 2), portraitColor);
            int width = gauge.Maximum == 0 ? 0 : 64 * gauge.Value / gauge.Maximum - 1;
            if (width > 0)
            {
                Color tint = new(gauge.HitFlash * 2, 128 - gauge.HitFlash, 0);
                batch.Draw(highwayGaugeTexture, Logical(new(bar.X, bar.Y, width, 4), bounds), new Rectangle(0, 118, 2, 10), tint);
            }
        }
        if (highway.FadeAlpha > 0)
        {
            batch.Draw(pixel, bounds, Color.Black * (highway.FadeAlpha / 255f));
        }
    }
}
