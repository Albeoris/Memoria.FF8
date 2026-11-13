namespace Memoria.FF7.Runtime;

/// <summary>A source status owns a nonstacking percentage reduction of the host's defensive parameters.</summary>
public sealed record Ff7BattleParameterOperation(uint SourceStatusMask, int ResistanceIndex, int DefenseReductionPercent, int MagicDefenseReductionPercent)
{
    public void Validate()
    {
        if (SourceStatusMask == 0 || (SourceStatusMask & (SourceStatusMask - 1)) != 0 || ResistanceIndex is < 0 or >= 40 || DefenseReductionPercent is < 0 or > 100 || MagicDefenseReductionPercent is < 0 or > 100)
        {
            throw new InvalidDataException("A parameter operation requires one source status, its resistance row and percentages from zero to one hundred.");
        }
    }

    public Ff7BattleDamageActor Project(Ff7BattleDamageActor actor)
    {
        Validate();
        return actor with { Defense = checked((ushort)(actor.Defense * (100 - DefenseReductionPercent) / 100)), MagicDefense = checked((ushort)(actor.MagicDefense * (100 - MagicDefenseReductionPercent) / 100)), ParameterStatus = this };
    }
}
