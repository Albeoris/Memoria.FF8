using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeSoftwarePrimitiveTableTests
{
    [Fact]
    public void AuthenticatedFourthTableRetainsAllEntriesAndBindsEveryPolygonVariantAndLiteralRet()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "An installed original resource source is required.");
        using BattleAssets assets = new(directory);
        NativeSoftwarePrimitiveTable table = assets.ReadNativeSoftwarePrimitiveTable();
        Assert.Same(table, assets.ReadNativeSoftwarePrimitiveTable());
        Assert.Equal(256, table.Handlers.Length);
        (int first, uint handler)[] groups = [(0x20, 0x4667C0), (0x24, 0x4669F0), (0x28, 0x4668F0), (0x2C, 0x466B10), (0x30, 0x4669D0), (0x34, 0x466C70), (0x38, 0x4669E0), (0x3C, 0x466D90)];
        foreach (var group in groups)
        {
            Assert.All(Enumerable.Range(group.first, 4), opcode => Assert.Equal(group.handler, table.Handler((byte)opcode)));
        }
        Assert.Throws<NotSupportedException>(() => NativeSoftwarePrimitiveTableReader.Read([]));
        Assert.Throws<InvalidDataException>(() => new NativeSoftwarePrimitiveTable(ImmutableArray<uint>.Empty).Handler(0x20));
    }
}
