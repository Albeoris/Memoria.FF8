using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Saved two DWORDs1CFE95C..1CFE963 written by native48B7A0 and48B8B0.</summary>
public sealed record NativeKnownMagic([property: JsonRequired] string InitOutSha256, [property: JsonRequired] ImmutableArray<uint> Words)
{
    public void Validate()
    {
        if (InitOutSha256 != NativeInputConfiguration.OriginalInitHash || Words.IsDefault || Words.Length != 2)
        {
            throw new InvalidDataException("Native known magic requires its authenticated two-DWORD saved owner.");
        }
    }

    public bool Contains(int spell)
    {
        Validate();
        if (spell is < 1 or > 64)
        {
            throw new NotSupportedException("Native magic identity lies outside its original64-bit saved bank.");
        }
        return (Words[(spell - 1) / 32] & (1u << ((spell - 1) & 31))) != 0;
    }

    public NativeKnownMagic Add(int spell)
    {
        _ = Contains(spell);
        return this with { Words = Words.SetItem((spell - 1) / 32, Words[(spell - 1) / 32] | (1u << ((spell - 1) & 31))) };
    }

    public bool SameValue(NativeKnownMagic other)
    {
        return InitOutSha256 == other.InitOutSha256 && Words.SequenceEqual(other.Words);
    }

    public static void ValidateReceipt(NativeKnownMagic? before, NativeKnownMagic? after)
    {
        if (before is null && after is null)
        {
            return;
        }
        before?.Validate();
        after?.Validate();
        if (before is null || after is null || before.InitOutSha256 != after.InitOutSha256 || before.Words.Where((word, index) => (word & after.Words[index]) != word).Any())
        {
            throw new InvalidDataException("Native known magic receipt must preserve its existing64 bits and authenticated owner.");
        }
    }
}
