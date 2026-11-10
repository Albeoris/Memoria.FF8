using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public readonly record struct Ff7WindowParameter(FieldBank Bank = FieldBank.Immediate, ushort Address = 0);

public sealed record Ff7WindowParameters(Ff7WindowParameter P0 = default, Ff7WindowParameter P1 = default, Ff7WindowParameter P2 = default, Ff7WindowParameter P3 = default, Ff7WindowParameter P4 = default, Ff7WindowParameter P5 = default, Ff7WindowParameter P6 = default, Ff7WindowParameter P7 = default)
{
    public void Validate()
    {
        if (Enumerable.Range(0, 8).Any(slot => (byte)Get(slot).Bank > 15))
        {
            throw new InvalidDataException("FFVII numeric window binding contains an invalid native selector.");
        }
    }
    public Ff7WindowParameter Get(int slot) => slot switch
    {
        0 => P0,
        1 => P1,
        2 => P2,
        3 => P3,
        4 => P4,
        5 => P5,
        6 => P6,
        7 => P7,
        _ => throw new InvalidDataException("FFVII numeric window parameter exceeds its eight native slots."),
    };
    public Ff7WindowParameters Set(int slot, Ff7WindowParameter value) => slot switch
    {
        0 => this with { P0 = value },
        1 => this with { P1 = value },
        2 => this with { P2 = value },
        3 => this with { P3 = value },
        4 => this with { P4 = value },
        5 => this with { P5 = value },
        6 => this with { P6 = value },
        7 => this with { P7 = value },
        _ => throw new InvalidDataException("FFVII numeric window parameter exceeds its eight native slots."),
    };
}

public sealed record Ff7DialogueNumbers(string? P0 = null, string? P1 = null, string? P2 = null, string? P3 = null, string? P4 = null, string? P5 = null, string? P6 = null, string? P7 = null)
{
    public void Validate()
    {
        for (int slot = 0; slot < 8; slot++)
        {
            if (Get(slot) is string value && (value.Length is < 1 or > 5 || value.Any(character => character is not (>= '0' and <= '9' or >= 'A' and <= 'F' or ' '))))
            {
                throw new InvalidDataException("FFVII cached numeric expansion is outside its native unsigned word format.");
            }
        }
    }
    public string? Get(int slot) => slot switch
    {
        0 => P0,
        1 => P1,
        2 => P2,
        3 => P3,
        4 => P4,
        5 => P5,
        6 => P6,
        7 => P7,
        _ => throw new InvalidDataException("FFVII numeric message exceeds its eight native parameters."),
    };
    public Ff7DialogueNumbers Set(int slot, string value) => slot switch
    {
        0 => this with { P0 = value },
        1 => this with { P1 = value },
        2 => this with { P2 = value },
        3 => this with { P3 = value },
        4 => this with { P4 = value },
        5 => this with { P5 = value },
        6 => this with { P6 = value },
        7 => this with { P7 = value },
        _ => throw new InvalidDataException("FFVII numeric message exceeds its eight native parameters."),
    };
}
