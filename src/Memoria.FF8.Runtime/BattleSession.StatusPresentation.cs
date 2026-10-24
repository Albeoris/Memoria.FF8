namespace Memoria.FF8.Runtime;

public readonly record struct BattleStatusPresentation(ushort Saved, uint Battle);

public sealed partial class BattleSession
{
    /// <summary>Reads effective status bits without allocating a battle snapshot or changing status owners.</summary>
    public BattleStatusPresentation ReadStatusPresentation(string actorId)
    {
        BattleCombatant actor = GetActor(actorId);
        return new(NativeSavedStatusBits(actor), NativeBattleStatusBits(actor));
    }
}
