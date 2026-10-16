using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>47E2D0's retained per-actor WORD B92/DWORD B1C, distinct from live B90/B18 and global E30/E38.</summary>
public sealed record NativeBattleStatusMirror([property: JsonRequired] ushort? Saved, [property: JsonRequired] uint? Battle)
{
    public void Validate()
    {
        if ((Saved is null) != (Battle is null))
        {
            throw new InvalidDataException("Native B92/B1C mirror must retain both fields from the same47E2D0 producer.");
        }
    }
}

public sealed partial class BattleCombatant
{
    internal NativeBattleStatusMirror NativePresentationStatus { get; set; } = new(null, null);
}
