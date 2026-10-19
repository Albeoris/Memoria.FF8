using System.Buffers.Binary;
using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Remastered;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Remastered.Tests;

public sealed class ScanDescriptionResourcesTests
{
    [Fact]
    public void EditionDescriptionBankReplacesGuestTextWithoutReadingGuestDescriptions()
    {
        byte[] bank = new byte[324];
        bank[320] = 42;
        bank[322] = 43;
        BinaryPrimitives.WriteUInt16LittleEndian(bank.AsSpan(18 * 2), 2);
        var result = NativeBattleScanTextResourcesReader.Read(new UnreadableMemory(), Kernel(), bank);
        Assert.Equal(bank.AsSpan(320).ToArray(), result.DescriptionBytes.ToArray());
        Assert.Equal((byte)43, result.ReadDescriptionByte(result.DescriptionPointer(18)));
        Assert.Equal((byte)42, result.ReadDescriptionByte(result.DescriptionPointer(0)));
    }

    [Fact]
    public void EditionDescriptionBankRejectsTruncationOutOfBoundsOffsetsAndMissingTerminators()
    {
        Assert.Throws<InvalidDataException>(() => NativeBattleScanTextResourcesReader.Read(new UnreadableMemory(), Kernel(), new byte[320]));
        byte[] bank = new byte[321];
        BinaryPrimitives.WriteUInt16LittleEndian(bank, 1);
        Assert.Throws<InvalidDataException>(() => NativeBattleScanTextResourcesReader.Read(new UnreadableMemory(), Kernel(), bank));
        BinaryPrimitives.WriteUInt16LittleEndian(bank, 0);
        bank[320] = 1;
        Assert.Throws<InvalidDataException>(() => NativeBattleScanTextResourcesReader.Read(new UnreadableMemory(), Kernel(), bank));
    }

    [Fact]
    public void InstalledScanConsumerUsesMountedEditionDescriptionsIncludingCorrectedEntries()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_REMASTERED_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory), "Set MEMORIA_FF8_REMASTERED_DIR to run optional installed Remastered checks.");
        using RemasteredGameDataSource source = new(directory!);
        ResourceService resources = new(source);
        using GameAssets game = new(directory!, resources: resources);
        byte[] bank = resources.Read(new ResourceKey("text/enemy-scan"));
        var result = game.ReadNativeScanTextResources();
        Assert.Same(result, game.ReadNativeScanTextResources());
        Assert.Equal(bank.AsSpan(320).ToArray(), result.DescriptionBytes.ToArray());
        foreach (int id in new[] { 18, 55, 56 })
        {
            int offset = BinaryPrimitives.ReadUInt16LittleEndian(bank.AsSpan(id * 2));
            Assert.Equal((ushort)offset, result.DescriptionOffsets[id]);
            Assert.Equal(bank[320 + offset], result.ReadDescriptionByte(result.DescriptionPointer(id)));
        }
    }

    private static byte[] Kernel()
    {
        byte[] kernel = new byte[231];
        BinaryPrimitives.WriteInt32LittleEndian(kernel, 56);
        for (int section = 0; section < 56; section++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(kernel.AsSpan(4 + section * 4), section <= 30 ? 228 : 230);
        }
        return kernel;
    }

    private sealed class UnreadableMemory : IGameNativeMemorySource
    {
        public string SourceHash => "synthetic";
        public byte[] Read(uint address, int count) => throw new InvalidOperationException("Edition descriptions must not read guest text.");
        public byte[] ReadLoader(uint address, int count) => throw new InvalidOperationException("Edition descriptions must not read loader memory.");
    }
}
