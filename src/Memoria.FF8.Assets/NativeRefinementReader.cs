using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using Albeoris.Games.FF8.FlArchives;
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
        using IFlArchive main = FlArchive.OpenForRead(Path.Combine(GameDirectory, "Data", "lang-en", "main.fs"), FlArchiveRepresentation.Files);
        using IFlArchive menu = FlArchive.OpenForRead(Path.Combine(GameDirectory, "Data", "lang-en", "menu.fs"), FlArchiveRepresentation.Files);
        byte[] kernelBytes = Read(main, "kernel.bin");
        string kernelHash = Convert.ToHexString(SHA256.HashData(kernelBytes));
        KernelData kernel = KernelData.ReadFromArray(kernelBytes, FF8Encoding.CreateEuropean());
        string[] hashes = ["66741E3A9F5D01C27F4D5D634F69E8449D10F6E46A724DA8F6FD8D291AEE06F9", "7FAF8EC84E54CF5ADA8B3F07CBE0CB049D9ED81606B5175466F088377D322BBD", "B9D8A2F73B15A680302713944BD7102BC799FD0817CBAB0A96D200D3053C0009", "14F1C84160AD776900D654E82A89A2B634143B75EC7C8B1A69046E92AD7048C1", "0DAD31178F36AF9EA4FDDF364B0FBBA2CA44C3B04C4581ED4F212D16DA503483"];
        ImmutableArray<ImmutableArray<NativeRefinementRecipe>>.Builder tables = ImmutableArray.CreateBuilder<ImmutableArray<NativeRefinementRecipe>>(5);
        for (int kind = 0; kind < 5; kind++)
        {
            byte[] bytes = Read(menu, $"m00{kind}.bin");
            if (Convert.ToHexString(SHA256.HashData(bytes)) != hashes[kind] || bytes.Length % 8 != 0)
            {
                throw new NotSupportedException("Refinement recipe table differs from its original resource.");
            }
            ImmutableArray<NativeRefinementRecipe>.Builder recipes = ImmutableArray.CreateBuilder<NativeRefinementRecipe>(bytes.Length / 8);
            for (int offset = 0; offset < bytes.Length; offset += 8)
            {
                recipes.Add(new NativeRefinementRecipe(BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset + 2)), bytes[offset + 4], bytes[offset + 5], bytes[offset + 6], bytes[offset + 7]));
            }
            tables.Add(recipes.MoveToImmutable());
        }
        NativeRefinementDefinitions definitions = new(NativeExecutableHash!, kernelHash, kernel.MenuAbilities.Select(ability => new NativeRefinementAbility(ability.Name ?? "", ability.Index, ability.Start, ability.End)).ToImmutableArray(), tables.MoveToImmutable());
        definitions.Validate();
        return nativeRefinement = definitions;
    }
}
