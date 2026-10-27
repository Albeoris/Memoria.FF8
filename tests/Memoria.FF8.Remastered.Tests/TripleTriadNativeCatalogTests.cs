using Albeoris.Games.FF8.TextEncoding;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Remastered;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Remastered.Tests;

public sealed class TripleTriadNativeCatalogTests
{
    [Fact]
    public void InstalledRemasteredLoadsCardsNamesAiAndRefinementsWithoutAnExecutableResource()
    {
        string directory = InstalledDirectory("MEMORIA_FF8_REMASTERED_DIR", "main.zzz");
        TripleTriadCatalog catalog = TripleTriadCatalog.Load(directory);
        Assert.Equal(110, catalog.Cards.Length);
        Assert.Equal(new TripleTriadCard(0, 1, 4, 1, 5, TripleTriadElement.None), catalog.Cards[0]);
        Assert.Equal(new TripleTriadCard(109, 10, 4, 6, 9, TripleTriadElement.None), catalog.Cards[109]);
        Assert.Equal("Geezard", catalog.Name(0));
        Assert.Equal("Squall", catalog.Name(109));
        Assert.Equal("7110DEBAA131165397435EA21EF51E52AE41FD6BAF8C450F4B2878F4566FE00A", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', catalog.Names)))));
        Assert.Equal(0x15, catalog.TradePriority(0));
        Assert.Equal(0x74, catalog.TradePriority(109));
        Assert.Equal(73, catalog.AiParameters.Depths.Length);
        Assert.Equal(8, catalog.AiParameters.Profiles.Length);
        Assert.Equal(110, catalog.Refinements.Length);
        Assert.Equal(new TripleTriadRefinement(0, 1, 118, 5), catalog.Refinements[0]);
        Assert.Equal(new TripleTriadRefinement(109, 1, 99, 3), catalog.Refinements[109]);
    }

    [Fact]
    public void InstalledEditionsAndClassicExecutableAdapterProduceTheSameCardContract()
    {
        string remasteredDirectory = InstalledDirectory("MEMORIA_FF8_REMASTERED_DIR", "main.zzz");
        string classicDirectory = InstalledDirectory("MEMORIA_FF8_GAME_DIR", "FF8_EN.exe");
        TripleTriadCatalog remastered = TripleTriadCatalog.Load(remasteredDirectory);
        TripleTriadCatalog classic = TripleTriadCatalog.Load(classicDirectory);
        TripleTriadCatalog executable = TripleTriadCatalog.ReadExecutable(File.ReadAllBytes(Path.Combine(classicDirectory, "FF8_EN.exe")));
        Assert.Equal(classic.Cards.ToArray(), remastered.Cards.ToArray());
        Assert.Equal(classic.Names.ToArray(), remastered.Names.ToArray());
        Assert.Equal(classic.TradePriorities.ToArray(), remastered.TradePriorities.ToArray());
        Assert.Equal(classic.AiParameters.Depths.ToArray(), remastered.AiParameters.Depths.ToArray());
        Assert.Equal(classic.AiParameters.Profiles.ToArray(), remastered.AiParameters.Profiles.ToArray());
        Assert.Equal(classic.Refinements.ToArray(), remastered.Refinements.ToArray());
        Assert.Equal(classic.Cards.ToArray(), executable.Cards.ToArray());
        Assert.Equal(classic.Names.ToArray(), executable.Names.ToArray());
        Assert.Equal(classic.TradePriorities.ToArray(), executable.TradePriorities.ToArray());
        Assert.Equal(classic.AiParameters.Depths.ToArray(), executable.AiParameters.Depths.ToArray());
        Assert.Equal(classic.AiParameters.Profiles.ToArray(), executable.AiParameters.Profiles.ToArray());
        Assert.Empty(executable.Refinements);
    }

    [Fact]
    public void ExplicitNameResourceOwnsCardNamesInsteadOfTheGuestImage()
    {
        using RemasteredGameDataSource source = new(InstalledDirectory("MEMORIA_FF8_REMASTERED_DIR", "main.zzz"));
        byte[] names = new ResourceService(source).Read(new ResourceKey("text/card-names"));
        names.AsSpan(4, 2).CopyTo(names.AsSpan(2, 2));
        TripleTriadCatalog catalog = TripleTriadCatalog.Read(source, names, language: FF8TextLanguage.EN);
        Assert.Equal("Funguar", catalog.Name(0));
        Assert.Equal(catalog.Name(1), catalog.Name(0));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void NameResourceBoundsAreEnforcedWithoutReadingPastTheResource(int corruption)
    {
        using RemasteredGameDataSource source = new(InstalledDirectory("MEMORIA_FF8_REMASTERED_DIR", "main.zzz"));
        byte[] names = new ResourceService(source).Read(new ResourceKey("text/card-names"));
        switch (corruption)
        {
            case 0:
                names = names[..1];
                break;
            case 1:
                BinaryPrimitives.WriteUInt16LittleEndian(names.AsSpan(2), 2);
                break;
            case 2:
                BinaryPrimitives.WriteUInt16LittleEndian(names.AsSpan(2), (ushort)names.Length);
                break;
            case 3:
                names.AsSpan(BinaryPrimitives.ReadUInt16LittleEndian(names.AsSpan(2))).Fill(0x20);
                break;
        }
        Assert.Throws<InvalidDataException>(() => TripleTriadCatalog.Read(source, names, language: FF8TextLanguage.EN));
    }

    private static string InstalledDirectory(string variable, string requiredFile)
    {
        string? directory = Environment.GetEnvironmentVariable(variable);
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory) && File.Exists(Path.Combine(directory, requiredFile)), $"Set {variable} to run optional installed card checks.");
        return directory!;
    }
}
