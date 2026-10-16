using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Saved five DWORDs1CFE964..1CFE977 written by native493810.</summary>
public sealed record NativeScannedModels([property: JsonRequired] string InitOutSha256, [property: JsonRequired] ImmutableArray<uint> Words)
{
    public void Validate()
    {
        if (InitOutSha256 != NativeInputConfiguration.OriginalInitHash || Words.IsDefault || Words.Length != 5)
        {
            throw new InvalidDataException("Native Scan history requires its authenticated five-DWORD saved owner.");
        }
    }

    public bool Contains(int model)
    {
        Validate();
        if ((uint)model >= 160)
        {
            throw new NotSupportedException("Native Scan model lies outside its original160-bit saved bank.");
        }
        return (Words[model / 32] & (1u << (model & 31))) != 0;
    }

    public NativeScannedModels Add(int model)
    {
        _ = Contains(model);
        return this with { Words = Words.SetItem(model / 32, Words[model / 32] | (1u << (model & 31))) };
    }

    public bool SameValue(NativeScannedModels other)
    {
        return InitOutSha256 == other.InitOutSha256 && Words.SequenceEqual(other.Words);
    }

    public static void ValidateReceipt(NativeScannedModels? before, NativeScannedModels? after)
    {
        if (before is null && after is null)
        {
            return;
        }
        before?.Validate();
        after?.Validate();
        if (before is null || after is null || before.InitOutSha256 != after.InitOutSha256 || before.Words.Where((word, index) => (word & after.Words[index]) != word).Any())
        {
            throw new InvalidDataException("Native Scan receipt must preserve its existing160 bits and authenticated owner.");
        }
    }
}
