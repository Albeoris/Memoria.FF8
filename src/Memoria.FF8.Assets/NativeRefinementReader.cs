using System.Collections.Immutable;
using System.Security.Cryptography;
using Albeoris.Games.FF8.FlArchives.Abstractions;
using Albeoris.Games.FF8.KernelBin;
using Albeoris.Games.FF8.MngrpBin;
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
        byte[] content = Read(menu, "mngrp.bin");
        byte[] header = Read(menu, "mngrphd.bin");
        MngrpArchive archive = MngrpArchive.Read(content, header, FF8Encoding.CreateEuropean());
        NativeRefinementDefinitions definitions = new(NativeExecutableHash!, kernelHash, kernel.MenuAbilities.Select(ability => new NativeRefinementAbility(ability.Name ?? "", ability.Index, ability.Start, ability.End)).ToImmutableArray(), NativeRefinementGroupReader.ReadTables(archive))
        {
            TableHashes = NativeRefinementGroupReader.ReadTableHashes(content, header)
        };
        definitions.Validate();
        return nativeRefinement = definitions;
    }
}
