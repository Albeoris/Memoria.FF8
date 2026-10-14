using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Process4B8530/4B7E00 state; the enable BYTE remains solely NativeSeeDState.SalaryUiEnabled.</summary>
public sealed record NativeSalaryPresentation
{
    public const string CurrentRule = "ff8-en-salary-ui-49acd0-4b86d0-4b7e00-v1";

    [JsonRequired]
    public required int Version { get; init; }
    [JsonRequired]
    public required string Rule { get; init; }
    [JsonRequired]
    public required short Phase { get; init; }
    [JsonRequired]
    public required short RankBlend98 { get; init; }
    [JsonRequired]
    public required short Scale9A { get; init; }
    [JsonRequired]
    public required byte OldRank9C { get; init; }
    [JsonRequired]
    public required byte NewRank9D { get; init; }
    [JsonRequired]
    public required byte Delay9E { get; init; }
    [JsonRequired]
    public required byte LeadingBlank9F { get; init; }
    [JsonRequired]
    public required ImmutableArray<byte> OldRankGlyphs { get; init; }
    [JsonRequired]
    public required ImmutableArray<byte> NewRankGlyphs { get; init; }
    [JsonRequired]
    public required ImmutableArray<byte> OldPayGlyphs { get; init; }
    [JsonRequired]
    public required ImmutableArray<byte> NewPayGlyphs { get; init; }

    [JsonIgnore]
    public bool IsColdProcess => Phase == 9 && RankBlend98 == 0 && Scale9A == 0 && OldRank9C == 0 && NewRank9D == 0 && Delay9E == 0 && LeadingBlank9F == 0 && !OldRankGlyphs.IsDefault && OldRankGlyphs.All(value => value == 0) && !NewRankGlyphs.IsDefault && NewRankGlyphs.All(value => value == 0) && !OldPayGlyphs.IsDefault && OldPayGlyphs.All(value => value == 0) && !NewPayGlyphs.IsDefault && NewPayGlyphs.All(value => value == 0);

    public static NativeSalaryPresentation FromColdProcess()
    {
        // Cold BSS is zero, then49ACD0 executes4B86D0:phase9 and both interpolation WORDs/rank BYTEs0.
        return new NativeSalaryPresentation { Version = 1, Rule = CurrentRule, Phase = 9, RankBlend98 = 0, Scale9A = 0, OldRank9C = 0, NewRank9D = 0, Delay9E = 0, LeadingBlank9F = 0, OldRankGlyphs = ImmutableArray.Create((byte)0, (byte)0, (byte)0), NewRankGlyphs = ImmutableArray.Create((byte)0, (byte)0, (byte)0), OldPayGlyphs = Enumerable.Repeat((byte)0, 6).ToImmutableArray(), NewPayGlyphs = Enumerable.Repeat((byte)0, 6).ToImmutableArray() };
    }

    public void Validate()
    {
        if (Version != 1 || Rule != CurrentRule || Phase is < 0 or > 9 || RankBlend98 is < 0 or > 4096 || Scale9A is < 0 or > 4096 || LeadingBlank9F > 5 || OldRankGlyphs.IsDefault || OldRankGlyphs.Length != 3 || NewRankGlyphs.IsDefault || NewRankGlyphs.Length != 3 || OldPayGlyphs.IsDefault || OldPayGlyphs.Length != 6 || NewPayGlyphs.IsDefault || NewPayGlyphs.Length != 6 || OldRankGlyphs[^1] != 0 || NewRankGlyphs[^1] != 0 || OldPayGlyphs[^1] != 0 || NewPayGlyphs[^1] != 0)
        {
            throw new InvalidDataException("Salary presentation requires its complete retained process words and original glyph buffers.");
        }
    }

    public NativeSalaryPresentation StartPayment(int oldRank, int newRank, int oldPay, int newPay)
    {
        Validate();
        ImmutableArray<byte> oldPayGlyphs = Digits(unchecked((uint)oldPay), 5, 4);
        ImmutableArray<byte> newPayGlyphs = Digits(unchecked((uint)newPay), 5, 4);
        int first = Enumerable.Range(0, 5).First(index => oldPayGlyphs[index] != 7 || newPayGlyphs[index] != 7);
        return (this with { Phase = 0, RankBlend98 = 0, Scale9A = 0, OldRank9C = unchecked((byte)oldRank), NewRank9D = unchecked((byte)newRank), Delay9E = 0, LeadingBlank9F = (byte)first, OldRankGlyphs = RankGlyphs(oldRank), NewRankGlyphs = RankGlyphs(newRank), OldPayGlyphs = oldPayGlyphs, NewPayGlyphs = newPayGlyphs }).Advance(1);
    }

    private static ImmutableArray<byte> RankGlyphs(int rank) => rank >= 31 ? ImmutableArray.Create((byte)7, (byte)0xB2, (byte)0) : Digits(unchecked((uint)rank), 2, 1);

    private static ImmutableArray<byte> Digits(uint value, int count, int trimCount)
    {
        //4B87A0 formats ten digits then4B8530 copies the last five;4B87F0 formats five then copies the last two.
        value %= count == 5 ? 100000u : 100u;
        byte[] glyphs = new byte[count + 1];
        uint divisor = count == 5 ? 10000u : 10u;
        for (int index = 0; index < count; index++)
        {
            glyphs[index] = unchecked((byte)(value / divisor + 96));
            value %= divisor;
            divisor /= 10;
        }
        for (int index = 0; index < trimCount && glyphs[index] == 96; index++)
        {
            glyphs[index] = 7;
        }
        return glyphs.ToImmutableArray();
    }

    public NativeSalaryPresentation Close()
    {
        Validate();
        return this with { Phase = 7 };
    }

    public NativeSalaryPresentation Reset()
    {
        Validate();
        //4B86D0 leaves delay/spacing and all formatted glyph buffers unchanged.
        return this with { Phase = 9, RankBlend98 = 0, Scale9A = 0, OldRank9C = 0, NewRank9D = 0 };
    }

    public NativeSalaryPresentation Advance(int ticks)
    {
        Validate();
        if (ticks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ticks));
        }
        NativeSalaryPresentation next = this;
        for (int tick = 0; tick < ticks; tick++)
        {
            next = next.Phase switch
            {
                0 => next with { Phase = 1, RankBlend98 = 0, Scale9A = 256 },
                1 => next with { Phase = next.Scale9A + 256 >= 4096 ? (short)2 : (short)1, Scale9A = (short)Math.Min(next.Scale9A + 256, 4096) },
                2 => next with { Phase = 3, Delay9E = 23 },
                3 => next with { Phase = next.Delay9E == 1 ? (short)4 : (short)3, Delay9E = unchecked((byte)(next.Delay9E - 1)) },
                4 => next.OldRank9C == next.NewRank9D ? next with { Phase = 6, RankBlend98 = 4096 } : next with { Phase = 5 },
                5 => next with { Phase = next.RankBlend98 + 341 >= 4096 ? (short)6 : (short)5, RankBlend98 = (short)Math.Min(next.RankBlend98 + 341, 4096) },
                7 => next with { Phase = 8 },
                8 => next with { Phase = next.Scale9A - 256 <= 0 ? (short)9 : (short)8, Scale9A = (short)Math.Max(next.Scale9A - 256, 0) },
                _ => next
            };
        }
        return next;
    }
}
