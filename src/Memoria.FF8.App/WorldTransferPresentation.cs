using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Memoria.FF8;

/// <summary>Freezes simulation while the two hosts exchange a fully black presented window.</summary>
internal sealed class WorldTransferPresentation : IDisposable
{
    private readonly WorldProcessHandoff transfer;
    private readonly bool incoming;
    private readonly Action shutdownSteam;
    private readonly Action initializeSteam;
    private readonly Action reveal;
    private readonly Action exit;
    private bool firstFrame;
    private bool coveredFrame;
    private bool revealed;
    private bool activated;
    private bool shutdownStarted;
    private float opacity;
    public bool Complete { get; private set; }
    public bool Committed => transfer.Committed || shutdownStarted;

    internal WorldTransferPresentation(WorldProcessHandoff transfer, bool incoming, Action shutdownSteam, Action initializeSteam, Action reveal, Action exit)
    {
        this.transfer = transfer;
        this.incoming = incoming;
        this.shutdownSteam = shutdownSteam;
        this.initializeSteam = initializeSteam;
        this.reveal = reveal;
        this.exit = exit;
        opacity = incoming ? 1 : 0;
    }

    public void Update(TimeSpan elapsed)
    {
        if (Complete)
        {
            return;
        }
        transfer.ThrowIfFailed();
        float step = (float)Math.Clamp(elapsed.TotalSeconds, 0, 0.1) / 0.3f;
        if (incoming)
        {
            if (firstFrame)
            {
                transfer.AcknowledgeReady();
            }
            if (transfer.ReleaseReceived && !revealed)
            {
                reveal();
                revealed = true;
                coveredFrame = false;
            }
            if (revealed && coveredFrame)
            {
                transfer.AcknowledgeCover();
            }
            if (transfer.IsReleased)
            {
                if (!activated)
                {
                    transfer.CompleteIncoming();
                    initializeSteam();
                    activated = true;
                }
                opacity = Math.Max(0, opacity - step);
                Complete = opacity == 0;
            }
            return;
        }
        if (!transfer.DestinationReady)
        {
            return;
        }
        if (!transfer.Committed)
        {
            if (coveredFrame)
            {
                transfer.Release(() =>
                {
                    shutdownStarted = true;
                    shutdownSteam();
                });
            }
            else
            {
                opacity = Math.Min(1, opacity + step);
            }
        }
        if (transfer.IsCovered)
        {
            exit();
        }
    }

    public void Draw(SpriteBatch batch, Texture2D pixel, Rectangle bounds)
    {
        if (opacity > 0)
        {
            batch.Begin(blendState: BlendState.AlphaBlend);
            batch.Draw(pixel, bounds, Color.Black * opacity);
            batch.End();
        }
        firstFrame = true;
        coveredFrame = opacity == 1;
    }

    public void Dispose()
    {
        // The incoming protocol also belongs to Program until its host has finished.
        if (!incoming)
        {
            transfer.Dispose();
        }
    }
}
