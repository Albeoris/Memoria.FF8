using System.Numerics;
using Memoria.FF7.Events;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7FieldHost
{
    public void InitializeBackgroundTriggers()
    {
        foreach (PcFieldTrigger trigger in field.Triggers.Triggers.Where(trigger => trigger.BackgroundParameter != 255))
        {
            if (trigger.Behavior <= 5)
            {
                ActivateBackgroundTrigger(trigger, trigger.Behavior % 2 == 0 ? 1 : 0);
            }
        }
    }

    public void UpdateBackgroundTriggers(Ff7Actor actor)
    {
        Vector3 from = new(actor.X4096 >> 12, actor.Y4096 >> 12, actor.Z4096 >> 12);
        foreach (PcFieldTrigger trigger in field.Triggers.Triggers.Where(trigger => trigger.BackgroundParameter != 255))
        {
            int distance = Ff7Session.DistanceToSegment(trigger.A, trigger.B, from, out Vector3 nearest);
            bool changed = false;
            if (distance != -1 && distance < unchecked(actor.CollisionRadius * actor.CollisionRadius))
            {
                bool exact = from.X == nearest.X && from.Y == nearest.Y;
                bool facing = exact || (((Movement ?? throw new NotSupportedException("FFVII background triggers require native direction tables.")).Direction((int)(nearest.X - from.X), (int)(nearest.Y - from.Y)) - actor.Direction + 64) & 255) < 128;
                if (facing)
                {
                    changed = ActivateBackgroundTrigger(trigger, trigger.Behavior);
                }
            }
            else if (trigger.Behavior < 4 || Ff7Session.Side(trigger.A, trigger.B, from) <= 0)
            {
                if (trigger.Behavior is 2 or 4)
                {
                    changed = ActivateBackgroundTrigger(trigger, 1);
                }
                else if (trigger.Behavior is 3 or 5)
                {
                    changed = ActivateBackgroundTrigger(trigger, 0);
                }
            }
            if (changed)
            {
                int sound = trigger.Sound switch
                {
                    0 => 0,
                    1 => 54,
                    2 => 122,
                    3 => 298,
                    _ => throw new InvalidDataException("The FFVII background trigger has an invalid native sound selector.")
                };
                ApplyAudio(new FieldAudioCommand(0x20, 64, sound));
            }
        }
    }

    private bool ActivateBackgroundTrigger(PcFieldTrigger trigger, int behavior)
    {
        if (behavior is < 0 or > 5)
        {
            return false;
        }
        int mask = unchecked((byte)(1 << trigger.State));
        ushort[] states = (ushort[])Process.BackgroundStates.Clone();
        int previous = (byte)states[trigger.BackgroundParameter];
        int updated = behavior % 2 == 0 ? previous | mask : previous & ~mask;
        states[trigger.BackgroundParameter] = (byte)updated;
        Process = Process with { BackgroundStates = states };
        return previous != updated;
    }
}
