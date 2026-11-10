using System.Numerics;

namespace Memoria.FF7.Runtime;

/// <summary>Target reaction callback retained by the FFVII battle action interpreter's F7 opcode.</summary>
public sealed record Ff7BattleTargetCallbacks(ushort RemainingTargets, byte Delay, ushort Elapsed = 0)
{
    public static Ff7BattleTargetCallbacks Schedule(ushort targets, byte delay)
    {
        if (targets is 0 or > 0x3FF)
        {
            throw new ArgumentOutOfRangeException(nameof(targets), "A FFVII reaction schedule requires native actor slots.");
        }
        return new(targets, delay);
    }

    public Ff7BattleTargetCallbacks Advance(bool paused, out int? targetSlot)
    {
        if (RemainingTargets > 0x3FF || Elapsed > Delay + Ff7NativeAtb.SlotCount)
        {
            throw new InvalidDataException("The FFVII target reaction schedule is corrupt.");
        }
        targetSlot = null;
        if (paused || RemainingTargets == 0)
        {
            return this;
        }
        ushort remaining = RemainingTargets;
        if (Elapsed >= Delay)
        {
            int slot = BitOperations.TrailingZeroCount((uint)remaining);
            targetSlot = slot;
            remaining = (ushort)(remaining & ~(1 << slot));
        }
        return this with { RemainingTargets = remaining, Elapsed = (ushort)(Elapsed + 1) };
    }
}
