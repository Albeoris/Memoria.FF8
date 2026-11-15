namespace Memoria.FF8.Runtime;

internal abstract partial record BattleExecutionReference
{
    internal abstract record CinematicNodeKey : BattleExecutionReference
    {
        internal sealed record Literal(ushort Value) : CinematicNodeKey;

        internal sealed record ActorRelative(BattleModelOwner Owner, ushort Displacement) : CinematicNodeKey;
    }
}
