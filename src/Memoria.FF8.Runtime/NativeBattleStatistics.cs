using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Saved battle totals, with the native DWORD victory and WORD defeat/escape widths.</summary>
public sealed record NativeBattleStatistics([property: JsonRequired] uint Victories, [property: JsonRequired] ushort Defeats, [property: JsonRequired] ushort Escapes)
{
    public static NativeBattleStatistics CreateNewGame() => new(0, 0, 0);

    public NativeBattleStatistics Complete(byte nativeResult)
    {
        return nativeResult switch
        {
            1 or 3 => this with { Defeats = unchecked((ushort)(Defeats + 1)) },
            2 => this with { Escapes = unchecked((ushort)(Escapes + 1)) },
            4 => this with { Victories = unchecked(Victories + 1) },
            5 => this,
            _ => throw new InvalidDataException("Native battle statistics require a completed native result byte.")
        };
    }
}
