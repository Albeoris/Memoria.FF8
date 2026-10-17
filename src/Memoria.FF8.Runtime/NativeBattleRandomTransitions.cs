namespace Memoria.FF8.Runtime;

/// <summary>The identical125*x+14/15-bit transition used by4A7080,6C7CF0 and7059E0; storage and initializer contracts remain separate.</summary>
internal static class NativeBattleRandomTransitions
{
    public static ushort Draw125(uint state)
    {
        return unchecked((ushort)((125u * state + 14u) & 0x7FFFu));
    }
}
