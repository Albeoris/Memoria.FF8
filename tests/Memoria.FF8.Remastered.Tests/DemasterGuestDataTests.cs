using System.Buffers.Binary;
using System.IO.Compression;
using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Remastered;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Remastered.Tests;

public sealed class DemasterGuestDataTests
{
    [Fact]
    public void SelectedTablesReachConsumersWhileNativeMemoryKeepsStockBytes()
    {
        string game = InstalledGame();
        using RemasteredGameDataSource source = new(game);
        source.PrepareLooseData([]);
        using FileStream selected = File.OpenRead(GuestFixture());
        source.PrepareGuestData(selected);
        source.SealLooseData();
        ResourceService resources = new(source);
        using GameAssets assets = new(game, resources: resources);

        NativeSalaryTable salary = assets.ReadNativeSalaryTable();
        Assert.Equal(10000, salary.Pay(20));
        NativeSalaryTable stockSalary = new(source.SourceHash, salary.Entries.SetItem(20, 1500));
        NativeSeeDState savedSeeD = NativeSeeDState.CreateNewGame() with { RankPoints = 2010, SalaryMovementCounter = 0x5FFB, SalaryTable = stockSalary };
        NativeEconomy economy = assets.ReadNewGameEconomy();
        NativeSalaryResult payout = NativeSalaryCalculator.Sample(savedSeeD, economy, assets.ReadNewGameCharacterVitals(), 5, 0, salary);
        Assert.Equal(10000, payout.Payment?.NewPay);
        Assert.Equal(economy.Gil + 10000, payout.Economy?.Gil);
        Assert.Same(stockSalary, payout.SeeD.SalaryTable);
        Assert.Equal(33, assets.ReadNativeDrawPointCatalog().PointBytes[3] & 63);
        TripleTriadCatalog cards = TripleTriadCatalog.Load(game, resources);
        Assert.Equal(TripleTriadElement.Water, cards.Cards[21].Element);
        Assert.Equal(new TripleTriadCard(47, 4, 10, 3, 2, TripleTriadElement.None), cards.Cards[47]);
        Assert.Equal(NativeFieldRuntimeProfile.RemasteredEnImageHash, source.SourceHash);
        Assert.Equal(1500, BinaryPrimitives.ReadUInt16LittleEndian(source.Read(0xB8E49C, 2)));
        Assert.Equal(155, Assert.Single(source.Read(0xB9232B, 1)));
    }

    [Theory]
    [InlineData(0xC74E78u, "card definition tables disagree")]
    [InlineData(0x401010u, "0x00401010")]
    public void RejectsConflictsWithoutPublishingAnyGuestTable(uint address, string diagnostic)
    {
        string game = InstalledGame();
        using RemasteredGameDataSource source = new(game);
        source.PrepareLooseData([]);
        using MemoryStream candidate = ChangeGuestByte(GuestFixture(), address);
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => source.PrepareGuestData(candidate));
        Assert.Contains(diagnostic, error.Message);
        Assert.Null(source.TableOverrides);

        source.SealLooseData();
        ResourceService resources = new(source);
        using GameAssets assets = new(game, resources: resources);
        Assert.Equal(15000, assets.ReadNativeSalaryTable().Pay(20));
        Assert.Equal(27, assets.ReadNativeDrawPointCatalog().PointBytes[3] & 63);
        TripleTriadCatalog cards = TripleTriadCatalog.Load(game, resources);
        Assert.Equal(TripleTriadElement.None, cards.Cards[21].Element);
        Assert.Equal(new TripleTriadCard(47, 3, 10, 2, 1, TripleTriadElement.None), cards.Cards[47]);
    }

    [Fact]
    public void RejectsUnknownBytesAfterTheInPlaceCompressedStream()
    {
        string game = InstalledGame();
        using RemasteredGameDataSource source = new(game);
        source.PrepareLooseData([]);
        byte[] candidate = File.ReadAllBytes(GuestFixture());
        int firstSectionEnd = 296 + BinaryPrimitives.ReadInt32LittleEndian(candidate.AsSpan(292));
        candidate[firstSectionEnd - 1] ^= 1;
        using MemoryStream stream = new(candidate);
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => source.PrepareGuestData(stream));
        Assert.Contains("zlib suffix differs", error.Message);
        Assert.Null(source.TableOverrides);
    }

    private static string InstalledGame()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_REMASTERED_DIR");
        Assert.SkipUnless(game is not null && File.Exists(Path.Combine(game, "main.zzz")), "Set MEMORIA_FF8_REMASTERED_DIR to run the installed guest table checks.");
        return game!;
    }

    private static string GuestFixture()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_DEMASTER_GUEST_FIXTURE");
        Assert.SkipUnless(File.Exists(path), "Set MEMORIA_FF8_DEMASTER_GUEST_FIXTURE to run the optional guest table checks.");
        return path!;
    }

    private static MemoryStream ChangeGuestByte(string path, uint address)
    {
        byte[] bytes = File.ReadAllBytes(path);
        int sectionCount = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(244));
        int header = 248;
        for (int index = 0; index < sectionCount; index++)
        {
            uint start = 0x400000 + BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(header + 12));
            int expandedSize = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(header + 40));
            int compressedSize = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(header + 44));
            int payload = header + 48;
            int name = payload + compressedSize;
            if (address >= start && address - start < expandedSize)
            {
                using MemoryStream original = new(bytes, payload, compressedSize);
                using ZLibStream zlib = new(original, CompressionMode.Decompress);
                byte[] expanded = new byte[expandedSize];
                zlib.ReadExactly(expanded);
                expanded[address - start] ^= 1;
                byte[] compressed = SyntheticResources.Compress(expanded);
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(header + 44), compressed.Length);
                MemoryStream candidate = new();
                candidate.Write(bytes.AsSpan(0, payload));
                candidate.Write(compressed);
                candidate.Write(bytes.AsSpan(name));
                candidate.Position = 0;
                return candidate;
            }
            header = name + 4 + BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(name));
        }
        throw new InvalidDataException($"Guest fixture misses address 0x{address:X8}.");
    }
}
