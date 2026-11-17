namespace Memoria.FF8.Runtime;

/// <summary>The Shot widget's original publication latch, timer and callback phase.</summary>
public sealed record NativeBattleShotWidgetState(short RemainingTimer, byte Phase, byte PublicationLatch, byte ClosingPolls, bool Rearmed)
{
    public bool Visible => Phase is >= 3 and <= 8;
    public bool Closing => Phase is >= 8 and < 12;
    public bool Closed => Phase == 12;

    public static NativeBattleShotWidgetState Register(short initialTimer)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(initialTimer);
        return new(initialTimer, 0, 4, 0, false);
    }

    public NativeBattleShotWidgetState Rearm()
    {
        return this with { Rearmed = true };
    }

    public NativeBattleShotWidgetStep Advance(bool frame33, bool fired, bool closeRequested, byte ammunition)
    {
        if (Phase > 12 || PublicationLatch > 4)
        {
            throw new InvalidDataException("Shot widget contains an invalid stored phase or publication latch.");
        }
        if (Closed)
        {
            return new(this, false, false, false, false);
        }

        byte latch = frame33 ? (byte)4 : PublicationLatch == 0 ? (byte)0 : (byte)(PublicationLatch - 1);
        short timer = latch == 0 ? RemainingTimer : unchecked((short)(RemainingTimer - 1));
        byte phase = Phase;
        if (phase < 8 && (timer == 0 || closeRequested))
        {
            timer = -1;
            phase = 8;
        }

        bool rearmed = Rearmed;
        bool queueClose = false;
        bool accepted = false;
        byte closing = ClosingPolls;
        switch (phase)
        {
            case 0:
                phase = 1;
                break;
            case 1 when frame33:
                phase = 3;
                break;
            case 3:
                phase = 4;
                break;
            case 4:
                phase = 5;
                break;
            case 5 when fired && timer > 0:
                accepted = true;
                phase = ammunition == 0 ? (byte)8 : (byte)6;
                break;
            case 6:
                phase = 7;
                break;
            case 7 when rearmed:
                rearmed = false;
                accepted = fired && timer > 0;
                phase = accepted ? (ammunition == 0 ? (byte)8 : (byte)6) : (byte)5;
                break;
            case 8:
                phase = 9;
                break;
            case 9 when frame33:
                phase = 10;
                break;
            case 10:
                queueClose = true;
                closing = 1;
                phase = 11;
                break;
            case 11:
                closing = unchecked((byte)(closing + 1));
                if (closing > 24 && frame33)
                {
                    phase = 12;
                }
                break;
        }
        NativeBattleShotWidgetState widget = new(timer, phase, latch, closing, rearmed);
        return new(widget, phase == 5, queueClose, widget.Closed, accepted);
    }
}

public readonly record struct NativeBattleShotWidgetStep(NativeBattleShotWidgetState Widget, bool Ready, bool QueueClosingCommand, bool Unregistered, bool Fired);
