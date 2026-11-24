namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleGraphicsResource
{
    /// <summary>Source of ordinary combat choreography, independent of the actor's identity and body.</summary>
    public byte? CombatStyleModelId { get; init; }
    /// <summary>Sound table row of an action bank authored for a different style than the equipped weapon.</summary>
    public byte? SequenceSoundModelId { get; init; }
    /// <summary>Body geometry directory entry occupied by a shared-skeleton weapon; absent preserves native entry one.</summary>
    public int? SharedWeaponBodyObjectIndex { get; init; }
}
