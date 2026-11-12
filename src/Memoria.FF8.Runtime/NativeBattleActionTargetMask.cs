namespace Memoria.FF8.Runtime;

// These words belong to the original command and graphics formats.
internal readonly record struct NativeBattleTargetRequest(ushort Word)
{
    public int Selection => Word & 0x1FFF;
    public int Modifiers => Word >> 13;
    public bool SelectsRandomTarget => (Modifiers & 1) != 0;
    public bool IncludesKnockedOutAllies => (Modifiers & 2) != 0;
    public bool SelectsGroup => (Modifiers & 4) != 0;
    public BattleSide Side => Selection <= NativeBattlePhysicalTargets.AllyBits ? BattleSide.Allies : BattleSide.Enemies;

    public NativeBattleTargetRequest WithRandomSelection(BattleRosterSlot? slot)
    {
        int target = slot is null ? 0 : NativeBattlePhysicalTargets.Single(slot.Value).Bits;
        return new(checked((ushort)((Word & 0x6000) | target)));
    }
}

internal readonly record struct NativeBattlePhysicalTargets
{
    public const int AllyBits = (1 << NativeBattleSlots.AllyCount) - 1;
    private const int AllBits = (1 << NativeBattleSlots.Count) - 1;

    private NativeBattlePhysicalTargets(ushort bits) => Bits = bits;

    public ushort Bits { get; }
    public bool IsEmpty => Bits == 0;

    public static NativeBattlePhysicalTargets FromNativeWord(ushort word) => new((ushort)(word & AllBits));
    public static NativeBattlePhysicalTargets Single(BattleRosterSlot slot) => new(checked((ushort)(1 << NativeBattleSlots.Encode(slot))));
    public NativeBattlePhysicalTargets Add(BattleRosterSlot slot) => new((ushort)(Bits | Single(slot).Bits));
    public bool Contains(BattleRosterSlot slot) => (Bits & Single(slot).Bits) != 0;
}

internal static class NativeBattleActionTargetMask
{
    internal static (int Result, bool Unavailable) Resolve(NativeBattleTargetRequest request, NativeBattlePhysicalTargets targets, bool disableAutomaticReplacement)
    {
        int selected = request.Selection;
        int available = targets.Bits;
        int result;
        if (request.SelectsGroup)
        {
            result = selected == 255 ? available : available & (request.Side == BattleSide.Allies ? 7 : 248);
        }
        else
        {
            result = available & selected;
            if (result == 0 && disableAutomaticReplacement)
            {
                result = selected;
            }
            else if (result == 0 && selected != 0)
            {
                int side = available & (request.Side == BattleSide.Allies ? 7 : 248);
                int shifted = selected;
                for (int index = 0; index < 8 && result == 0; index++)
                {
                    shifted <<= 1;
                    result = side & shifted;
                }
                shifted = selected;
                for (int index = 0; index < 8 && result == 0; index++)
                {
                    shifted >>= 1;
                    result = side & shifted;
                }
            }
        }
        bool unavailable = result == 0 && (request.SelectsGroup || !disableAutomaticReplacement);
        if (unavailable && !request.SelectsGroup)
        {
            result = selected;
        }
        return (result, unavailable);
    }
}
