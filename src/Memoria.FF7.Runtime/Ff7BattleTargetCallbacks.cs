using Memoria.FF7.Formats;

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
        Ff7BattlePrimaryTargetReaction next = Ff7BattlePrimaryEffects.AdvanceTargetReaction(new(RemainingTargets, Delay) { Age = Elapsed }, paused, out targetSlot);
        return this with { RemainingTargets = next.RemainingTargets, Elapsed = checked((ushort)next.Age) };
    }
}
