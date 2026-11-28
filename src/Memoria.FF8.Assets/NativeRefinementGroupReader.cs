using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using Albeoris.Games.FF8.MngrpBin;
using Albeoris.Games.FF8.MngrpBin.Abstractions;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

/// <summary>The five menu record groups actually selected by the native refinement controller.</summary>
public static class NativeRefinementGroupReader
{
    public static ImmutableArray<ImmutableArray<NativeRefinementRecipe>> ReadTables(MngrpArchive archive)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ImmutableArray<ImmutableArray<NativeRefinementRecipe>>.Builder tables = ImmutableArray.CreateBuilder<ImmutableArray<NativeRefinementRecipe>>(5);
        for (int kind = 0; kind < 5; kind++)
        {
            MngrpTextRecordSection section = archive.GetSection<MngrpTextRecordSection>(188 + kind);
            if (section.Records.Count is < 1 or > 256 || kind == 4 && section.Records.Count != 110)
            {
                string expected = kind == 4 ? "110 card records" : "1 through 256 records";
                throw new InvalidDataException($"Refinement group {188 + kind} contains {section.Records.Count} records and requires {expected}.");
            }
            ImmutableArray<NativeRefinementRecipe>.Builder recipes = ImmutableArray.CreateBuilder<NativeRefinementRecipe>(section.Records.Count);
            foreach (MngrpTextRecord record in section.Records)
            {
                byte[] payload = record.Payload;
                if (payload.Length != 6)
                {
                    throw new InvalidDataException($"Record {recipes.Count} in refinement group {188 + kind} has {payload.Length} payload bytes after its text pointer and requires six.");
                }
                ushort outputCount = BinaryPrimitives.ReadUInt16LittleEndian(payload);
                int ingredient = payload[3];
                int output = payload[5];
                bool validIngredient = kind == 4 ? ingredient < 110 : ingredient > 0 && ingredient < (kind == 2 ? 57 : 199);
                if (!validIngredient || output == 0 || output >= (kind is 0 or 2 ? 57 : 199) || outputCount == 0 || payload[4] == 0 || kind == 4 && (outputCount > 100 || payload[4] > 100))
                {
                    throw new InvalidDataException($"Record {recipes.Count} in refinement group {188 + kind} has an invalid inventory ID or quantity (ingredient {ingredient}, quantity {payload[4]}, output {output}, quantity {outputCount}).");
                }
                recipes.Add(new NativeRefinementRecipe(outputCount, payload[2], payload[3], payload[4], payload[5]));
            }
            tables.Add(recipes.MoveToImmutable());
        }
        return tables.MoveToImmutable();
    }

    public static ImmutableArray<TripleTriadRefinement> ReadCardRefinements(MngrpArchive archive, NativeRefinementAbility? ability = null)
    {
        ImmutableArray<NativeRefinementRecipe> table = ReadTables(archive)[4];
        ability ??= new NativeRefinementAbility("", 4, 0, 109);
        if (ability.Kind != 4)
        {
            return [];
        }
        if (ability.First > ability.Last || ability.Last >= table.Length)
        {
            throw new InvalidDataException("Native Card Mod range lies outside its selected record group.");
        }
        HashSet<byte> seen = [];
        ImmutableArray<TripleTriadRefinement>.Builder result = ImmutableArray.CreateBuilder<TripleTriadRefinement>();
        foreach (NativeRefinementRecipe recipe in table.Skip(ability.First).Take(ability.Last - ability.First + 1))
        {
            if (seen.Add(recipe.Ingredient) && recipe.Requirement <= 100)
            {
                result.Add(new TripleTriadRefinement(recipe.Ingredient, recipe.IngredientCount, recipe.Output, recipe.OutputCount));
            }
        }
        return result.ToImmutable();
    }

    public static string[] ReadCardDescriptions(MngrpArchive archive)
    {
        _ = ReadTables(archive);
        string[] descriptions = Enumerable.Repeat("", 110).ToArray();
        HashSet<byte> seen = [];
        foreach (MngrpTextRecord record in archive.GetSection<MngrpTextRecordSection>(192).Records)
        {
            byte card = record.Payload[3];
            if (seen.Add(card))
            {
                descriptions[card] = record.Text.Value;
            }
        }
        return descriptions;
    }

    public static ImmutableArray<string> ReadTableHashes(byte[] content, byte[] header)
    {
        ImmutableArray<string>.Builder hashes = ImmutableArray.CreateBuilder<string>(5);
        for (int kind = 0; kind < 5; kind++)
        {
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (int slot in new[] { 188 + kind, 196 + kind })
            {
                if (header.Length != 256 * 8)
                {
                    throw new InvalidDataException("Native menu groups require their complete slot directory.");
                }
                ReadOnlySpan<byte> descriptor = header.AsSpan(slot * 8, 8);
                uint rawOffset = BinaryPrimitives.ReadUInt32LittleEndian(descriptor);
                uint length = BinaryPrimitives.ReadUInt32LittleEndian(descriptor[4..]);
                if (rawOffset is 0 or uint.MaxValue || length == 0 || (ulong)rawOffset - 1 + length > (ulong)content.Length)
                {
                    throw new InvalidDataException($"Refinement slot {slot} specifies offset {rawOffset} and length {length} outside the {content.Length}-byte menu resource.");
                }
                hash.AppendData(descriptor);
                hash.AppendData(content.AsSpan(checked((int)rawOffset - 1), checked((int)length)));
            }
            hashes.Add(Convert.ToHexString(hash.GetHashAndReset()));
        }
        return hashes.MoveToImmutable();
    }
}
