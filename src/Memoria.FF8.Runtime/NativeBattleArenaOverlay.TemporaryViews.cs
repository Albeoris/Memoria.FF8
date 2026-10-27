namespace Memoria.FF8.Runtime;

internal sealed partial class NativeBattleArenaOverlay
{
    internal bool HasPrivateByte(int offset)
    {
        Source.RequireRange(offset, 1);
        return offset < present.Length && present[offset];
    }
}
