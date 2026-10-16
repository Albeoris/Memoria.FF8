namespace Albeoris.Games.FF8.KernelBin.Abstractions;

/// <summary>A playable character's identity and stat growth curve.</summary>
public sealed class Character
{
    /// <summary>The display name, or <see langword="null"/> if this slot has no name.</summary>
    public String? Name { get; set; }

    public Byte CrisisLevel { get; set; }
    public Byte Gender { get; set; }
    public Byte LimitId { get; set; }
    public Byte LimitParam { get; set; }

    public Byte Exp1 { get; set; }
    public Byte Exp2 { get; set; }

    public Byte Hp1 { get; set; }
    public Byte Hp2 { get; set; }
    public Byte Hp3 { get; set; }
    public Byte Hp4 { get; set; }

    public Byte Str1 { get; set; }
    public Byte Str2 { get; set; }
    public Byte Str3 { get; set; }
    public Byte Str4 { get; set; }

    public Byte Vit1 { get; set; }
    public Byte Vit2 { get; set; }
    public Byte Vit3 { get; set; }
    public Byte Vit4 { get; set; }

    public Byte Mag1 { get; set; }
    public Byte Mag2 { get; set; }
    public Byte Mag3 { get; set; }
    public Byte Mag4 { get; set; }

    public Byte Spr1 { get; set; }
    public Byte Spr2 { get; set; }
    public Byte Spr3 { get; set; }
    public Byte Spr4 { get; set; }

    public Byte Spd1 { get; set; }
    public Byte Spd2 { get; set; }
    public Byte Spd3 { get; set; }
    public Byte Spd4 { get; set; }

    public Byte Luck1 { get; set; }
    public Byte Luck2 { get; set; }
    public Byte Luck3 { get; set; }
    public Byte Luck4 { get; set; }
}
