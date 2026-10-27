using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Albeoris.Games.FF8.KernelBin;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed partial class GameAssets
{
    private NativeRefinementDefinitions? nativeRefinement;

    public NativeRefinementDefinitions ReadNativeRefinementDefinitions()
    {
        if (nativeRefinement is not null)
        {
            return nativeRefinement;
        }
        if (NativeRuntimeProfile is not { IsVerified: true })
        {
            throw new NotSupportedException("Refinement requires the authenticated original executable.");
        }
        using IFlArchive main = OpenArchive("main");
        using IFlArchive menu = OpenArchive("menu");
        byte[] kernelBytes = Read(main, "kernel.bin");
        string kernelHash = Convert.ToHexString(SHA256.HashData(kernelBytes));
        _ = NativeKernelDirectory.Read(kernelBytes);
        KernelData kernel = KernelData.ReadFromArray(kernelBytes, FF8Encoding.CreateEuropean());
        ImmutableArray<string>.Builder hashes = ImmutableArray.CreateBuilder<string>(5);
        ImmutableArray<ImmutableArray<NativeRefinementRecipe>>.Builder tables = ImmutableArray.CreateBuilder<ImmutableArray<NativeRefinementRecipe>>(5);
        for (int kind = 0; kind < 5; kind++)
        {
            byte[] bytes = Read(menu, $"m00{kind}.bin");
            NativeMenuDataReader.ValidateRefinementTable(bytes, kind);
            hashes.Add(Convert.ToHexString(SHA256.HashData(bytes)));
            ImmutableArray<NativeRefinementRecipe>.Builder recipes = ImmutableArray.CreateBuilder<NativeRefinementRecipe>(bytes.Length / 8);
            for (int offset = 0; offset < bytes.Length; offset += 8)
            {
                recipes.Add(new NativeRefinementRecipe(BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + 2)), bytes[offset + 4], bytes[offset + 5], bytes[offset + 6], bytes[offset + 7]));
            }
            tables.Add(recipes.MoveToImmutable());
        }
        NativeRefinementDefinitions definitions = new(NativeExecutableHash!, kernelHash, kernel.MenuAbilities.Select(ability => new NativeRefinementAbility(ability.Name ?? "", ability.Index, ability.Start, ability.End)).ToImmutableArray(), tables.MoveToImmutable())
        {
            TableHashes = hashes.MoveToImmutable()
        };
        definitions.Validate();
        return nativeRefinement = definitions;
    }
}
