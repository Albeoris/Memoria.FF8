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
        string executableHash = LoadRefinementInput("the original executable profile", () =>
        {
            if (NativeRuntimeProfile is not { IsVerified: true } || NativeExecutableHash is not { } hash)
            {
                throw new NotSupportedException("Refinement requires the authenticated original executable.");
            }
            return hash;
        });
        using IFlArchive main = LoadRefinementInput("the main archive", () => OpenArchive("main"));
        using IFlArchive menu = LoadRefinementInput("the menu archive", () => OpenArchive("menu"));
        (byte[] kernelBytes, string kernelSource) = ReadRefinementResource(main, "main", "kernel.bin");
        string kernelHash = Convert.ToHexString(SHA256.HashData(kernelBytes));
        var encoding = NativeTextEncoding.Create(resources.Source.Language);
        ImmutableArray<NativeRefinementAbility> abilities = LoadRefinementInput($"{kernelSource} menu ability ranges", () =>
        {
            _ = NativeKernelDirectory.Read(kernelBytes);
            KernelData kernel = KernelData.ReadFromArray(kernelBytes, encoding);
            return kernel.MenuAbilities.Select(ability => new NativeRefinementAbility(ability.Name ?? "", ability.Index, ability.Start, ability.End)).ToImmutableArray();
        });
        (byte[] content, string contentSource) = ReadRefinementResource(menu, "menu", "mngrp.bin");
        (byte[] header, string headerSource) = ReadRefinementResource(menu, "menu", "mngrphd.bin");
        string menuSources = $"{contentSource} and {headerSource}";
        NativeRefinementDefinitions definitions = LoadRefinementInput($"recipe tables in {menuSources}", () =>
        {
            MngrpArchive archive = MngrpArchive.Read(content, header, encoding);
            return new NativeRefinementDefinitions(executableHash, kernelHash, abilities, NativeRefinementGroupReader.ReadTables(archive))
            {
                TableHashes = NativeRefinementGroupReader.ReadTableHashes(content, header)
            };
        });
        LoadRefinementInput($"ability and recipe compatibility between {kernelSource} (SHA-256 {kernelHash}) and {menuSources}", () =>
        {
            definitions.Validate();
            return definitions;
        });
        return nativeRefinement = definitions;
    }

    private (byte[] Bytes, string Source) ReadRefinementResource(IFlArchive archive, string family, string name)
    {
        string? provenance = null;
        string source = $"'{family}/{name}'";
        try
        {
            byte[] bytes = Read(archive, name, out provenance);
            return (bytes, RefinementSource(source, provenance));
        }
        catch (Exception exception) when (IsRefinementInputFailure(exception))
        {
            throw RefinementLoadFailure(RefinementSource(source, provenance), exception);
        }
    }

    private T LoadRefinementInput<T>(string input, Func<T> load)
    {
        try
        {
            return load();
        }
        catch (Exception exception) when (IsRefinementInputFailure(exception))
        {
            throw RefinementLoadFailure(input, exception);
        }
    }

    private static string RefinementSource(string resource, string? provenance) => provenance is null ? resource : $"{resource} from {provenance}";

    private InvalidDataException RefinementLoadFailure(string input, Exception cause)
    {
        return new InvalidDataException($"Could not load native refinement data for {input} ({resources.Source.Edition}, {resources.Source.Language}). {cause.Message}", cause);
    }

    private static bool IsRefinementInputFailure(Exception exception)
    {
        return exception is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException or ArgumentException or FormatException or OverflowException or KeyNotFoundException or InvalidCastException or IndexOutOfRangeException;
    }
}
