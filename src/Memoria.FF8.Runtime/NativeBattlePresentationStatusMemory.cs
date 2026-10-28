using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>The seven physical participant B92/B1C pairs, preserved by allocation and distinct from party E30/E38.</summary>
public sealed record NativeBattlePresentationStatusMemory([property: JsonRequired] ImmutableArray<NativeBattleStatusMirror> Rows)
{
    public const int PhysicalCount = 7;

    public static NativeBattlePresentationStatusMemory Unknown() => new(Enumerable.Range(0, PhysicalCount).Select(_ => new NativeBattleStatusMirror(null, null)).ToImmutableArray());

    public static NativeBattlePresentationStatusMemory FromColdProcess()
    {
        // The authenticated image's seven participant rows are loader-zero-filled.
        return new(Enumerable.Range(0, PhysicalCount).Select(_ => new NativeBattleStatusMirror(0, 0)).ToImmutableArray());
    }

    public void Validate()
    {
        if (Rows.IsDefault || Rows.Length != PhysicalCount || Rows.Any(row => row is null))
        {
            throw new InvalidDataException("Native B92/B1C memory requires seven complete physical participant pairs.");
        }
        foreach (NativeBattleStatusMirror row in Rows)
        {
            row.Validate();
        }
    }

    public NativeBattlePresentationStatusMemory Observe(int index, NativeBattleStatusMirror value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, PhysicalCount);
        value.Validate();
        return this with { Rows = Rows.SetItem(index, value) };
    }
}

internal sealed class NativeBattlePresentationStatusMemoryOwner
{
    public NativeBattlePresentationStatusMemory State { get; set; } = NativeBattlePresentationStatusMemory.Unknown();
}

public sealed partial class BattleSession
{
    private readonly NativeBattlePresentationStatusMemoryOwner nativePresentationStatusMemory = new();
    public NativeBattlePresentationStatusMemory NativePresentationStatusMemory => nativePresentationStatusMemory.State;

    private void RestoreNativePresentationStatusMemory(NativeBattlePresentationStatusMemory? memory)
    {
        // An older snapshot has no current physical bank. Restore only its explicit
        // actor projections below; the definition's initial bank is not current history.
        NativeBattlePresentationStatusMemory observed = memory ?? NativeBattlePresentationStatusMemory.Unknown();
        observed.Validate();
        nativePresentationStatusMemory.State = observed;
    }
}
